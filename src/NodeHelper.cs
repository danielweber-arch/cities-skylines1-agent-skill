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
            return FindNearest(position, maxDistance, delegate(NetNode node)
            {
                return CanReuseNode(node.Info, prefab);
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

            ushort existing = FindNearestNode(position, snapDistance, prefab);
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
