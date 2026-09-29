using System.Collections.Generic;
using System;
using System.Reflection;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace SkylinesAgentBridge
{
    public static class BuildingCommands
    {
        public static CommandResult PlaceBuilding(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            string prefabName = JsonUtil.GetString(body, "buildingPrefab", "");
            float angleDegrees = JsonUtil.GetNumber(body, "angleDegrees", 0f);
            Vector3 position = ReadPoint(body, "position");

            if (prefabName == null || prefabName.Length == 0)
            {
                return CommandResult.Fail("buildingPrefab is required.");
            }

            BuildingInfo info = PrefabCollection<BuildingInfo>.FindLoaded(prefabName);
            if (info == null)
            {
                return CommandResult.Fail("Building prefab was not found: " + prefabName);
            }
            if (AssetPolicy.IsBlockedBuildingPrefab(info))
            {
                return CommandResult.Fail("Building prefab is blocked and must not be used: " + prefabName + " (" + AssetPolicy.BlockReason(prefabName) + ")");
            }

            // Match the build panel's unlock gate. Unique buildings are otherwise easy to
            // place accidentally through the API because BuildingManager.CreateBuilding does
            // not enforce the milestone itself. Keep an explicit escape hatch for a player who
            // is deliberately testing a locked prefab.
            bool ignoreUnlock = JsonUtil.GetBool(body, "ignoreUnlock", false);
            MilestoneInfo unlockMilestone = info.GetUnlockMilestone();
            if (!ignoreUnlock && !UnlockReport.IsUnlocked(unlockMilestone))
            {
                string milestoneName = unlockMilestone == null ? "unknown milestone" : (unlockMilestone.m_name ?? unlockMilestone.name ?? "unnamed milestone");
                return CommandResult.Fail("Building prefab is locked: " + prefabName + " (" + milestoneName + "). Pass ignoreUnlock:true only for an intentional test.");
            }

            // Buildings whose position the in-game tool snaps (harbors and other shoreline
            // buildings) are validated by default; anything else only on "validate":true.
            bool validate = JsonUtil.GetBool(body, "validate", NeedsPlacementCheck(info));
            if (validate)
            {
                float elevation = JsonUtil.GetNumber(body, "elevation", 0f);
                bool validateDryRun = dryRun;
                string validatePrefab = prefabName;
                Vector3 requested = position;
                float requestedAngle = angleDegrees * Mathf.Deg2Rad;
                SimulationJob job = null;
                job = new SimulationJob(delegate
                {
                    return ValidateAndPlace(info, validatePrefab, requested, requestedAngle, elevation, validateDryRun, job);
                });
                Singleton<SimulationManager>.instance.AddAction(job.Run);
                CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
                deferred.Deferred = job;
                return deferred;
            }

            TerrainManager terrain = TerrainManager.instance;
            position.y = terrain.SampleRawHeightSmoothWithWater(position, false, 0f);

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"message\":\"Place-building validation passed.\",\"buildingPrefab\":\"" + JsonUtil.Escape(prefabName) + "\",\"unlockIgnored\":" + JsonUtil.Bool(ignoreUnlock && !UnlockReport.IsUnlocked(unlockMilestone)) + "}");
            }

            SimulationManager simulation = Singleton<SimulationManager>.instance;
            BuildingManager buildings = BuildingManager.instance;
            Randomizer randomizer = simulation.m_randomizer;
            ushort buildingId;
            float angle = angleDegrees * Mathf.Deg2Rad;

            bool created = buildings.CreateBuilding(
                out buildingId,
                ref randomizer,
                info,
                position,
                angle,
                info.GetLength(),
                simulation.m_currentBuildIndex);

            simulation.m_randomizer = randomizer;

            if (!created)
            {
                return CommandResult.Fail("Failed to create building.");
            }

            simulation.m_currentBuildIndex += 1u;

            string json = "{\"ok\":true,\"dryRun\":false,\"buildingId\":" + buildingId +
                ",\"buildingPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"" +
                ",\"service\":\"" + JsonUtil.Escape(info.m_class.m_service.ToString()) + "\"" +
                ",\"subService\":\"" + JsonUtil.Escape(info.m_class.m_subService.ToString()) + "\"" +
                ",\"unlockIgnored\":" + JsonUtil.Bool(ignoreUnlock && !UnlockReport.IsUnlocked(unlockMilestone)) + "}";

            Debug.Log("[SkylinesAgentBridge] Placed building " + buildingId + " with prefab " + prefabName);
            return CommandResult.FromJson(json);
        }

        public static CommandResult MoveBuilding(string body)
        {
            ushort id = (ushort)JsonUtil.GetNumber(body, "id", 0f);
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);

            if (id == 0)
            {
                return CommandResult.Fail("id is required.");
            }

            BuildingManager buildings = BuildingManager.instance;
            if ((buildings.m_buildings.m_buffer[id].m_flags & Building.Flags.Created) == Building.Flags.None)
            {
                return CommandResult.Fail("Building was not found: " + id);
            }

            Building oldBuilding = buildings.m_buildings.m_buffer[id];
            BuildingInfo info = oldBuilding.Info;
            if (info == null)
            {
                return CommandResult.Fail("Building prefab info was not found for building: " + id);
            }
            if (AssetPolicy.IsBlockedBuildingPrefab(info))
            {
                return CommandResult.Fail("Building prefab is blocked and must not be used for move/recreate: " + info.name + " (" + AssetPolicy.BlockReason(info.name) + ")");
            }

            Vector3 position = ReadPoint(body, "position");
            TerrainManager terrain = TerrainManager.instance;
            position.y = terrain.SampleRawHeightSmoothWithWater(position, false, 0f);
            float angleDegrees = JsonUtil.GetNumber(body, "angleDegrees", oldBuilding.m_angle * Mathf.Rad2Deg);
            float angle = angleDegrees * Mathf.Deg2Rad;

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"message\":\"Move-building validation passed.\",\"id\":" + id +
                    ",\"buildingPrefab\":\"" + JsonUtil.Escape(info.name) + "\"}");
            }

            SimulationManager simulation = Singleton<SimulationManager>.instance;
            Randomizer randomizer = simulation.m_randomizer;
            ushort newBuildingId;
            bool created = buildings.CreateBuilding(
                out newBuildingId,
                ref randomizer,
                info,
                position,
                angle,
                info.GetLength(),
                simulation.m_currentBuildIndex);

            simulation.m_randomizer = randomizer;
            if (!created)
            {
                return CommandResult.Fail("Failed to create moved building.");
            }

            simulation.m_currentBuildIndex += 1u;
            GameThreadHelpers.ReleaseBuilding(buildings, id);

            string json = "{\"ok\":true,\"dryRun\":false,\"oldBuildingId\":" + id +
                ",\"newBuildingId\":" + newBuildingId +
                ",\"buildingPrefab\":\"" + JsonUtil.Escape(info.name) + "\"" +
                ",\"position\":{\"x\":" + JsonUtil.Number(position.x) +
                ",\"y\":" + JsonUtil.Number(position.y) +
                ",\"z\":" + JsonUtil.Number(position.z) + "}}";

            Debug.Log("[SkylinesAgentBridge] Moved building " + id + " to " + newBuildingId + " with prefab " + info.name);
            return CommandResult.FromJson(json);
        }

        /// <summary>
        /// The info panel's "Empty" button for landfills and cemeteries (ToggleEmptying):
        /// BuildingAI.SetEmptying(id, ref building, value) on the simulation thread. Emptying
        /// sends the contents to other facilities of the same service, so it needs capacity
        /// elsewhere (incinerators, crematoria).
        /// </summary>
        public static CommandResult SetBuildingEmptying(string body)
        {
            ushort id = (ushort)JsonUtil.GetNumber(body, "id", 0f);
            bool emptying = JsonUtil.GetBool(body, "emptying", true);

            if (id == 0)
            {
                return CommandResult.Fail("id is required.");
            }

            BuildingManager buildings = BuildingManager.instance;
            if ((buildings.m_buildings.m_buffer[id].m_flags & Building.Flags.Created) == Building.Flags.None)
            {
                return CommandResult.Fail("Building was not found: " + id);
            }

            ushort buildingId = id;
            bool value = emptying;
            SimulationJob job = null;
            job = new SimulationJob(delegate
            {
                if (!job.BeginCommit())
                {
                    return CommandResult.Fail("Timed out before emptying was set.");
                }
                BuildingInfo info = buildings.m_buildings.m_buffer[buildingId].Info;
                if (info == null || info.m_buildingAI == null)
                {
                    return CommandResult.Fail("Building has no AI: " + buildingId);
                }
                bool full = info.m_buildingAI.IsFull(buildingId, ref buildings.m_buildings.m_buffer[buildingId]);
                info.m_buildingAI.SetEmptying(buildingId, ref buildings.m_buildings.m_buffer[buildingId], value);
                Building.Flags flags = buildings.m_buildings.m_buffer[buildingId].m_flags;
                return CommandResult.FromJson("{\"ok\":true,\"id\":" + buildingId +
                    ",\"prefab\":\"" + JsonUtil.Escape(info.name) + "\"" +
                    ",\"ai\":\"" + JsonUtil.Escape(info.m_buildingAI.GetType().Name) + "\"" +
                    ",\"emptying\":" + JsonUtil.Bool(value) +
                    ",\"wasFull\":" + JsonUtil.Bool(full) +
                    ",\"flags\":\"" + JsonUtil.Escape(flags.ToString()) + "\"}");
            });
            Singleton<SimulationManager>.instance.AddAction(job.Run);
            CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
            deferred.Deferred = job;
            return deferred;
        }

        public static CommandResult SetBuildingActive(string body)
        {
            ushort id = (ushort)JsonUtil.GetNumber(body, "id", 0f);
            bool active = JsonUtil.GetBool(body, "active", true);

            if (id == 0)
            {
                return CommandResult.Fail("id is required.");
            }

            BuildingManager buildings = BuildingManager.instance;
            if ((buildings.m_buildings.m_buffer[id].m_flags & Building.Flags.Created) == Building.Flags.None)
            {
                return CommandResult.Fail("Building was not found: " + id);
            }

            // Same as the info panel's on/off button (CityServiceWorldInfoPanel.ToggleBuilding):
            // BuildingAI.SetProductionRate(id, ref building, 100 or 0) on the simulation thread.
            // The old version only flipped the Active flag on a copy, which the game set back
            // within a step, so the building never actually stopped.
            ushort buildingId = id;
            bool turnOn = active;
            SimulationJob job = null;
            job = new SimulationJob(delegate
            {
                if (!job.BeginCommit())
                {
                    return CommandResult.Fail("Timed out before the building was toggled.");
                }
                BuildingInfo info = buildings.m_buildings.m_buffer[buildingId].Info;
                if (info == null || info.m_buildingAI == null)
                {
                    return CommandResult.Fail("Building has no AI: " + buildingId);
                }
                byte rateBefore = buildings.m_buildings.m_buffer[buildingId].m_productionRate;
                info.m_buildingAI.SetProductionRate(buildingId, ref buildings.m_buildings.m_buffer[buildingId], (byte)(turnOn ? 100 : 0));
                byte rateAfter = buildings.m_buildings.m_buffer[buildingId].m_productionRate;
                return CommandResult.FromJson("{\"ok\":true,\"id\":" + buildingId +
                    ",\"active\":" + JsonUtil.Bool(turnOn) +
                    ",\"prefab\":\"" + JsonUtil.Escape(info.name) + "\"" +
                    ",\"productionRateBefore\":" + rateBefore +
                    ",\"productionRateAfter\":" + rateAfter +
                    ",\"flags\":\"" + JsonUtil.Escape(buildings.m_buildings.m_buffer[buildingId].m_flags.ToString()) + "\"}");
            });
            Singleton<SimulationManager>.instance.AddAction(job.Run);
            CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
            deferred.Deferred = job;
            return deferred;
        }

        private static void InvokeManualActivation(BuildingAI ai, ushort id, ref Building building, bool active)
        {
            string methodName = active ? "ManualActivation" : "ManualDeactivation";
            MethodInfo method = ai.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                method = typeof(BuildingAI).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (method == null)
            {
                return;
            }

            object[] args = new object[] { id, building };
            method.Invoke(ai, args);
            building = (Building)args[1];
        }

        private const ToolBase.ToolErrors ShoreClearedErrors =
            ToolBase.ToolErrors.CannotBuildOnWater | ToolBase.ToolErrors.CannotConnect | ToolBase.ToolErrors.HeightTooHigh;

        private sealed class PlacementCheck
        {
            public Vector3 Position;
            public float Angle;
            public ToolBase.ToolErrors Errors;
            public float WaterHeight;
            public bool ShoreFound;
            public bool CanalSnap;
            public Segment3 Connection;
            public int ConstructionCost;
            public string Branch;
            public List<int> CollidingSegments = new List<int>();
            public List<int> CollidingBuildings = new List<int>();
        }

        private static bool NeedsPlacementCheck(BuildingInfo info)
        {
            return info.m_placementMode == BuildingInfo.PlacementMode.Shoreline ||
                info.m_placementMode == BuildingInfo.PlacementMode.ShorelineOrGround;
        }

        /// <summary>
        /// Runs on the simulation thread. Mirrors BuildingTool.SimulationStep (Assembly-CSharp,
        /// the Shoreline/ShorelineOrGround branch): SnapToCanal, then TerrainManager.GetShorePos
        /// twice within 50 m, offset by m_placementOffset, angle from the shore direction, then
        /// BuildingAI.CheckBuildPosition with the shore's water height (HarborAI adds the
        /// 32 m-above-water rule and ShipDockAI.FindConnectionPath + HasWater for the dock), then
        /// BuildingTool.CheckSpace in test mode (no objects are released). Other placement modes
        /// get CheckBuildPosition + CheckSpace at the given point on terrain; the zone-grid snap
        /// of Roadside growables is not reproduced.
        /// </summary>
        private static PlacementCheck CheckPlacement(BuildingInfo info, Vector3 requested, float angle, float elevation)
        {
            PlacementCheck check = new PlacementCheck();
            BuildingAI ai = info.m_buildingAI;
            TerrainManager terrain = Singleton<TerrainManager>.instance;
            ulong[] segmentBuffer = new ulong[NetManager.MAX_SEGMENT_COUNT >> 6];
            ulong[] buildingBuffer = new ulong[BuildingManager.MAX_BUILDING_COUNT >> 6];
            int width = info.m_cellWidth;
            int length = info.m_cellLength;
            Vector3 pos = requested;
            float waterHeight = 0f;
            Segment3 connection = new Segment3();
            int productionRate;
            int constructionCost = 0;
            float minY;
            float maxY;
            float buildY;
            ToolBase.ToolErrors errors = ToolBase.ToolErrors.None;
            BuildingInfo.PlacementMode mode = info.m_placementMode;

            if (mode == BuildingInfo.PlacementMode.Shoreline || mode == BuildingInfo.PlacementMode.ShorelineOrGround)
            {
                Vector3 canalPos;
                Vector3 canalDir;
                bool isQuay;
                bool canal = BuildingTool.SnapToCanal(pos, out canalPos, out canalDir, out isQuay, 40f, false);
                Vector3 shorePos;
                Vector3 shoreDir;
                bool shore = terrain.GetShorePos(canalPos, 50f, out shorePos, out shoreDir, out waterHeight);

                if (canal)
                {
                    check.Branch = "canal";
                    check.CanalSnap = true;
                    check.ShoreFound = shore;
                    pos = canalPos;
                    angle = Mathf.Atan2(canalDir.x, -canalDir.z);
                    float buildWater = Mathf.Max(0f, canalPos.y);
                    Building.SampleBuildingHeight(pos, angle, width, length, info, out minY, out maxY, out buildY, ref buildWater);
                    minY -= 20f;
                    buildY = Mathf.Max(pos.y, buildY);
                    float groundY = pos.y;
                    pos.y = buildY;
                    errors = ai.CheckBuildPosition(0, ref pos, ref angle, waterHeight, elevation, ref connection, out productionRate, out constructionCost);
                    errors |= BuildingTool.CheckSpace(info, BuildingInfo.PlacementMode.Shoreline, 0, pos, minY, buildY + info.m_collisionHeight, angle, width, length, true, segmentBuffer, buildingBuffer);
                    if (groundY - minY > 128f)
                    {
                        errors |= ToolBase.ToolErrors.HeightTooHigh;
                    }
                }
                else if (shore)
                {
                    pos = shorePos;
                    if (terrain.GetShorePos(pos, 50f, out shorePos, out shoreDir, out waterHeight))
                    {
                        check.Branch = "shore";
                        check.ShoreFound = true;
                        shorePos += shoreDir.normalized * info.m_placementOffset;
                        pos = shorePos;
                        angle = Mathf.Atan2(shoreDir.x, -shoreDir.z);
                        Building.SampleBuildingHeight(pos, angle, width, length, info, out minY, out maxY, out buildY);
                        minY = Mathf.Min(waterHeight, minY);
                        buildY = Mathf.Max(pos.y, buildY);
                        float groundY = pos.y;
                        pos.y = buildY;
                        errors = ai.CheckBuildPosition(0, ref pos, ref angle, waterHeight, elevation, ref connection, out productionRate, out constructionCost);
                        errors |= BuildingTool.CheckSpace(info, BuildingInfo.PlacementMode.Shoreline, 0, pos, minY, buildY + info.m_collisionHeight, angle, width, length, true, segmentBuffer, buildingBuffer);
                        if (groundY - waterHeight > 128f)
                        {
                            errors |= ToolBase.ToolErrors.HeightTooHigh;
                        }
                        if (buildY <= waterHeight)
                        {
                            errors = (errors & ~ShoreClearedErrors) | ToolBase.ToolErrors.ShoreNotFound;
                        }
                    }
                    else
                    {
                        check.Branch = "shore-lost";
                        errors = ai.CheckBuildPosition(0, ref pos, ref angle, waterHeight, elevation, ref connection, out productionRate, out constructionCost);
                        errors = (errors & ~ShoreClearedErrors) | ToolBase.ToolErrors.ShoreNotFound;
                    }
                }
                else if (mode == BuildingInfo.PlacementMode.ShorelineOrGround)
                {
                    check.Branch = "ground-fallback";
                    pos -= Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.down) * info.m_centerOffset;
                    errors = CheckOnGround(info, ai, ref pos, ref angle, waterHeight, elevation, ref connection, out constructionCost, segmentBuffer, buildingBuffer);
                }
                else
                {
                    check.Branch = "no-shore";
                    errors = ToolBase.ToolErrors.ShoreNotFound;
                }
            }
            else
            {
                check.Branch = "ground";
                errors = CheckOnGround(info, ai, ref pos, ref angle, waterHeight, elevation, ref connection, out constructionCost, segmentBuffer, buildingBuffer);
            }

            if (!Singleton<BuildingManager>.instance.CheckLimits())
            {
                errors |= ToolBase.ToolErrors.TooManyObjects;
            }

            check.Position = pos;
            check.Angle = angle;
            check.Errors = errors;
            check.WaterHeight = waterHeight;
            check.Connection = connection;
            check.ConstructionCost = constructionCost;
            // CheckSpace marks what it collides with; report it so a caller can see what is in the way.
            CollectBits(segmentBuffer, check.CollidingSegments);
            CollectBits(buildingBuffer, check.CollidingBuildings);
            return check;
        }

        private static ToolBase.ToolErrors CheckOnGround(BuildingInfo info, BuildingAI ai, ref Vector3 pos, ref float angle, float waterHeight, float elevation, ref Segment3 connection, out int constructionCost, ulong[] segmentBuffer, ulong[] buildingBuffer)
        {
            float minY;
            float maxY;
            float buildY;
            int productionRate;
            Building.SampleBuildingHeight(pos, angle, info.m_cellWidth, info.m_cellLength, info, out minY, out maxY, out buildY);
            pos.y = buildY;
            ToolBase.ToolErrors errors = ai.CheckBuildPosition(0, ref pos, ref angle, waterHeight, elevation, ref connection, out productionRate, out constructionCost);
            errors |= BuildingTool.CheckSpace(info, BuildingInfo.PlacementMode.OnGround, 0, pos, minY, buildY + info.m_collisionHeight, angle, info.m_cellWidth, info.m_cellLength, true, segmentBuffer, buildingBuffer);
            if ((errors & ToolBase.ToolErrors.CannotBuildOnWater) == ToolBase.ToolErrors.None && maxY - minY > info.m_maxHeightOffset)
            {
                errors |= ToolBase.ToolErrors.SlopeTooSteep;
            }
            return errors;
        }

        private static void CollectBits(ulong[] buffer, List<int> ids)
        {
            for (int word = 0; word < buffer.Length && ids.Count < 64; word++)
            {
                ulong bits = buffer[word];
                if (bits == 0UL)
                {
                    continue;
                }
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((bits & (1UL << bit)) != 0UL)
                    {
                        ids.Add((word << 6) | bit);
                    }
                }
            }
        }

        private static string IdList(List<int> ids)
        {
            string[] parts = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                parts[i] = ids[i].ToString();
            }
            return "[" + string.Join(",", parts) + "]";
        }

        private static string ErrorNames(ToolBase.ToolErrors errors)
        {
            StringBuilder sb = new StringBuilder("[");
            bool first = true;
            ulong bits = (ulong)errors;
            for (int i = 0; i < 64; i++)
            {
                ulong bit = 1UL << i;
                if ((bits & bit) == 0UL)
                {
                    continue;
                }
                string name = Enum.GetName(typeof(ToolBase.ToolErrors), (ToolBase.ToolErrors)bit);
                if (name == null)
                {
                    name = "0x" + bit.ToString("X");
                }
                if (!first)
                {
                    sb.Append(",");
                }
                sb.Append("\"").Append(JsonUtil.Escape(name)).Append("\"");
                first = false;
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string PointJson(Vector3 p)
        {
            return "{\"x\":" + JsonUtil.Number(p.x) + ",\"y\":" + JsonUtil.Number(p.y) + ",\"z\":" + JsonUtil.Number(p.z) + "}";
        }

        private static string CheckJson(BuildingInfo info, Vector3 requested, PlacementCheck check)
        {
            bool hasConnection = check.Connection.a != Vector3.zero || check.Connection.b != Vector3.zero;
            Vector3 d = check.Position - requested;
            d.y = 0f;
            return "\"validated\":true" +
                ",\"placementMode\":\"" + JsonUtil.Escape(info.m_placementMode.ToString()) + "\"" +
                ",\"branch\":\"" + JsonUtil.Escape(check.Branch) + "\"" +
                ",\"requested\":" + PointJson(requested) +
                ",\"position\":" + PointJson(check.Position) +
                ",\"snapDistance\":" + JsonUtil.Number(d.magnitude) +
                ",\"angleDegrees\":" + JsonUtil.Number(check.Angle * Mathf.Rad2Deg) +
                ",\"shoreFound\":" + JsonUtil.Bool(check.ShoreFound) +
                ",\"canalSnap\":" + JsonUtil.Bool(check.CanalSnap) +
                ",\"waterHeight\":" + JsonUtil.Number(check.WaterHeight) +
                ",\"heightAboveWater\":" + JsonUtil.Number(check.Position.y - check.WaterHeight) +
                ",\"connection\":" + (hasConnection ? "{\"a\":" + PointJson(check.Connection.a) + ",\"b\":" + PointJson(check.Connection.b) + "}" : "null") +
                ",\"constructionCost\":" + check.ConstructionCost +
                ",\"toolErrors\":" + ErrorNames(check.Errors) +
                ",\"subBuildings\":" + (info.m_subBuildings == null ? 0 : info.m_subBuildings.Length) +
                ",\"collidingSegmentIds\":" + IdList(check.CollidingSegments) +
                ",\"collidingBuildingIds\":" + IdList(check.CollidingBuildings) +
                ",\"canPlace\":" + JsonUtil.Bool(check.Errors == ToolBase.ToolErrors.None);
        }

        /// <summary>Simulation thread: validate, and unless dryRun, create at the adjusted position.</summary>
        private static CommandResult ValidateAndPlace(BuildingInfo info, string prefabName, Vector3 requested, float angle, float elevation, bool dryRun, SimulationJob job)
        {
            PlacementCheck check = CheckPlacement(info, requested, angle, elevation);
            string checkJson = CheckJson(info, requested, check);

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"buildingPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"," + checkJson + "}");
            }

            if (check.Errors != ToolBase.ToolErrors.None)
            {
                CommandResult fail = CommandResult.Fail("Placement rejected by " + info.m_buildingAI.GetType().Name + ".CheckBuildPosition/CheckSpace; nothing was placed.");
                fail.Json = "{\"ok\":false,\"dryRun\":false,\"error\":\"" + JsonUtil.Escape(fail.Error) + "\",\"buildingPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"," + checkJson + "}";
                return fail;
            }

            if (job != null && !job.BeginCommit())
            {
                return CommandResult.Fail("Caller gave up waiting; nothing was placed.");
            }

            SimulationManager simulation = Singleton<SimulationManager>.instance;
            BuildingManager buildings = Singleton<BuildingManager>.instance;
            Randomizer randomizer = simulation.m_randomizer;
            ushort buildingId;
            bool created = buildings.CreateBuilding(out buildingId, ref randomizer, info, check.Position, check.Angle, info.GetLength(), simulation.m_currentBuildIndex);
            simulation.m_randomizer = randomizer;
            if (!created)
            {
                return CommandResult.Fail("Validation passed but BuildingManager.CreateBuilding failed (building pool full?).");
            }
            simulation.m_currentBuildIndex += 1u;
            if (info.m_subBuildings != null)
            {
                Quaternion rotation = Quaternion.AngleAxis(check.Angle * Mathf.Rad2Deg, Vector3.down);
                for (int i = 0; i < info.m_subBuildings.Length; i++)
                {
                    BuildingInfo.SubInfo sub = info.m_subBuildings[i];
                    if (sub.m_buildingInfo == null)
                    {
                        continue;
                    }
                    Vector3 subPosition = check.Position + (rotation * sub.m_position);
                    if (sub.m_fixedHeight)
                    {
                        subPosition.y = sub.m_position.y;
                    }
                    float subAngle = check.Angle + (sub.m_angle * Mathf.Deg2Rad);
                    randomizer = simulation.m_randomizer;
                    ushort subId;
                    bool subCreated = buildings.CreateBuilding(out subId, ref randomizer, sub.m_buildingInfo, subPosition, subAngle, 0, simulation.m_currentBuildIndex);
                    simulation.m_randomizer = randomizer;
                    if (!subCreated)
                    {
                        buildings.ReleaseBuilding(buildingId);
                        return CommandResult.Fail("Main building " + buildingId + " was released because sub-building " + i + " failed to create.");
                    }
                    simulation.m_currentBuildIndex += 1u;
                }
            }

            Debug.Log("[SkylinesAgentBridge] Placed validated building " + buildingId + " with prefab " + prefabName);
            return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"buildingId\":" + buildingId +
                ",\"buildingPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"" +
                ",\"service\":\"" + JsonUtil.Escape(info.m_class.m_service.ToString()) + "\"" +
                ",\"subService\":\"" + JsonUtil.Escape(info.m_class.m_subService.ToString()) + "\"," + checkJson + "}");
        }

        private static Vector3 ReadPoint(string body, string name)
        {
            float x = JsonUtil.GetPointNumber(body, name, "x", 0f);
            float z = JsonUtil.GetPointNumber(body, name, "z", 0f);
            float y = JsonUtil.GetPointNumber(body, name, "y", 0f);
            return new Vector3(x, y, z);
        }
    }
}
