using ColossalFramework;
using ColossalFramework.Math;
using System.Collections.Generic;
using UnityEngine;

namespace SkylinesAgentBridge
{
    public static class RoadCommands
    {
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

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"message\":\"Build-road validation passed.\",\"roadPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"" +
                    ",\"startY\":" + JsonUtil.Number(start.y) + ",\"endY\":" + JsonUtil.Number(end.y) + "}");
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
                    return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"segmentId\":" + existing +
                        ",\"startNodeId\":" + startNode +
                        ",\"endNodeId\":" + endNode +
                        ",\"alreadyConnected\":true" +
                        ",\"roadPrefab\":\"" + JsonUtil.Escape(prefabName) + "\"}");
                }

                createdSegments.Add(segment);

                string json = "{\"ok\":true,\"dryRun\":false,\"segmentId\":" + segment +
                    ",\"startNodeId\":" + startNode +
                    ",\"endNodeId\":" + endNode +
                    ",\"alreadyConnected\":false" +
                    ",\"createdNodeIds\":[" + string.Join(",", ToStrings(createdNodes)) + "]" +
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
