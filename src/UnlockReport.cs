using System;
using System.Globalization;
using System.Text;
using ColossalFramework;
using ColossalFramework.Globalization;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Reports why a prefab is (not) unlocked, in the game's own terms.
    ///
    /// What the game checks (decompiled Assembly-CSharp):
    ///  - Every build-panel button, including a transport line tool, is enabled by
    ///    GeneratedScrollPanel.CreateAssetItem -> ToolsModifierControl.IsUnlocked(info.GetUnlockMilestone())
    ///    -> UnlockManager.Unlocked(MilestoneInfo), which is "m_data != null && m_data.m_passedCount != 0"
    ///    (true when the milestone is null). For a TransportInfo GetUnlockMilestone() is m_UnlockMilestone,
    ///    a per-prefab milestone that TransportManager re-checks every simulation step
    ///    (UnlockManager.CheckMilestone(m_UnlockMilestone, false, false)).
    ///  - The public transport tab itself is gated by PublicTransportGroupPanel.PTGroupInfo.IsSubServiceUnlocked
    ///    -> UnlockManager.Unlocked(ItemClass.SubService) (m_SubServiceMilestones[subService]), with DLC
    ///    special cases (Feature milestones) for a few tabs.
    ///  - TransportTool itself does no unlock check.
    ///  - Not reported: the tab's DLC special cases (UnlockManager.Feature milestones for a few groups).
    /// </summary>
    public static class UnlockReport
    {
        public static bool IsUnlocked(MilestoneInfo milestone)
        {
            if (!Singleton<UnlockManager>.exists) return true;
            return Singleton<UnlockManager>.instance.Unlocked(milestone);
        }

        /// <summary>UnlockManager.Unlocked(Service) as JSON; "null" if it throws (it indexes an array by the enum).</summary>
        public static string UnlockedJson(ItemClass.Service service)
        {
            try
            {
                return JsonUtil.Bool(!Singleton<UnlockManager>.exists || Singleton<UnlockManager>.instance.Unlocked(service));
            }
            catch (Exception)
            {
                return "null";
            }
        }

        /// <summary>UnlockManager.Unlocked(SubService) as JSON; "null" if it throws.</summary>
        public static string UnlockedJson(ItemClass.SubService subService)
        {
            try
            {
                return JsonUtil.Bool(!Singleton<UnlockManager>.exists || Singleton<UnlockManager>.instance.Unlocked(subService));
            }
            catch (Exception)
            {
                return "null";
            }
        }

        /// <summary>
        /// The milestone's progress without side effects. MilestoneInfo.GetLocalizedProgress() also
        /// queues the milestone for UnlockManager.CheckMilestone (MilestonCheckNeeded) when it is not
        /// already queued; the public virtual GetLocalizedProgressImpl it wraps only fills the struct.
        /// </summary>
        private static MilestoneInfo.ProgressInfo ReadProgress(MilestoneInfo milestone)
        {
            MilestoneInfo.ProgressInfo progress = default(MilestoneInfo.ProgressInfo);
            milestone.GetLocalizedProgressImpl(1, ref progress, new FastList<MilestoneInfo.ProgressInfo>());
            return progress;
        }

        /// <summary>
        /// The milestone as JSON: asset name, milestone class, localized title, passed state,
        /// the game's localized progress text, and for the two common requirement types the
        /// requirement itself. "null" when there is no milestone (the game treats that as unlocked).
        /// </summary>
        public static string MilestoneJson(MilestoneInfo milestone)
        {
            return MilestoneJson(milestone, 1);
        }

        private static string MilestoneJson(MilestoneInfo milestone, int depth)
        {
            if ((object)milestone == null)
            {
                return "null";
            }

            StringBuilder json = new StringBuilder();
            json.Append("{\"name\":\"").Append(JsonUtil.Escape(SafeName(milestone))).Append("\"");
            json.Append(",\"type\":\"").Append(JsonUtil.Escape(milestone.GetType().Name)).Append("\"");
            json.Append(",\"title\":\"").Append(JsonUtil.Escape(SafeTitle(milestone))).Append("\"");
            json.Append(",\"passed\":").Append(JsonUtil.Bool(IsUnlocked(milestone)));
            MilestoneInfo.Data data = milestone.m_data;
            json.Append(",\"hasData\":").Append(JsonUtil.Bool(data != null));
            json.Append(",\"passedCount\":").Append(data == null ? "null" : data.m_passedCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"canRelock\":").Append(JsonUtil.Bool(milestone.m_canRelock));

            try
            {
                MilestoneInfo.ProgressInfo progress = ReadProgress(milestone);
                json.Append(",\"progress\":{\"passed\":").Append(JsonUtil.Bool(progress.m_passed));
                json.Append(",\"current\":").Append(JsonUtil.Number(progress.m_current));
                json.Append(",\"max\":").Append(JsonUtil.Number(progress.m_max));
                json.Append(",\"description\":\"").Append(JsonUtil.Escape(progress.m_description ?? "")).Append("\"");
                json.Append(",\"text\":\"").Append(JsonUtil.Escape(progress.m_progress ?? "")).Append("\"}");
            }
            catch (Exception ex)
            {
                json.Append(",\"progress\":null,\"progressError\":\"").Append(JsonUtil.Escape(ex.GetType().Name + ": " + ex.Message)).Append("\"");
            }

            BuildingCountMilestone buildingCount = milestone as BuildingCountMilestone;
            if (buildingCount != null)
            {
                json.Append(",\"requires\":{\"buildingsOf\":\"").Append(buildingCount.m_service.ToString())
                    .Append("/").Append(buildingCount.m_subService.ToString()).Append("\"");
                json.Append(",\"level\":\"").Append(buildingCount.m_level.ToString()).Append("\"");
                json.Append(",\"targetCount\":").Append(buildingCount.m_targetCount.ToString(CultureInfo.InvariantCulture));
                json.Append(",\"requireDifferentBuildings\":").Append(JsonUtil.Bool(buildingCount.m_requireDifferentBuildings)).Append("}");
            }

            CombinedMilestone combined = milestone as CombinedMilestone;
            if (combined != null && depth > 0)
            {
                json.Append(",\"requirePassed\":").Append(MilestoneArrayJson(combined.m_requirePassed, depth - 1));
                json.Append(",\"requirePassedLimit\":").Append(combined.m_requirePassedLimit.ToString(CultureInfo.InvariantCulture));
                json.Append(",\"forbidPassed\":").Append(MilestoneArrayJson(combined.m_forbidPassed, depth - 1));
            }

            json.Append("}");
            return json.ToString();
        }

        private static string MilestoneArrayJson(MilestoneInfo[] milestones, int depth)
        {
            if (milestones == null) return "[]";
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < milestones.Length; i++)
            {
                if (i > 0) json.Append(",");
                json.Append(MilestoneJson(milestones[i], depth));
            }
            return json.Append("]").ToString();
        }

        /// <summary>
        /// The unlock gates for a transport type, as JSON: the line-tool
        /// milestone (the one the panel button and the bridge check) and the service and
        /// sub-service milestones that gate the panel and its tab. The tab's DLC Feature special cases
        /// (PTGroupInfo.IsSubServiceUnlocked) are not included.
        /// </summary>
        public static string TransportUnlockJson(TransportInfo info)
        {
            StringBuilder json = new StringBuilder();
            MilestoneInfo milestone = info.m_UnlockMilestone;
            json.Append("{\"lineTool\":").Append(JsonUtil.Bool(IsUnlocked(milestone)));
            json.Append(",\"milestone\":").Append(MilestoneJson(milestone));
            if (info.m_class != null)
            {
                json.Append(",\"service\":\"").Append(info.m_class.m_service.ToString()).Append("\"");
                json.Append(",\"serviceUnlocked\":").Append(UnlockedJson(info.m_class.m_service));
                json.Append(",\"subService\":\"").Append(info.m_class.m_subService.ToString()).Append("\"");
                json.Append(",\"subServiceUnlocked\":").Append(UnlockedJson(info.m_class.m_subService));
            }
            json.Append("}");
            return json.ToString();
        }

        /// <summary>One line naming the gate that failed, for an error message.</summary>
        public static string TransportUnlockSummary(TransportInfo info)
        {
            MilestoneInfo milestone = info.m_UnlockMilestone;
            StringBuilder text = new StringBuilder();
            text.Append("line-tool milestone ");
            if ((object)milestone == null)
            {
                text.Append("(none)");
            }
            else
            {
                text.Append("'").Append(SafeName(milestone)).Append("' (").Append(milestone.GetType().Name);
                string title = SafeTitle(milestone);
                if (title.Length > 0) text.Append(", \"").Append(title).Append("\"");
                text.Append(") passed=").Append(IsUnlocked(milestone) ? "true" : "false");
                try
                {
                    MilestoneInfo.ProgressInfo progress = ReadProgress(milestone);
                    if (!string.IsNullOrEmpty(progress.m_description)) text.Append(", requirement: ").Append(progress.m_description);
                    if (!string.IsNullOrEmpty(progress.m_progress)) text.Append(" [").Append(progress.m_progress).Append("]");
                }
                catch (Exception)
                {
                }
            }
            if (info.m_class != null)
            {
                text.Append("; service ").Append(info.m_class.m_service.ToString()).Append(" unlocked=")
                    .Append(UnlockedJson(info.m_class.m_service));
                text.Append("; subService ").Append(info.m_class.m_subService.ToString()).Append(" unlocked=")
                    .Append(UnlockedJson(info.m_class.m_subService));
            }
            return text.ToString();
        }

        private static string SafeName(MilestoneInfo milestone)
        {
            try
            {
                return milestone.m_name ?? milestone.name ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string SafeTitle(MilestoneInfo milestone)
        {
            try
            {
                if (milestone.m_name != null && Locale.Exists("UNLOCK_MILESTONE_TITLE", milestone.m_name))
                {
                    return milestone.GetLocalizedName() ?? "";
                }
            }
            catch (Exception)
            {
            }
            return "";
        }
    }
}
