using System;
using ColossalFramework;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Fair-play rules (owner order 2026-10-17: "do not cheat the game"). The bridge must give the
    /// agent nothing a human player lacks: every build pays what the game's tool would charge,
    /// locked prefabs and zones are refused, nothing is built on land the city does not own, and
    /// every bypass flag is refused. CS1_ALLOW_CHEATS=1 in the game's environment (read once, at
    /// first use) turns the bypass flags back on for deliberate testing.
    /// </summary>
    public static class FairPlay
    {
        public static readonly bool CheatsAllowed = ReadCheatsAllowed();

        private static bool ReadCheatsAllowed()
        {
            try
            {
                return Environment.GetEnvironmentVariable("CS1_ALLOW_CHEATS") == "1";
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Null when none of <paramref name="flags"/> is set to true in the body (or cheats are
        /// allowed), otherwise the refusal message for the first one that is.
        /// </summary>
        public static string RefuseFlags(string body, params string[] flags)
        {
            if (CheatsAllowed)
            {
                return null;
            }
            for (int i = 0; i < flags.Length; i++)
            {
                if (JsonUtil.GetBool(body, flags[i], false))
                {
                    return "fair play: " + flags[i] + " is disabled";
                }
            }
            return null;
        }

        /// <summary>EconomyManager.PeekResource(Construction), the check the game's tools make before building.</summary>
        public static bool CanAfford(int cost)
        {
            if (cost <= 0 || !Singleton<EconomyManager>.exists)
            {
                return true;
            }
            return Singleton<EconomyManager>.instance.PeekResource(EconomyManager.Resource.Construction, cost) == cost;
        }

        /// <summary>EconomyManager.FetchResource(Construction, cost, class), as NetTool and BuildingTool charge it.</summary>
        public static void Charge(int cost, ItemClass itemClass)
        {
            if (cost <= 0 || !Singleton<EconomyManager>.exists)
            {
                return;
            }
            Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.Construction, cost, itemClass);
        }

        /// <summary>Gives back a charge for something the bridge built and then rolled back.</summary>
        public static void Refund(int cost, ItemClass itemClass)
        {
            if (cost <= 0 || !Singleton<EconomyManager>.exists)
            {
                return;
            }
            Singleton<EconomyManager>.instance.AddResource(EconomyManager.Resource.RefundAmount, cost, itemClass);
        }

        public static string NotEnoughMoney(int cost)
        {
            long cash = Singleton<EconomyManager>.exists ? Singleton<EconomyManager>.instance.LastCashAmount : 0;
            return "not enough money: costs " + cost + ", city has " + cash + " (cents, EconomyManager units)";
        }

        /// <summary>The build panel gate: ToolsModifierControl.IsUnlocked(info.GetUnlockMilestone()).</summary>
        public static bool PrefabUnlocked(PrefabInfo info)
        {
            if (info == null)
            {
                return false;
            }
            try
            {
                return UnlockReport.IsUnlocked(info.GetUnlockMilestone());
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static string LockedPrefab(PrefabInfo info)
        {
            MilestoneInfo milestone = null;
            try { milestone = info.GetUnlockMilestone(); } catch (Exception) { }
            string milestoneName = milestone == null ? "unknown milestone" : (milestone.m_name ?? milestone.name ?? "unnamed milestone");
            return "fair play: prefab " + info.name + " is locked (" + milestoneName + ")";
        }

        public static bool ZoneUnlocked(ItemClass.Zone zone)
        {
            if (zone == ItemClass.Zone.Unzoned || zone == ItemClass.Zone.None || !Singleton<UnlockManager>.exists)
            {
                return true;
            }
            try
            {
                return Singleton<UnlockManager>.instance.Unlocked(zone);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>GameAreaManager.PointOutOfArea: the land the city has not bought (NetTool/BuildingTool OutOfArea).</summary>
        public static bool OutOfArea(Vector3 point)
        {
            if (!Singleton<GameAreaManager>.exists)
            {
                return false;
            }
            return Singleton<GameAreaManager>.instance.PointOutOfArea(point);
        }

        /// <summary>True when any 8 m sample along a-b lies outside the owned tiles.</summary>
        public static bool LineOutOfArea(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            d.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 8f));
            for (int i = 0; i <= steps; i++)
            {
                if (OutOfArea(Vector3.Lerp(a, b, (float)i / steps)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the building footprint's corners or centre lie outside the owned tiles.</summary>
        public static bool FootprintOutOfArea(BuildingInfo info, Vector3 position, float angle)
        {
            if (OutOfArea(position))
            {
                return true;
            }
            float hw = info.m_cellWidth * 4f;
            float hl = info.m_cellLength * 4f;
            Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 forward = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            return OutOfArea(position + right * hw + forward * hl) || OutOfArea(position - right * hw + forward * hl) ||
                OutOfArea(position + right * hw - forward * hl) || OutOfArea(position - right * hw - forward * hl);
        }

        public const string OutOfAreaMessage = "fair play: outside the city's owned tiles (buy the tile with unlock-area first)";

        /// <summary>
        /// Segment cost as BulldozeTool.GetSegmentRefundAmount prices it (the inverse of NetTool's
        /// charge): NetAI.GetConstructionCost(startPos, endPos, startElevation, endElevation), with
        /// the node elevations negated for underground networks.
        /// </summary>
        public static int SegmentCost(NetInfo info, Vector3 start, Vector3 end, float startElevation, float endElevation)
        {
            if (info == null || info.m_netAI == null)
            {
                return 0;
            }
            float h1 = Mathf.Abs(startElevation);
            float h2 = Mathf.Abs(endElevation);
            if (info.m_netAI.IsUnderground() || startElevation < 0f)
            {
                h1 = -h1;
            }
            if (info.m_netAI.IsUnderground() || endElevation < 0f)
            {
                h2 = -h2;
            }
            return info.m_netAI.GetConstructionCost(start, end, h1, h2);
        }

        /// <summary>BuildingAI.GetConstructionCost() (new) or GetRelocationCost() (move), the base of the BuildingTool charge.</summary>
        public static int BuildingCost(BuildingInfo info, bool relocating)
        {
            if (info == null || info.m_buildingAI == null)
            {
                return 0;
            }
            return relocating ? info.m_buildingAI.GetRelocationCost() : info.m_buildingAI.GetConstructionCost();
        }
    }
}
