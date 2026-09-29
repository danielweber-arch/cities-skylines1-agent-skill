using System.Text;
using ColossalFramework;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Map tiles ("areas"): which are owned, which can be bought now, and what they cost; and
    /// buying one the way the game's area panel does (GameAreaTool.UnlockArea).
    ///
    /// The game has two limits on buying a tile, both checked by GameAreaManager.CanUnlock:
    ///   1. a hard cap, GameAreaManager.m_maxAreaCount (MaxAreaCount defaults it to 9; mods raise
    ///      it through IAreas.maxAreaCount, clamped to 1..25). Not saved: a load resets it to
    ///      max(9, owned tiles).
    ///   2. a milestone gate, UnlockManager.Unlocked(m_areaCount): the next tile needs the
    ///      area milestone at index m_areaCount (UnlockManagerProperties.m_AreaMilestones)
    ///      to be passed. This one is progression state, and the bridge never fakes it.
    /// plus: the tile must be inside the 5x5 grid, not owned, and edge-adjacent to an owned tile.
    /// </summary>
    public static class AreaCommands
    {
        // IAreas.maxAreaCount clamps to this; the grid has 25 tiles.
        private const int GridMaxAreaCount = GameAreaManager.AREAGRID_RESOLUTION * GameAreaManager.AREAGRID_RESOLUTION;

        public static CommandResult BuildAreasJson()
        {
            GameAreaManager areas = Singleton<GameAreaManager>.instance;
            int res = GameAreaManager.AREAGRID_RESOLUTION;
            int maxAreaCount = areas.MaxAreaCount;
            bool milestoneReached = Singleton<UnlockManager>.instance.Unlocked(areas.m_areaCount);
            int startX;
            int startZ;
            areas.GetStartTile(out startX, out startZ);

            float originMinX;
            float originMinZ;
            float originMaxX;
            float originMaxZ;
            areas.GetAreaBounds(0, 0, out originMinX, out originMinZ, out originMaxX, out originMaxZ);

            StringBuilder tiles = new StringBuilder();
            int purchasable = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int index = areas.GetTileIndex(x, z);
                    bool owned = areas.IsUnlocked(x, z);
                    bool adjacent = !owned && IsAdjacentToOwned(areas, x, z);
                    bool canUnlock = areas.CanUnlock(x, z);
                    if (canUnlock) purchasable++;

                    float minX;
                    float minZ;
                    float maxX;
                    float maxZ;
                    areas.GetAreaBounds(x, z, out minX, out minZ, out maxX, out maxZ);

                    if (tiles.Length > 0) tiles.Append(",");
                    tiles.Append("{\"tileX\":").Append(x)
                        .Append(",\"tileZ\":").Append(z)
                        .Append(",\"index\":").Append(index)
                        .Append(",\"owned\":").Append(JsonUtil.Bool(owned))
                        .Append(",\"unlockOrder\":").Append(areas.m_areaGrid[index])
                        .Append(",\"start\":").Append(JsonUtil.Bool(x == startX && z == startZ))
                        .Append(",\"adjacentToOwned\":").Append(JsonUtil.Bool(adjacent))
                        .Append(",\"purchasable\":").Append(JsonUtil.Bool(canUnlock))
                        .Append(",\"bounds\":{\"minX\":").Append(JsonUtil.Number(minX))
                        .Append(",\"minZ\":").Append(JsonUtil.Number(minZ))
                        .Append(",\"maxX\":").Append(JsonUtil.Number(maxX))
                        .Append(",\"maxZ\":").Append(JsonUtil.Number(maxZ)).Append("}")
                        .Append(",\"center\":{\"x\":").Append(JsonUtil.Number((minX + maxX) * 0.5f))
                        .Append(",\"z\":").Append(JsonUtil.Number((minZ + maxZ) * 0.5f)).Append("}");
                    if (owned)
                    {
                        tiles.Append(",\"price\":null");
                    }
                    else
                    {
                        int price = EstimateTilePrice(areas, x, z);
                        tiles.Append(",\"price\":").Append(price)
                            .Append(",\"priceDisplay\":").Append(price / 100)
                            .Append(",\"affordable\":").Append(JsonUtil.Bool(price == 0 ||
                                Singleton<EconomyManager>.instance.PeekResource(EconomyManager.Resource.LandPrice, price) == price));
                    }
                    tiles.Append("}");
                }
            }

            return CommandResult.FromJson("{\"ok\":true" +
                ",\"gridResolution\":" + res +
                ",\"tileSize\":" + JsonUtil.Number(GameAreaManager.AREAGRID_CELL_SIZE) +
                ",\"origin\":{\"x\":" + JsonUtil.Number(originMinX) + ",\"z\":" + JsonUtil.Number(originMinZ) + "}" +
                ",\"ownedCount\":" + areas.m_areaCount +
                ",\"maxAreaCount\":" + maxAreaCount +
                ",\"gridMaxAreaCount\":" + GridMaxAreaCount +
                ",\"atMaxAreaCount\":" + JsonUtil.Bool(areas.m_areaCount >= maxAreaCount) +
                ",\"nextAreaMilestoneReached\":" + JsonUtil.Bool(milestoneReached) +
                ",\"nextAreaMilestone\":" + MilestoneJson(areas.m_areaCount) +
                ",\"purchasableCount\":" + purchasable +
                ",\"startTile\":{\"tileX\":" + startX + ",\"tileZ\":" + startZ + "}" +
                ",\"cash\":" + Singleton<EconomyManager>.instance.LastCashAmount +
                ",\"tiles\":[" + tiles.ToString() + "]}");
        }

        /// <summary>
        /// Body: {tileX, tileZ} or {x, z} (world position), dryRun, ignoreMaxAreaCount (alias
        /// ignoreMilestoneLimit). ignoreMaxAreaCount raises the hard cap m_maxAreaCount to 25
        /// (the official IAreas.maxAreaCount setter) before buying; it does not bypass the
        /// milestone gate.
        /// </summary>
        public static CommandResult UnlockArea(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            bool raiseCap = JsonUtil.GetBool(body, "ignoreMaxAreaCount", false) ||
                JsonUtil.GetBool(body, "ignoreMilestoneLimit", false);

            GameAreaManager areas = Singleton<GameAreaManager>.instance;
            int res = GameAreaManager.AREAGRID_RESOLUTION;
            float rawTileX = JsonUtil.GetNumber(body, "tileX", -9999f);
            float rawTileZ = JsonUtil.GetNumber(body, "tileZ", -9999f);
            if ((rawTileX != -9999f && rawTileX != Mathf.Floor(rawTileX)) ||
                (rawTileZ != -9999f && rawTileZ != Mathf.Floor(rawTileZ)))
            {
                return CommandResult.Fail("tileX and tileZ must be whole numbers.");
            }
            int tileX = (int)rawTileX;
            int tileZ = (int)rawTileZ;
            if (tileX == -9999 || tileZ == -9999)
            {
                float wx = JsonUtil.GetNumber(body, "x", float.NaN);
                float wz = JsonUtil.GetNumber(body, "z", float.NaN);
                if (float.IsNaN(wx) || float.IsNaN(wz))
                {
                    return CommandResult.Fail("tileX and tileZ (0.." + (res - 1) + "), or a world position x and z, are required.");
                }
                // Same arithmetic as GameAreaManager.GetAreaIndex, without its -1 for outside.
                tileX = Mathf.FloorToInt(wx / GameAreaManager.AREAGRID_CELL_SIZE + res * 0.5f);
                tileZ = Mathf.FloorToInt(wz / GameAreaManager.AREAGRID_CELL_SIZE + res * 0.5f);
            }

            if (tileX < 0 || tileZ < 0 || tileX >= res || tileZ >= res)
            {
                return CommandResult.Fail("Tile " + tileX + "," + tileZ + " is outside the " + res + "x" + res + " grid.");
            }

            int index = areas.GetTileIndex(tileX, tileZ);
            int maxBefore = areas.MaxAreaCount;
            int effectiveMax = raiseCap ? GridMaxAreaCount : maxBefore;
            bool owned = areas.IsUnlocked(tileX, tileZ);
            bool adjacent = IsAdjacentToOwned(areas, tileX, tileZ);
            bool milestoneReached = Singleton<UnlockManager>.instance.Unlocked(areas.m_areaCount);
            bool underCap = areas.m_areaCount < effectiveMax;
            int price = owned ? 0 : EstimateTilePrice(areas, tileX, tileZ);
            bool affordable = price == 0 ||
                Singleton<EconomyManager>.instance.PeekResource(EconomyManager.Resource.LandPrice, price) == price;

            string reason = null;
            if (owned) reason = "Tile is already owned.";
            else if (!underCap) reason = "At the maximum area count (" + areas.m_areaCount + " of " + effectiveMax + "). Pass ignoreMaxAreaCount:true to raise the cap to " + GridMaxAreaCount + ".";
            else if (!milestoneReached) reason = "The next tile needs an area milestone the city has not reached: " + MilestoneName(areas.m_areaCount) + ".";
            else if (!adjacent) reason = "Tile is not edge-adjacent to an owned tile.";
            else if (!affordable) reason = "Not enough money for price " + price + ".";
            // CanUnlock also runs mods' IAreasExtension.OnCanUnlockArea hooks. With the cap about
            // to be raised it would still see the old cap, so it is only consulted without one.
            else if (!raiseCap && !areas.CanUnlock(tileX, tileZ)) reason = "GameAreaManager.CanUnlock refused the tile (a mod's area hook?).";

            string facts = ",\"tileX\":" + tileX + ",\"tileZ\":" + tileZ + ",\"index\":" + index +
                ",\"owned\":" + JsonUtil.Bool(owned) +
                ",\"adjacentToOwned\":" + JsonUtil.Bool(adjacent) +
                ",\"nextAreaMilestoneReached\":" + JsonUtil.Bool(milestoneReached) +
                ",\"nextAreaMilestone\":" + MilestoneJson(areas.m_areaCount) +
                ",\"ownedCount\":" + areas.m_areaCount +
                ",\"maxAreaCount\":" + maxBefore +
                ",\"effectiveMaxAreaCount\":" + effectiveMax +
                ",\"price\":" + price + ",\"priceDisplay\":" + (price / 100) +
                ",\"affordable\":" + JsonUtil.Bool(affordable);

            if (reason != null)
            {
                CommandResult refused = CommandResult.Fail(reason);
                refused.Json = "{\"ok\":false,\"dryRun\":" + JsonUtil.Bool(dryRun) +
                    ",\"canUnlock\":false,\"error\":\"" + JsonUtil.Escape(reason) + "\"" + facts + "}";
                return refused;
            }

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"canUnlock\":true" +
                    ",\"wouldRaiseMaxAreaCount\":" + JsonUtil.Bool(effectiveMax > maxBefore) + facts + "}");
            }

            // GameAreaManager.UnlockArea deactivates a tutorial guide and posts a chirper message:
            // it belongs on the simulation thread, where GameAreaTool.UnlockArea and
            // AreasWrapper.UnlockArea both run it.
            int tile = index;
            int tx = tileX;
            int tz = tileZ;
            bool raise = raiseCap && maxBefore < GridMaxAreaCount;
            SimulationJob job = null;
            job = new SimulationJob(delegate
            {
                if (!job.BeginCommit())
                {
                    return CommandResult.Fail("Timed out before the tile was bought; nothing was changed.");
                }

                bool raised = false;
                int capBefore = areas.MaxAreaCount;
                if (raise && capBefore < GridMaxAreaCount)
                {
                    SetMaxAreaCount(areas, GridMaxAreaCount);
                    raised = true;
                }

                if (!areas.CanUnlock(tx, tz))
                {
                    // Nothing was bought, so put the cap back rather than leave a failed call's side effect.
                    if (raised) SetMaxAreaCount(areas, capBefore);
                    return CommandResult.Fail("The game refused tile " + tx + "," + tz + " (GameAreaManager.CanUnlock false at commit time); nothing was changed.");
                }

                // Same order as GameAreaTool.UnlockArea: price, take the money, unlock. Peek first,
                // because FetchResource deducts the full amount even when the city cannot cover it.
                EconomyManager economy = Singleton<EconomyManager>.instance;
                long cashBefore = economy.LastCashAmount;
                int cost = areas.CalculateTilePrice(tile);
                int paid = 0;
                if (cost != 0)
                {
                    if (economy.PeekResource(EconomyManager.Resource.LandPrice, cost) != cost)
                    {
                        if (raised) SetMaxAreaCount(areas, capBefore);
                        return CommandResult.Fail("Not enough money for price " + cost + "; nothing was changed.");
                    }
                    paid = economy.FetchResource(EconomyManager.Resource.LandPrice, cost,
                        ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.Level1);
                    if (paid != cost)
                    {
                        // FetchResource has already deducted the amount (see EconomyManager.FetchResource);
                        // GameAreaTool.UnlockArea also stops here without unlocking.
                        return CommandResult.Fail("FetchResource returned " + paid + " for price " + cost +
                            " after the peek passed (an economy mod hook?); the money was deducted and the tile was not unlocked. cashBefore " +
                            cashBefore + ", cashAfter " + economy.LastCashAmount + ".");
                    }
                }

                bool unlocked = areas.UnlockArea(tile);
                string json = "{\"ok\":" + JsonUtil.Bool(unlocked) + ",\"dryRun\":false" +
                    ",\"unlocked\":" + JsonUtil.Bool(unlocked) +
                    ",\"tileX\":" + tx + ",\"tileZ\":" + tz + ",\"index\":" + tile +
                    ",\"price\":" + cost + ",\"priceDisplay\":" + (cost / 100) +
                    ",\"cashBefore\":" + cashBefore +
                    ",\"cashAfter\":" + economy.LastCashAmount +
                    ",\"ownedCount\":" + areas.m_areaCount +
                    ",\"maxAreaCount\":" + areas.MaxAreaCount +
                    ",\"raisedMaxAreaCount\":" + JsonUtil.Bool(raised);
                if (!unlocked)
                {
                    // UnlockArea returns false when its own CanUnlock refuses (a mod hook changing its
                    // answer) or TerrainManager.SetDetailedPatch fails; the game's own tool keeps the
                    // money in both cases too.
                    json += ",\"error\":\"GameAreaManager.UnlockArea returned false after payment (CanUnlock refused at unlock time, or the terrain detail patch failed); the money was deducted.\"";
                }
                if (unlocked)
                {
                    return CommandResult.FromJson(json + "}");
                }
                CommandResult failed = CommandResult.Fail("GameAreaManager.UnlockArea returned false after payment.");
                failed.Json = json + "}";
                return failed;
            });
            Singleton<SimulationManager>.instance.AddAction(job.Run);
            CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
            deferred.Deferred = job;
            return deferred;
        }

        /// <summary>The official modding setter (IAreas.maxAreaCount): clamps to 1..25 and refreshes milestones on the main thread.</summary>
        private static void SetMaxAreaCount(GameAreaManager areas, int value)
        {
            if (areas.m_AreasWrapper != null) areas.m_AreasWrapper.maxAreaCount = value;
            else areas.m_maxAreaCount = value;
        }

        private static bool IsAdjacentToOwned(GameAreaManager areas, int x, int z)
        {
            return areas.IsUnlocked(x, z - 1) || areas.IsUnlocked(x - 1, z) ||
                areas.IsUnlocked(x + 1, z) || areas.IsUnlocked(x, z + 1);
        }

        /// <summary>
        /// The price of buying this tile next. GameAreaManager.CalculateTilePrice(int) returns 0
        /// for any tile CanUnlock refuses, so this gathers the same inputs itself and calls the
        /// 10-argument overload, as GameAreaInfoPanel does for its price label. The price depends
        /// on m_areaCount, so it changes after every purchase. Game money units (cents).
        /// </summary>
        private static int EstimateTilePrice(GameAreaManager areas, int x, int z)
        {
            BuildingManager buildings = Singleton<BuildingManager>.instance;
            NetManager nets = Singleton<NetManager>.instance;
            int incoming;
            int outgoing;

            buildings.CalculateOutsideConnectionCount(ItemClass.Service.Road, ItemClass.SubService.None, out incoming, out outgoing);
            bool road = (incoming != 0 || outgoing != 0) &&
                nets.GetTileNodeCount(x, z, ItemClass.Service.Road, ItemClass.SubService.None) != 0;

            buildings.CalculateOutsideConnectionCount(ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportTrain, out incoming, out outgoing);
            bool train = (incoming != 0 || outgoing != 0) &&
                nets.GetTileNodeCount(x, z, ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportTrain) != 0;

            buildings.CalculateOutsideConnectionCount(ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportShip, out incoming, out outgoing);
            bool ship = (incoming != 0 || outgoing != 0) &&
                nets.GetTileNodeCount(x, z, ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportShip) != 0;

            buildings.CalculateOutsideConnectionCount(ItemClass.Service.PublicTransport, ItemClass.SubService.PublicTransportPlane, out incoming, out outgoing);
            bool plane = incoming != 0 || outgoing != 0;

            uint ore;
            uint oil;
            uint forest;
            uint fertility;
            uint water;
            Singleton<NaturalResourceManager>.instance.GetTileResources(x, z, out ore, out oil, out forest, out fertility, out water);
            float flatness = Singleton<TerrainManager>.instance.GetTileFlatness(x, z);
            return areas.CalculateTilePrice(ore, oil, forest, fertility, water, road, train, ship, plane, flatness);
        }

        private static MilestoneInfo AreaMilestone(int areaCount)
        {
            UnlockManager unlocks = Singleton<UnlockManager>.instance;
            if (unlocks.m_properties == null || unlocks.m_properties.m_AreaMilestones == null ||
                unlocks.m_properties.m_AreaMilestones.Length == 0)
            {
                return null;
            }
            MilestoneInfo[] milestones = unlocks.m_properties.m_AreaMilestones;
            return milestones[Mathf.Clamp(areaCount, 0, milestones.Length - 1)];
        }

        private static string MilestoneName(int areaCount)
        {
            MilestoneInfo milestone = AreaMilestone(areaCount);
            if (milestone == null) return "none";
            string localized = null;
            try { localized = milestone.GetLocalizedName(); } catch { }
            return string.IsNullOrEmpty(localized) ? milestone.name : localized + " (" + milestone.name + ")";
        }

        private static string MilestoneJson(int areaCount)
        {
            MilestoneInfo milestone = AreaMilestone(areaCount);
            if (milestone == null) return "null";
            string localized = "";
            try { localized = milestone.GetLocalizedName(); } catch { }
            return "{\"name\":\"" + JsonUtil.Escape(milestone.name) + "\"" +
                ",\"title\":\"" + JsonUtil.Escape(localized ?? "") + "\"" +
                ",\"reached\":" + JsonUtil.Bool(Singleton<UnlockManager>.instance.Unlocked(milestone)) + "}";
        }
    }
}
