using ColossalFramework;
using System;
using System.Threading;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Renders an orthographic top-down view into an off-screen texture and returns it as PNG.
    /// Deliberately not the player's camera: the agent needs a deterministic frame of a named
    /// area, not whatever the human happens to be looking at, and the human's view must not
    /// move as a side effect of the agent looking at something.
    ///
    /// Switching info view is not instantaneous in CS1 — the overlay fades in over several
    /// frames — so a capture is a small state machine driven once per frame from the game
    /// thread rather than a single blocking call.
    /// </summary>
    public static class CaptureCommands
    {
        public const int MaxPixels = 1024;
        public const int DefaultPixels = 1024;
        public const float DefaultSize = 1000f;
        private const float CameraAltitude = 2000f;
        private const int DefaultSettleFrames = 8;
        private const int AbandonAfterFrames = 900;

        private enum Stage
        {
            Start = 0,
            Settling = 1,
            Render = 2
        }

        private sealed class Request
        {
            public Vector3 Center;
            public float Size;
            public int Pixels;
            public InfoManager.InfoMode Mode;
            public InfoManager.SubInfoMode SubMode;
            public int SettleFrames;
            public bool AllowBlank;

            public readonly ManualResetEvent Done = new ManualResetEvent(false);
            public byte[] Png;
            public string Error;
            public int DistinctColors;

            public Stage Stage;
            public int FramesLeft;
            public int TotalFrames;
            public bool ModeChanged;
            public InfoManager.InfoMode PreviousMode;
            public InfoManager.SubInfoMode PreviousSubMode;
        }

        private static readonly object gate = new object();
        private static Request active;

        public sealed class CaptureOutcome
        {
            public byte[] Png;
            public string Error;
            public string ResolvedMode;
            public int DistinctColors;
        }

        /// <summary>Called from an HTTP worker thread. Blocks until the game thread renders.</summary>
        public static CaptureOutcome Capture(float x, float z, float size, int pixels,
                                             string modeName, int settleFrames, bool allowBlank, int timeoutMs)
        {
            CaptureOutcome outcome = new CaptureOutcome();

            InfoManager.InfoMode mode;
            string resolved;
            if (!TryParseMode(modeName, out mode, out resolved))
            {
                outcome.Error = "Unknown info mode: " + modeName + ". Valid modes: " + ModeNames();
                return outcome;
            }
            outcome.ResolvedMode = resolved;

            if (size <= 0f || size > 10000f)
            {
                outcome.Error = "size must be between 0 and 10000 metres.";
                return outcome;
            }

            if (pixels <= 0)
            {
                pixels = DefaultPixels;
            }
            if (pixels > MaxPixels)
            {
                pixels = MaxPixels;
            }

            Request request = new Request();
            request.Center = new Vector3(x, 0f, z);
            request.Size = size;
            request.Pixels = pixels;
            request.Mode = mode;
            request.SubMode = InfoManager.SubInfoMode.Default;
            request.SettleFrames = settleFrames > 0 ? settleFrames : DefaultSettleFrames;
            request.AllowBlank = allowBlank;
            request.Stage = Stage.Start;

            lock (gate)
            {
                if (active != null)
                {
                    outcome.Error = "A capture is already in progress. Captures are serialised; retry shortly.";
                    return outcome;
                }
                active = request;
            }

            try
            {
                if (!request.Done.WaitOne(timeoutMs, false))
                {
                    lock (gate)
                    {
                        if (active == request)
                        {
                            active = null;
                        }
                    }
                    outcome.Error = "Timed out waiting for the game thread to render. Is the game paused in a menu?";
                    return outcome;
                }

                outcome.Png = request.Png;
                outcome.Error = request.Error;
                outcome.DistinctColors = request.DistinctColors;
                return outcome;
            }
            finally
            {
                ((IDisposable)request.Done).Dispose();
            }
        }

        /// <summary>Game thread only. Called once per frame.</summary>
        public static void Update()
        {
            Request request;
            lock (gate)
            {
                request = active;
            }

            if (request == null)
            {
                return;
            }

            request.TotalFrames++;
            if (request.TotalFrames > AbandonAfterFrames)
            {
                Finish(request, null, "Capture was abandoned after " + AbandonAfterFrames + " frames.");
                return;
            }

            try
            {
                if (request.Stage == Stage.Start)
                {
                    InfoManager info = Singleton<InfoManager>.instance;
                    request.PreviousMode = info.CurrentMode;
                    request.PreviousSubMode = info.CurrentSubMode;

                    if (info.CurrentMode != request.Mode)
                    {
                        info.SetCurrentMode(request.Mode, request.SubMode);
                        request.ModeChanged = true;
                        request.FramesLeft = request.SettleFrames;
                        request.Stage = Stage.Settling;
                        return;
                    }

                    request.Stage = Stage.Render;
                }

                if (request.Stage == Stage.Settling)
                {
                    if (--request.FramesLeft > 0)
                    {
                        return;
                    }
                    request.Stage = Stage.Render;
                }

                int distinct;
                byte[] png = Render(request, out distinct);
                request.DistinctColors = distinct;

                if (distinct <= 1 && !request.AllowBlank)
                {
                    Finish(request, null,
                        "The render came back as a single flat colour, which means the off-screen camera did not " +
                        "pick up the scene. Retry with allowBlank=true to inspect the raw image.");
                    return;
                }

                Finish(request, png, null);
            }
            catch (Exception ex)
            {
                Finish(request, null, ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Finish(Request request, byte[] png, string error)
        {
            try
            {
                if (request.ModeChanged)
                {
                    Singleton<InfoManager>.instance.SetCurrentMode(request.PreviousMode, request.PreviousSubMode);
                }
            }
            catch (Exception ex)
            {
                BridgeLog.Queue("[SkylinesAgentBridge] Could not restore the info view: " + ex.Message);
            }

            request.Png = png;
            request.Error = error;

            lock (gate)
            {
                if (active == request)
                {
                    active = null;
                }
            }

            request.Done.Set();
        }

        private static byte[] Render(Request request, out int distinctColors)
        {
            GameObject holder = null;
            RenderTexture texture = null;
            Texture2D image = null;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                holder = new GameObject("SkylinesAgentBridgeCaptureCamera");
                Camera camera = holder.AddComponent<Camera>();

                // Never let this camera render on its own: it exists only for the explicit
                // Render() call below.
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = request.Size / 2f;
                camera.transform.position = new Vector3(request.Center.x, CameraAltitude, request.Center.z);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.nearClipPlane = 1f;
                camera.farClipPlane = CameraAltitude + 2000f;

                Camera main = Camera.main;
                if (main != null)
                {
                    camera.cullingMask = main.cullingMask;
                    camera.clearFlags = main.clearFlags;
                    camera.backgroundColor = main.backgroundColor;
                    camera.renderingPath = main.renderingPath;
                }
                else
                {
                    camera.cullingMask = ~0;
                    camera.clearFlags = CameraClearFlags.Color;
                    camera.backgroundColor = Color.black;
                }

                texture = new RenderTexture(request.Pixels, request.Pixels, 24);
                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;
                image = new Texture2D(request.Pixels, request.Pixels, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, request.Pixels, request.Pixels), 0, 0);
                image.Apply();

                distinctColors = CountDistinctSamples(image, request.Pixels);
                camera.targetTexture = null;
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;

                if (image != null)
                {
                    UnityEngine.Object.Destroy(image);
                }
                if (texture != null)
                {
                    texture.Release();
                    UnityEngine.Object.Destroy(texture);
                }
                if (holder != null)
                {
                    UnityEngine.Object.Destroy(holder);
                }
            }
        }

        /// <summary>
        /// Cheap "did anything actually render" probe over a 16x16 sample. A dedicated camera
        /// that fails to pick up the scene produces a uniform fill, which is otherwise an
        /// invisible failure — the agent gets a valid PNG of nothing.
        /// </summary>
        private static int CountDistinctSamples(Texture2D image, int pixels)
        {
            const int steps = 16;
            int stride = Mathf.Max(1, pixels / steps);
            Color32 first = image.GetPixel(0, 0);
            int distinct = 1;

            for (int y = 0; y < pixels; y += stride)
            {
                for (int x = 0; x < pixels; x += stride)
                {
                    Color32 sample = image.GetPixel(x, y);
                    if (sample.r != first.r || sample.g != first.g || sample.b != first.b)
                    {
                        distinct++;
                        if (distinct > 8)
                        {
                            return distinct;
                        }
                    }
                }
            }

            return distinct;
        }

        /// <summary>Cancels an in-flight capture, e.g. when the level unloads.</summary>
        public static void Cancel()
        {
            Request request;
            lock (gate)
            {
                request = active;
                active = null;
            }

            if (request != null)
            {
                request.Error = "The level unloaded while the capture was in flight.";
                request.Done.Set();
            }
        }

        private static bool TryParseMode(string name, out InfoManager.InfoMode mode, out string resolved)
        {
            mode = InfoManager.InfoMode.None;
            resolved = "None";

            if (name == null || name.Length == 0)
            {
                return true;
            }

            // CS1 has no "Zone" info view — zoning colours are painted on the ground in the
            // default view. Accept the name anyway because it is the obvious thing to ask for.
            if (string.Compare(name, "Zone", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(name, "Zoning", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            try
            {
                mode = (InfoManager.InfoMode)Enum.Parse(typeof(InfoManager.InfoMode), name, true);
                resolved = mode.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ModeNames()
        {
            string[] names = Enum.GetNames(typeof(InfoManager.InfoMode));
            return string.Join(", ", names);
        }
    }
}
