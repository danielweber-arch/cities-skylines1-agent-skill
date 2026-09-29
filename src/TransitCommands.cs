using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Write side of public transport: create / edit / delete lines, service budgets, policies.
    ///
    /// Stop placement is a port of TransportTool.GetStopPosition (decompiled from the installed
    /// Assembly-CSharp.dll), not an invented rule. The tool raycasts under the mouse; we have a
    /// point instead, so we gather every segment / station building the tool's raycast would
    /// accept near that point, nearest first, and run the tool's own snapping on each until one
    /// succeeds. Road stops end up at the middle of the segment (the tool always calls
    /// CalculateStopPositionAndDirection with offset 128/255); station stops end up on one of
    /// the station's spawn positions (its platforms).
    ///
    /// Loop closure is the game's own: TransportLine.AddStop at a position within 2.5 m of the
    /// first stop, on an incomplete line with at least two stops, creates the closing segment
    /// and sets TransportLine.Flags.Complete itself. We never set Complete by hand.
    /// </summary>
    public static class TransitCommands
    {
        public const float DefaultRoadSnapDistance = 32f;
        public const float DefaultStationSnapDistance = 64f;
        private const int MaxStops = 64;
        private const int MaxCandidatesTried = 64;

        // ------------------------------------------------------------------ create

        public static CommandResult CreateLine(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);

            bool ignoreUnlock = JsonUtil.GetBool(body, "ignoreUnlock", false);

            TransportInfo info;
            string error;
            bool unlockIgnored;
            if (!ResolveTransportInfo(body, ignoreUnlock, out info, out unlockIgnored, out error))
            {
                CommandResult refused = CommandResult.Fail(error);
                if (info != null && IsCreatableType(info.m_transportType) && !UnlockReport.IsUnlocked(info.m_UnlockMilestone))
                {
                    refused.Json = "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(error) + "\"" +
                        ",\"prefab\":\"" + JsonUtil.Escape(info.name) + "\"" +
                        ",\"unlock\":" + UnlockReport.TransportUnlockJson(info) + "}";
                }
                return refused;
            }

            float roadSnap = Mathf.Clamp(JsonUtil.GetNumber(body, "roadSnapDistance", DefaultRoadSnapDistance), 1f, 128f);
            float stationSnap = Mathf.Clamp(JsonUtil.GetNumber(body, "stationSnapDistance", DefaultStationSnapDistance), 1f, 256f);

            List<string> stopObjects = JsonUtil.GetObjectArray(body, "stops");
            if (stopObjects.Count < 2)
            {
                return CommandResult.Fail("stops must list at least 2 points [{x,z},...] in order; the line is closed back to the first stop automatically.");
            }
            if (stopObjects.Count > MaxStops)
            {
                return CommandResult.Fail("At most " + MaxStops + " stops per call.");
            }

            string name = JsonUtil.GetString(body, "name", null);
            string colorText = JsonUtil.GetString(body, "color", null);
            Color32 color = new Color32(0, 0, 0, 255);
            if (colorText != null && !TryParseColor(colorText, out color))
            {
                return CommandResult.Fail("color must be \"#RRGGBB\", got: " + colorText);
            }
            float budget = JsonUtil.GetNumber(body, "budget", float.NaN);
            if (!float.IsNaN(budget) && (budget < 0f || budget > 500f))
            {
                return CommandResult.Fail("budget is a per-line percentage and must be between 0 and 500.");
            }

            // Resolve every stop before touching the city, so a bad point late in the list costs
            // nothing and dryRun reports exactly what the real call would do.
            List<StopResolution> resolved = new List<StopResolution>();
            for (int i = 0; i < stopObjects.Count; i++)
            {
                float x = JsonUtil.GetNumber(stopObjects[i], "x", float.NaN);
                float z = JsonUtil.GetNumber(stopObjects[i], "z", float.NaN);
                if (float.IsNaN(x) || float.IsNaN(z))
                {
                    return CommandResult.Fail("stops[" + i + "] needs numeric x and z.");
                }

                StopResolution stop = ResolveStop(info, x, z, roadSnap, stationSnap);
                if (!stop.Ok)
                {
                    return CommandResult.Fail("stops[" + i + "] at (" + JsonUtil.Number(x) + ", " + JsonUtil.Number(z) + "): " + stop.Error);
                }
                resolved.Add(stop);
            }

            // Mirror TransportLine.CanAddStop (rejects a stop within 1 m of its neighbour) and the
            // AddStop closure rule (any stop within 2.5 m of the first one closes the loop).
            bool droppedClosingStop = false;
            for (int i = 1; i < resolved.Count; i++)
            {
                if ((resolved[i].Position - resolved[0].Position).sqrMagnitude < 6.25f)
                {
                    if (i == resolved.Count - 1 && i >= 2)
                    {
                        resolved.RemoveAt(i);
                        droppedClosingStop = true;
                        break;
                    }
                    return CommandResult.Fail("stops[" + i + "] snaps onto the first stop, which would close the line early. The line is closed automatically; do not repeat the first stop.");
                }
                if ((resolved[i].Position - resolved[i - 1].Position).sqrMagnitude < 1f)
                {
                    return CommandResult.Fail("stops[" + i + "] snaps to the same position as stops[" + (i - 1) + "]; the game rejects consecutive stops closer than 1 m.");
                }
            }

            TransportManager transport = Singleton<TransportManager>.instance;
            if (!transport.CheckLimits())
            {
                return CommandResult.Fail("The city is at the transport line limit (TransportManager.CheckLimits: m_lineCount " + transport.m_lineCount + ").");
            }
            if (!Singleton<NetManager>.instance.CheckLimits())
            {
                return CommandResult.Fail("The network node/segment limit is reached (NetManager.CheckLimits).");
            }

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true" +
                    ",\"message\":\"Transit line validation passed; no line was created. Path finding between stops only runs after creation.\"" +
                    ",\"prefab\":\"" + JsonUtil.Escape(info.name) + "\"" +
                    ",\"transportType\":\"" + info.m_transportType.ToString() + "\"" +
                    ",\"unlockIgnored\":" + JsonUtil.Bool(unlockIgnored) +
                    ",\"droppedDuplicateClosingStop\":" + JsonUtil.Bool(droppedClosingStop) +
                    ",\"stops\":" + StopsJson(resolved, null) + "}");
            }

            SimulationManager simulation = Singleton<SimulationManager>.instance;
            ushort lineId;
            if (!transport.CreateLine(out lineId, ref simulation.m_randomizer, info, true))
            {
                return CommandResult.Fail("TransportManager.CreateLine failed (line buffer full?).");
            }

            List<ushort> nodeIds = new List<ushort>();
            try
            {
                for (int i = 0; i < resolved.Count; i++)
                {
                    StopResolution stop = resolved[i];
                    if (!transport.m_lines.m_buffer[lineId].CanAddStop(lineId, -1, stop.Position))
                    {
                        transport.ReleaseLine(lineId);
                        return CommandResult.Fail("TransportLine.CanAddStop rejected stops[" + i + "]; the line was rolled back.");
                    }
                    if (!transport.m_lines.m_buffer[lineId].AddStop(lineId, -1, stop.Position, stop.FixedPlatform))
                    {
                        transport.ReleaseLine(lineId);
                        return CommandResult.Fail("TransportLine.AddStop failed for stops[" + i + "]; the line was rolled back.");
                    }
                    if (transport.m_lines.m_buffer[lineId].Complete)
                    {
                        transport.ReleaseLine(lineId);
                        return CommandResult.Fail("stops[" + i + "] closed the line early (it landed on the first stop); the line was rolled back.");
                    }
                    nodeIds.Add(transport.m_lines.m_buffer[lineId].GetStop(-1));
                }

                // Close the loop the way the tool does: add a stop at the first stop's exact
                // position; AddStop sees it is within 2.5 m and joins last -> first instead.
                ushort firstStop = transport.m_lines.m_buffer[lineId].m_stops;
                Vector3 firstPosition = Singleton<NetManager>.instance.m_nodes.m_buffer[firstStop].m_position;
                bool closed = transport.m_lines.m_buffer[lineId].AddStop(lineId, -1, firstPosition, false);
                if (!closed || !transport.m_lines.m_buffer[lineId].Complete)
                {
                    transport.ReleaseLine(lineId);
                    return CommandResult.Fail("Closing the line back to its first stop failed (TransportLine.AddStop did not set Complete); the line was rolled back.");
                }

                List<string> warnings = new List<string>();
                if (unlockIgnored)
                {
                    warnings.Add("ignoreUnlock: the line-tool milestone is not passed (" + UnlockReport.TransportUnlockSummary(info) +
                        "). If it requires a depot or station, the line gets no vehicles until one exists.");
                }
                ApplyLineProperties(transport, lineId, name, colorText != null, color, budget, float.NaN, warnings);

                Debug.Log("[SkylinesAgentBridge] Created transit line " + lineId + " (" + info.name + ") with " + resolved.Count + " stops");

                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false" +
                    ",\"lineId\":" + lineId +
                    ",\"unlockIgnored\":" + JsonUtil.Bool(unlockIgnored) +
                    ",\"droppedDuplicateClosingStop\":" + JsonUtil.Bool(droppedClosingStop) +
                    ",\"line\":" + LineSummaryJson(transport, lineId) +
                    ",\"stops\":" + StopsJson(resolved, nodeIds) +
                    ",\"warnings\":" + StringArray(warnings) +
                    ",\"note\":\"Paths between stops are computed asynchronously. Re-read /state/transit?includeStops=true after a few seconds of simulation: a LineNotConnected problem on a stop means the path failed.\"}");
            }
            catch (Exception ex)
            {
                string rollback = "the line was rolled back";
                try
                {
                    transport.ReleaseLine(lineId);
                    if ((transport.m_lines.m_buffer[lineId].m_flags & TransportLine.Flags.Created) != TransportLine.Flags.None)
                    {
                        rollback = "ROLLBACK INCOMPLETE: line " + lineId + " is still flagged Created; delete it with transit-line-delete";
                    }
                }
                catch (Exception releaseError)
                {
                    rollback = "ROLLBACK FAILED (" + releaseError.GetType().Name + ": " + releaseError.Message + "); line " + lineId + " may still exist";
                }
                return CommandResult.Fail("Creating the line threw " + ex.GetType().Name + ": " + ex.Message + "; " + rollback + ".");
            }
        }

        // ------------------------------------------------------------------ edit

        public static CommandResult EditLine(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            TransportManager transport = Singleton<TransportManager>.instance;

            ushort lineId;
            string error;
            if (!ReadLineId(body, transport, out lineId, out error))
            {
                return CommandResult.Fail(error);
            }

            TransportInfo info = transport.m_lines.m_buffer[lineId].Info;
            if (info == null)
            {
                return CommandResult.Fail("Line " + lineId + " has no TransportInfo prefab.");
            }

            string name = JsonUtil.GetString(body, "name", null);
            string colorText = JsonUtil.GetString(body, "color", null);
            Color32 color = new Color32(0, 0, 0, 255);
            if (colorText != null && !TryParseColor(colorText, out color))
            {
                return CommandResult.Fail("color must be \"#RRGGBB\", got: " + colorText);
            }
            float budget = JsonUtil.GetNumber(body, "budget", float.NaN);
            if (!float.IsNaN(budget) && (budget < 0f || budget > 500f))
            {
                return CommandResult.Fail("budget is a per-line percentage and must be between 0 and 500.");
            }
            float ticketPrice = JsonUtil.GetNumber(body, "ticketPrice", float.NaN);
            if (!float.IsNaN(ticketPrice) && (ticketPrice < 0f || ticketPrice > 65535f))
            {
                return CommandResult.Fail("ticketPrice is in cents (TransportLine.m_ticketPrice) and must be between 0 and 65535.");
            }

            float roadSnap = Mathf.Clamp(JsonUtil.GetNumber(body, "roadSnapDistance", DefaultRoadSnapDistance), 1f, 128f);
            float stationSnap = Mathf.Clamp(JsonUtil.GetNumber(body, "stationSnapDistance", DefaultStationSnapDistance), 1f, 256f);

            List<int> removals;
            if (!TryGetIntArray(body, "removeStopIndexes", out removals, out error))
            {
                return CommandResult.Fail(error);
            }

            int stopCount = transport.m_lines.m_buffer[lineId].CountStops(lineId);

            // Removals: validated against the current line, applied highest index first so the
            // lower indexes stay valid.
            removals.Sort();
            removals.Reverse();
            for (int i = 0; i < removals.Count; i++)
            {
                if (removals[i] < 0 || removals[i] >= stopCount)
                {
                    return CommandResult.Fail("removeStopIndexes[" + i + "] = " + removals[i] + " is out of range (line has " + stopCount + " stops).");
                }
                if (i > 0 && removals[i] == removals[i - 1])
                {
                    return CommandResult.Fail("removeStopIndexes lists " + removals[i] + " twice.");
                }
            }
            int afterRemoval = stopCount - removals.Count;
            if (removals.Count > 0 && afterRemoval < 2)
            {
                return CommandResult.Fail("Removing those stops would leave " + afterRemoval + " stop(s); a line needs at least 2. Use transit-line-delete instead.");
            }

            // Moves: indexes refer to the line after removals.
            List<StopEdit> moves = new List<StopEdit>();
            List<string> moveObjects = JsonUtil.GetObjectArray(body, "moveStops");
            for (int i = 0; i < moveObjects.Count; i++)
            {
                StopEdit edit;
                if (!ReadStopEdit(moveObjects[i], "moveStops[" + i + "]", false, out edit, out error))
                {
                    return CommandResult.Fail(error);
                }
                if (edit.Index < 0 || edit.Index >= afterRemoval)
                {
                    return CommandResult.Fail("moveStops[" + i + "].index " + edit.Index + " is out of range (line has " + afterRemoval + " stops after removals).");
                }
                edit.Resolution = ResolveStop(info, edit.X, edit.Z, roadSnap, stationSnap);
                if (!edit.Resolution.Ok)
                {
                    return CommandResult.Fail("moveStops[" + i + "]: " + edit.Resolution.Error);
                }
                moves.Add(edit);
            }

            // Adds: index = insert before the stop currently at that index (after removals and
            // moves, and after earlier adds in this list); -1 or omitted appends at the end of
            // the loop, i.e. between the last stop and the first.
            List<StopEdit> adds = new List<StopEdit>();
            List<string> addObjects = JsonUtil.GetObjectArray(body, "addStops");
            int running = afterRemoval;
            for (int i = 0; i < addObjects.Count; i++)
            {
                StopEdit edit;
                if (!ReadStopEdit(addObjects[i], "addStops[" + i + "]", true, out edit, out error))
                {
                    return CommandResult.Fail(error);
                }
                if (edit.Index < -1 || edit.Index > running)
                {
                    return CommandResult.Fail("addStops[" + i + "].index " + edit.Index + " is out of range (0.." + running + ", or -1 to append).");
                }
                edit.Resolution = ResolveStop(info, edit.X, edit.Z, roadSnap, stationSnap);
                if (!edit.Resolution.Ok)
                {
                    return CommandResult.Fail("addStops[" + i + "]: " + edit.Resolution.Error);
                }
                adds.Add(edit);
                running++;
            }

            if (adds.Count > 0 && !Singleton<NetManager>.instance.CheckLimits())
            {
                return CommandResult.Fail("The network node/segment limit is reached (NetManager.CheckLimits).");
            }

            bool nothing = name == null && colorText == null && float.IsNaN(budget) && float.IsNaN(ticketPrice) &&
                removals.Count == 0 && moves.Count == 0 && adds.Count == 0;
            if (nothing)
            {
                return CommandResult.Fail("Nothing to change: pass name, color, budget, ticketPrice, removeStopIndexes, moveStops or addStops.");
            }

            string simulationError = SimulateStopEdits(transport, lineId, removals, moves, adds);
            if (simulationError != null)
            {
                return CommandResult.Fail(simulationError + " Nothing was changed.");
            }

            if (dryRun)
            {
                // CanMoveStop / CanAddStop can only be asked of the line as it is now. When
                // removals precede them the answer describes the current line, and says so.
                bool exact = removals.Count == 0;
                StringBuilder plan = new StringBuilder();
                bool exactChecks = exact && moves.Count + adds.Count <= 1;
                bool allCan = true;
                for (int i = 0; i < moves.Count; i++)
                {
                    if (!transport.m_lines.m_buffer[lineId].CanMoveStop(lineId, moves[i].Index, moves[i].Resolution.Position)) allCan = false;
                }
                for (int i = 0; i < adds.Count; i++)
                {
                    if (!transport.m_lines.m_buffer[lineId].CanAddStop(lineId, adds[i].Index, adds[i].Resolution.Position)) allCan = false;
                }
                if (exactChecks && !allCan)
                {
                    return CommandResult.Fail("dryRun: TransportLine.CanMoveStop/CanAddStop rejects this edit on the current line (a stop would land within 1 m of its neighbour).");
                }
                plan.Append("{\"ok\":true,\"dryRun\":true,\"lineId\":").Append(lineId);
                plan.Append(",\"line\":").Append(LineSummaryJson(transport, lineId));
                plan.Append(",\"removeStopIndexes\":").Append(IntArray(removals));
                plan.Append(",\"moveStops\":[");
                for (int i = 0; i < moves.Count; i++)
                {
                    if (i > 0) plan.Append(",");
                    bool can = transport.m_lines.m_buffer[lineId].CanMoveStop(lineId, moves[i].Index, moves[i].Resolution.Position);
                    plan.Append(EditJson(moves[i], can));
                }
                plan.Append("],\"addStops\":[");
                for (int i = 0; i < adds.Count; i++)
                {
                    if (i > 0) plan.Append(",");
                    bool can = transport.m_lines.m_buffer[lineId].CanAddStop(lineId, adds[i].Index, adds[i].Resolution.Position);
                    plan.Append(EditJson(adds[i], can));
                }
                plan.Append("]");
                plan.Append(",\"canChecksExact\":").Append(JsonUtil.Bool(exactChecks));
                plan.Append(",\"message\":\"Validation passed; nothing was changed. canApply flags come from TransportLine.CanMoveStop/CanAddStop against the current line").Append(exact ? "" : " (removals not applied in dryRun)").Append(".\"}");
                return CommandResult.FromJson(plan.ToString());
            }

            List<string> applied = new List<string>();
            List<string> warnings = new List<string>();
            try
            {
                for (int i = 0; i < removals.Count; i++)
                {
                    if (!transport.m_lines.m_buffer[lineId].RemoveStop(lineId, removals[i]))
                    {
                        return PartialFailure(transport, lineId, applied, "TransportLine.RemoveStop failed for index " + removals[i] + ".");
                    }
                    applied.Add("removed stop " + removals[i]);
                }

                for (int i = 0; i < moves.Count; i++)
                {
                    StopEdit move = moves[i];
                    if (!transport.m_lines.m_buffer[lineId].CanMoveStop(lineId, move.Index, move.Resolution.Position))
                    {
                        return PartialFailure(transport, lineId, applied, "TransportLine.CanMoveStop rejected moveStops[" + i + "] (index " + move.Index + ").");
                    }
                    if (!transport.m_lines.m_buffer[lineId].MoveStop(lineId, move.Index, move.Resolution.Position, move.Resolution.FixedPlatform))
                    {
                        return PartialFailure(transport, lineId, applied, "TransportLine.MoveStop failed for moveStops[" + i + "] (index " + move.Index + ").");
                    }
                    applied.Add("moved stop " + move.Index);
                }

                for (int i = 0; i < adds.Count; i++)
                {
                    StopEdit add = adds[i];
                    if (!transport.m_lines.m_buffer[lineId].CanAddStop(lineId, add.Index, add.Resolution.Position))
                    {
                        return PartialFailure(transport, lineId, applied, "TransportLine.CanAddStop rejected addStops[" + i + "] (index " + add.Index + ").");
                    }
                    if (!transport.m_lines.m_buffer[lineId].AddStop(lineId, add.Index, add.Resolution.Position, add.Resolution.FixedPlatform))
                    {
                        return PartialFailure(transport, lineId, applied, "TransportLine.AddStop failed for addStops[" + i + "] (index " + add.Index + ").");
                    }
                    applied.Add("added stop at index " + add.Index);
                }

                ApplyLineProperties(transport, lineId, name, colorText != null, color, budget, ticketPrice, warnings);
                if (name != null) applied.Add("name");
                if (colorText != null) applied.Add("color");
                if (!float.IsNaN(budget)) applied.Add("budget");
                if (!float.IsNaN(ticketPrice)) applied.Add("ticketPrice");
            }
            catch (Exception ex)
            {
                return PartialFailure(transport, lineId, applied, ex.GetType().Name + ": " + ex.Message);
            }

            if (!transport.m_lines.m_buffer[lineId].Complete)
            {
                warnings.Add("The line is no longer Complete after the edit; vehicles will not run until it is closed again.");
            }

            Debug.Log("[SkylinesAgentBridge] Edited transit line " + lineId + ": " + string.Join(", ", applied.ToArray()));
            return CommandResult.FromJson("{\"ok\":true,\"dryRun\":false,\"lineId\":" + lineId +
                ",\"applied\":" + StringArray(applied) +
                ",\"warnings\":" + StringArray(warnings) +
                ",\"moveStops\":" + EditListJson(moves) +
                ",\"addStops\":" + EditListJson(adds) +
                ",\"line\":" + LineSummaryJson(transport, lineId) + "}");
        }

        // ------------------------------------------------------------------ delete

        public static CommandResult DeleteLine(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            TransportManager transport = Singleton<TransportManager>.instance;

            ushort lineId;
            string error;
            if (!ReadLineId(body, transport, out lineId, out error))
            {
                return CommandResult.Fail(error);
            }

            string summary = LineSummaryJson(transport, lineId);
            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true,\"lineId\":" + lineId +
                    ",\"message\":\"The line would be released with its stops; its vehicles return to their depots.\"" +
                    ",\"line\":" + summary + "}");
            }

            transport.ReleaseLine(lineId);
            bool released = transport.m_lines.m_buffer[lineId].m_flags == TransportLine.Flags.None ||
                (transport.m_lines.m_buffer[lineId].m_flags & TransportLine.Flags.Created) == TransportLine.Flags.None;

            Debug.Log("[SkylinesAgentBridge] Deleted transit line " + lineId);
            return CommandResult.FromJson("{\"ok\":" + JsonUtil.Bool(released) + ",\"dryRun\":false,\"lineId\":" + lineId +
                ",\"released\":" + JsonUtil.Bool(released) +
                (released ? "" : ",\"error\":\"TransportManager.ReleaseLine returned but the line slot is still flagged Created.\"") +
                ",\"deletedLine\":" + summary + "}");
        }

        // ------------------------------------------------------------------ budget

        public static CommandResult SetServiceBudget(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            string serviceText = JsonUtil.GetString(body, "service", "").Trim();
            string subServiceText = JsonUtil.GetString(body, "subService", "").Trim();

            ItemClass.Service service;
            if (!TryParseEnum<ItemClass.Service>(serviceText, out service) || service == ItemClass.Service.None)
            {
                return CommandResult.Fail("service is required and must be an ItemClass.Service name, e.g. PublicTransport. Got: " + serviceText);
            }
            if (ItemClass.GetPublicServiceIndex(service) == -1)
            {
                return CommandResult.Fail("service " + service + " has no budget slider (ItemClass.GetPublicServiceIndex returned -1).");
            }

            ItemClass.SubService subService = ItemClass.SubService.None;
            if (subServiceText.Length > 0 && !string.Equals(subServiceText, "None", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseEnum<ItemClass.SubService>(subServiceText, out subService))
                {
                    return CommandResult.Fail("Unknown subService: " + subServiceText);
                }
                if (ItemClass.GetPublicSubServiceIndex(subService) == -1)
                {
                    return CommandResult.Fail("subService " + subService + " has no budget of its own (ItemClass.GetPublicSubServiceIndex returned -1).");
                }
            }

            float day = JsonUtil.GetNumber(body, "day", float.NaN);
            float night = JsonUtil.GetNumber(body, "night", float.NaN);
            if (!float.IsNaN(day) && (day < 0f || day > 150f))
            {
                return CommandResult.Fail("day must be between 0 and 150.");
            }
            if (!float.IsNaN(night) && (night < 0f || night > 150f))
            {
                return CommandResult.Fail("night must be between 0 and 150.");
            }
            if (!dryRun && float.IsNaN(day) && float.IsNaN(night))
            {
                return CommandResult.Fail("Pass day and/or night (0..150), or dryRun:true to read the current values.");
            }

            EconomyManager economy = Singleton<EconomyManager>.instance;
            List<ItemClass.SubService> targets = new List<ItemClass.SubService>();
            targets.Add(subService);
            if (service == ItemClass.Service.PublicTransport && subService == ItemClass.SubService.None)
            {
                // EconomyManager.SetBudget cascades from the parent to every transit sub-service.
                targets.AddRange(TransitState.TransitSubServices);
            }

            int[] beforeDay = new int[targets.Count];
            int[] beforeNight = new int[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                beforeDay[i] = economy.GetBudget(service, targets[i], false);
                beforeNight[i] = economy.GetBudget(service, targets[i], true);
            }

            if (!dryRun)
            {
                if (!float.IsNaN(day)) economy.SetBudget(service, subService, Mathf.RoundToInt(day), false);
                if (!float.IsNaN(night)) economy.SetBudget(service, subService, Mathf.RoundToInt(night), true);
            }

            StringBuilder rows = new StringBuilder();
            for (int i = 0; i < targets.Count; i++)
            {
                if (i > 0) rows.Append(",");
                rows.Append("{\"service\":\"").Append(service.ToString()).Append("\"");
                rows.Append(",\"subService\":\"").Append(targets[i].ToString()).Append("\"");
                rows.Append(",\"dayBefore\":").Append(beforeDay[i]);
                rows.Append(",\"nightBefore\":").Append(beforeNight[i]);
                rows.Append(",\"day\":").Append(economy.GetBudget(service, targets[i], false));
                rows.Append(",\"night\":").Append(economy.GetBudget(service, targets[i], true)).Append("}");
            }

            return CommandResult.FromJson("{\"ok\":true,\"dryRun\":" + JsonUtil.Bool(dryRun) +
                ",\"source\":\"EconomyManager.GetBudget/SetBudget(service, subService, budget, night)\"" +
                ",\"budgets\":[" + rows.ToString() + "]}");
        }

        // ------------------------------------------------------------------ policy

        public static CommandResult SetPolicy(string body)
        {
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            string policyText = JsonUtil.GetString(body, "policy", "").Trim();
            int districtId = (int)JsonUtil.GetNumber(body, "districtId", 0f);

            bool enabledTrue = JsonUtil.GetBool(body, "enabled", true);
            bool enabledFalse = JsonUtil.GetBool(body, "enabled", false);
            if (enabledTrue != enabledFalse)
            {
                return CommandResult.Fail("enabled (true or false) is required.");
            }
            bool enabled = enabledTrue;

            DistrictPolicies.Policies policy;
            if (!TryParseEnum<DistrictPolicies.Policies>(policyText, out policy) || policy == DistrictPolicies.Policies.None ||
                !Enum.IsDefined(typeof(DistrictPolicies.Policies), policy))
            {
                return CommandResult.Fail("Unknown policy: " + policyText + ". See GET /state/policies for the available names.");
            }

            int type = (int)policy >> 5;
            DistrictManager districts = Singleton<DistrictManager>.instance;
            if (districtId < 0 || districtId >= districts.m_districts.m_buffer.Length)
            {
                return CommandResult.Fail("districtId must be 0 (whole city) or a district id from /state/policies.");
            }
            if (districtId > 0 && (districts.m_districts.m_buffer[districtId].m_flags & District.Flags.Created) == District.Flags.None)
            {
                return CommandResult.Fail("District " + districtId + " does not exist.");
            }
            if (!IsSettablePolicyType(type, districtId > 0))
            {
                return CommandResult.Fail("Policy " + policy + " (" + ((DistrictPolicies.Types)type).ToString() + ") cannot be set " +
                    (districtId > 0 ? "on a district" : "city-wide") + " through this command.");
            }
            if (!districts.IsPolicyLoaded(policy))
            {
                return CommandResult.Fail("Policy " + policy + " is not loaded in this game (DistrictManager.IsPolicyLoaded is false; usually a missing DLC).");
            }
            UnlockManager unlock = Singleton<UnlockManager>.instance;
            if (unlock != null && !unlock.Unlocked(policy))
            {
                return CommandResult.Fail("Policy " + policy + " is not unlocked yet (UnlockManager.Unlocked is false).");
            }

            byte district = (byte)districtId;
            bool before = districtId == 0 ? districts.IsCityPolicySet(policy) : districts.IsDistrictPolicySet(policy, district);

            if (!dryRun && before != enabled)
            {
                // Same calls PolicyContainer.SetPolicy makes when a policy is toggled in the UI.
                if (districtId == 0)
                {
                    if (enabled) districts.SetCityPolicy(policy); else districts.UnsetCityPolicy(policy);
                }
                else
                {
                    if (enabled) districts.SetDistrictPolicy(policy, district); else districts.UnsetDistrictPolicy(policy, district);
                }
            }

            bool after = districtId == 0 ? districts.IsCityPolicySet(policy) : districts.IsDistrictPolicySet(policy, district);
            bool ok = dryRun || after == enabled;

            return CommandResult.FromJson("{\"ok\":" + JsonUtil.Bool(ok) + ",\"dryRun\":" + JsonUtil.Bool(dryRun) +
                ",\"policy\":\"" + policy.ToString() + "\"" +
                ",\"type\":\"" + ((DistrictPolicies.Types)type).ToString() + "\"" +
                ",\"districtId\":" + districtId +
                ",\"districtName\":\"" + JsonUtil.Escape(districtId == 0 ? "" : districts.GetDistrictName(districtId)) + "\"" +
                ",\"requested\":" + JsonUtil.Bool(enabled) +
                ",\"before\":" + JsonUtil.Bool(before) +
                ",\"after\":" + JsonUtil.Bool(after) +
                ",\"changed\":" + JsonUtil.Bool(before != after) +
                (ok ? "" : ",\"error\":\"The policy state did not change to the requested value.\"") + "}");
        }

        /// <summary>
        /// Services, Taxation and CityPlanning policies are city-wide or per district;
        /// Specialization is district-only (the policies panel never offers it city-wide).
        /// Special, Event and Park policies belong to other panels and are refused.
        /// </summary>
        public static bool IsSettablePolicyType(int type, bool district)
        {
            if (type == (int)DistrictPolicies.Types.Services ||
                type == (int)DistrictPolicies.Types.Taxation ||
                type == (int)DistrictPolicies.Types.CityPlanning)
            {
                return true;
            }
            return district && type == (int)DistrictPolicies.Types.Specialization;
        }

        // ------------------------------------------------------------------ transport info

        /// <summary>
        /// Types a player draws as a free-standing line with TransportTool. Taxi has no lines,
        /// and evacuation / tourist / post / fishing / balloon / pedestrian lines are owned by a
        /// building and are started from it, which this command does not model.
        /// </summary>
        public static bool IsCreatableType(TransportInfo.TransportType type)
        {
            switch (type)
            {
                case TransportInfo.TransportType.Bus:
                case TransportInfo.TransportType.Metro:
                case TransportInfo.TransportType.Train:
                case TransportInfo.TransportType.Ship:
                case TransportInfo.TransportType.Airplane:
                case TransportInfo.TransportType.Tram:
                case TransportInfo.TransportType.Monorail:
                case TransportInfo.TransportType.CableCar:
                case TransportInfo.TransportType.Trolleybus:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ResolveTransportInfo(string body, bool ignoreUnlock, out TransportInfo info, out bool unlockIgnored, out string error)
        {
            info = null;
            error = null;
            unlockIgnored = false;
            string prefabName = JsonUtil.GetString(body, "prefab", "").Trim();
            string typeText = JsonUtil.GetString(body, "transportType", "").Trim();

            if (prefabName.Length > 0)
            {
                info = PrefabCollection<TransportInfo>.FindLoaded(prefabName);
                if (info == null)
                {
                    error = "TransportInfo prefab was not found: " + prefabName + ". GET /state/transit lists transportPrefabs.";
                    return false;
                }
            }
            else if (typeText.Length > 0)
            {
                TransportInfo.TransportType type;
                if (!TransitState.TryParseTransportType(typeText, out type))
                {
                    error = "Unknown transportType: " + typeText + ". Valid: " + TransitState.TransportTypeNames();
                    return false;
                }
                info = Singleton<TransportManager>.instance.GetTransportInfo(type);
                if (info == null)
                {
                    error = "No TransportInfo is loaded for " + type + " (TransportManager.GetTransportInfo returned null; usually a missing DLC).";
                    return false;
                }
            }
            else
            {
                error = "transportType (e.g. Bus, Metro, Train) or prefab is required.";
                return false;
            }

            if (!IsCreatableType(info.m_transportType))
            {
                error = info.m_transportType + " lines cannot be created with this command (only Bus, Metro, Train, Ship, Airplane, Tram, Monorail, CableCar, Trolleybus).";
                return false;
            }

            if (!UnlockReport.IsUnlocked(info.m_UnlockMilestone))
            {
                // The same test the Public Transport panel uses to enable the line tool button
                // (GeneratedScrollPanel.CreateAssetItem -> UnlockManager.Unlocked(GetUnlockMilestone())).
                if (!ignoreUnlock)
                {
                    error = info.name + " is not unlocked yet: " + UnlockReport.TransportUnlockSummary(info) +
                        ". Pass ignoreUnlock:true to skip this check (the game's line tool would not offer it).";
                    return false;
                }
                unlockIgnored = true;
            }

            return true;
        }

        // ------------------------------------------------------------------ stop snapping

        public sealed class StopResolution
        {
            public bool Ok;
            public string Error;
            public float RequestedX;
            public float RequestedZ;
            public Vector3 Position;
            public bool FixedPlatform;
            public string Via;
            public ushort SegmentId;
            public ushort BuildingId;
            public int CandidatesTried;
        }

        private struct Candidate
        {
            public float Distance;
            public ushort Segment;
            public ushort Building;
            public float Y;
        }

        /// <summary>
        /// Approximates the stop TransportTool would produce for a click at (x, z). It is NOT a
        /// raycast: the tool takes the single object under the camera ray (3D, by mesh/bounds)
        /// and fails if that one object is unsuitable. Here, segments and station buildings the
        /// raycast filter would accept are gathered by flat distance (station distance is to the
        /// building pivot), sorted nearest first, and tried in turn (up to MaxCandidatesTried)
        /// through the ported GetStopPosition until one succeeds. Stacked networks at the same
        /// x/z tie on distance and the elevation picked is not controlled.
        /// </summary>
        public static StopResolution ResolveStop(TransportInfo info, float x, float z, float roadSnap, float stationSnap)
        {
            StopResolution result = new StopResolution();
            result.RequestedX = x;
            result.RequestedZ = z;

            NetManager net = Singleton<NetManager>.instance;
            BuildingManager buildings = Singleton<BuildingManager>.instance;
            List<Candidate> candidates = new List<Candidate>();

            // TransportTool: m_ignoreSegmentFlags = All when m_netService is None.
            if (info.m_netService != ItemClass.Service.None)
            {
                NetSegment[] segments = net.m_segments.m_buffer;
                for (int i = 1; i < segments.Length; i++)
                {
                    NetSegment.Flags flags = segments[i].m_flags;
                    if ((flags & NetSegment.Flags.Created) == NetSegment.Flags.None ||
                        (flags & (NetSegment.Flags.Deleted | NetSegment.Flags.Collapsed)) != NetSegment.Flags.None)
                    {
                        continue;
                    }

                    Bounds bounds = segments[i].m_bounds;
                    if (x < bounds.min.x - roadSnap || x > bounds.max.x + roadSnap ||
                        z < bounds.min.z - roadSnap || z > bounds.max.z + roadSnap)
                    {
                        continue;
                    }

                    NetInfo segmentInfo = segments[i].Info;
                    if (segmentInfo == null || !SegmentMatches(segmentInfo, info))
                    {
                        continue;
                    }

                    Vector3 closest = segments[i].GetClosestPosition(new Vector3(x, bounds.center.y, z));
                    float distance = Mathf.Sqrt(FlatSqr(closest, new Vector3(x, 0f, z)));
                    if (distance <= roadSnap)
                    {
                        Candidate c = new Candidate();
                        c.Distance = distance;
                        c.Segment = (ushort)i;
                        c.Y = closest.y;
                        candidates.Add(c);
                    }
                }
            }

            // TransportTool: m_ignoreBuildingFlags = All when m_stationService is None (except
            // for Pedestrian lines, which this command does not create).
            if (info.m_stationService != ItemClass.Service.None)
            {
                Building[] buffer = buildings.m_buildings.m_buffer;
                for (int i = 1; i < buffer.Length; i++)
                {
                    Building.Flags flags = buffer[i].m_flags;
                    if ((flags & Building.Flags.Created) == Building.Flags.None ||
                        (flags & Building.Flags.Deleted) != Building.Flags.None)
                    {
                        continue;
                    }

                    Vector3 position = buffer[i].m_position;
                    float dx = position.x - x;
                    float dz = position.z - z;
                    if (Mathf.Abs(dx) > stationSnap || Mathf.Abs(dz) > stationSnap)
                    {
                        continue;
                    }

                    BuildingInfo buildingInfo = buffer[i].Info;
                    if (buildingInfo == null || buildingInfo.m_class == null || !ClassMatches(buildingInfo.m_class, info.m_stationService, info.m_stationSubService, info.m_stationLayer))
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance <= stationSnap)
                    {
                        Candidate c = new Candidate();
                        c.Distance = distance;
                        c.Building = (ushort)i;
                        c.Y = position.y;
                        candidates.Add(c);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                result.Error = "no " + DescribeTargets(info, roadSnap, stationSnap) + ".";
                return result;
            }

            candidates.Sort(delegate(Candidate a, Candidate b) { return a.Distance.CompareTo(b.Distance); });

            int tried = 0;
            for (int i = 0; i < candidates.Count && tried < MaxCandidatesTried; i++)
            {
                tried++;
                Candidate c = candidates[i];
                Vector3 hit = new Vector3(x, c.Y, z);
                bool fixedPlatform;
                ushort usedBuilding;
                if (GetStopPosition(info, c.Segment, c.Building, 0, ref hit, out fixedPlatform, out usedBuilding))
                {
                    result.Ok = true;
                    result.Position = hit;
                    result.FixedPlatform = fixedPlatform;
                    result.SegmentId = usedBuilding == 0 ? c.Segment : (ushort)0;
                    result.BuildingId = usedBuilding;
                    result.Via = usedBuilding != 0 ? "station" : "segment";
                    result.CandidatesTried = tried;
                    return result;
                }
            }

            result.CandidatesTried = tried;
            result.Error = "found " + candidates.Count + " candidate(s) (nearest " + JsonUtil.Number(candidates[0].Distance) +
                " m) but TransportTool's GetStopPosition rejected every one: no stop-capable lane or matching station platform for " +
                info.name + ".";
            return result;
        }

        private static string DescribeTargets(TransportInfo info, float roadSnap, float stationSnap)
        {
            List<string> parts = new List<string>();
            if (info.m_netService != ItemClass.Service.None)
            {
                parts.Add(info.m_netService + "/" + info.m_netSubService + " segment within " + JsonUtil.Number(roadSnap) + " m");
            }
            if (info.m_stationService != ItemClass.Service.None)
            {
                parts.Add(info.m_stationService + "/" + info.m_stationSubService + " station building within " + JsonUtil.Number(stationSnap) + " m");
            }
            return parts.Count == 0 ? "stop target defined for this prefab" : string.Join(" or ", parts.ToArray());
        }

        /// <summary>
        /// NetManager.RayCast's segment filter with TransportTool's inputs: the connection
        /// class or intersect class matches m_netService/m_netSubService/m_netLayer, or the
        /// connection class matches the secondary service (and NetAI.CanIntersect(null)).
        /// </summary>
        private static bool SegmentMatches(NetInfo segment, TransportInfo info)
        {
            ItemClass connection = segment.GetConnectionClass();
            if (connection != null && ClassMatches(connection, info.m_netService, info.m_netSubService, info.m_netLayer))
            {
                return true;
            }
            if (segment.m_intersectClass != null && ClassMatches(segment.m_intersectClass, info.m_netService, info.m_netSubService, info.m_netLayer))
            {
                return true;
            }
            if (connection != null && info.m_secondaryNetService != ItemClass.Service.None && segment.m_netAI != null &&
                segment.m_netAI.CanIntersect(null) &&
                connection.m_service == info.m_secondaryNetService &&
                (info.m_secondaryNetSubService == ItemClass.SubService.None || connection.m_subService == info.m_secondaryNetSubService) &&
                (info.m_netLayer == ItemClass.Layer.None || (connection.m_layer & info.m_netLayer) != ItemClass.Layer.None))
            {
                return true;
            }
            return false;
        }

        private static bool ClassMatches(ItemClass itemClass, ItemClass.Service service, ItemClass.SubService subService, ItemClass.Layer layer)
        {
            return (service == ItemClass.Service.None || itemClass.m_service == service) &&
                (subService == ItemClass.SubService.None || itemClass.m_subService == subService) &&
                (layer == ItemClass.Layer.None || (itemClass.m_layer & layer) != ItemClass.Layer.None);
        }

        private static bool SameLineKind(TransportInfo candidate, TransportInfo info)
        {
            return candidate != null && candidate.m_transportType == info.m_transportType && candidate.m_vehicleType == info.m_vehicleType;
        }

        /// <summary>
        /// Port of TransportTool.GetStopPosition for non-pedestrian lines with no owning
        /// building (TransportTool.m_building == 0). Differences from the original: the random
        /// vehicle used for spawn geometry comes from a local Randomizer instead of the
        /// simulation's shared one, and usedBuilding reports which station was used.
        /// </summary>
        private static bool GetStopPosition(TransportInfo info, ushort segment, ushort building, ushort firstStop, ref Vector3 hitPos,
            out bool fixedPlatform, out ushort usedBuilding)
        {
            NetManager net = Singleton<NetManager>.instance;
            BuildingManager buildings = Singleton<BuildingManager>.instance;
            fixedPlatform = false;
            usedBuilding = 0;

            if (segment != 0)
            {
                if ((net.m_segments.m_buffer[segment].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                {
                    // A station's own platform segment: hand over to the building branch.
                    building = NetSegment.FindOwnerBuilding(segment, 363f);
                    if (building != 0)
                    {
                        BuildingInfo owner = buildings.m_buildings.m_buffer[building].Info;
                        TransportInfo primary = owner == null ? null : owner.m_buildingAI.GetTransportLineInfo();
                        TransportInfo secondary = owner == null ? null : owner.m_buildingAI.GetSecondaryTransportLineInfo();
                        if ((primary != null && primary.m_transportType == info.m_transportType) ||
                            (secondary != null && secondary.m_transportType == info.m_transportType))
                        {
                            segment = 0;
                        }
                        else
                        {
                            building = 0;
                        }
                    }
                }

                Vector3 pedestrianPosition;
                uint pedestrianLane;
                int pedestrianLaneIndex;
                float pedestrianOffset;
                if (segment != 0 && net.m_segments.m_buffer[segment].GetClosestLanePosition(hitPos, NetInfo.LaneType.Pedestrian,
                    VehicleInfo.VehicleType.None, info.vehicleCategory, info.m_vehicleType,
                    out pedestrianPosition, out pedestrianLane, out pedestrianLaneIndex, out pedestrianOffset))
                {
                    NetInfo segmentInfo = net.m_segments.m_buffer[segment].Info;
                    bool inverted = (net.m_segments.m_buffer[segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
                    Vector3 direction;

                    if (info.m_vehicleType == VehicleInfo.VehicleType.None)
                    {
                        NetLane.Flags laneStops = (NetLane.Flags)net.m_lanes.m_buffer[pedestrianLane].m_flags & NetLane.Flags.Stops;
                        NetLane.Flags wanted = info.m_stopFlag;
                        if (segmentInfo.m_vehicleTypes != VehicleInfo.VehicleType.None)
                        {
                            wanted = NetLane.Flags.None;
                        }
                        if (laneStops != NetLane.Flags.None && wanted != NetLane.Flags.None && laneStops != wanted)
                        {
                            return false;
                        }
                        float offset = segmentInfo.m_lanes[pedestrianLaneIndex].m_stopOffset;
                        if (inverted) offset = -offset;
                        net.m_lanes.m_buffer[pedestrianLane].CalculateStopPositionAndDirection(128f / 255f, offset, out hitPos, out direction);
                        fixedPlatform = true;
                        return true;
                    }

                    Vector3 vehiclePosition;
                    uint vehicleLane;
                    int vehicleLaneIndex;
                    float vehicleOffset;
                    if (net.m_segments.m_buffer[segment].GetClosestLanePosition(pedestrianPosition,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, info.vehicleCategory,
                        out vehiclePosition, out vehicleLane, out vehicleLaneIndex, out vehicleOffset))
                    {
                        // The original checks the pedestrian lane's stop flags here, not the vehicle lane's.
                        NetLane.Flags laneStops = (NetLane.Flags)net.m_lanes.m_buffer[pedestrianLane].m_flags & NetLane.Flags.Stops;
                        if (laneStops != NetLane.Flags.None && info.m_stopFlag != NetLane.Flags.None && laneStops != info.m_stopFlag)
                        {
                            return false;
                        }
                        float offset = segmentInfo.m_lanes[vehicleLaneIndex].m_stopOffset;
                        if (inverted) offset = -offset;
                        net.m_lanes.m_buffer[vehicleLane].CalculateStopPositionAndDirection(128f / 255f, offset, out hitPos, out direction);
                        fixedPlatform = true;
                        return true;
                    }
                }
            }

            if (building == 0)
            {
                return false;
            }

            ushort parent = 0;
            if ((buildings.m_buildings.m_buffer[building].m_flags & Building.Flags.Untouchable) != Building.Flags.None)
            {
                parent = Building.FindParentBuilding(building);
            }

            Randomizer vehicleRandomizer = new Randomizer((ulong)building);
            VehicleInfo vehicle = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref vehicleRandomizer,
                info.m_class.m_service, info.m_class.m_subService, info.m_class.m_level, info.m_vehicleType);
            if (vehicle == null)
            {
                return false;
            }

            BuildingInfo stationInfo = buildings.m_buildings.m_buffer[building].Info;
            if (stationInfo == null || stationInfo.m_buildingAI == null)
            {
                return false;
            }
            TransportInfo lineInfo = stationInfo.m_buildingAI.GetTransportLineInfo();
            TransportInfo secondaryLineInfo = stationInfo.m_buildingAI.GetSecondaryTransportLineInfo();
            if (parent != 0 && !SameLineKind(lineInfo, info) && !SameLineKind(secondaryLineInfo, info))
            {
                building = parent;
                stationInfo = buildings.m_buildings.m_buffer[building].Info;
                if (stationInfo == null || stationInfo.m_buildingAI == null)
                {
                    return false;
                }
                lineInfo = stationInfo.m_buildingAI.GetTransportLineInfo();
                secondaryLineInfo = stationInfo.m_buildingAI.GetSecondaryTransportLineInfo();
            }

            if (!SameLineKind(lineInfo, info) && !SameLineKind(secondaryLineInfo, info))
            {
                return false;
            }

            Vector3 best = Vector3.zero;
            int bestCount = 1000000;
            for (int i = 0; i < 12; i++)
            {
                Randomizer randomizer = new Randomizer((ulong)i);
                Vector3 position;
                Vector3 target;
                stationInfo.m_buildingAI.CalculateSpawnPosition(building, ref buildings.m_buildings.m_buffer[building], ref randomizer, vehicle, out position, out target);
                int lineCount = 0;
                if (info.m_avoidSameStopPlatform)
                {
                    lineCount = GetLineCount(position, target - position, info.m_transportType);
                }
                if (info.m_transportType != TransportInfo.TransportType.Metro)
                {
                    if (lineCount < bestCount)
                    {
                        best = position;
                        bestCount = lineCount;
                    }
                    else if (lineCount == bestCount && Vector3.SqrMagnitude(position - hitPos) < Vector3.SqrMagnitude(best - hitPos))
                    {
                        best = position;
                    }
                }
                else if (Vector3.SqrMagnitude(position - hitPos) < Vector3.SqrMagnitude(best - hitPos))
                {
                    best = position;
                    bestCount = 0;
                }
            }

            if (firstStop != 0)
            {
                Vector3 firstPosition = net.m_nodes.m_buffer[firstStop].m_position;
                if (Vector3.SqrMagnitude(firstPosition - best) < 16384f)
                {
                    uint lane = net.m_nodes.m_buffer[firstStop].m_lane;
                    if (lane != 0)
                    {
                        ushort laneSegment = net.m_lanes.m_buffer[lane].m_segment;
                        if (laneSegment != 0 && (net.m_segments.m_buffer[laneSegment].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None &&
                            NetSegment.FindOwnerBuilding(laneSegment, 363f) == building)
                        {
                            hitPos = firstPosition;
                            usedBuilding = building;
                            return true;
                        }
                    }
                }
            }

            hitPos = best;
            usedBuilding = building;
            return bestCount != 1000000;
        }

        /// <summary>Port of TransportTool.GetLineCount: existing stops of this type within 4 m of a platform.</summary>
        private static int GetLineCount(Vector3 stopPosition, Vector3 stopDirection, TransportInfo.TransportType transportType)
        {
            NetManager net = Singleton<NetManager>.instance;
            TransportManager transport = Singleton<TransportManager>.instance;
            stopDirection.Normalize();
            Segment3 segment = new Segment3(stopPosition - stopDirection * 16f, stopPosition + stopDirection * 16f);
            Vector3 min = segment.Min();
            Vector3 max = segment.Max();
            int minX = Mathf.Max((int)((min.x - 4f) / 64f + 135f), 0);
            int minZ = Mathf.Max((int)((min.z - 4f) / 64f + 135f), 0);
            int maxX = Mathf.Min((int)((max.x + 4f) / 64f + 135f), 269);
            int maxZ = Mathf.Min((int)((max.z + 4f) / 64f + 135f), 269);
            int count = 0;

            for (int gz = minZ; gz <= maxZ; gz++)
            {
                for (int gx = minX; gx <= maxX; gx++)
                {
                    ushort node = net.m_nodeGrid[gz * 270 + gx];
                    int guard = 0;
                    while (node != 0)
                    {
                        ushort line = net.m_nodes.m_buffer[node].m_transportLine;
                        if (line != 0)
                        {
                            TransportInfo lineInfo = transport.m_lines.m_buffer[line].Info;
                            if (lineInfo != null && lineInfo.m_transportType == transportType &&
                                (transport.m_lines.m_buffer[line].m_flags & TransportLine.Flags.Temporary) == TransportLine.Flags.None &&
                                segment.DistanceSqr(net.m_nodes.m_buffer[node].m_position) < 16f)
                            {
                                count++;
                            }
                        }
                        node = net.m_nodes.m_buffer[node].m_nextGridNode;
                        if (++guard >= 32768)
                        {
                            break;
                        }
                    }
                }
            }

            return count;
        }

        // ------------------------------------------------------------------ helpers

        private sealed class StopEdit
        {
            public int Index;
            public float X;
            public float Z;
            public StopResolution Resolution;
        }

        private static bool ReadStopEdit(string json, string label, bool indexOptional, out StopEdit edit, out string error)
        {
            edit = new StopEdit();
            error = null;
            float index = JsonUtil.GetNumber(json, "index", float.NaN);
            if (float.IsNaN(index))
            {
                if (!indexOptional)
                {
                    error = label + ".index is required.";
                    return false;
                }
                index = -1f;
            }
            edit.Index = (int)index;
            edit.X = JsonUtil.GetNumber(json, "x", float.NaN);
            edit.Z = JsonUtil.GetNumber(json, "z", float.NaN);
            if (float.IsNaN(edit.X) || float.IsNaN(edit.Z))
            {
                error = label + " needs numeric x and z.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Replays the edit on a list of stop positions using the same neighbour rule the game
        /// applies at each step (TransportLine.CanAddStop / CanMoveStop reject a stop within 1 m
        /// of the stop before or after it), so an edit the game would refuse halfway through is
        /// refused up front instead. Index semantics follow TransportLine.AddStop: on a complete
        /// line, index 0, -1 and count all insert between the last stop and the first (the end
        /// of the list); on an incomplete line index 0 inserts a new first stop.
        /// </summary>
        private static string SimulateStopEdits(TransportManager transport, ushort lineId, List<int> removals, List<StopEdit> moves, List<StopEdit> adds)
        {
            NetManager net = Singleton<NetManager>.instance;
            bool complete = transport.m_lines.m_buffer[lineId].Complete;
            List<Vector3> stops = new List<Vector3>();
            ushort first = transport.m_lines.m_buffer[lineId].m_stops;
            ushort stop = first;
            while (stop != 0 && stops.Count < 1024)
            {
                stops.Add(net.m_nodes.m_buffer[stop].m_position);
                stop = TransportLine.GetNextStop(stop);
                if (stop == first) break;
            }

            for (int i = 0; i < removals.Count; i++)
            {
                if (removals[i] < stops.Count) stops.RemoveAt(removals[i]);
            }

            for (int i = 0; i < moves.Count; i++)
            {
                int index = moves[i].Index;
                Vector3 position = moves[i].Resolution.Position;
                if (TooCloseToNeighbour(stops, index, position, complete, true))
                {
                    return "moveStops[" + i + "] would put stop " + index + " within 1 m of a neighbouring stop, which the game refuses.";
                }
                stops[index] = position;
            }

            for (int i = 0; i < adds.Count; i++)
            {
                int index = adds[i].Index;
                int insertAt;
                if (index == -1 || index >= stops.Count || (complete && index == 0))
                {
                    insertAt = stops.Count;
                }
                else
                {
                    insertAt = index;
                }
                Vector3 position = adds[i].Resolution.Position;
                stops.Insert(insertAt, position);
                if (TooCloseToNeighbour(stops, insertAt, position, complete, true))
                {
                    return "addStops[" + i + "] would land within 1 m of a neighbouring stop (after the earlier edits in this call), which the game refuses.";
                }
            }

            return null;
        }

        private static bool TooCloseToNeighbour(List<Vector3> stops, int index, Vector3 position, bool loop, bool excludeSelf)
        {
            int count = stops.Count;
            if (count < 2) return false;
            int prev = index - 1;
            int next = index + 1;
            if (loop)
            {
                prev = (prev + count) % count;
                next = next % count;
            }
            if (prev >= 0 && prev < count && prev != index && (stops[prev] - position).sqrMagnitude < 1f) return true;
            if (next >= 0 && next < count && next != index && (stops[next] - position).sqrMagnitude < 1f) return true;
            return false;
        }

        private static bool ReadLineId(string body, TransportManager transport, out ushort lineId, out string error)
        {
            lineId = 0;
            error = null;
            float raw = JsonUtil.GetNumber(body, "lineId", 0f);
            if (raw < 1f || raw >= transport.m_lines.m_buffer.Length)
            {
                error = "lineId is required (1.." + (transport.m_lines.m_buffer.Length - 1) + "); GET /state/transit lists the lines.";
                return false;
            }
            lineId = (ushort)raw;
            TransportLine.Flags flags = transport.m_lines.m_buffer[lineId].m_flags;
            if ((flags & TransportLine.Flags.Created) == TransportLine.Flags.None ||
                (flags & (TransportLine.Flags.Deleted | TransportLine.Flags.Temporary)) != TransportLine.Flags.None)
            {
                error = "Transport line " + lineId + " does not exist.";
                return false;
            }
            return true;
        }

        private static void ApplyLineProperties(TransportManager transport, ushort lineId, string name, bool hasColor, Color32 color,
            float budget, float ticketPrice, List<string> warnings)
        {
            // SetLineColor/SetLineName notify the UI through ThreadHelper.dispatcher.Dispatch, which
            // throws "Already in the same thread" when called from the main thread (where the bridge
            // queue runs). The game's own panel queues them on the simulation thread with AddAction
            // (PublicTransportWorldInfoPanel), so do the same: fire and forget, applied within a frame.
            SimulationManager simulation = ColossalFramework.Singleton<SimulationManager>.instance;
            if (hasColor)
            {
                simulation.AddAction(transport.SetLineColor(lineId, color));
                warnings.Add("Colour queued on the simulation thread; re-read /state/transit to confirm.");
            }

            if (name != null)
            {
                simulation.AddAction(transport.SetLineName(lineId, name));
                warnings.Add("Name queued on the simulation thread; re-read /state/transit to confirm.");
            }

            if (!float.IsNaN(budget))
            {
                // What the line panel's vehicle-count slider writes.
                transport.m_lines.m_buffer[lineId].m_budget = (ushort)Mathf.RoundToInt(budget);
            }

            if (!float.IsNaN(ticketPrice))
            {
                // What the line panel's ticket price slider writes (cents).
                transport.m_lines.m_buffer[lineId].m_ticketPrice = (ushort)Mathf.RoundToInt(ticketPrice);
            }
        }

        private static CommandResult PartialFailure(TransportManager transport, ushort lineId, List<string> applied, string error)
        {
            string summary = "";
            try
            {
                summary = LineSummaryJson(transport, lineId);
            }
            catch (Exception)
            {
                summary = "null";
            }
            CommandResult result = CommandResult.Fail(error);
            result.Json = "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(error + " Earlier steps were NOT rolled back.") + "\"" +
                ",\"lineId\":" + lineId +
                ",\"applied\":" + StringArray(applied) +
                ",\"line\":" + (summary.Length == 0 ? "null" : summary) + "}";
            return result;
        }

        public static string LineSummaryJson(TransportManager transport, ushort lineId)
        {
            TransportInfo info = transport.m_lines.m_buffer[lineId].Info;
            StringBuilder json = new StringBuilder();
            json.Append("{\"id\":").Append(lineId);
            json.Append(",\"number\":").Append(transport.m_lines.m_buffer[lineId].m_lineNumber);
            json.Append(",\"name\":\"").Append(JsonUtil.Escape(TransitState.SafeLineName(transport, lineId))).Append("\"");
            json.Append(",\"prefab\":\"").Append(info == null ? "" : JsonUtil.Escape(info.name)).Append("\"");
            json.Append(",\"transportType\":\"").Append(info == null ? "" : info.m_transportType.ToString()).Append("\"");
            json.Append(",\"color\":\"").Append(TransitState.ColorHex(transport.GetLineColor(lineId))).Append("\"");
            json.Append(",\"flags\":").Append(TransitState.FlagNames(transport.m_lines.m_buffer[lineId].m_flags));
            json.Append(",\"complete\":").Append(JsonUtil.Bool(transport.m_lines.m_buffer[lineId].Complete));
            json.Append(",\"stopCount\":").Append(transport.m_lines.m_buffer[lineId].CountStops(lineId));
            json.Append(",\"vehicleCount\":").Append(transport.m_lines.m_buffer[lineId].CountVehicles(lineId));
            json.Append(",\"budget\":").Append(transport.m_lines.m_buffer[lineId].m_budget);
            json.Append(",\"ticketPrice\":").Append(transport.m_lines.m_buffer[lineId].m_ticketPrice);
            json.Append(",\"stops\":[");
            NetManager net = Singleton<NetManager>.instance;
            ushort first = transport.m_lines.m_buffer[lineId].m_stops;
            ushort stop = first;
            int index = 0;
            while (stop != 0 && index < 1024)
            {
                if (index > 0) json.Append(",");
                Vector3 p = net.m_nodes.m_buffer[stop].m_position;
                json.Append("{\"index\":").Append(index).Append(",\"nodeId\":").Append(stop).Append(",").Append(PointFields(p)).Append("}");
                index++;
                stop = TransportLine.GetNextStop(stop);
                if (stop == first) break;
            }
            json.Append("]}");
            return json.ToString();
        }

        private static string StopsJson(List<StopResolution> stops, List<ushort> nodeIds)
        {
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < stops.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append("{\"index\":").Append(i);
                if (nodeIds != null && i < nodeIds.Count)
                {
                    json.Append(",\"nodeId\":").Append(nodeIds[i]);
                }
                json.Append(",").Append(ResolutionFields(stops[i])).Append("}");
            }
            json.Append("]");
            return json.ToString();
        }

        private static string EditJson(StopEdit edit, bool canApply)
        {
            return "{\"index\":" + edit.Index + ",\"canApply\":" + JsonUtil.Bool(canApply) + "," + ResolutionFields(edit.Resolution) + "}";
        }

        private static string EditListJson(List<StopEdit> edits)
        {
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < edits.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append("{\"index\":").Append(edits[i].Index).Append(",").Append(ResolutionFields(edits[i].Resolution)).Append("}");
            }
            json.Append("]");
            return json.ToString();
        }

        private static string ResolutionFields(StopResolution stop)
        {
            float snap = Mathf.Sqrt(FlatSqr(stop.Position, new Vector3(stop.RequestedX, 0f, stop.RequestedZ)));
            return "\"requested\":{\"x\":" + JsonUtil.Number(stop.RequestedX) + ",\"z\":" + JsonUtil.Number(stop.RequestedZ) + "}" +
                ",\"resolved\":{" + PointFields(stop.Position) + "}" +
                ",\"snapDistance\":" + JsonUtil.Number(snap) +
                ",\"via\":\"" + stop.Via + "\"" +
                ",\"segmentId\":" + stop.SegmentId +
                ",\"buildingId\":" + stop.BuildingId +
                ",\"fixedPlatform\":" + JsonUtil.Bool(stop.FixedPlatform) +
                ",\"candidatesTried\":" + stop.CandidatesTried;
        }

        private static string PointFields(Vector3 p)
        {
            return "\"x\":" + JsonUtil.Number(p.x) + ",\"y\":" + JsonUtil.Number(p.y) + ",\"z\":" + JsonUtil.Number(p.z);
        }

        private static string StringArray(List<string> values)
        {
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append("\"").Append(JsonUtil.Escape(values[i])).Append("\"");
            }
            json.Append("]");
            return json.ToString();
        }

        private static string IntArray(List<int> values)
        {
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
            json.Append("]");
            return json.ToString();
        }

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>Parses "#RRGGBB" (or "RRGGBB") into an opaque colour, invariantly.</summary>
        public static bool TryParseColor(string text, out Color32 color)
        {
            color = new Color32(0, 0, 0, 255);
            if (text == null) return false;
            string hex = text.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return false;
            int value;
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return false;
            color = new Color32((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF), 255);
            return true;
        }

        /// <summary>Reads a top-level JSON array of integers; a missing property is an empty list.</summary>
        public static bool TryGetIntArray(string json, string name, out List<int> values, out string error)
        {
            values = new List<int>();
            error = null;
            int property = json.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
            if (property < 0)
            {
                return true;
            }
            int colon = json.IndexOf(':', property);
            int open = colon < 0 ? -1 : json.IndexOf('[', colon);
            int close = open < 0 ? -1 : json.IndexOf(']', open);
            if (open < 0 || close < 0 || json.Substring(colon + 1, open - colon - 1).Trim().Length > 0)
            {
                error = name + " must be an array of integers.";
                return false;
            }
            string inner = json.Substring(open + 1, close - open - 1).Trim();
            if (inner.Length == 0)
            {
                return true;
            }
            string[] parts = inner.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                int value;
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                {
                    error = name + "[" + i + "] is not an integer: " + parts[i].Trim();
                    return false;
                }
                values.Add(value);
            }
            return true;
        }

        private static bool TryParseEnum<T>(string value, out T result)
        {
            result = default(T);
            if (value == null || value.Length == 0)
            {
                return false;
            }
            string[] names = Enum.GetNames(typeof(T));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    result = (T)Enum.Parse(typeof(T), names[i]);
                    return true;
                }
            }
            return false;
        }
    }
}
