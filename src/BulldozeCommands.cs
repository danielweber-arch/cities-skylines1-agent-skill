using ColossalFramework;

namespace SkylinesAgentBridge
{
    public static class BulldozeCommands
    {
        public static CommandResult Bulldoze(string body)
        {
            string entityType = JsonUtil.GetString(body, "entityType", "");
            ushort id = (ushort)JsonUtil.GetNumber(body, "id", 0f);
            bool keepNodes = JsonUtil.GetBool(body, "keepNodes", false);
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);

            if (id == 0)
            {
                return CommandResult.Fail("id is required.");
            }

            if (entityType == "building")
            {
                BuildingManager manager = BuildingManager.instance;
                if ((manager.m_buildings.m_buffer[id].m_flags & Building.Flags.Created) == Building.Flags.None)
                {
                    return CommandResult.Fail("Building was not found: " + id);
                }

                if (!dryRun)
                {
                    // ReleaseBuilding fires UI events through ThreadHelper.dispatcher, which throws on
                    // the main thread part-way through the release and left buildings stuck as
                    // Created|Deleted (24712, 1621, 5177). The game's tools release on the
                    // simulation thread, so do the same and report what actually happened.
                    ushort buildingId = id;
                    SimulationJob job = null;
                    job = new SimulationJob(delegate
                    {
                        if (!job.BeginCommit())
                        {
                            return CommandResult.Fail("Timed out before the building was released.");
                        }
                        Building.Flags before = manager.m_buildings.m_buffer[buildingId].m_flags;
                        // A release that threw part-way on the main thread (before this fix) left
                        // Deleted set with the rest of the building intact, and
                        // ReleaseBuildingImplementation returns at once for Deleted buildings, so
                        // they could never be removed. Clear the flag and run the full release.
                        bool recoveredStuck = (before & Building.Flags.Deleted) != Building.Flags.None;
                        if (recoveredStuck)
                        {
                            manager.m_buildings.m_buffer[buildingId].m_flags &= ~Building.Flags.Deleted;
                        }
                        manager.ReleaseBuilding(buildingId);
                        Building.Flags after = manager.m_buildings.m_buffer[buildingId].m_flags;
                        return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"entityType\":\"building\",\"id\":" + buildingId +
                            ",\"flagsBefore\":\"" + JsonUtil.Escape(before.ToString()) + "\"" +
                            ",\"flagsAfter\":\"" + JsonUtil.Escape(after.ToString()) + "\"" +
                            ",\"recoveredStuck\":" + JsonUtil.Bool(recoveredStuck) +
                            ",\"released\":" + JsonUtil.Bool(after == Building.Flags.None) + "}");
                    });
                    Singleton<SimulationManager>.instance.AddAction(job.Run);
                    CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
                    deferred.Deferred = job;
                    return deferred;
                }

                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":" + JsonUtil.Bool(dryRun) + ",\"entityType\":\"building\",\"id\":" + id + "}");
            }

            if (entityType == "netSegment")
            {
                NetManager manager = NetManager.instance;
                if ((manager.m_segments.m_buffer[id].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
                {
                    return CommandResult.Fail("Net segment was not found: " + id);
                }

                if (!dryRun)
                {
                    // Same reason as buildings: releasing on the main thread threw part-way
                    // (IndexOutOfRange via the reflection fallback on segment 30352). Release on the
                    // simulation thread, as NetTool does, and report whether the slot was freed.
                    ushort segmentId = id;
                    bool keep = keepNodes;
                    SimulationJob job = null;
                    job = new SimulationJob(delegate
                    {
                        if (!job.BeginCommit())
                        {
                            return CommandResult.Fail("Timed out before the segment was released.");
                        }
                        NetSegment.Flags before = manager.m_segments.m_buffer[segmentId].m_flags;
                        manager.ReleaseSegment(segmentId, keep);
                        NetSegment.Flags after = manager.m_segments.m_buffer[segmentId].m_flags;
                        return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"entityType\":\"netSegment\",\"id\":" + segmentId +
                            ",\"keepNodes\":" + JsonUtil.Bool(keep) +
                            ",\"flagsBefore\":\"" + JsonUtil.Escape(before.ToString()) + "\"" +
                            ",\"flagsAfter\":\"" + JsonUtil.Escape(after.ToString()) + "\"" +
                            ",\"released\":" + JsonUtil.Bool(after == NetSegment.Flags.None) + "}");
                    });
                    Singleton<SimulationManager>.instance.AddAction(job.Run);
                    CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
                    deferred.Deferred = job;
                    return deferred;
                }

                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":" + JsonUtil.Bool(dryRun) + ",\"entityType\":\"netSegment\",\"id\":" + id + ",\"keepNodes\":" + JsonUtil.Bool(keepNodes) + "}");
            }

            if (entityType == "netNode")
            {
                NetManager manager = NetManager.instance;
                if ((manager.m_nodes.m_buffer[id].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
                {
                    return CommandResult.Fail("Net node was not found: " + id);
                }

                if (!dryRun)
                {
                    ushort nodeId = id;
                    SimulationJob job = null;
                    job = new SimulationJob(delegate
                    {
                        if (!job.BeginCommit())
                        {
                            return CommandResult.Fail("Timed out before the node was released.");
                        }
                        manager.ReleaseNode(nodeId);
                        NetNode.Flags after = manager.m_nodes.m_buffer[nodeId].m_flags;
                        return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"entityType\":\"netNode\",\"id\":" + nodeId +
                            ",\"released\":" + JsonUtil.Bool(after == NetNode.Flags.None) + "}");
                    });
                    Singleton<SimulationManager>.instance.AddAction(job.Run);
                    CommandResult deferred = CommandResult.FromJson("{\"ok\":true,\"queued\":true}");
                    deferred.Deferred = job;
                    return deferred;
                }

                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":" + JsonUtil.Bool(dryRun) + ",\"entityType\":\"netNode\",\"id\":" + id + "}");
            }

            return CommandResult.Fail("Unsupported entityType. Use building, netSegment, or netNode.");
        }
    }
}
