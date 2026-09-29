using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// The single place nodes and segments are created. Everything that builds network
    /// geometry goes through here so that node reuse is consistent: a CS1 crossing is not
    /// an intersection unless the two networks literally share a node, which is the root
    /// cause of most "it looks connected but traffic will not route" bugs.
    /// </summary>
    public static class NodeHelper
    {
        /// <summary>
        /// Two node positions within this distance are treated as the same junction.
        /// 8m is one zoning cell — close enough that the agent's rounded coordinates snap
        /// onto existing geometry, far enough that adjacent blocks stay distinct.
        /// </summary>
        public const float DefaultSnapDistance = 8f;

        public const float MaxSnapDistance = 64f;

        /// <summary>
        /// Returns an existing reusable node within <paramref name="maxDistance"/>, or 0.
        /// Walks only the node-grid cells covering the search radius. The naive alternative
        /// scans all 32768 node slots, which is fine once and ruinous inside a loop of 200.
        /// </summary>
        public static ushort FindNearestNode(Vector3 position, float maxDistance, NetInfo prefab)
        {
            return FindNearestNode(position, maxDistance, prefab, 0f);
        }

        /// <summary>
        /// <paramref name="elevation"/> is the requested endpoint's height above terrain: a path only
        /// joins a road node through CanJoinPathToRoadNode when it is built at ground level (0).
        /// </summary>
        public static ushort FindNearestNode(Vector3 position, float maxDistance, NetInfo prefab, float elevation)
        {
            ushort same = FindNearest(position, maxDistance, delegate(NetNode node)
            {
                return CanReuseNode(node.Info, prefab);
            });
            // The path-to-road join is a fallback: whenever the old rule finds a node, that node wins.
            if (same != 0 || elevation != 0f)
            {
                return same;
            }
            return FindNearest(position, maxDistance, delegate(NetNode node)
            {
                return CanJoinPathToRoadNode(node, prefab);
            });
        }

        /// <summary>
        /// Same grid walk, but matched on network service rather than prefab identity. This is
        /// what /commands/connect uses: "find me the nearest road", not "find me the nearest
        /// node of exactly this prefab".
        /// </summary>
        public static ushort FindNearestNodeOfService(Vector3 position, float maxDistance, ItemClass.Service service)
        {
            return FindNearest(position, maxDistance, delegate(NetNode node)
            {
                NetInfo info = node.Info;
                return info != null && info.m_class != null && info.m_class.m_service == service;
            });
        }

        private delegate bool NodeFilter(NetNode node);

        private static ushort FindNearest(Vector3 position, float maxDistance, NodeFilter accept)
        {
            NetManager net = NetManager.instance;
            if (net == null || maxDistance <= 0f)
            {
                return 0;
            }

            float cell = NetManager.NODEGRID_CELL_SIZE;
            int resolution = NetManager.NODEGRID_RESOLUTION;
            int half = resolution / 2;

            int minX = Mathf.Max((int)((position.x - maxDistance) / cell + half), 0);
            int maxX = Mathf.Min((int)((position.x + maxDistance) / cell + half), resolution - 1);
            int minZ = Mathf.Max((int)((position.z - maxDistance) / cell + half), 0);
            int maxZ = Mathf.Min((int)((position.z + maxDistance) / cell + half), resolution - 1);

            float bestDistanceSq = maxDistance * maxDistance;
            ushort best = 0;

            for (int gridZ = minZ; gridZ <= maxZ; gridZ++)
            {
                for (int gridX = minX; gridX <= maxX; gridX++)
                {
                    ushort candidate = net.m_nodeGrid[gridZ * resolution + gridX];
                    int guard = 0;

                    while (candidate != 0)
                    {
                        NetNode node = net.m_nodes.m_buffer[candidate];

                        if ((node.m_flags & NetNode.Flags.Created) != NetNode.Flags.None &&
                            accept(node))
                        {
                            Vector3 delta = node.m_position - position;
                            delta.y = 0f;
                            float distanceSq = delta.sqrMagnitude;
                            if (distanceSq <= bestDistanceSq)
                            {
                                bestDistanceSq = distanceSq;
                                best = candidate;
                            }
                        }

                        candidate = node.m_nextGridNode;

                        if (++guard >= 32768)
                        {
                            BridgeLog.Queue("[SkylinesAgentBridge] Node grid cell " + gridX + "/" + gridZ + " looks corrupt; stopping walk.");
                            break;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Snaps onto an existing node when one is close enough, otherwise creates a new one
        /// at terrain height. <paramref name="created"/> reports which happened so callers can
        /// roll back only what they actually made.
        /// </summary>
        public static ushort FindOrCreateNode(Vector3 position, NetInfo prefab, float snapDistance, out bool created)
        {
            return FindOrCreateNode(position, prefab, snapDistance, 0f, out created);
        }

        /// <summary>
        /// As above, but a new node is placed <paramref name="elevation"/> metres above the
        /// terrain (below it when negative) and records that height in NetNode.m_elevation,
        /// the way NetTool does for elevated and tunnel pieces. Snapping ignores height.
        /// </summary>
        public static ushort FindOrCreateNode(Vector3 position, NetInfo prefab, float snapDistance, float elevation, out bool created)
        {
            created = false;

            if (prefab == null)
            {
                throw new BridgeException("A network prefab is required to create a node.");
            }

            ushort existing = FindNearestNode(position, snapDistance, prefab, elevation);
            if (existing != 0)
            {
                return existing;
            }

            TerrainManager terrain = TerrainManager.instance;
            if (terrain != null)
            {
                // Match RoadCommands.BuildRoad so nodes from both paths sit at the same height.
                position.y = terrain.SampleRawHeightSmoothWithWater(position, false, 0f) + elevation;
            }

            SimulationManager simulation = Singleton<SimulationManager>.instance;
            NetManager net = NetManager.instance;
            Randomizer randomizer = simulation.m_randomizer;

            ushort node;
            bool ok = net.CreateNode(out node, ref randomizer, prefab, position, simulation.m_currentBuildIndex);
            simulation.m_randomizer = randomizer;

            if (!ok)
            {
                throw new BridgeException("CreateNode failed at " +
                    JsonUtil.Number(position.x) + "," + JsonUtil.Number(position.z) +
                    " — the net node pool is most likely exhausted.");
            }

            if (elevation != 0f)
            {
                net.m_nodes.m_buffer[node].m_elevation = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(elevation)), 1, 255);
                if (elevation < 0f)
                {
                    // NetTool marks tunnel nodes this way; without it the node renders and
                    // simulates as if it sat on the surface.
                    net.m_nodes.m_buffer[node].m_flags |= NetNode.Flags.Underground;
                }
            }

            simulation.m_currentBuildIndex += 1u;
            created = true;
            return node;
        }

        /// <summary>
        /// Creates a segment between two nodes. Returns 0 when the pair is degenerate or
        /// already joined, so callers can treat "nothing to do" separately from failure.
        /// </summary>
        public static ushort CreateSegment(ushort startNode, ushort endNode, NetInfo prefab, string name)
        {
            if (startNode == 0 || endNode == 0 || startNode == endNode)
            {
                return 0;
            }

            if (FindExistingSegment(startNode, endNode) != 0)
            {
                return 0;
            }

            NetManager net = NetManager.instance;
            SimulationManager simulation = Singleton<SimulationManager>.instance;

            Vector3 start = net.m_nodes.m_buffer[startNode].m_position;
            Vector3 end = net.m_nodes.m_buffer[endNode].m_position;
            Vector3 direction = end - start;
            direction.y = 0f;

            if (direction.sqrMagnitude < 1f)
            {
                return 0;
            }

            direction = direction.normalized;
            Randomizer randomizer = simulation.m_randomizer;

            ushort segment;
            // PlayerNetAI.CreateSegment ends with m_createPassMilestone.Unlock() (e.g. "Metro Track
            // Created"), which raises UnlockManager events through ThreadHelper.dispatcher. The bridge
            // runs on the main thread, so that throws "Already in the same thread" after the segment
            // is half built (no lanes, never initialised). Detach the milestone for the call and
            // replay the unlock on the simulation thread, where the game's own tools run it.
            PlayerNetAI playerAI = prefab.m_netAI as PlayerNetAI;
            ManualMilestone passMilestone = playerAI != null ? playerAI.m_createPassMilestone : null;
            if (passMilestone != null)
            {
                playerAI.m_createPassMilestone = null;
            }

            bool ok;
            try
            {
                ok = net.CreateSegment(
                    out segment,
                    ref randomizer,
                    prefab,
                    startNode,
                    endNode,
                    direction,
                    -direction,
                    simulation.m_currentBuildIndex,
                    simulation.m_currentBuildIndex,
                    false);
            }
            finally
            {
                if (passMilestone != null)
                {
                    playerAI.m_createPassMilestone = passMilestone;
                }
            }

            if (ok && passMilestone != null)
            {
                ManualMilestone milestone = passMilestone;
                simulation.AddAction(delegate { milestone.Unlock(); });
            }

            simulation.m_randomizer = randomizer;

            if (!ok)
            {
                throw new BridgeException("CreateSegment failed between nodes " + startNode + " and " + endNode +
                    " — the net segment pool is most likely exhausted.");
            }

            simulation.m_currentBuildIndex += 2u;

            if (name != null && name.Length > 0)
            {
                net.SetSegmentNameImpl(segment, name);
            }

            return segment;
        }

        /// <summary>Returns the segment already joining the two nodes, or 0.</summary>
        public static ushort FindExistingSegment(ushort startNode, ushort endNode)
        {
            NetManager net = NetManager.instance;
            NetNode node = net.m_nodes.m_buffer[startNode];

            for (int i = 0; i < 8; i++)
            {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0)
                {
                    continue;
                }

                NetSegment segment = net.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
                {
                    continue;
                }

                if ((segment.m_startNode == startNode && segment.m_endNode == endNode) ||
                    (segment.m_startNode == endNode && segment.m_endNode == startNode))
                {
                    return segmentId;
                }
            }

            return 0;
        }

        /// <summary>
        /// Undoes a partially built operation. Segments first, then nodes, so that releasing
        /// a node never leaves a segment pointing at nothing.
        /// </summary>
        public static void Rollback(System.Collections.Generic.List<ushort> segments,
                                    System.Collections.Generic.List<ushort> nodes)
        {
            NetManager net = NetManager.instance;

            if (segments != null)
            {
                for (int i = segments.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (segments[i] != 0)
                        {
                            GameThreadHelpers.ReleaseSegment(net, segments[i], true);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        BridgeLog.Queue("[SkylinesAgentBridge] Rollback could not release segment " + segments[i] + ": " + ex.Message);
                    }
                }
            }

            if (nodes != null)
            {
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (nodes[i] != 0)
                        {
                            net.ReleaseNode(nodes[i]);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        BridgeLog.Queue("[SkylinesAgentBridge] Rollback could not release node " + nodes[i] + ": " + ex.Message);
                    }
                }
            }
        }

        public static bool CanReuseNode(NetInfo existing, NetInfo requested)
        {
            if (existing == null || requested == null || existing.m_class == null || requested.m_class == null)
            {
                return false;
            }

            if (existing == requested)
            {
                return true;
            }

            if (existing.m_class.m_service == ItemClass.Service.Road &&
                requested.m_class.m_service == ItemClass.Service.Road)
            {
                return true;
            }

            // Track, pipes and lines of the same family (e.g. "Train Track" onto a "Train Station
            // Track" platform node, an elevated or bridge node) join like the game's own tools do.
            return existing.m_class.m_service == requested.m_class.m_service &&
                existing.m_class.m_subService == requested.m_class.m_subService &&
                existing.m_class.m_layer == requested.m_class.m_layer;
        }

        /// <summary>
        /// A ground pedestrian path (PedestrianPathAI) may end on an existing road node only when the
        /// game's own net tool would snap it there. The rule is the node filter NetTool.MakeControlPoint
        /// hands to NetManager.RayCast (decompiled): a node is a snap target for the net being built when
        ///   (a) the node's connection class matches the built net's connection class
        ///       (service equal; subService equal unless None; layers overlap unless None), or
        ///   (b) the node's m_intersectClass matches the built net's connection class, or
        ///   (c) node.m_netAI.CanIntersect(builtNet) and the node's connection class matches the
        ///       built net's m_intersectClass (MakeControlPoint puts it in RaycastService m_netService2;
        ///       with no intersect class m_netService2 stays the RaycastInput default Service.None),
        /// and node.m_netAI.CanConnect(builtNet) and builtNet.m_netAI.CanConnect(node.Info) both hold,
        /// the node is not flagged Underground (MakeControlPoint ignores those unless building a tunnel),
        /// and it is not a Middle|Untouchable asset node with two or more untouchable segments.
        /// The game tests the class references with (object)x != null, so this does too.
        ///
        /// For the vanilla prefabs this NEVER matches, and that is the game's behaviour, not a bridge
        /// bug: every Pedestrian* prefab is Beautification/BeautificationParks/Default and Basic/Medium/
        /// Large Road are Road/None/Default, all with a null m_intersectClass (live read 2026-09-27), so
        /// none of (a)-(c) holds and the net tool's raycast never returns a road node or road segment
        /// for a path. Vanilla paths reach roads through a lane connection instead:
        /// PedestrianPathAI.UpdateLaneConnection links a path node to the nearest road pedestrian lane
        /// (m_connectService1 = Road) within m_maxConnectDistanceEnd1 = 16.5 m for a dead end
        /// (m_maxConnectDistance1 = 8 m otherwise), setting NetNode.m_lane. A path drawn onto a road is
        /// refused by NetTool.CreateNode as a collision (roads are a public service, not auto-removed).
        /// This stays for modded prefabs that do set an intersect class.
        /// </summary>
        /// Only a surface path prefab (PedestrianPathAI with m_underground false) built at elevation 0
        /// onto a surface road node (RoadAI) qualifies; FindNearestNode enforces the elevation and only
        /// tries this when CanReuseNode finds no node in range.
        public static bool CanJoinPathToRoadNode(NetNode node, NetInfo requested)
        {
            NetInfo existing = node.Info;
            if (existing == null || requested == null || existing.m_class == null || requested.m_class == null ||
                existing.m_netAI == null || requested.m_netAI == null)
            {
                return false;
            }
            PedestrianPathAI pathAI = requested.m_netAI as PedestrianPathAI;
            // RoadAI is the surface road AI (RoadBridgeAI / RoadTunnelAI derive from RoadBaseAI, not
            // RoadAI). The bridge's snap is 2D, unlike the tool's 3D raycast, so bridge and tunnel
            // nodes are refused outright rather than trusted to the class match.
            if (pathAI == null || pathAI.m_underground || existing.m_class.m_service != ItemClass.Service.Road ||
                !(existing.m_netAI is RoadAI))
            {
                return false;
            }
            if ((node.m_flags & NetNode.Flags.Underground) != NetNode.Flags.None)
            {
                return false;
            }
            const NetNode.Flags middleUntouchable = NetNode.Flags.Middle | NetNode.Flags.Untouchable;
            if ((node.m_flags & middleUntouchable) == middleUntouchable &&
                node.CountSegments(NetSegment.Flags.Untouchable, 0) >= 2)
            {
                return false;
            }

            ItemClass nodeClass = existing.GetConnectionClass();
            ItemClass builtClass = requested.GetConnectionClass();
            if (nodeClass == null || builtClass == null)
            {
                return false;
            }

            bool matches = ClassMatches(nodeClass, builtClass) ||
                ((object)existing.m_intersectClass != null && ClassMatches(existing.m_intersectClass, builtClass)) ||
                ((object)requested.m_intersectClass != null && existing.m_netAI.CanIntersect(requested) &&
                    ClassMatches(nodeClass, requested.m_intersectClass));
            return matches && existing.m_netAI.CanConnect(requested) && requested.m_netAI.CanConnect(existing);
        }

        /// <summary>NetManager.RayCast's class test: service equal, subService and layer wildcards when None.</summary>
        private static bool ClassMatches(ItemClass candidate, ItemClass filter)
        {
            return candidate.m_service == filter.m_service &&
                (filter.m_subService == ItemClass.SubService.None || candidate.m_subService == filter.m_subService) &&
                (filter.m_layer == ItemClass.Layer.None || (candidate.m_layer & filter.m_layer) != ItemClass.Layer.None);
        }

        /// <summary>
        /// A surface pedestrian path the road-overlap guard applies to: PedestrianPathAI, not
        /// underground, and no intersect class (a modded path with one can join road nodes through
        /// CanJoinPathToRoadNode, so it is left alone).
        /// </summary>
        public static bool IsSurfacePathWithoutIntersect(NetInfo info)
        {
            if (info == null)
            {
                return false;
            }
            PedestrianPathAI pathAI = info.m_netAI as PedestrianPathAI;
            return pathAI != null && !pathAI.m_underground && (object)info.m_intersectClass == null;
        }

        /// <summary>Where a straight path first runs inside a surface road, as found by FindRoadOverlap.</summary>
        public struct RoadOverlap
        {
            public ushort Segment;
            public Vector3 Point;
            public float DistanceFromCentre;
            public float RoadHalfWidth;
        }

        /// <summary>
        /// Surface road segments whose bounds (NetSegment.m_bounds, which include the road's width)
        /// come within 1 m of the horizontal box around <paramref name="from"/>-<paramref name="to"/>.
        /// Surface road = RoadAI (not RoadBridgeAI/RoadTunnelAI), Road service, neither node flagged
        /// Underground. Scans the whole segment buffer rather than the segment grid: the grid files a
        /// segment only under its node midpoint (NetManager.InitializeSegment), and build-network can
        /// make segments of any length, so no fixed grid margin is safe.
        /// </summary>
        public static System.Collections.Generic.List<ushort> CollectSurfaceRoads(Vector3 from, Vector3 to)
        {
            System.Collections.Generic.List<ushort> roads = new System.Collections.Generic.List<ushort>();
            NetManager net = NetManager.instance;
            if (net == null)
            {
                return roads;
            }
            const float margin = 1f;
            float minX = Mathf.Min(from.x, to.x) - margin;
            float maxX = Mathf.Max(from.x, to.x) + margin;
            float minZ = Mathf.Min(from.z, to.z) - margin;
            float maxZ = Mathf.Max(from.z, to.z) + margin;
            NetSegment[] buffer = net.m_segments.m_buffer;
            for (int i = 1; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created)
                {
                    continue;
                }
                Bounds bounds = buffer[i].m_bounds;
                if (bounds.max.x < minX || bounds.min.x > maxX || bounds.max.z < minZ || bounds.min.z > maxZ)
                {
                    continue;
                }
                NetInfo info = buffer[i].Info;
                if (info == null || info.m_class == null || info.m_class.m_service != ItemClass.Service.Road || !(info.m_netAI is RoadAI))
                {
                    continue;
                }
                if ((net.m_nodes.m_buffer[buffer[i].m_startNode].m_flags & NetNode.Flags.Underground) != NetNode.Flags.None ||
                    (net.m_nodes.m_buffer[buffer[i].m_endNode].m_flags & NetNode.Flags.Underground) != NetNode.Flags.None)
                {
                    continue;
                }
                roads.Add((ushort)i);
            }
            return roads;
        }

        /// <summary>
        /// Walks the straight line from <paramref name="from"/> towards <paramref name="to"/> in 0.5 m
        /// steps (both ends included) and returns the first sample whose horizontal distance to one of
        /// <paramref name="roads"/>' centre line is less than that road's m_halfWidth (carriageway plus
        /// sidewalks). Segment = 0 when the line stays clear.
        /// The game's NetTool.CreateNode refuses such a path as a collision; this is a centre-line
        /// approximation of that test (it ignores the path's own width and can miss a corner clipped
        /// between two samples), not a port of it. Endpoints are always sampled exactly.
        /// </summary>
        public static RoadOverlap FindRoadOverlap(Vector3 from, Vector3 to, System.Collections.Generic.List<ushort> roads)
        {
            RoadOverlap result = new RoadOverlap();
            NetManager net = NetManager.instance;
            if (net == null || roads == null || roads.Count == 0)
            {
                return result;
            }

            Vector3 line = to - from;
            line.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(line.magnitude * 2f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 sample = from + (to - from) * ((float)i / steps);
                for (int r = 0; r < roads.Count; r++)
                {
                    NetSegment segment = net.m_segments.m_buffer[roads[r]];
                    NetInfo info = segment.Info;
                    if (info == null)
                    {
                        continue;
                    }
                    Vector3 closest = segment.GetClosestPosition(sample);
                    float dx = closest.x - sample.x;
                    float dz = closest.z - sample.z;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance < info.m_halfWidth)
                    {
                        result.Segment = roads[r];
                        result.Point = sample;
                        result.DistanceFromCentre = distance;
                        result.RoadHalfWidth = info.m_halfWidth;
                        return result;
                    }
                }
            }
            return result;
        }

        public static float ClampSnapDistance(float requested)
        {
            if (requested <= 0f)
            {
                return DefaultSnapDistance;
            }
            if (requested > MaxSnapDistance)
            {
                return MaxSnapDistance;
            }
            return requested;
        }
    }
}
