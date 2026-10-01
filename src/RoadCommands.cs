using ColossalFramework;
using ColossalFramework.Math;
using System.Collections.Generic;
using UnityEngine;

namespace SkylinesAgentBridge
{
    public static class RoadCommands
    {
        /// <summary>The path-on-road guard applies while both endpoints are below this height (m).</summary>
        private const float PathRoadGuardMaxElevation = 5f;

        public static CommandResult BuildRoad(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            string prefabName = JsonUtil.GetString(body, "roadPrefab", "Basic Road");
            string name = JsonUtil.GetString(body, "name", "");

            Vector3 start = ReadPoint(body, "start");
            Vector3 end = ReadPoint(body, "end");

            NetInfo info = PrefabCollection<NetInfo>.FindLoaded(prefabName);
            if (info == null)
            {
                return CommandResult.Fail("Road prefab was not found: " + prefabName);
            }

            if ((end - start).sqrMagnitude < 16f)
            {
                return CommandResult.Fail("Road is too short.");
            }

            // Optional per-point "elevation": metres above the terrain (negative = below it).
            // Elevated track and roads need it; without it every node sits on the ground.
            float startElevation = JsonUtil.GetPointNumber(body, "start", "elevation", 0f);
            float endElevation = JsonUtil.GetPointNumber(body, "end", "elevation", 0f);

            TerrainManager terrain = TerrainManager.instance;
            start.y = terrain.SampleRawHeightSmoothWithWater(start, false, 0f) + startElevation;
            end.y = terrain.SampleRawHeightSmoothWithWater(end, false, 0f) + endElevation;

            // Never build a ground road/track/path on water: the heights above come from the water
            // surface, so without this check the node is created floating on a river or lake.
            bool allowWater = JsonUtil.GetBool(body, "allowWater", false);
            string waterCheck;
            try
            {
                float guardSnap = NodeHelper.ClampSnapDistance(JsonUtil.GetNumber(body, "snapDistance", NodeHelper.DefaultSnapDistance));
                WaterGuard.AssertNetworkPointsClear(info, start, startElevation, end, endElevation, allowWater, guardSnap, out waterCheck);
            }
            catch (BridgeException ex)
            {
                return CommandResult.Fail(ex.Message);
            }

            // A surface pedestrian path never joins a road node or splits a road segment in the game:
            // the net tool's raycast cannot return road nodes/segments for a path (see
            // NodeHelper.CanJoinPathToRoadNode) and NetTool.CreateNode refuses a path drawn onto a road
            // as a collision. Paths reach roads by a lane connection from a path node within 16.5 m
            // (dead end) of the road's sidewalk. So a path endpoint on the road would only stack an
            // unjoined node on top of it; refuse and say where the road edge is instead.
            // Any endpoint below 5 m counts as ground here: a tiny elevation still stacks a node on
            // the road (FindOrCreateNode rounds a non-zero elevation up to 1 m).
            bool allowRoadOverlap = JsonUtil.GetBool(body, "allowRoadOverlap", false);
            if (!allowRoadOverlap && Mathf.Abs(startElevation) < PathRoadGuardMaxElevation &&
                Mathf.Abs(endElevation) < PathRoadGuardMaxElevation && NodeHelper.IsSurfacePathWithoutIntersect(info))
            {
                List<ushort> roads = NodeHelper.CollectSurfaceRoads(start, end);
                NodeHelper.RoadOverlap overlap = NodeHelper.FindRoadOverlap(start, end, roads);
                if (overlap.Segment != 0)
                {
                    return RoadOverlapFailure(prefabName, start, end, overlap, roads);
                }
            }

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"message\":\"Build-road validation passed.\",\"roadPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"" +
                    ",\"startY\":" + JsonUtil.Number(start.y) + ",\"endY\":" + JsonUtil.Number(end.y) +
                    ",\"waterCheck\":" + waterCheck + "}");
            }

            // Node reuse now goes through NodeHelper, which snaps at 8m instead of the old 2m
            // and searches the node grid instead of all 32768 slots. The wider radius is the
            // point: at 2m an agent's rounded coordinates land next to an existing junction
            // rather than on it, producing the "looks connected, is not connected" segments
            // the repo's own gotchas list warns about.
            float snapDistance = NodeHelper.ClampSnapDistance(
                JsonUtil.GetNumber(body, "snapDistance", NodeHelper.DefaultSnapDistance));

            List<ushort> createdNodes = new List<ushort>();
            List<ushort> createdSegments = new List<ushort>();

            try
            {
                bool createdStart;
                ushort startNode = NodeHelper.FindOrCreateNode(start, info, snapDistance, startElevation, out createdStart);
                if (createdStart)
                {
                    createdNodes.Add(startNode);
                }

                bool createdEnd;
                ushort endNode = NodeHelper.FindOrCreateNode(end, info, snapDistance, endElevation, out createdEnd);
                if (createdEnd)
                {
                    createdNodes.Add(endNode);
                }

                if (startNode == endNode)
                {
                    NodeHelper.Rollback(createdSegments, createdNodes);
                    return CommandResult.Fail("Both endpoints snapped to the same node (" + startNode +
                        "). Move them further apart or lower snapDistance.");
                }

                ushort segment = NodeHelper.CreateSegment(startNode, endNode, info, name);
                if (segment == 0)
                {
                    ushort existing = NodeHelper.FindExistingSegment(startNode, endNode);
                    NetInfo existingInfo = existing == 0 ? null : NetManager.instance.m_segments.m_buffer[existing].Info;
                    if (existingInfo != null && existingInfo.m_class != null && info.m_class != null && existingInfo.m_class.m_service != info.m_class.m_service)
                    {
                        // Only reachable when a path reused two road nodes (NodeHelper.CanJoinPathToRoadNode):
                        // the pair is joined by a different kind of network, so nothing was built.
                        NodeHelper.Rollback(createdSegments, createdNodes);
                        return CommandResult.Fail("Nodes " + startNode + " and " + endNode + " are already joined by segment " + existing +
                            " (" + existingInfo.name + "); a " + prefabName + " segment was not built on top of it.");
                    }
                    return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"segmentId\":" + existing +
                        ",\"startNodeId\":" + startNode +
                        ",\"endNodeId\":" + endNode +
                        ",\"alreadyConnected\":true" +
                        ",\"waterCheck\":" + waterCheck +
                        ",\"roadPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"}");
                }

                createdSegments.Add(segment);

                string json = "{\"ok\":true,\"dryRun\":false,\"segmentId\":" + segment +
                    ",\"startNodeId\":" + startNode +
                    ",\"endNodeId\":" + endNode +
                    ",\"alreadyConnected\":false" +
                    ",\"createdNodeIds\":[" + string.Join(",", ToStrings(createdNodes)) + "]" +
                    ",\"waterCheck\":" + waterCheck +
                    ",\"roadPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"}";

                Debug.Log("[SkylinesAgentBridge] Built road segment " + segment + " with prefab " + prefabName);
                return CommandResult.FromJson(json);
            }
            catch (System.Exception ex)
            {
                NodeHelper.Rollback(createdSegments, createdNodes);
                return CommandResult.Fail(ex is BridgeException
                    ? ex.Message
                    : ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// The refusal for a surface path that runs onto a surface road. For each endpoint that is
        /// inside the road, suggests the first point 1 m clear of every surface road when walking
        /// back along the path towards the other end (null when the path is inside a road all the
        /// way, or only its middle crosses one).
        /// </summary>
        private static CommandResult RoadOverlapFailure(string prefabName, Vector3 start, Vector3 end, NodeHelper.RoadOverlap overlap, List<ushort> roads)
        {
            NetManager net = NetManager.instance;
            NetInfo road = net.m_segments.m_buffer[overlap.Segment].Info;
            string roadName = road == null ? "" : road.name;
            Vector3? suggestedStart = SuggestClearPoint(start, end, roads);
            Vector3? suggestedEnd = SuggestClearPoint(end, start, roads);

            string message = prefabName + " runs onto road segment " + overlap.Segment + " (" + roadName + ") at " +
                JsonUtil.Number(overlap.Point.x) + "," + JsonUtil.Number(overlap.Point.z) + ", " +
                JsonUtil.Number(overlap.DistanceFromCentre) + " m from its centre line (road half-width " +
                JsonUtil.Number(overlap.RoadHalfWidth) + " m). The game never joins a surface path to a road node " +
                "or splits a road for it; it refuses this as a collision. End the path at the road edge (just " +
                "outside the half-width): if the road has sidewalks (pedestrian lanes; highways do not), the path's " +
                "dead-end node then links to the nearest one by a lane connection within 16.5 m. Cross roads with " +
                "elevated/tunnel pieces. allowRoadOverlap:true builds it anyway.";

            CommandResult result = CommandResult.Fail(message);
            result.Json = "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(message) + "\"" +
                ",\"reason\":\"pathOnRoad\"" +
                ",\"roadSegmentId\":" + overlap.Segment +
                ",\"roadPrefab\":\"" + JsonUtil.Escape(roadName) + "\"" +
                ",\"overlapAt\":" + PointJson(overlap.Point) +
                ",\"distanceFromCentre\":" + JsonUtil.Number(overlap.DistanceFromCentre) +
                ",\"roadHalfWidth\":" + JsonUtil.Number(overlap.RoadHalfWidth) +
                ",\"suggestedStart\":" + (suggestedStart.HasValue ? PointJson(suggestedStart.Value) : "null") +
                ",\"suggestedEnd\":" + (suggestedEnd.HasValue ? PointJson(suggestedEnd.Value) : "null") + "}";
            return result;
        }

        /// <summary>
        /// When <paramref name="point"/> is inside a surface road, walks from it towards
        /// <paramref name="other"/> in 0.5 m steps and returns the first position that is 1 m clear
        /// of every surface road. Null when the point is already clear or no such position exists
        /// before reaching the other end.
        /// </summary>
        private static Vector3? SuggestClearPoint(Vector3 point, Vector3 other, List<ushort> roads)
        {
            if (NodeHelper.FindRoadOverlap(point, point, roads).Segment == 0)
            {
                return null;
            }
            Vector3 line = other - point;
            line.y = 0f;
            float length = line.magnitude;
            if (length < 0.5f)
            {
                return null;
            }
            Vector3 direction = line / length;
            for (float t = 0.5f; t + 1f <= length; t += 0.5f)
            {
                if (NodeHelper.FindRoadOverlap(point + direction * t, point + direction * t, roads).Segment == 0)
                {
                    Vector3 clear = point + direction * (t + 1f);
                    if (NodeHelper.FindRoadOverlap(clear, clear, roads).Segment == 0)
                    {
                        clear.y = 0f;
                        return clear;
                    }
                }
            }
            return null;
        }

        private static string PointJson(Vector3 p)
        {
            return "{\"x\":" + JsonUtil.Number(p.x) + ",\"z\":" + JsonUtil.Number(p.z) + "}";
        }

        private static string[] ToStrings(List<ushort> ids)
        {
            string[] values = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                values[i] = ids[i].ToString();
            }
            return values;
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
