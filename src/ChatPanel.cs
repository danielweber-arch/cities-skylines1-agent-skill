using System;
using System.Collections.Generic;
using System.Globalization;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// The in-game Codex chat window. Game thread only. It never waits on ChatStore's lock:
    /// every read goes through TryEnter and simply tries again next frame if an HTTP thread
    /// happens to hold it.
    ///
    /// Hotkey safety while typing, as read from the shipped ColossalManaged / Assembly-CSharp:
    ///   * UIInput.ProcessKeyEvent hands a KeyDown to the focused component first and only
    ///     calls the game's KeyShortcuts when the event was not Use()d.
    ///   * GameKeyShortcuts.OnProcessKeyEvent (pause, speed, bulldoze, Esc -> pause menu, all
    ///     tool hotkeys) is wrapped in `!UIView.HasModalInput() &amp;&amp; !UIView.HasInputFocus()`,
    ///     and HasInputFocus() is true exactly when the active component is a UITextField.
    ///   * CameraController only runs HandleKeyEvents (WASD/QE/RF) when
    ///     ToolManager.m_properties.HasInputFocus is false; ToolController refreshes that
    ///     from UIView.HasInputFocus() every frame.
    ///   * UITextField (with builtinKeyNavigation) Use()s Escape after cancelling and
    ///     unfocusing, so Esc leaves the field instead of opening the pause menu.
    /// Enter is caught in eventKeyDown and Use()d, which stops UITextField from running
    /// OnSubmit (it returns early on a used event) so the field keeps focus between messages.
    /// </summary>
    public static class ChatPanel
    {
        private const float DefaultWidth = 420f;
        private const float DefaultHeight = 360f;
        private const float MinWidth = 300f;
        private const float MinHeight = 220f;
        private const float TitleHeight = 30f;
        private const float InputHeight = 26f;
        private const float StatusHeight = 18f;
        private const float ScrollbarWidth = 10f;
        private const int MaxLabels = 200;
        private const float SnapshotInterval = 0.5f;

        private static readonly Color32 PanelColor = new Color32(32, 38, 44, 230);
        private static readonly Color32 TitleColor = new Color32(235, 245, 255, 255);
        private static readonly Color32 PlayerColor = new Color32(150, 205, 255, 255);
        private static readonly Color32 ReplyColor = new Color32(240, 240, 225, 255);
        private static readonly Color32 UpdateColor = new Color32(150, 160, 170, 255);
        private static readonly Color32 StatusKindColor = new Color32(140, 185, 150, 255);
        private static readonly Color32 ErrorColor = new Color32(255, 150, 130, 255);
        private static readonly Color32 StatusLineColor = new Color32(185, 195, 205, 255);

        private static bool wanted;
        private static bool visible = true;
        private static Vector3 savedPosition = new Vector3(-1f, -1f);
        private static Vector2 savedSize = new Vector2(DefaultWidth, DefaultHeight);

        private static UIPanel panel;
        private static UILabel titleLabel;
        private static UIButton closeButton;
        private static UIScrollablePanel list;
        private static UIScrollbar scrollbar;
        private static UISlicedSprite track;
        private static UISlicedSprite thumb;
        private static UILabel statusLabel;
        private static UITextField input;
        private static UIButton sendButton;
        private static UIButton resizeHandle;
        private static readonly List<UILabel> labels = new List<UILabel>();

        private static long shownId;
        private static long shownStatusVersion = -1;
        private static int pendingScrollFrames;
        private static bool refocusInput;
        private static float snapshotTimer;
        private static bool dragging;
        private static bool resizing;
        private static Vector3 lastMousePosition;
        private static int errorCount;

        public static bool IsVisible
        {
            get { return wanted && panel != null && visible; }
        }

        /// <summary>Called when a level finishes loading. The panel is built lazily on the next frame.</summary>
        public static void Create()
        {
            wanted = true;
            shownId = 0;
            shownStatusVersion = -1;
            errorCount = 0;
        }

        /// <summary>Called when a level unloads. Keeps position, size and visibility for the session.</summary>
        public static void Destroy()
        {
            wanted = false;
            dragging = false;
            resizing = false;
            ChatStore.Instance.ClearCachedContext();

            if (input != null && input.hasFocus)
            {
                try { input.Unfocus(); } catch { }
            }

            labels.Clear();
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel.gameObject);
            }

            panel = null;
            titleLabel = null;
            closeButton = null;
            list = null;
            scrollbar = null;
            track = null;
            thumb = null;
            statusLabel = null;
            input = null;
            sendButton = null;
            resizeHandle = null;
            shownId = 0;
        }

        public static void Toggle()
        {
            if (!wanted)
            {
                return;
            }

            visible = !visible;
            if (panel == null)
            {
                return;
            }

            if (!visible && input != null && input.hasFocus)
            {
                input.Unfocus();
            }

            panel.isVisible = visible;
            if (visible)
            {
                panel.BringToFront();
                pendingScrollFrames = 3;
            }
        }

        /// <summary>Game thread, once per frame.</summary>
        public static void Update(float realTimeDelta)
        {
            if (!wanted)
            {
                return;
            }

            try
            {
                EnsurePanel();
                if (panel == null)
                {
                    return;
                }

                HandleHotkey();
                KeepOnScreen();
                UpdateDragAndResize();

                snapshotTimer -= realTimeDelta;
                if (snapshotTimer <= 0f)
                {
                    snapshotTimer = SnapshotInterval;
                    ChatStore.Instance.TrySetCachedContext(CaptureContext("cached"));
                    RefreshStatusLine(true);
                }
                else
                {
                    RefreshStatusLine(false);
                }

                PullNewEntries();

                if (refocusInput && input != null && visible)
                {
                    refocusInput = false;
                    input.Focus();
                }

                if (pendingScrollFrames > 0 && list != null)
                {
                    pendingScrollFrames--;
                    list.ScrollToBottom();
                }
            }
            catch (Exception ex)
            {
                // Never let the chat take the frame loop down; log a few times, then go quiet.
                if (++errorCount <= 3)
                {
                    Debug.Log("[SkylinesAgentBridge] Chat panel update failed: " + ex);
                }
            }
        }

        // --- Build ---------------------------------------------------------------------

        private static void EnsurePanel()
        {
            if (panel != null)
            {
                return;
            }

            UIView view = UIView.GetAView();
            if (view == null)
            {
                return;
            }

            panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
            if (panel == null)
            {
                return;
            }

            panel.name = "SkylinesAgentBridgeChat";
            panel.backgroundSprite = "MenuPanel2";
            panel.color = PanelColor;
            panel.clipChildren = true;
            panel.width = Mathf.Max(MinWidth, savedSize.x);
            panel.height = Mathf.Max(MinHeight, savedSize.y);

            if (savedPosition.x < 0f)
            {
                // Default: top-right quadrant, clear of the API console on the left.
                savedPosition = new Vector3(Mathf.Max(0f, VisibleWidth(view) - panel.width - 24f), 92f);
            }
            panel.relativePosition = ClampPanelPosition(savedPosition);

            titleLabel = panel.AddUIComponent(typeof(UILabel)) as UILabel;
            titleLabel.name = "SkylinesAgentBridgeChatTitle";
            titleLabel.text = "Codex";
            titleLabel.textScale = 0.9f;
            titleLabel.textColor = TitleColor;
            titleLabel.autoSize = false;
            titleLabel.height = 22f;
            titleLabel.tooltip = "Drag to move. Ctrl+Shift+C toggles this window.";
            titleLabel.eventMouseDown += delegate(UIComponent sender, UIMouseEventParameter eventParam)
            {
                dragging = true;
                lastMousePosition = Input.mousePosition;
            };
            titleLabel.eventMouseUp += delegate(UIComponent sender, UIMouseEventParameter eventParam)
            {
                dragging = false;
            };

            closeButton = CreateButton(panel, "SkylinesAgentBridgeChatClose", "X", 30f, 22f);
            closeButton.tooltip = "Hide (Ctrl+Shift+C or the Codex button on the API console brings it back)";
            closeButton.eventClick += delegate(UIComponent component, UIMouseEventParameter eventParam)
            {
                Toggle();
            };

            list = panel.AddUIComponent(typeof(UIScrollablePanel)) as UIScrollablePanel;
            list.name = "SkylinesAgentBridgeChatList";
            list.clipChildren = true;
            list.autoLayout = true;
            list.autoLayoutDirection = LayoutDirection.Vertical;
            list.autoLayoutPadding = new RectOffset(0, 0, 0, 4);
            list.scrollPadding = new RectOffset(4, 4, 4, 4);
            list.scrollWheelDirection = UIOrientation.Vertical;
            list.scrollWheelAmount = 40;
            list.builtinKeyNavigation = false;

            scrollbar = panel.AddUIComponent(typeof(UIScrollbar)) as UIScrollbar;
            scrollbar.name = "SkylinesAgentBridgeChatScrollbar";
            scrollbar.orientation = UIOrientation.Vertical;
            scrollbar.minValue = 0f;
            scrollbar.value = 0f;
            scrollbar.incrementAmount = 40f;
            scrollbar.autoHide = true;

            track = scrollbar.AddUIComponent(typeof(UISlicedSprite)) as UISlicedSprite;
            track.spriteName = "ScrollbarTrack";
            track.relativePosition = Vector3.zero;
            scrollbar.trackObject = track;

            thumb = track.AddUIComponent(typeof(UISlicedSprite)) as UISlicedSprite;
            thumb.spriteName = "ScrollbarThumb";
            thumb.relativePosition = Vector3.zero;
            scrollbar.thumbObject = thumb;

            list.verticalScrollbar = scrollbar;

            statusLabel = panel.AddUIComponent(typeof(UILabel)) as UILabel;
            statusLabel.name = "SkylinesAgentBridgeChatStatus";
            statusLabel.textScale = 0.72f;
            statusLabel.textColor = StatusLineColor;
            statusLabel.autoSize = false;
            statusLabel.height = StatusHeight;
            statusLabel.wordWrap = false;
            statusLabel.processMarkup = false;

            input = panel.AddUIComponent(typeof(UITextField)) as UITextField;
            input.name = "SkylinesAgentBridgeChatInput";
            input.normalBgSprite = "TextFieldPanel";
            input.hoveredBgSprite = "TextFieldPanelHovered";
            input.focusedBgSprite = "TextFieldPanel";
            input.selectionSprite = "EmptySprite";
            input.selectionBackgroundColor = new Color32(0, 105, 210, 255);
            input.color = new Color32(60, 66, 74, 255);
            input.textColor = TitleColor;
            input.textScale = 0.8f;
            input.padding = new RectOffset(6, 6, 6, 4);
            input.horizontalAlignment = UIHorizontalAlignment.Left;
            input.maxLength = ChatStore.MaxPlayerTextLength;
            input.multiline = false;
            input.readOnly = false;
            input.numericalOnly = false;
            input.selectOnFocus = false;
            // Clicking away keeps the draft (a submit just ends editing; nothing is sent).
            // With false, UITextField would cancel and restore the text from focus time.
            input.submitOnFocusLost = true;
            // Required for a code-built field: without it UITextField ignores typing entirely.
            input.builtinKeyNavigation = true;
            input.tooltip = "Type to Codex. Enter sends, Esc leaves the field. Use /model NAME to switch the chat model.";
            input.eventKeyDown += delegate(UIComponent component, UIKeyEventParameter eventParam)
            {
                if (eventParam.used)
                {
                    return;
                }
                if (eventParam.keycode == KeyCode.Return || eventParam.keycode == KeyCode.KeypadEnter)
                {
                    // Use() before UITextField's own switch runs: it returns early on a used
                    // event, so OnSubmit (and its Unfocus) never happens and the game's
                    // shortcut handlers never see the key.
                    eventParam.Use();
                    Send();
                }
            };

            sendButton = CreateButton(panel, "SkylinesAgentBridgeChatSend", "Send", 60f, InputHeight);
            sendButton.eventClick += delegate(UIComponent component, UIMouseEventParameter eventParam)
            {
                Send();
            };

            resizeHandle = CreateButton(panel, "SkylinesAgentBridgeChatResize", "", 14f, 14f);
            resizeHandle.normalBgSprite = "ScrollbarThumb";
            resizeHandle.hoveredBgSprite = "ScrollbarThumb";
            resizeHandle.pressedBgSprite = "ScrollbarThumb";
            resizeHandle.tooltip = "Drag to resize";
            resizeHandle.eventMouseDown += delegate(UIComponent sender, UIMouseEventParameter eventParam)
            {
                resizing = true;
                lastMousePosition = Input.mousePosition;
            };
            resizeHandle.eventMouseUp += delegate(UIComponent sender, UIMouseEventParameter eventParam)
            {
                resizing = false;
            };

            Layout();
            panel.isVisible = visible;
            pendingScrollFrames = 3;
            RefreshStatusLine(true);
        }

        private static UIButton CreateButton(UIComponent parent, string name, string text, float width, float height)
        {
            UIButton button = parent.AddUIComponent(typeof(UIButton)) as UIButton;
            button.name = name;
            button.text = text;
            button.textScale = 0.78f;
            button.textColor = TitleColor;
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.width = width;
            button.height = height;
            return button;
        }

        /// <summary>Positions every child from the panel's current size.</summary>
        private static void Layout()
        {
            if (panel == null)
            {
                return;
            }

            float w = panel.width;
            float h = panel.height;
            float listTop = TitleHeight + 4f;
            float inputTop = h - InputHeight - 10f;
            float statusTop = inputTop - StatusHeight - 4f;
            float listHeight = Mathf.Max(40f, statusTop - listTop - 4f);
            float listWidth = w - 16f - ScrollbarWidth - 2f;

            titleLabel.width = w - 60f;
            titleLabel.relativePosition = new Vector3(10f, 8f);
            closeButton.relativePosition = new Vector3(w - 38f, 5f);

            list.relativePosition = new Vector3(8f, listTop);
            list.width = listWidth;
            list.height = listHeight;

            scrollbar.relativePosition = new Vector3(8f + listWidth + 2f, listTop);
            scrollbar.width = ScrollbarWidth;
            scrollbar.height = listHeight;
            track.width = ScrollbarWidth;
            track.height = listHeight;
            thumb.width = ScrollbarWidth;

            statusLabel.relativePosition = new Vector3(10f, statusTop);
            statusLabel.width = w - 20f;

            input.relativePosition = new Vector3(8f, inputTop);
            input.width = w - 8f - 8f - 60f - 6f;
            input.height = InputHeight;

            sendButton.relativePosition = new Vector3(w - 68f, inputTop);
            resizeHandle.relativePosition = new Vector3(w - 15f, h - 15f);
            resizeHandle.BringToFront();

            float labelWidth = LabelWidth();
            for (int i = 0; i < labels.Count; i++)
            {
                labels[i].width = labelWidth;
            }
        }

        private static float LabelWidth()
        {
            return list == null ? 300f : Mathf.Max(60f, list.width - 8f);
        }

        // --- Messages ------------------------------------------------------------------

        private static void PullNewEntries()
        {
            ChatStore store = ChatStore.Instance;
            if (store.LatestHistoryId == shownId)
            {
                return;
            }

            List<ChatEntry> entries = store.TryGetHistory(shownId, MaxLabels);
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            bool stickToBottom = IsAtBottom();
            for (int i = 0; i < entries.Count; i++)
            {
                ChatEntry entry = entries[i];
                AddLabel(entry);
                if (entry.Id > shownId)
                {
                    shownId = entry.Id;
                }
                if (entry.From == "player" && entry.Source == "panel")
                {
                    // Your own message always brings you back to the end.
                    stickToBottom = true;
                }
            }

            if (stickToBottom)
            {
                // autoHeight labels settle over the next frame or two, so keep pinning briefly.
                pendingScrollFrames = 3;
            }
        }

        private static void AddLabel(ChatEntry entry)
        {
            string time = entry.Utc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
            string prefix;
            Color32 color;
            UIHorizontalAlignment alignment = UIHorizontalAlignment.Left;

            if (entry.From == "player")
            {
                prefix = entry.Source == "api" ? "You (terminal)" : "You";
                color = PlayerColor;
                alignment = UIHorizontalAlignment.Right;
            }
            else if (entry.Kind == "update")
            {
                prefix = "Codex (working)";
                color = UpdateColor;
            }
            else if (entry.Kind == "status")
            {
                prefix = "Codex (status)";
                color = StatusKindColor;
            }
            else
            {
                prefix = "Codex";
                color = ReplyColor;
            }

            AddLabelText("[" + time + "] " + prefix + ": " + entry.Text, color, alignment, entry.Kind == "update" ? 0.72f : 0.78f);
        }

        private static void AddLabelText(string text, Color32 color, UIHorizontalAlignment alignment, float scale)
        {
            UILabel label = list.AddUIComponent(typeof(UILabel)) as UILabel;
            label.processMarkup = false;
            label.autoSize = false;
            label.wordWrap = true;
            label.autoHeight = true;
            label.width = LabelWidth();
            label.textScale = scale;
            label.textColor = color;
            label.textAlignment = alignment;
            label.text = text;
            labels.Add(label);

            while (labels.Count > MaxLabels)
            {
                UILabel oldest = labels[0];
                labels.RemoveAt(0);
                list.RemoveUIComponent(oldest);
                UnityEngine.Object.Destroy(oldest.gameObject);
            }
        }

        private static bool IsAtBottom()
        {
            if (list == null)
            {
                return true;
            }

            float viewHeight = list.height - list.scrollPadding.vertical;
            float maxScroll = list.CalculateViewSize().y - viewHeight;
            return maxScroll <= 0f || list.scrollPosition.y >= maxScroll - 12f;
        }

        private static void Send()
        {
            if (input == null)
            {
                return;
            }

            string text = ChatStore.NormalizeText(input.text, ChatStore.MaxPlayerTextLength);
            if (text.Length == 0)
            {
                return;
            }

            try
            {
                ChatStore.Instance.AddPlayerMessage(text, CaptureContext("live"), "panel");
                input.text = "";
            }
            catch (Exception ex)
            {
                AddLabelText("Could not send: " + ex.Message, ErrorColor, UIHorizontalAlignment.Left, 0.72f);
            }

            pendingScrollFrames = 3;
            refocusInput = !input.hasFocus;
        }

        private static void RefreshStatusLine(bool force)
        {
            if (statusLabel == null)
            {
                return;
            }

            ChatStore store = ChatStore.Instance;
            if (!force && store.StatusVersion == shownStatusVersion)
            {
                return;
            }

            ChatClaudeStatus status = store.TryGetClaudeStatus();
            if (status == null)
            {
                return;
            }
            shownStatusVersion = store.StatusVersion;

            string line;
            if (status.State == "offline")
            {
                line = "AI offline - no agent is watching the chat.";
            }
            else if (status.State == "thinking")
            {
                line = "AI online - thinking" + Suffix(status.Text);
            }
            else if (status.State == "working")
            {
                line = "AI online - working" + Suffix(status.Text);
            }
            else
            {
                line = "AI online - listening" + Suffix(status.Text);
            }

            if (statusLabel.text != line)
            {
                statusLabel.text = line;
                statusLabel.tooltip = line;
            }

            string title = status.State == "offline" ? "Codex (AI offline)" : "Codex (AI online)";
            if (titleLabel != null && titleLabel.text != title)
            {
                titleLabel.text = title;
            }
        }

        private static string Suffix(string text)
        {
            return string.IsNullOrEmpty(text) ? "" : ": " + text.Replace("\n", " ");
        }

        // --- Input ---------------------------------------------------------------------

        private static void HandleHotkey()
        {
            // Never while any text field has focus: Ctrl+Shift+C there is the player's to use.
            if (UIView.HasInputFocus() || !Input.GetKeyDown(KeyCode.C))
            {
                return;
            }

            bool modifier = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (modifier && shift)
            {
                Toggle();
            }
        }

        private static void UpdateDragAndResize()
        {
            if (!dragging && !resizing)
            {
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                dragging = false;
                resizing = false;
                return;
            }

            Vector3 mousePosition = Input.mousePosition;
            Vector3 delta = mousePosition - lastMousePosition;
            lastMousePosition = mousePosition;
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            UIView view = panel.GetUIView();
            float scale = view == null ? 1f : view.ratio;
            delta *= scale;

            if (dragging)
            {
                panel.relativePosition = ClampPanelPosition(panel.relativePosition + new Vector3(delta.x, -delta.y, 0f));
                savedPosition = panel.relativePosition;
                return;
            }

            bool stick = IsAtBottom();
            panel.width = Mathf.Max(MinWidth, panel.width + delta.x);
            panel.height = Mathf.Max(MinHeight, panel.height - delta.y);
            savedSize = new Vector2(panel.width, panel.height);
            Layout();
            if (stick)
            {
                pendingScrollFrames = 2;
            }
        }

        private static Vector3 ClampPanelPosition(Vector3 position)
        {
            UIView view = UIView.GetAView();
            if (view == null || panel == null)
            {
                return position;
            }

            // Keep the whole panel on screen: shrink it first if the screen is smaller than it.
            float visibleWidth = VisibleWidth(view);
            float visibleHeight = view.fixedHeight;
            if (panel.width > visibleWidth - 8f) panel.width = Mathf.Max(MinWidth, visibleWidth - 8f);
            if (panel.height > visibleHeight - 8f) panel.height = Mathf.Max(MinHeight, visibleHeight - 8f);
            float maxX = Mathf.Max(0f, visibleWidth - panel.width);
            float maxY = Mathf.Max(0f, visibleHeight - panel.height);
            position.x = Mathf.Clamp(position.x, 0f, maxX);
            position.y = Mathf.Clamp(position.y, 0f, maxY);
            return position;
        }

        // UIView.fixedWidth is the 1920-unit reference canvas. The game scales the UI by height
        // (fixedHeight 1080), so on a 16:10 screen only 1080 * 16/10 = 1728 units are visible and a
        // panel placed against fixedWidth ends up partly off the right edge. Use the real aspect.
        private static float VisibleWidth(UIView view)
        {
            if (Screen.height <= 0)
            {
                return view.fixedWidth;
            }
            float visible = view.fixedHeight * ((float)Screen.width / (float)Screen.height);
            return visible > 0f ? visible : view.fixedWidth;
        }

        private static int lastScreenWidth;
        private static int lastScreenHeight;

        // Re-clamp when the window or resolution changes, so the panel can never sit off-screen.
        private static void KeepOnScreen()
        {
            if (panel == null)
            {
                return;
            }
            if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            {
                return;
            }
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            panel.relativePosition = ClampPanelPosition(panel.relativePosition);
            savedPosition = panel.relativePosition;
            Layout();
        }

        // --- Context capture (game thread) ----------------------------------------------

        private static ChatContext CaptureContext(string source)
        {
            ChatContext context = new ChatContext();
            context.Source = source;
            context.CapturedUtc = DateTime.UtcNow;

            try
            {
                context.GameTime = Singleton<SimulationManager>.instance.m_currentGameTime.ToString("s", CultureInfo.InvariantCulture);
            }
            catch
            {
            }

            try
            {
                CameraController camera = ToolsModifierControl.cameraController;
                if (camera != null)
                {
                    context.HasCamera = true;
                    context.CameraX = camera.m_targetPosition.x;
                    context.CameraZ = camera.m_targetPosition.z;
                }
            }
            catch
            {
            }

            try
            {
                context.Selected = DescribeSelection(WorldInfoPanel.GetCurrentInstanceID());
            }
            catch
            {
                context.Selected = ChatSelection.None();
            }

            return context;
        }

        /// <summary>
        /// The entity whose info panel is open (WorldInfoPanel clears its record when the
        /// panel hides, so a closed panel reads as none).
        /// </summary>
        private static ChatSelection DescribeSelection(InstanceID id)
        {
            ChatSelection selection = ChatSelection.None();
            if (id.IsEmpty)
            {
                return selection;
            }

            switch (id.Type)
            {
                case InstanceType.Building:
                {
                    ushort buildingId = id.Building;
                    BuildingManager manager = BuildingManager.instance;
                    Building building = manager.m_buildings.m_buffer[buildingId];
                    selection.Type = "building";
                    selection.Id = buildingId;
                    selection.Name = manager.GetBuildingName(buildingId, InstanceID.Empty);
                    selection.Prefab = building.Info == null ? null : building.Info.name;
                    SetPosition(selection, building.m_position);
                    break;
                }
                case InstanceType.NetSegment:
                {
                    ushort segmentId = id.NetSegment;
                    NetManager manager = NetManager.instance;
                    NetSegment segment = manager.m_segments.m_buffer[segmentId];
                    selection.Type = "segment";
                    selection.Id = segmentId;
                    selection.Name = manager.GetSegmentName(segmentId);
                    selection.Prefab = segment.Info == null ? null : segment.Info.name;
                    SetPosition(selection, segment.m_middlePosition);
                    break;
                }
                case InstanceType.NetNode:
                {
                    ushort nodeId = id.NetNode;
                    NetNode node = NetManager.instance.m_nodes.m_buffer[nodeId];
                    selection.Type = "node";
                    selection.Id = nodeId;
                    selection.Prefab = node.Info == null ? null : node.Info.name;
                    SetPosition(selection, node.m_position);
                    break;
                }
                case InstanceType.Citizen:
                {
                    uint citizenId = id.Citizen;
                    CitizenManager manager = CitizenManager.instance;
                    Citizen citizen = manager.m_citizens.m_buffer[citizenId];
                    selection.Type = "citizen";
                    selection.Id = citizenId;
                    selection.Name = manager.GetCitizenName(citizenId);
                    if (citizen.m_instance != 0)
                    {
                        CitizenInstance walker = manager.m_instances.m_buffer[citizen.m_instance];
                        selection.Prefab = walker.Info == null ? null : walker.Info.name;
                        SetPosition(selection, walker.GetLastFramePosition());
                    }
                    break;
                }
                case InstanceType.CitizenInstance:
                {
                    ushort instanceId = id.CitizenInstance;
                    CitizenInstance walker = CitizenManager.instance.m_instances.m_buffer[instanceId];
                    selection.Type = "citizen";
                    selection.Id = instanceId;
                    selection.Name = "citizen instance #" + instanceId.ToString(CultureInfo.InvariantCulture);
                    selection.Prefab = walker.Info == null ? null : walker.Info.name;
                    SetPosition(selection, walker.GetLastFramePosition());
                    break;
                }
                case InstanceType.Vehicle:
                {
                    ushort vehicleId = id.Vehicle;
                    VehicleManager manager = VehicleManager.instance;
                    Vehicle vehicle = manager.m_vehicles.m_buffer[vehicleId];
                    selection.Type = "vehicle";
                    selection.Id = vehicleId;
                    selection.Name = manager.GetVehicleName(vehicleId);
                    selection.Prefab = vehicle.Info == null ? null : vehicle.Info.name;
                    SetPosition(selection, vehicle.GetLastFramePosition());
                    break;
                }
                case InstanceType.District:
                {
                    byte districtId = id.District;
                    DistrictManager manager = DistrictManager.instance;
                    selection.Type = "district";
                    selection.Id = districtId;
                    selection.Name = manager.GetDistrictName(districtId);
                    SetPosition(selection, manager.m_districts.m_buffer[districtId].m_nameLocation);
                    break;
                }
                default:
                    // Parks, transport lines and the like: report what it is without guessing more.
                    selection.Type = id.Type.ToString().ToLowerInvariant();
                    selection.Id = id.Index;
                    break;
            }

            return selection;
        }

        private static void SetPosition(ChatSelection selection, Vector3 position)
        {
            selection.HasPosition = true;
            selection.X = position.x;
            selection.Z = position.z;
        }
    }
}
