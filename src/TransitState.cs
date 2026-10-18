using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ColossalFramework;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Read side of public transport: lines, stops, per-type totals, the city passenger
    /// counters, transit facilities, and the policy list.
    ///
    /// Every number here is read from a named game field, and the JSON says which one, because
    /// the info panels show several different passenger counts and they are easy to mix up:
    /// the line panel's "passengers" is GroupData.m_averageCount, not the last finished period.
    /// </summary>
    public static class TransitState
    {
        /// <summary>TransportLine.Flags values, one bit each (All and None are excluded).</summary>
        private static readonly TransportLine.Flags[] LineFlagBits = new TransportLine.Flags[]
        {
            TransportLine.Flags.Created,
            TransportLine.Flags.Deleted,
            TransportLine.Flags.Complete,
            TransportLine.Flags.Temporary,
            TransportLine.Flags.Hidden,
            TransportLine.Flags.Selected,
            TransportLine.Flags.Invalid,
            TransportLine.Flags.CompleteSet,
            TransportLine.Flags.CustomColor,
            TransportLine.Flags.CustomName,
            TransportLine.Flags.DisabledDay,
            TransportLine.Flags.DisabledNight,
            TransportLine.Flags.Highlighted
        };

        /// <summary>The sub-services EconomyManager.SetBudget cascades to from PublicTransport.</summary>
        public static readonly ItemClass.SubService[] TransitSubServices = new ItemClass.SubService[]
        {
            ItemClass.SubService.PublicTransportBus,
            ItemClass.SubService.PublicTransportMetro,
            ItemClass.SubService.PublicTransportTrain,
            ItemClass.SubService.PublicTransportShip,
            ItemClass.SubService.PublicTransportPlane,
            ItemClass.SubService.PublicTransportTaxi,
            ItemClass.SubService.PublicTransportTram,
            ItemClass.SubService.PublicTransportMonorail,
            ItemClass.SubService.PublicTransportCableCar,
            ItemClass.SubService.PublicTransportTours,
            ItemClass.SubService.PublicTransportPost,
            ItemClass.SubService.PublicTransportTrolleybus
        };

        public static CommandResult BuildTransitJson(string typeFilter, bool includeStops, int limit)
        {
            if (limit < 0) limit = 0;
            if (limit > 256) limit = 256;

            bool hasFilter = typeFilter != null && typeFilter.Trim().Length > 0;
            TransportInfo.TransportType filterType = TransportInfo.TransportType.Bus;
            if (hasFilter && !TryParseTransportType(typeFilter.Trim(), out filterType))
            {
                return CommandResult.Fail("Unknown transport type: " + typeFilter + ". Valid: " + TransportTypeNames());
            }

            TransportManager transport = Singleton<TransportManager>.instance;
            NetManager net = Singleton<NetManager>.instance;
            TransportLine[] lines = transport.m_lines.m_buffer;

            int typeCount = Enum.GetValues(typeof(TransportInfo.TransportType)).Length;
            TypeTotals[] totals = new TypeTotals[typeCount + 1];

            StringBuilder lineJson = new StringBuilder();
            int emitted = 0;
            int matched = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                ushort lineId = (ushort)i;
                TransportLine.Flags flags = lines[i].m_flags;
                if ((flags & TransportLine.Flags.Created) == TransportLine.Flags.None ||
                    (flags & (TransportLine.Flags.Deleted | TransportLine.Flags.Temporary)) != TransportLine.Flags.None)
                {
                    // Temporary lines are TransportTool's live preview, not real lines.
                    continue;
                }

                TransportInfo info = lines[i].Info;
                if (info == null)
                {
                    continue;
                }

                if (hasFilter && info.m_transportType != filterType)
                {
                    continue;
                }

                matched++;
                int stopCount = lines[i].CountStops(lineId);
                int vehicleCount = lines[i].CountVehicles(lineId);
                int targetVehicles = SafeTargetVehicleCount(lineId);
                uint residents = lines[i].m_passengers.m_residentPassengers.m_averageCount;
                uint tourists = lines[i].m_passengers.m_touristPassengers.m_averageCount;

                int typeIndex = (int)info.m_transportType;
                if (typeIndex >= 0 && typeIndex < totals.Length)
                {
                    if (totals[typeIndex] == null) totals[typeIndex] = new TypeTotals();
                    TypeTotals t = totals[typeIndex];
                    t.Lines++;
                    if ((flags & TransportLine.Flags.Complete) != TransportLine.Flags.None) t.CompleteLines++;
                    t.Stops += stopCount;
                    t.Vehicles += vehicleCount;
                    if (targetVehicles > 0) t.TargetVehicles += targetVehicles;
                    t.Passengers += residents + tourists;
                }

                if (emitted >= limit)
                {
                    continue;
                }

                if (emitted > 0) lineJson.Append(",");
                AppendLine(lineJson, transport, net, lineId, info, stopCount, vehicleCount, targetVehicles, includeStops);
                emitted++;
            }

            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true");
            json.Append(",\"typeFilter\":\"").Append(JsonUtil.Escape(hasFilter ? filterType.ToString() : "")).Append("\"");
            json.Append(",\"includeStops\":").Append(JsonUtil.Bool(includeStops));
            json.Append(",\"limit\":").Append(limit);
            json.Append(",\"lineCount\":").Append(matched);
            json.Append(",\"returned\":").Append(emitted);
            json.Append(",\"cityLineCount\":").Append(transport.m_lineCount);
            json.Append(",\"passengerSource\":\"TransportLine.m_passengers.<group>.m_averageCount (the line panel's weekly passenger figure)\"");
            json.Append(",\"lines\":[").Append(lineJson.ToString()).Append("]");

            json.Append(",\"totalsByType\":{");
            bool first = true;
            for (int t = 0; t < totals.Length; t++)
            {
                if (totals[t] == null) continue;
                if (!first) json.Append(",");
                first = false;
                json.Append("\"").Append(((TransportInfo.TransportType)t).ToString()).Append("\":{");
                json.Append("\"lines\":").Append(totals[t].Lines);
                json.Append(",\"completeLines\":").Append(totals[t].CompleteLines);
                json.Append(",\"stops\":").Append(totals[t].Stops);
                json.Append(",\"vehicles\":").Append(totals[t].Vehicles);
                json.Append(",\"targetVehicles\":").Append(totals[t].TargetVehicles);
                json.Append(",\"passengersLastWeek\":").Append(totals[t].Passengers.ToString(CultureInfo.InvariantCulture));
                json.Append("}");
            }
            json.Append("}");

            AppendCityPassengers(json, transport, hasFilter, filterType);
            AppendBudgets(json);
            AppendTransportPrefabs(json, hasFilter, filterType);
            AppendFacilities(json, hasFilter, filterType, limit);

            // There is no city-wide modal split in the game data. The public transport info view
            // shows only the per-type counters appended above, so we do not invent one.
            json.Append(",\"modalSplit\":null");
            json.Append(",\"modalSplitNote\":\"CS1 does not track a public-transport share; see cityPassengersByType for the counters the Public Transport info view shows.\"");
            json.Append("}");

            return CommandResult.FromJson(json.ToString());
        }

        private static void AppendLine(StringBuilder json, TransportManager transport, NetManager net, ushort lineId, TransportInfo info,
            int stopCount, int vehicleCount, int targetVehicles, bool includeStops)
        {
            TransportLine[] lines = transport.m_lines.m_buffer;
            TransportPassengerData passengers = lines[lineId].m_passengers;

            json.Append("{\"id\":").Append(lineId);
            json.Append(",\"number\":").Append(lines[lineId].m_lineNumber);
            json.Append(",\"name\":\"").Append(JsonUtil.Escape(SafeLineName(transport, lineId))).Append("\"");
            json.Append(",\"transportType\":\"").Append(info.m_transportType.ToString()).Append("\"");
            json.Append(",\"prefab\":\"").Append(JsonUtil.Escape(info.name)).Append("\"");
            json.Append(",\"vehicleType\":\"").Append(info.m_vehicleType.ToString()).Append("\"");
            json.Append(",\"subService\":\"").Append(info.m_class == null ? "" : info.m_class.m_subService.ToString()).Append("\"");
            json.Append(",\"color\":\"").Append(ColorHex(transport.GetLineColor(lineId))).Append("\"");
            json.Append(",\"flags\":").Append(FlagNames(lines[lineId].m_flags));
            json.Append(",\"complete\":").Append(JsonUtil.Bool(lines[lineId].Complete));

            bool day;
            bool night;
            lines[lineId].GetActive(out day, out night);
            json.Append(",\"activeDay\":").Append(JsonUtil.Bool(day));
            json.Append(",\"activeNight\":").Append(JsonUtil.Bool(night));

            json.Append(",\"stopCount\":").Append(stopCount);
            json.Append(",\"lengthMeters\":").Append(JsonUtil.Number(lines[lineId].m_totalLength));
            json.Append(",\"vehicleCount\":").Append(vehicleCount);
            json.Append(",\"targetVehicleCount\":").Append(targetVehicles);
            json.Append(",\"budget\":").Append(lines[lineId].m_budget);
            json.Append(",\"ticketPrice\":").Append(lines[lineId].m_ticketPrice);
            json.Append(",\"averageInterval\":").Append(lines[lineId].m_averageInterval);
            json.Append(",\"depotBuildingId\":").Append(lines[lineId].m_building);

            json.Append(",\"passengers\":{");
            json.Append("\"residents\":").Append(passengers.m_residentPassengers.m_averageCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"tourists\":").Append(passengers.m_touristPassengers.m_averageCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"total\":").Append((passengers.m_residentPassengers.m_averageCount + passengers.m_touristPassengers.m_averageCount).ToString(CultureInfo.InvariantCulture));
            json.Append(",\"carOwning\":").Append(passengers.m_carOwningPassengers.m_averageCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"source\":\"m_averageCount\"");
            json.Append(",\"lastPeriod\":{\"residents\":").Append(passengers.m_residentPassengers.m_finalCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"tourists\":").Append(passengers.m_touristPassengers.m_finalCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"source\":\"m_finalCount\"}");
            json.Append(",\"current\":{\"residents\":").Append(passengers.m_residentPassengers.m_tempCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"tourists\":").Append(passengers.m_touristPassengers.m_tempCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"source\":\"m_tempCount\"}");
            json.Append("}");

            // Stop-node problems are where the game reports a broken line: LineNotConnected
            // (TransportLineAI, the path between stops failed) and TooLong (TransportLine).
            List<string> lineProblems = new List<string>();
            StringBuilder stops = includeStops ? new StringBuilder() : null;
            ushort firstStop = lines[lineId].m_stops;
            ushort stop = firstStop;
            int index = 0;
            int guard = 0;
            while (stop != 0)
            {
                NetNode node = net.m_nodes.m_buffer[stop];
                if (!node.m_problems.IsNone)
                {
                    string problemText = node.m_problems.ToString();
                    if (!lineProblems.Contains(problemText)) lineProblems.Add(problemText);
                }

                if (stops != null)
                {
                    if (index > 0) stops.Append(",");
                    AppendStop(stops, transport, net, lineId, stop, index, node);
                }

                index++;
                stop = TransportLine.GetNextStop(stop);
                if (stop == firstStop || ++guard >= 1024)
                {
                    break;
                }
            }

            json.Append(",\"problems\":[");
            for (int p = 0; p < lineProblems.Count; p++)
            {
                if (p > 0) json.Append(",");
                json.Append("\"").Append(JsonUtil.Escape(lineProblems[p])).Append("\"");
            }
            json.Append("]");

            if (stops != null)
            {
                json.Append(",\"stops\":[").Append(stops.ToString()).Append("]");
            }

            json.Append("}");
        }

        private static void AppendStop(StringBuilder json, TransportManager transport, NetManager net, ushort lineId, ushort stop, int index, NetNode node)
        {
            json.Append("{\"index\":").Append(index);
            json.Append(",\"nodeId\":").Append(stop);
            json.Append(",\"x\":").Append(JsonUtil.Number(node.m_position.x));
            json.Append(",\"y\":").Append(JsonUtil.Number(node.m_position.y));
            json.Append(",\"z\":").Append(JsonUtil.Number(node.m_position.z));

            int waiting;
            try
            {
                // The same call the stop list in the line info panel makes.
                waiting = transport.m_lines.m_buffer[lineId].CalculatePassengerCount(stop);
            }
            catch (Exception)
            {
                waiting = -1;
            }
            json.Append(",\"waitingPassengers\":").Append(waiting);
            json.Append(",\"nodeFinalCounter\":").Append(node.m_finalCounter);
            json.Append(",\"fixedPlatform\":").Append(JsonUtil.Bool((node.m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None));

            ushort segmentId = 0;
            ushort ownerBuilding = 0;
            if (node.m_lane != 0)
            {
                segmentId = net.m_lanes.m_buffer[node.m_lane].m_segment;
                if (segmentId != 0 && (net.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Untouchable) != NetSegment.Flags.None)
                {
                    ownerBuilding = NetSegment.FindOwnerBuilding(segmentId, 363f);
                }
            }
            json.Append(",\"laneId\":").Append(node.m_lane.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"segmentId\":").Append(segmentId);
            json.Append(",\"stationBuildingId\":").Append(ownerBuilding);
            json.Append(",\"problems\":\"").Append(node.m_problems.IsNone ? "" : JsonUtil.Escape(node.m_problems.ToString())).Append("\"");
            json.Append("}");
        }

        private static void AppendCityPassengers(StringBuilder json, TransportManager transport, bool hasFilter, TransportInfo.TransportType filterType)
        {
            TransportPassengerData[] data = transport.m_passengers;
            json.Append(",\"cityPassengersByType\":{");
            json.Append("\"source\":\"TransportManager.m_passengers[TransportType].<group>.m_averageCount (the Public Transport info view)\"");
            ulong totalResidents = 0;
            ulong totalTourists = 0;
            if (data != null)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (hasFilter && i != (int)filterType) continue;
                    uint residents = data[i].m_residentPassengers.m_averageCount;
                    uint tourists = data[i].m_touristPassengers.m_averageCount;
                    if (residents == 0 && tourists == 0) continue;
                    totalResidents += residents;
                    totalTourists += tourists;
                    json.Append(",\"").Append(((TransportInfo.TransportType)i).ToString()).Append("\":{");
                    json.Append("\"residents\":").Append(residents.ToString(CultureInfo.InvariantCulture));
                    json.Append(",\"tourists\":").Append(tourists.ToString(CultureInfo.InvariantCulture));
                    json.Append(",\"total\":").Append(((ulong)residents + tourists).ToString(CultureInfo.InvariantCulture));
                    json.Append("}");
                }
            }
            json.Append(",\"total\":{\"residents\":").Append(totalResidents.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"tourists\":").Append(totalTourists.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"total\":").Append((totalResidents + totalTourists).ToString(CultureInfo.InvariantCulture)).Append("}");
            json.Append("}");
        }

        private static void AppendBudgets(StringBuilder json)
        {
            EconomyManager economy = Singleton<EconomyManager>.instance;
            json.Append(",\"budgets\":[");
            json.Append("{\"subService\":\"None\",\"day\":").Append(economy.GetBudget(ItemClass.Service.PublicTransport, ItemClass.SubService.None, false));
            json.Append(",\"night\":").Append(economy.GetBudget(ItemClass.Service.PublicTransport, ItemClass.SubService.None, true)).Append("}");
            for (int i = 0; i < TransitSubServices.Length; i++)
            {
                ItemClass.SubService sub = TransitSubServices[i];
                json.Append(",{\"subService\":\"").Append(sub.ToString()).Append("\"");
                json.Append(",\"day\":").Append(economy.GetBudget(ItemClass.Service.PublicTransport, sub, false));
                json.Append(",\"night\":").Append(economy.GetBudget(ItemClass.Service.PublicTransport, sub, true)).Append("}");
            }
            json.Append("]");
        }

        private static void AppendTransportPrefabs(StringBuilder json, bool hasFilter, TransportInfo.TransportType filterType)
        {
            TransportManager transport = Singleton<TransportManager>.instance;
            UnlockManager unlock = Singleton<UnlockManager>.instance;
            json.Append(",\"transportPrefabs\":[");
            uint count = (uint)PrefabCollection<TransportInfo>.LoadedCount();
            bool first = true;
            for (uint i = 0; i < count; i++)
            {
                TransportInfo info = PrefabCollection<TransportInfo>.GetLoaded(i);
                if (info == null) continue;
                if (hasFilter && info.m_transportType != filterType) continue;
                if (!first) json.Append(",");
                first = false;
                json.Append("{\"name\":\"").Append(JsonUtil.Escape(info.name)).Append("\"");
                json.Append(",\"transportType\":\"").Append(info.m_transportType.ToString()).Append("\"");
                json.Append(",\"vehicleType\":\"").Append(info.m_vehicleType.ToString()).Append("\"");
                json.Append(",\"defaultForType\":").Append(JsonUtil.Bool((object)transport.GetTransportInfo(info.m_transportType) == (object)info));
                json.Append(",\"netService\":\"").Append(info.m_netService.ToString()).Append("/").Append(info.m_netSubService.ToString()).Append("\"");
                json.Append(",\"stationService\":\"").Append(info.m_stationService.ToString()).Append("/").Append(info.m_stationSubService.ToString()).Append("\"");
                json.Append(",\"unlocked\":").Append(JsonUtil.Bool(unlock == null || unlock.Unlocked(info.m_UnlockMilestone)));
                json.Append(",\"unlock\":").Append(UnlockReport.TransportUnlockJson(info));
                json.Append(",\"creatableByBridge\":").Append(JsonUtil.Bool(TransitCommands.IsCreatableType(info.m_transportType)));
                json.Append("}");
            }
            json.Append("]");
        }

        private static void AppendFacilities(StringBuilder json, bool hasFilter, TransportInfo.TransportType filterType, int limit)
        {
            BuildingManager buildings = Singleton<BuildingManager>.instance;
            Building[] buffer = buildings.m_buildings.m_buffer;
            StringBuilder items = new StringBuilder();
            Dictionary<string, int> bySubService = new Dictionary<string, int>();
            int total = 0;
            int emitted = 0;
            int facilityLimit = limit < 50 ? 500 : limit * 4;

            for (int i = 1; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Building.Flags.Created) == Building.Flags.None ||
                    (buffer[i].m_flags & Building.Flags.Deleted) != Building.Flags.None)
                {
                    continue;
                }

                BuildingInfo info = buffer[i].Info;
                if (info == null || info.m_class == null || info.m_class.m_service != ItemClass.Service.PublicTransport)
                {
                    continue;
                }

                TransportInfo primary = null;
                TransportInfo secondary = null;
                if (info.m_buildingAI != null)
                {
                    try
                    {
                        primary = info.m_buildingAI.GetTransportLineInfo();
                        secondary = info.m_buildingAI.GetSecondaryTransportLineInfo();
                    }
                    catch (Exception)
                    {
                        primary = null;
                        secondary = null;
                    }
                }

                if (hasFilter && !MatchesType(primary, filterType) && !MatchesType(secondary, filterType) &&
                    !MatchesDepotType(info.m_buildingAI, filterType))
                {
                    continue;
                }

                string sub = info.m_class.m_subService.ToString();
                int count;
                bySubService.TryGetValue(sub, out count);
                bySubService[sub] = count + 1;
                total++;

                if (emitted >= facilityLimit)
                {
                    continue;
                }

                if (emitted > 0) items.Append(",");
                emitted++;

                ushort id = (ushort)i;
                Vector3 position = buffer[i].m_position;
                Building.Flags flags = buffer[i].m_flags;
                items.Append("{\"id\":").Append(id);
                items.Append(",\"prefab\":\"").Append(JsonUtil.Escape(info.name)).Append("\"");
                items.Append(",\"subService\":\"").Append(sub).Append("\"");
                items.Append(",\"ai\":\"").Append(info.m_buildingAI == null ? "" : info.m_buildingAI.GetType().Name).Append("\"");
                items.Append(",\"lineType\":\"").Append(primary == null ? "" : primary.m_transportType.ToString()).Append("\"");
                items.Append(",\"secondaryLineType\":\"").Append(secondary == null ? "" : secondary.m_transportType.ToString()).Append("\"");
                items.Append(",\"subBuilding\":").Append(JsonUtil.Bool((flags & Building.Flags.Untouchable) != Building.Flags.None));
                items.Append(",\"active\":").Append(JsonUtil.Bool((flags & Building.Flags.Active) != Building.Flags.None));
                items.Append(",\"angleDegrees\":").Append(JsonUtil.Number(buffer[i].m_angle * Mathf.Rad2Deg));
                items.Append(",\"problems\":\"").Append(buffer[i].m_problems.IsNone ? "" : JsonUtil.Escape(buffer[i].m_problems.ToString())).Append("\"");
                items.Append(",\"position\":{\"x\":").Append(JsonUtil.Number(position.x));
                items.Append(",\"y\":").Append(JsonUtil.Number(position.y));
                items.Append(",\"z\":").Append(JsonUtil.Number(position.z)).Append("}");

                DepotAI depot = info.m_buildingAI as DepotAI;
                if (depot != null)
                {
                    items.Append(",\"maxVehicleCount\":").Append(depot.m_maxVehicleCount);
                    if (depot.m_transportInfo != null)
                    {
                        int vehicles;
                        try
                        {
                            vehicles = depot.GetVehicleCount(id, ref buffer[i]);
                        }
                        catch (Exception)
                        {
                            vehicles = -1;
                        }
                        items.Append(",\"vehicleCount\":").Append(vehicles);
                    }
                }

                TransportStationAI station = info.m_buildingAI as TransportStationAI;
                if (station != null)
                {
                    int passengersAtStation;
                    try
                    {
                        passengersAtStation = station.GetPassengerCount(id, ref buffer[i]);
                    }
                    catch (Exception)
                    {
                        passengersAtStation = -1;
                    }
                    items.Append(",\"passengerCount\":").Append(passengersAtStation);
                }

                items.Append("}");
            }

            json.Append(",\"facilityCount\":").Append(total);
            json.Append(",\"facilitiesReturned\":").Append(emitted);
            json.Append(",\"facilitiesBySubService\":{");
            bool first = true;
            foreach (KeyValuePair<string, int> pair in bySubService)
            {
                if (!first) json.Append(",");
                first = false;
                json.Append("\"").Append(JsonUtil.Escape(pair.Key)).Append("\":").Append(pair.Value);
            }
            json.Append("}");
            json.Append(",\"facilities\":[").Append(items.ToString()).Append("]");
        }

        public static CommandResult BuildPoliciesJson()
        {
            DistrictManager districts = Singleton<DistrictManager>.instance;
            UnlockManager unlock = Singleton<UnlockManager>.instance;
            Array values = Enum.GetValues(typeof(DistrictPolicies.Policies));

            StringBuilder city = new StringBuilder();
            StringBuilder available = new StringBuilder();
            bool firstCity = true;
            bool firstAvailable = true;

            foreach (object value in values)
            {
                DistrictPolicies.Policies policy = (DistrictPolicies.Policies)value;
                if (policy == DistrictPolicies.Policies.None)
                {
                    continue;
                }

                int type = (int)policy >> 5;
                if (type == (int)DistrictPolicies.Types.Park)
                {
                    continue;
                }

                bool loaded = districts.IsPolicyLoaded(policy);
                bool unlocked = unlock == null || unlock.Unlocked(policy);

                if (TransitCommands.IsSettablePolicyType(type, true) && loaded)
                {
                    if (!firstAvailable) available.Append(",");
                    firstAvailable = false;
                    available.Append("{\"name\":\"").Append(policy.ToString()).Append("\"");
                    available.Append(",\"type\":\"").Append(((DistrictPolicies.Types)type).ToString()).Append("\"");
                    available.Append(",\"cityWide\":").Append(JsonUtil.Bool(TransitCommands.IsSettablePolicyType(type, false)));
                    available.Append(",\"unlocked\":").Append(JsonUtil.Bool(unlocked)).Append("}");
                }

                if (districts.IsCityPolicySet(policy))
                {
                    if (!firstCity) city.Append(",");
                    firstCity = false;
                    city.Append("\"").Append(policy.ToString()).Append("\"");
                }
            }

            StringBuilder districtJson = new StringBuilder();
            bool firstDistrict = true;
            District[] buffer = districts.m_districts.m_buffer;
            for (int d = 1; d < buffer.Length; d++)
            {
                if ((buffer[d].m_flags & District.Flags.Created) == District.Flags.None)
                {
                    continue;
                }

                if (!firstDistrict) districtJson.Append(",");
                firstDistrict = false;
                districtJson.Append("{\"id\":").Append(d);
                districtJson.Append(",\"name\":\"").Append(JsonUtil.Escape(districts.GetDistrictName(d))).Append("\"");
                districtJson.Append(",\"policies\":[");
                bool firstPolicy = true;
                foreach (object value in values)
                {
                    DistrictPolicies.Policies policy = (DistrictPolicies.Policies)value;
                    if (policy == DistrictPolicies.Policies.None || ((int)policy >> 5) == (int)DistrictPolicies.Types.Park)
                    {
                        continue;
                    }
                    if (districts.IsDistrictPolicySet(policy, (byte)d))
                    {
                        if (!firstPolicy) districtJson.Append(",");
                        firstPolicy = false;
                        districtJson.Append("\"").Append(policy.ToString()).Append("\"");
                    }
                }
                districtJson.Append("]}");
            }

            return CommandResult.FromJson("{\"ok\":true" +
                ",\"source\":\"DistrictManager.IsCityPolicySet (district 0) and IsDistrictPolicySet\"" +
                ",\"cityPolicies\":[" + city.ToString() + "]" +
                ",\"districts\":[" + districtJson.ToString() + "]" +
                ",\"available\":[" + available.ToString() + "]}");
        }

        // ------------------------------------------------------------------ helpers

        private sealed class TypeTotals
        {
            public int Lines;
            public int CompleteLines;
            public int Stops;
            public int Vehicles;
            public int TargetVehicles;
            public ulong Passengers;
        }

        private static bool MatchesType(TransportInfo info, TransportInfo.TransportType type)
        {
            return info != null && info.m_transportType == type;
        }

        private static bool MatchesDepotType(BuildingAI ai, TransportInfo.TransportType type)
        {
            DepotAI depot = ai as DepotAI;
            if (depot == null)
            {
                return false;
            }
            return MatchesType(depot.m_transportInfo, type) || MatchesType(depot.m_secondaryTransportInfo, type);
        }

        private static int SafeTargetVehicleCount(ushort lineId)
        {
            try
            {
                return Singleton<TransportManager>.instance.m_lines.m_buffer[lineId].CalculateTargetVehicleCount();
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static string SafeLineName(TransportManager transport, ushort lineId)
        {
            try
            {
                string name = transport.GetLineName(lineId);
                return name == null ? "" : name;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static string FlagNames(TransportLine.Flags flags)
        {
            StringBuilder json = new StringBuilder("[");
            bool first = true;
            for (int i = 0; i < LineFlagBits.Length; i++)
            {
                if ((flags & LineFlagBits[i]) == TransportLine.Flags.None) continue;
                if (!first) json.Append(",");
                first = false;
                json.Append("\"").Append(LineFlagBits[i].ToString()).Append("\"");
            }
            json.Append("]");
            return json.ToString();
        }

        public static string ColorHex(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("X2", CultureInfo.InvariantCulture) +
                c.g.ToString("X2", CultureInfo.InvariantCulture) +
                c.b.ToString("X2", CultureInfo.InvariantCulture);
        }

        public static bool TryParseTransportType(string value, out TransportInfo.TransportType type)
        {
            type = TransportInfo.TransportType.Bus;
            if (value == null || value.Length == 0)
            {
                return false;
            }

            string[] names = Enum.GetNames(typeof(TransportInfo.TransportType));
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    type = (TransportInfo.TransportType)Enum.Parse(typeof(TransportInfo.TransportType), names[i]);
                    return true;
                }
            }

            return false;
        }

        public static string TransportTypeNames()
        {
            return string.Join(", ", Enum.GetNames(typeof(TransportInfo.TransportType)));
        }
    }
}
