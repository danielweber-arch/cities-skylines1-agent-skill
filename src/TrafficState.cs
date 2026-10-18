using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ColossalFramework;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Road congestion, read from NetSegment.m_trafficDensity: the 0..100 value RoadBaseAI
    /// maintains (it moves by at most 5 per step toward trafficBuffer*100/capacity) and that the
    /// Traffic info view colours roads by. The city-wide "traffic flow" percentage is
    /// VehicleManager.m_lastTrafficFlow, which is exactly what TrafficInfoViewPanel displays.
    /// </summary>
    public static class TrafficState
    {
        private struct Row
        {
            public ushort Id;
            public int Density;
        }

        public static CommandResult BuildTrafficJson(int limit, int minDensity, bool hasArea, float centerX, float centerZ, float radius)
        {
            if (limit < 1) limit = 1;
            if (limit > 2000) limit = 2000;
            if (minDensity < 0) minDensity = 0;
            if (minDensity > 100) minDensity = 100;
            if (hasArea && radius <= 0f)
            {
                return CommandResult.Fail("radius must be positive when x/z are given.");
            }

            NetManager net = Singleton<NetManager>.instance;
            NetSegment[] segments = net.m_segments.m_buffer;
            List<Row> rows = new List<Row>();
            long densitySum = 0;
            double weightedSum = 0;
            double lengthSum = 0;
            int roadSegments = 0;
            int[] histogram = new int[5];

            for (int i = 1; i < segments.Length; i++)
            {
                NetSegment.Flags flags = segments[i].m_flags;
                if ((flags & NetSegment.Flags.Created) == NetSegment.Flags.None ||
                    (flags & (NetSegment.Flags.Deleted | NetSegment.Flags.Collapsed)) != NetSegment.Flags.None)
                {
                    continue;
                }

                NetInfo info = segments[i].Info;
                if (info == null || !(info.m_netAI is RoadBaseAI))
                {
                    // Only RoadBaseAI updates m_trafficDensity; on other networks it is stale.
                    continue;
                }

                if (hasArea)
                {
                    Vector3 middle = segments[i].m_middlePosition;
                    float dx = middle.x - centerX;
                    float dz = middle.z - centerZ;
                    if (dx * dx + dz * dz > radius * radius)
                    {
                        continue;
                    }
                }

                int density = segments[i].m_trafficDensity;
                float length = segments[i].m_averageLength;
                roadSegments++;
                densitySum += density;
                weightedSum += (double)density * length;
                lengthSum += length;
                histogram[Mathf.Min(density / 20, 4)]++;

                if (density >= minDensity)
                {
                    Row row = new Row();
                    row.Id = (ushort)i;
                    row.Density = density;
                    rows.Add(row);
                }
            }

            rows.Sort(delegate(Row a, Row b)
            {
                int byDensity = b.Density.CompareTo(a.Density);
                return byDensity != 0 ? byDensity : a.Id.CompareTo(b.Id);
            });

            Dictionary<NetInfo, string> laneCache = new Dictionary<NetInfo, string>();
            StringBuilder items = new StringBuilder();
            int emitted = 0;
            for (int r = 0; r < rows.Count && emitted < limit; r++)
            {
                ushort id = rows[r].Id;
                NetInfo info = segments[id].Info;
                if (emitted > 0) items.Append(",");
                emitted++;

                Vector3 start = net.m_nodes.m_buffer[segments[id].m_startNode].m_position;
                Vector3 end = net.m_nodes.m_buffer[segments[id].m_endNode].m_position;
                Vector3 middle = segments[id].m_middlePosition;

                string segmentName = "";
                try
                {
                    segmentName = net.GetSegmentName(id);
                }
                catch (Exception)
                {
                    segmentName = "";
                }

                items.Append("{\"id\":").Append(id);
                items.Append(",\"prefab\":\"").Append(JsonUtil.Escape(info.name)).Append("\"");
                items.Append(",\"name\":\"").Append(JsonUtil.Escape(segmentName)).Append("\"");
                items.Append(",\"density\":").Append(rows[r].Density);
                items.Append(",\"trafficBuffer\":").Append(segments[id].m_trafficBuffer);
                items.Append(",\"lengthMeters\":").Append(JsonUtil.Number(segments[id].m_averageLength));
                items.Append(",\"start\":").Append(Point(start));
                items.Append(",\"end\":").Append(Point(end));
                items.Append(",\"middle\":").Append(Point(middle));

                string lanes;
                if (!laneCache.TryGetValue(info, out lanes))
                {
                    lanes = LaneSummary(info);
                    laneCache[info] = lanes;
                }
                items.Append(",\"lanes\":").Append(lanes);
                items.Append("}");
            }

            float average = roadSegments == 0 ? 0f : (float)densitySum / roadSegments;
            float weighted = lengthSum <= 0 ? 0f : (float)(weightedSum / lengthSum);
            uint trafficFlow = Singleton<VehicleManager>.instance.m_lastTrafficFlow;

            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true");
            json.Append(",\"source\":\"NetSegment.m_trafficDensity (0..100, RoadBaseAI segments only)\"");
            json.Append(",\"limit\":").Append(limit);
            json.Append(",\"minDensity\":").Append(minDensity);
            if (hasArea)
            {
                json.Append(",\"area\":{\"x\":").Append(JsonUtil.Number(centerX));
                json.Append(",\"z\":").Append(JsonUtil.Number(centerZ));
                json.Append(",\"radius\":").Append(JsonUtil.Number(radius)).Append("}");
            }
            json.Append(",\"roadSegments\":").Append(roadSegments);
            json.Append(",\"matching\":").Append(rows.Count);
            json.Append(",\"returned\":").Append(emitted);
            json.Append(",\"averageDensity\":").Append(JsonUtil.Number(average));
            json.Append(",\"lengthWeightedAverageDensity\":").Append(JsonUtil.Number(weighted));
            json.Append(",\"densityHistogram\":{\"0-19\":").Append(histogram[0]);
            json.Append(",\"20-39\":").Append(histogram[1]);
            json.Append(",\"40-59\":").Append(histogram[2]);
            json.Append(",\"60-79\":").Append(histogram[3]);
            json.Append(",\"80-100\":").Append(histogram[4]).Append("}");
            json.Append(",\"trafficFlowPercent\":").Append(trafficFlow.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"trafficFlowSource\":\"VehicleManager.m_lastTrafficFlow (the Traffic info view's average traffic flow; city-wide, ignores x/z/radius)\"");
            json.Append(",\"segments\":[").Append(items.ToString()).Append("]}");
            return CommandResult.FromJson(json.ToString());
        }

        /// <summary>Lane make-up of a road prefab, from NetInfo.m_lanes.</summary>
        private static string LaneSummary(NetInfo info)
        {
            int vehicle = 0;
            int busLanes = 0;
            int pedestrian = 0;
            int parking = 0;
            int tram = 0;
            int trolleybus = 0;
            int stopLanes = 0;
            if (info.m_lanes != null)
            {
                for (int i = 0; i < info.m_lanes.Length; i++)
                {
                    NetInfo.Lane lane = info.m_lanes[i];
                    if (lane == null) continue;
                    if ((lane.m_laneType & NetInfo.LaneType.Pedestrian) != NetInfo.LaneType.None) pedestrian++;
                    if ((lane.m_laneType & NetInfo.LaneType.Parking) != NetInfo.LaneType.None) parking++;
                    if ((lane.m_laneType & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None &&
                        (lane.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
                    {
                        // Bus lanes are car lanes with LaneType.TransportVehicle instead of Vehicle.
                        busLanes++;
                    }
                    if ((lane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None)
                    {
                        if ((lane.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) vehicle++;
                        if ((lane.m_vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None) tram++;
                        if ((lane.m_vehicleType & VehicleInfo.VehicleType.Trolleybus) != VehicleInfo.VehicleType.None) trolleybus++;
                    }
                    if (lane.m_stopType != VehicleInfo.VehicleType.None) stopLanes++;
                }
            }

            return "{\"carLanes\":" + vehicle +
                ",\"busLanes\":" + busLanes +
                ",\"tramLanes\":" + tram +
                ",\"trolleybusLanes\":" + trolleybus +
                ",\"pedestrianLanes\":" + pedestrian +
                ",\"parkingLanes\":" + parking +
                ",\"lanesWithStops\":" + stopLanes + "}";
        }

        private static string Point(Vector3 p)
        {
            return "{\"x\":" + JsonUtil.Number(p.x) + ",\"y\":" + JsonUtil.Number(p.y) + ",\"z\":" + JsonUtil.Number(p.z) + "}";
        }
    }
}
