using System.Text;
using ColossalFramework.Math;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Refuses ground networks and buildings placed on water. The builders set a node's height
    /// from SampleRawHeightSmoothWithWater, so without this a ground road over a river is created
    /// floating on the water surface. The water surface at a point moves several metres per minute,
    /// so the decision uses the game's own hasWater flags rather than a fixed height comparison.
    /// </summary>
    public static class WaterGuard
    {
        /// <summary>Water deeper than this over the terrain counts as water even when hasWater is false.</summary>
        private const float DepthEpsilon = 0.05f;

        /// <summary>
        /// An endpoint whose |elevation| is below this is a ground piece. FindOrCreateNode rounds
        /// any non-zero elevation up to at least 1 m, and a piece under 1 m still sits on the surface.
        /// </summary>
        public const float GroundElevationLimit = 1f;

        /// <summary>Spacing of the samples along a centreline and across a building footprint.</summary>
        public const float SampleSpacing = 8f;

        private const int MaxReportedWetPoints = 8;

        private const string Tail = ". Build it elevated with an elevated or bridge prefab and elevation (1 m or more) on both points, or move it onto dry land. (allowWater:true on the raw HTTP body overrides this for a human-run script; the MCP tools do not expose it.)";

        public static bool IsGround(float elevation)
        {
            return Mathf.Abs(elevation) < GroundElevationLimit;
        }

        /// <summary>
        /// True for ground roads, train/metro track and pedestrian paths. Pipes, power lines,
        /// quays, canals, flood walls, ship/ferry paths, pedestrian bridges, bridge pieces and
        /// tunnels are not guarded.
        /// </summary>
        public static bool GuardsPrefab(NetInfo info)
        {
            if (info == null || info.m_netAI == null)
            {
                return false;
            }
            NetAI ai = info.m_netAI;
            bool groundKind = ai is RoadBaseAI || ai is TrainTrackBaseAI || ai is MetroTrackBaseAI || ai is PedestrianPathAI;
            if (!groundKind)
            {
                return false;
            }
            // Bridge pieces derive from the ground AIs (RoadBridgeAI : RoadAI : RoadBaseAI) but are
            // the over-water answer, not the problem.
            // DamAI is a network AI deriving from RoadAI (verified from the assembly typedefs), and
            // a dam belongs across water.
            if (ai is RoadBridgeAI || ai is TrainTrackBridgeAI || ai is MetroTrackBridgeAI || ai is DamAI)
            {
                return false;
            }
            return !ai.BuildOnWater() && !ai.IsUnderground();
        }

        /// <summary>
        /// terrainY is the water-free terrain height, waterY the height the builders place a node
        /// at (terrain or water surface, whichever is higher). Water when the game's block sampler
        /// reports it, or when the surface stands more than 5 cm above the terrain.
        /// </summary>
        public static bool PointHasWater(Vector3 p, out float terrainY, out float waterY)
        {
            TerrainManager terrain = TerrainManager.instance;
            terrainY = terrain.SampleRawHeightSmooth(p);
            waterY = terrain.SampleRawHeightSmoothWithWater(p, false, 0f);
            bool hasWater;
            terrain.SampleBlockHeightSmoothWithWater(p, false, 0f, out hasWater);
            return hasWater || waterY > terrainY + DepthEpsilon;
        }

        /// <summary>The game's own water test along the centreline (1 m radius, any cell).</summary>
        public static bool SegmentHasWater(Vector3 a, Vector3 b)
        {
            Segment2 segment = new Segment2(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            return TerrainManager.instance.HasWater(segment, 1f, true);
        }

        public static string NotCheckedJson(string reason)
        {
            return "{\"checked\":false,\"reason\":\"" + JsonUtil.Escape(reason) + "\"}";
        }

        /// <summary>Why a network is not checked, or null when it must be.</summary>
        public static string SkipReason(NetInfo info, bool allowWater)
        {
            if (allowWater)
            {
                return "allowWater:true";
            }
            if (!GuardsPrefab(info))
            {
                return "prefab '" + (info == null ? "" : info.name) + "' is not a ground road/track/path (bridge, tunnel, pipe, power line, quay, canal, ship path or similar)";
            }
            return null;
        }

        /// <summary>
        /// Throws BridgeException when a ground endpoint (|elevation| below 1 m), the existing
        /// node it would snap to, or any ground-level sample along the centreline is on water.
        /// An endpoint with |elevation| of 1 m or more is a bridge or tunnel piece and is exempt;
        /// between a ground end and an elevated end the elevation is interpolated and only the
        /// samples still at ground level are tested.
        /// </summary>
        public static void AssertNetworkPointsClear(NetInfo info, Vector3 a, float elevA, Vector3 b, float elevB, bool allowWater, float snapDistance, out string waterCheckJson)
        {
            string reason = SkipReason(info, allowWater);
            if (reason != null)
            {
                waterCheckJson = NotCheckedJson(reason);
                return;
            }

            StringBuilder points = new StringBuilder();
            int count = 0;
            if (IsGround(elevA))
            {
                AppendPoint(points, ref count, AssertPointClear(info, a, snapDistance));
            }
            if (IsGround(elevB))
            {
                AppendPoint(points, ref count, AssertPointClear(info, b, snapDistance));
            }
            int samples = AssertSegmentClear(info, a, elevA, b, elevB);

            waterCheckJson = "{\"checked\":true,\"onWater\":false,\"centrelineSamples\":" + samples + ",\"points\":[" + points.ToString() + "]}";
        }

        /// <summary>
        /// Throws when a ground point is on water, or when the existing ground node it would snap
        /// to (within snapDistance) is on water; otherwise returns the point's sample JSON.
        /// </summary>
        public static string AssertPointClear(NetInfo info, Vector3 p, float snapDistance)
        {
            float terrainY;
            float waterY;
            if (PointHasWater(p, out terrainY, out waterY))
            {
                throw new BridgeException("Cannot build on water: point (" + JsonUtil.Number(p.x) + "," + JsonUtil.Number(p.z) +
                    ") is under " + JsonUtil.Number(Mathf.Max(0f, waterY - terrainY)) + " m of water (prefab '" + PrefabName(info) +
                    "' is a ground road/track/path)" + Tail);
            }

            if (snapDistance > 0f)
            {
                ushort existing = NodeHelper.FindNearestNode(p, snapDistance, info, 0f);
                if (existing != 0)
                {
                    NetNode node = NetManager.instance.m_nodes.m_buffer[existing];
                    float nodeTerrainY;
                    float nodeWaterY;
                    if (node.m_elevation == 0 && PointHasWater(node.m_position, out nodeTerrainY, out nodeWaterY))
                    {
                        throw new BridgeException("Cannot build on water: point (" + JsonUtil.Number(p.x) + "," + JsonUtil.Number(p.z) +
                            ") would snap to existing node " + existing + " at (" + JsonUtil.Number(node.m_position.x) + "," + JsonUtil.Number(node.m_position.z) +
                            "), which stands in " + JsonUtil.Number(Mathf.Max(0f, nodeWaterY - nodeTerrainY)) + " m of water (prefab '" + PrefabName(info) +
                            "' is a ground road/track/path)" + Tail);
                    }
                }
            }
            return SampleJson(p, terrainY, waterY);
        }

        /// <summary>
        /// Samples the centreline every SampleSpacing metres with the elevation interpolated
        /// between the ends, and throws on the first ground-level sample that is on water. When
        /// both ends are ground pieces the game's own segment test runs as well. Returns the number
        /// of samples taken.
        /// </summary>
        public static int AssertSegmentClear(NetInfo info, Vector3 a, float elevA, Vector3 b, float elevB)
        {
            Vector3 flatA = new Vector3(a.x, 0f, a.z);
            Vector3 flatB = new Vector3(b.x, 0f, b.z);
            float length = Vector3.Distance(flatA, flatB);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / SampleSpacing));
            int samples = 0;
            for (int k = 0; k <= steps; k++)
            {
                float t = (float)k / steps;
                float elevation = Mathf.Lerp(elevA, elevB, t);
                if (!IsGround(elevation))
                {
                    continue;
                }
                Vector3 p = Vector3.Lerp(flatA, flatB, t);
                float terrainY;
                float waterY;
                samples++;
                if (PointHasWater(p, out terrainY, out waterY))
                {
                    throw new BridgeException("Cannot build on water: the segment from (" + JsonUtil.Number(a.x) + "," + JsonUtil.Number(a.z) +
                        ") to (" + JsonUtil.Number(b.x) + "," + JsonUtil.Number(b.z) + ") crosses water at (" + JsonUtil.Number(p.x) + "," + JsonUtil.Number(p.z) +
                        "), " + JsonUtil.Number(Mathf.Max(0f, waterY - terrainY)) + " m deep, where the piece is still at ground level (elevation " +
                        JsonUtil.Number(elevation) + "; prefab '" + PrefabName(info) + "' is a ground road/track/path)" + Tail);
                }
            }

            if (IsGround(elevA) && IsGround(elevB) && SegmentHasWater(a, b))
            {
                throw new BridgeException("Cannot build on water: the segment from (" + JsonUtil.Number(a.x) + "," + JsonUtil.Number(a.z) +
                    ") to (" + JsonUtil.Number(b.x) + "," + JsonUtil.Number(b.z) + ") crosses water (prefab '" + PrefabName(info) +
                    "' is a ground road/track/path)" + Tail);
            }
            return samples;
        }

        /// <summary>
        /// Samples the whole rotated footprint of a building on a grid at most SampleSpacing
        /// apart (edges and interior) plus the centre and the four corners. Half extents are
        /// m_cellWidth*4 and m_cellLength*4 (8 m cells). Rotation: world = local.x*(cos a, sin a) +
        /// local.z*(-sin a, cos a) in x,z. BuildingTool.CheckSpaceImpl (read from the game assembly)
        /// uses the axes (cos a, sin a) and (sin a, -cos a); the second is the negation of ours, and
        /// the sweep below is symmetric in it, so the sampled footprint is the game's footprint.
        /// Exempt placement modes (Shoreline, ShorelineOrGround, OnWater) return checked:false.
        /// </summary>
        public static string BuildingFootprintWaterCheck(BuildingInfo info, Vector3 position, float angleRadians, bool allowWater, out bool onWater)
        {
            onWater = false;
            if (allowWater)
            {
                return NotCheckedJson("allowWater:true");
            }
            if (info == null)
            {
                return NotCheckedJson("no building prefab");
            }
            if (IsWaterPlacement(info))
            {
                return NotCheckedJson("placement mode " + info.m_placementMode + " belongs on or at water");
            }
            float halfWidth = Mathf.Max(4f, info.m_cellWidth * 4f);
            float halfLength = Mathf.Max(4f, info.m_cellLength * 4f);
            float cos = Mathf.Cos(angleRadians);
            float sin = Mathf.Sin(angleRadians);
            Vector3 right = new Vector3(cos, 0f, sin);
            Vector3 forward = new Vector3(-sin, 0f, cos);

            int nx = Mathf.Max(1, Mathf.CeilToInt(2f * halfWidth / SampleSpacing));
            int nz = Mathf.Max(1, Mathf.CeilToInt(2f * halfLength / SampleSpacing));

            StringBuilder wet = new StringBuilder();
            int wetCount = 0;
            int reported = 0;
            int checkedCount = 0;
            for (int i = 0; i <= nx; i++)
            {
                float lx = -halfWidth + i * (2f * halfWidth / nx);
                for (int j = 0; j <= nz; j++)
                {
                    float lz = -halfLength + j * (2f * halfLength / nz);
                    Vector3 p = position + right * lx + forward * lz;
                    float terrainY;
                    float waterY;
                    checkedCount++;
                    if (PointHasWater(p, out terrainY, out waterY))
                    {
                        onWater = true;
                        wetCount++;
                        if (reported < MaxReportedWetPoints)
                        {
                            AppendPoint(wet, ref reported, SampleJson(p, terrainY, waterY));
                        }
                    }
                }
            }

            // The centre and the corners are always reported so a caller can see the footprint.
            Vector3[] marks = new Vector3[]
            {
                position,
                position + right * halfWidth + forward * halfLength,
                position + right * halfWidth - forward * halfLength,
                position - right * halfWidth + forward * halfLength,
                position - right * halfWidth - forward * halfLength
            };
            StringBuilder points = new StringBuilder();
            int count = 0;
            for (int i = 0; i < marks.Length; i++)
            {
                float terrainY;
                float waterY;
                if (PointHasWater(marks[i], out terrainY, out waterY))
                {
                    onWater = true;
                }
                AppendPoint(points, ref count, SampleJson(marks[i], terrainY, waterY));
            }

            return "{\"checked\":true,\"onWater\":" + JsonUtil.Bool(onWater) +
                ",\"samplesChecked\":" + checkedCount + ",\"wetSamples\":" + wetCount +
                ",\"wetPoints\":[" + wet.ToString() + "],\"points\":[" + points.ToString() + "]}";
        }

        /// <summary>The refusal message for a building whose footprint is on water.</summary>
        public static string BuildingOnWaterMessage(BuildingInfo info, Vector3 position)
        {
            return "Cannot build on water: building '" + (info == null ? "" : info.name) + "' at (" +
                JsonUtil.Number(position.x) + "," + JsonUtil.Number(position.z) +
                ") has part of its footprint on water (placement mode " + (info == null ? "" : info.m_placementMode.ToString()) +
                " is a ground placement). Move it onto dry land. (allowWater:true on the raw HTTP body overrides this for a human-run script; the MCP tools do not expose it.)";
        }

        public static bool IsWaterPlacement(BuildingInfo info)
        {
            return info.m_placementMode == BuildingInfo.PlacementMode.Shoreline ||
                info.m_placementMode == BuildingInfo.PlacementMode.ShorelineOrGround ||
                info.m_placementMode == BuildingInfo.PlacementMode.OnWater;
        }

        public static string SampleJson(Vector3 p, float terrainY, float waterY)
        {
            return "{\"x\":" + JsonUtil.Number(p.x) + ",\"z\":" + JsonUtil.Number(p.z) +
                ",\"terrainHeight\":" + JsonUtil.Number(terrainY) + ",\"waterHeight\":" + JsonUtil.Number(waterY) + "}";
        }

        private static void AppendPoint(StringBuilder points, ref int count, string json)
        {
            if (count > 0)
            {
                points.Append(",");
            }
            points.Append(json);
            count++;
        }

        private static string PrefabName(NetInfo info)
        {
            return info == null ? "" : info.name;
        }
    }
}
