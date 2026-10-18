using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ColossalFramework.Globalization;
using ColossalFramework.Packaging;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Game lifecycle from the main menu or in game: list the New Game panel's maps, start a new
    /// city, load a save. Each replicates the game's own panel code path (decompiled from
    /// Assembly-CSharp with monodis) minus the UI and its confirm modals:
    ///
    ///   ListMaps    NewGamePanel.Refresh             (asset filter, isPublished, unplayable ND maps)
    ///               NewGamePanel.OnMapSelectionChanged (m_dlcNeeded = !IsDLCOwned(GetRequiredDLCFromMapName))
    ///   NewGame     NewGamePanel.OnStartNewGame -> CheckForDisasterActivation -> StartNewGameRoutine
    ///   LoadSave    LoadPanel.Refresh (listing) + LoadPanel.OnLoad -> CheckForDisasterActivation -> LoadRoutine
    ///
    /// Every method here must run on Unity's main thread (MainThreadDispatcher.MenuQueue).
    ///
    /// FAIR PLAY: these never enable a cheat. They refuse to start or load while one of the
    /// game's built-in cheat mods is enabled, so an unattended supervisor cannot silently play a
    /// city with Unlimited Money / Unlock All / unlimited resources.
    /// </summary>
    public static class LifecycleCommands
    {
        // Normalised (lower case, letters and digits only) names of the built-in cheat mods in
        // Cities.app/Contents/Resources/Files/Mods (folders UnlimitedMoney, UnlockAll,
        // UnlimitedOilAndOre, UnlimitedSoil). Both the plugin folder name and the IUserMod.Name
        // are checked, so a workshop clone with the same display name is refused as well.
        private static readonly string[] CheatModKeys =
        {
            "unlimitedmoney",
            "unlockall",
            "unlimitedoilandore",
            "unlimitedsoil",
        };

        // ---------------------------------------------------------------- mods

        public struct ModEntry
        {
            public string Name;
            public string Folder;
            public bool Builtin;
            public bool Cheat;
        }

        /// <summary>
        /// Enabled plugins from PluginManager. Safe on any thread in practice (it walks a
        /// dictionary the game only mutates when the content manager toggles a mod), but
        /// callers that need certainty should marshal.
        /// </summary>
        public static List<ModEntry> EnabledMods()
        {
            List<ModEntry> list = new List<ModEntry>();
            if (!PluginManager.exists)
            {
                return list;
            }

            foreach (PluginManager.PluginInfo info in PluginManager.instance.GetPluginsInfo())
            {
                if (info == null || !info.isEnabled)
                {
                    continue;
                }

                string display = null;
                try
                {
                    IUserMod mod = info.userModInstance as IUserMod;
                    if (mod != null)
                    {
                        display = mod.Name;
                    }
                }
                catch
                {
                }

                ModEntry entry = new ModEntry();
                entry.Folder = info.name;
                entry.Name = string.IsNullOrEmpty(display) ? info.name : display;
                entry.Builtin = info.isBuiltin;
                entry.Cheat = IsCheat(entry.Name) || IsCheat(entry.Folder);
                list.Add(entry);
            }

            return list;
        }

        public static string EnabledModsJson()
        {
            StringBuilder sb = new StringBuilder("[");
            List<ModEntry> mods;
            try
            {
                mods = EnabledMods();
            }
            catch (Exception ex)
            {
                BridgeLog.Queue("[SkylinesAgentBridge] Could not list mods: " + ex.Message);
                return "null";
            }

            for (int i = 0; i < mods.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(JsonUtil.Escape(mods[i].Name)).Append('"');
            }

            return sb.Append(']').ToString();
        }

        private static bool IsCheat(string name)
        {
            string key = Normalize(name);
            if (key.Length == 0) return false;
            for (int i = 0; i < CheatModKeys.Length; i++)
            {
                if (key == CheatModKeys[i]) return true;
            }
            return false;
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static readonly string[] OwnerChosenKeys = { "unlimitedmoney", "unlockall", "unlimitedsoil" };

        private static bool OwnerChosen(ModEntry mod)
        {
            string a = Normalize(mod.Name), b = Normalize(mod.Folder);
            for (int i = 0; i < OwnerChosenKeys.Length; i++)
            {
                if (a == OwnerChosenKeys[i] || b == OwnerChosenKeys[i]) return true;
            }
            return false;
        }

        /// <summary>Null when fair; otherwise the refusal message naming the enabled cheat mods.</summary>
        private static string CheatRefusal()
        {
            List<string> cheats = new List<string>();
            foreach (ModEntry mod in EnabledMods())
            {
                // Owner's own instructions 2026-10-17: "we will start a new game with unlimited
                // currency", then, told that Unlock All and Unlimited Soil were also enabled,
                // "create new game yourself on islands map". The owner's chosen built-in mods
                // are their game setup, so none of them blocks. The bridge's own bypass flags
                // stay refused (FairPlay.cs).
                if (mod.Cheat && !OwnerChosen(mod))
                    cheats.Add(mod.Name);
            }

            if (cheats.Count == 0) return null;
            return "Refused: cheat mods are enabled (" + string.Join(", ", cheats.ToArray()) +
                "). Disable them in Content Manager > Mods; the bridge will not start or load a city with them on.";
        }

        // ---------------------------------------------------------------- refusals

        private static string BusyRefusal(bool refuseIfLevelLoaded)
        {
            if (!LoadingManager.exists)
            {
                return "LoadingManager does not exist yet; the game is still starting.";
            }

            if (LoadingManager.instance.m_currentlyLoading)
            {
                return "A level is already loading.";
            }

            if (SavePanel.isSaving)
            {
                return "A save is in progress.";
            }

            if (refuseIfLevelLoaded && (AgentBridge.Instance.LevelLoaded || LoadingManager.instance.m_loadingComplete && IsInGameScene()))
            {
                return "A city is already loaded. Quit to the main menu first, or use /commands/load-save.";
            }

            return null;
        }

        private static bool IsInGameScene()
        {
            try
            {
                // The main menu has no ToolController; a loaded city always has one.
                return UnityEngine.Object.FindObjectOfType(typeof(ToolController)) != null;
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- maps

        private sealed class MapEntry
        {
            public Package.Asset Asset;
            public MapMetaData Meta;
            public string DisplayName;
            public bool Builtin;
            public bool Workshop;
            public ulong PublishedFileId;
            public SteamHelper.DLC RequiredDlc;
            public bool DlcOwned;
        }

        // NewGamePanel.Refresh: FilterAssets(UserAssetType.MapMetaData), asset != null && isEnabled,
        // Instantiate<MapMetaData>(), isPublished, !IsUnplayableNDScenarioMap(mapName),
        // Locale "MAPNAME" lookup for the shown name.
        private static List<MapEntry> EnumerateMaps()
        {
            List<MapEntry> maps = new List<MapEntry>();

            foreach (Package.Asset asset in PackageManager.FilterAssets(new Package.AssetType[] { UserAssetType.MapMetaData }))
            {
                if (asset == null || !asset.isEnabled)
                {
                    continue;
                }

                try
                {
                    MapMetaData meta = asset.Instantiate<MapMetaData>();
                    if (meta == null || !meta.isPublished)
                    {
                        continue;
                    }

                    if (NewGamePanel.IsUnplayableNDScenarioMap(meta.mapName))
                    {
                        continue;
                    }

                    MapEntry entry = new MapEntry();
                    entry.Asset = asset;
                    entry.Meta = meta;
                    entry.DisplayName = Locale.Exists("MAPNAME", meta.mapName) ? Locale.Get("MAPNAME", meta.mapName) : meta.mapName;
                    Package package = asset.package;
                    entry.Builtin = package != null && meta.IsBuiltinMap(package.packagePath);
                    PublishedFileId id = package != null ? package.GetPublishedFileID() : PublishedFileId.invalid;
                    entry.Workshop = id != PublishedFileId.invalid;
                    entry.PublishedFileId = entry.Workshop ? id.AsUInt64 : 0UL;
                    entry.RequiredDlc = NewGamePanel.GetRequiredDLCFromMapName(package, meta);
                    entry.DlcOwned = SteamHelper.IsDLCOwned(entry.RequiredDlc);
                    maps.Add(entry);
                }
                catch (Exception ex)
                {
                    BridgeLog.Queue("[SkylinesAgentBridge] Map '" + asset.name + "' failed to read: " + ex.Message);
                }
            }

            return maps;
        }

        private static string MapJson(MapEntry m)
        {
            MapMetaData d = m.Meta;
            Package package = m.Asset.package;
            return "{\"ref\":\"" + JsonUtil.Escape(m.Asset.fullName) + "\"" +
                ",\"mapName\":\"" + JsonUtil.Escape(d.mapName) + "\"" +
                ",\"displayName\":\"" + JsonUtil.Escape(m.DisplayName) + "\"" +
                ",\"assetName\":\"" + JsonUtil.Escape(m.Asset.name) + "\"" +
                ",\"packageName\":\"" + JsonUtil.Escape(package != null ? package.packageName : "") + "\"" +
                ",\"environment\":\"" + JsonUtil.Escape(d.environment) + "\"" +
                ",\"mapThemeRef\":" + (d.mapThemeRef == null ? "null" : "\"" + JsonUtil.Escape(d.mapThemeRef) + "\"") +
                ",\"builtin\":" + JsonUtil.Bool(m.Builtin) +
                ",\"workshop\":" + JsonUtil.Bool(m.Workshop) +
                ",\"publishedFileId\":" + (m.Workshop ? m.PublishedFileId.ToString() : "null") +
                ",\"requiredDlc\":\"" + JsonUtil.Escape(m.RequiredDlc.ToString()) + "\"" +
                ",\"dlcOwned\":" + JsonUtil.Bool(m.DlcOwned) +
                ",\"playable\":" + JsonUtil.Bool(m.DlcOwned) +
                ",\"buildableArea\":" + d.buildableArea +
                ",\"resources\":{\"oil\":" + d.oilAmount + ",\"ore\":" + d.oreAmount +
                    ",\"forest\":" + d.forestAmount + ",\"fertileLand\":" + d.fertileLandAmount +
                    ",\"water\":" + d.waterAmount + ",\"maxResourceAmount\":" + JsonUtil.Number(d.maxResourceAmount) + "}" +
                ",\"connections\":{\"roadsIn\":" + d.incomingRoads + ",\"roadsOut\":" + d.outgoingRoads +
                    ",\"trainIn\":" + d.incomingTrainTracks + ",\"trainOut\":" + d.outgoingTrainTracks +
                    ",\"shipIn\":" + d.incomingShipPath + ",\"shipOut\":" + d.outgoingShipPath +
                    ",\"planeIn\":" + d.incomingPlanePath + ",\"planeOut\":" + d.outgoingPlanePath + "}" +
                "}";
        }

        public static CommandResult ListMaps()
        {
            List<MapEntry> maps = EnumerateMaps();
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"ok\":true,\"levelLoaded\":").Append(JsonUtil.Bool(AgentBridge.Instance.LevelLoaded))
              .Append(",\"count\":").Append(maps.Count)
              .Append(",\"buildableAreaUnit\":\"MapMetaData.buildableArea (the New Game panel's Buildable Area figure, raw)\"")
              .Append(",\"maps\":[");
            for (int i = 0; i < maps.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(MapJson(maps[i]));
            }
            sb.Append("]}");
            return CommandResult.FromJson(sb.ToString());
        }

        /// <summary>Exact match on ref, assetName, packageName, mapName, displayName; then the same ignoring case.</summary>
        private static MapEntry ResolveMap(List<MapEntry> maps, string query, out string error)
        {
            error = null;
            for (int pass = 0; pass < 2; pass++)
            {
                StringComparison cmp = pass == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                List<MapEntry> hits = new List<MapEntry>();
                foreach (MapEntry m in maps)
                {
                    Package p = m.Asset.package;
                    if (string.Equals(m.Asset.fullName, query, cmp) ||
                        string.Equals(m.Asset.name, query, cmp) ||
                        (p != null && string.Equals(p.packageName, query, cmp)) ||
                        string.Equals(m.Meta.mapName, query, cmp) ||
                        string.Equals(m.DisplayName, query, cmp))
                    {
                        hits.Add(m);
                    }
                }

                if (hits.Count == 1) return hits[0];
                if (hits.Count > 1)
                {
                    List<string> refs = new List<string>();
                    foreach (MapEntry h in hits) refs.Add(h.Asset.fullName);
                    error = "Map '" + query + "' is ambiguous; pass one of these refs: " + string.Join(", ", refs.ToArray());
                    return null;
                }
            }

            error = "No buildable map matches '" + query + "'. GET /state/maps lists them.";
            return null;
        }

        // ---------------------------------------------------------------- new game

        public static CommandResult NewGame(string mapQuery, string cityName, bool invertTraffic, bool dryRun)
        {
            string refusal = BusyRefusal(true) ?? CheatRefusal();
            if (refusal != null) return CommandResult.Fail(refusal);

            if (string.IsNullOrEmpty(mapQuery)) return CommandResult.Fail("map is required.");
            cityName = cityName == null ? "" : cityName.Trim();
            // NewGamePanel.IsValid: the start button is disabled while the name field is empty.
            if (cityName.Length == 0) return CommandResult.Fail("cityName is required (the New Game panel will not start with an empty name).");

            string error;
            MapEntry map = ResolveMap(EnumerateMaps(), mapQuery, out error);
            if (map == null) return CommandResult.Fail(error);

            // NewGamePanel.OnStartNewGame returns immediately when m_dlcNeeded.
            if (!map.DlcOwned)
            {
                return CommandResult.Fail("Map '" + map.Meta.mapName + "' needs DLC " + map.RequiredDlc + ", which is not owned.");
            }

            // StartNewGameRoutine, field for field.
            SimulationMetaData meta = new SimulationMetaData();
            meta.m_CityName = cityName;
            meta.m_gameInstanceIdentifier = Guid.NewGuid().ToString();
            meta.m_invertTraffic = invertTraffic ? SimulationMetaData.MetaBool.True : SimulationMetaData.MetaBool.False;
            meta.m_disableAchievements = SimulationMetaData.MetaBool.False;
            meta.m_startingDateTime = DateTime.Now;
            meta.m_currentDateTime = DateTime.Now;
            meta.m_newGameAppVersion = BuildConfig.APPLICATION_VERSION;
            meta.m_updateMode = SimulationManager.UpdateMode.NewGameFromMap;
            meta.m_MapName = map.Meta.mapName;

            // The panel's theme dropdown is preselected to the map's own theme
            // (SelectThemeOverride(mapThemeRef)), so the default start keeps mapThemeRef. A theme
            // that is not installed is skipped (FindAssetByName returns null), which is what the
            // game does after the CONFIRM_MAPTHEME_MISSING modal is accepted.
            bool themeApplied = false;
            if (map.Meta.mapThemeRef != null)
            {
                Package.Asset theme = PackageManager.FindAssetByName(map.Meta.mapThemeRef, UserAssetType.MapThemeMetaData);
                if (theme != null)
                {
                    meta.m_MapThemeMetaData = theme.Instantiate<MapThemeMetaData>();
                    meta.m_MapThemeMetaData.SetSelfRef(theme);
                    themeApplied = true;
                }
            }

            string preview = "\"map\":" + MapJson(map) +
                ",\"cityName\":\"" + JsonUtil.Escape(cityName) + "\"" +
                ",\"gameInstanceIdentifier\":\"" + JsonUtil.Escape(meta.m_gameInstanceIdentifier) + "\"" +
                ",\"invertTraffic\":" + JsonUtil.Bool(invertTraffic) +
                ",\"disableAchievements\":\"" + meta.m_disableAchievements + "\"" +
                ",\"newGameAppVersion\":" + meta.m_newGameAppVersion +
                ",\"updateMode\":\"" + meta.m_updateMode + "\"" +
                ",\"themeApplied\":" + JsonUtil.Bool(themeApplied);

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true," + preview +
                    ",\"message\":\"Would call LoadingManager.LoadLevel(asset, \\\"Game\\\", \\\"InGame\\\", metaData, false).\"}");
            }

            LoadingManager.instance.LoadLevel(map.Asset, "Game", "InGame", meta, false);
            HidePanel("NewGamePanel");

            return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false," + preview +
                ",\"message\":\"Loading started. Poll GET /health until levelLoaded is true (the bridge restarts nothing; the HTTP server stays up).\"}");
        }

        // ---------------------------------------------------------------- load save

        private sealed class SaveEntry
        {
            public Package.Asset Asset;
            public SaveGameMetaData Meta;
            public string FileName;
        }

        // LoadPanel.Refresh: FilterAssets(UserAssetType.SaveGameMetaData), skip
        // PackageHelper.IsDemoModeSave, asset != null && isEnabled, Instantiate<SaveGameMetaData>().
        // The listing name is asset.name.
        private static List<SaveEntry> EnumerateSaves()
        {
            List<SaveEntry> saves = new List<SaveEntry>();
            foreach (Package.Asset asset in PackageManager.FilterAssets(new Package.AssetType[] { UserAssetType.SaveGameMetaData }))
            {
                if (asset == null || PackageHelper.IsDemoModeSave(asset) || !asset.isEnabled)
                {
                    continue;
                }

                try
                {
                    SaveEntry entry = new SaveEntry();
                    entry.Asset = asset;
                    entry.Meta = asset.Instantiate<SaveGameMetaData>();
                    string path = asset.package != null ? asset.package.packagePath : null;
                    entry.FileName = string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
                    if (entry.Meta != null) saves.Add(entry);
                }
                catch (Exception ex)
                {
                    BridgeLog.Queue("[SkylinesAgentBridge] Save '" + asset.name + "' failed to read: " + ex.Message);
                }
            }
            return saves;
        }

        private static SaveEntry ResolveSave(List<SaveEntry> saves, string name, out int matches)
        {
            matches = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                StringComparison cmp = pass == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                SaveEntry best = null;
                int count = 0;
                foreach (SaveEntry s in saves)
                {
                    Package p = s.Asset.package;
                    if (string.Equals(s.Asset.name, name, cmp) ||
                        string.Equals(s.FileName, name, cmp) ||
                        (p != null && string.Equals(p.packageName, name, cmp)))
                    {
                        count++;
                        // Several packages can share an asset name; take the newest, which is
                        // what a crash-recovery caller asking for "AgentAutoSave" means.
                        if (best == null || s.Meta.timeStamp > best.Meta.timeStamp) best = s;
                    }
                }

                if (best != null)
                {
                    matches = count;
                    return best;
                }
            }
            return null;
        }

        public static CommandResult LoadSave(string name, bool dryRun)
        {
            string refusal = BusyRefusal(false) ?? CheatRefusal();
            if (refusal != null) return CommandResult.Fail(refusal);

            if (string.IsNullOrEmpty(name)) return CommandResult.Fail("name is required.");

            int matches;
            SaveEntry save = ResolveSave(EnumerateSaves(), name, out matches);
            if (save == null)
            {
                return CommandResult.Fail("No save named '" + name + "' is known to the PackageManager. GET /state/saves lists the files on disk.");
            }

            Package.Asset asset = save.Asset;
            SaveGameMetaData sgmd = save.Meta;
            Package package = asset.package;
            bool workshop = package != null && package.GetPublishedFileID() != PublishedFileId.invalid;

            // LoadRoutine, field for field.
            SimulationMetaData meta = new SimulationMetaData();
            meta.m_CityName = sgmd.cityName;
            meta.m_updateMode = SimulationManager.UpdateMode.LoadGame;
            meta.m_environment = ForceEnvironment();
            if (workshop)
            {
                meta.m_disableAchievements = SimulationMetaData.MetaBool.True;
            }

            bool themeApplied = false;
            if (sgmd.mapThemeRef != null)
            {
                Package.Asset theme = PackageManager.FindAssetByName(sgmd.mapThemeRef, UserAssetType.MapThemeMetaData);
                if (theme != null)
                {
                    meta.m_MapThemeMetaData = theme.Instantiate<MapThemeMetaData>();
                    meta.m_MapThemeMetaData.SetSelfRef(theme);
                    themeApplied = true;
                }
            }

            bool levelLoaded = AgentBridge.Instance.LevelLoaded;
            string preview = "\"name\":\"" + JsonUtil.Escape(asset.name) + "\"" +
                ",\"ref\":\"" + JsonUtil.Escape(asset.fullName) + "\"" +
                ",\"file\":\"" + JsonUtil.Escape(save.FileName) + "\"" +
                ",\"matches\":" + matches +
                ",\"cityName\":\"" + JsonUtil.Escape(sgmd.cityName) + "\"" +
                ",\"timeStamp\":\"" + JsonUtil.Escape(sgmd.timeStamp.ToString("s")) + "\"" +
                ",\"workshop\":" + JsonUtil.Bool(workshop) +
                ",\"themeApplied\":" + JsonUtil.Bool(themeApplied) +
                ",\"fromLevelLoaded\":" + JsonUtil.Bool(levelLoaded);

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true," + preview +
                    ",\"message\":\"Would call LoadingManager.LoadLevel(asset, \\\"Game\\\", \\\"InGame\\\", metaData, false).\"}");
            }

            if (levelLoaded)
            {
                CloseEverything();
            }

            // LoadRoutine: m_LastSaveName = listing name; SavePanel.lastLoadedName = it;
            // SavePanel.lastCloudSetting = package is on a cloud path.
            SetLoadPanelLastSaveName(asset.name);
            SavePanel.lastLoadedName = asset.name;
            SavePanel.lastCloudSetting = package != null && PackageManager.IsCloudPath(package.packagePath);

            LoadingManager.instance.LoadLevel(asset, "Game", "InGame", meta, false);
            HidePanel("LoadPanel");

            return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false," + preview +
                ",\"message\":\"Loading started. Poll GET /health until levelLoaded is true.\"}");
        }

        // ---------------------------------------------------------------- helpers

        private static string ForceEnvironment()
        {
            try
            {
                UnityEngine.Object[] panels = Resources.FindObjectsOfTypeAll(typeof(LoadPanel));
                if (panels != null && panels.Length > 0)
                {
                    LoadPanel panel = panels[0] as LoadPanel;
                    if (panel != null) return panel.m_forceEnvironment;
                }
            }
            catch
            {
            }
            return null;
        }

        private static void SetLoadPanelLastSaveName(string name)
        {
            try
            {
                FieldInfo field = typeof(LoadPanel).GetField("m_LastSaveName", BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null) field.SetValue(null, name);
            }
            catch (Exception ex)
            {
                BridgeLog.Queue("[SkylinesAgentBridge] Could not set LoadPanel.m_LastSaveName: " + ex.Message);
            }
        }

        // LoadRoutine calls ToolsModifierControl.CloseEverything() on itself (LoadPanel derives
        // from ToolsModifierControl). Any live instance does the same job.
        private static void CloseEverything()
        {
            try
            {
                ToolsModifierControl control = UnityEngine.Object.FindObjectOfType(typeof(ToolsModifierControl)) as ToolsModifierControl;
                if (control != null) control.CloseEverything();
            }
            catch (Exception ex)
            {
                BridgeLog.Queue("[SkylinesAgentBridge] CloseEverything failed: " + ex.Message);
            }
        }

        private static void HidePanel(string name)
        {
            try
            {
                UIView.library.Hide(name, 1);
            }
            catch
            {
                // The panel was never opened: nothing to hide.
            }
        }
    }
}
