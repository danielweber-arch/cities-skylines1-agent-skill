using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ColossalFramework;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// GET /state/segment-route-share: which vehicles' CURRENT remaining route includes one road
    /// segment at this instant, by class and by origin/destination area. Not throughput.
    /// Path walk (settled from TrainAI.ResetTargets IL): start in unit Vehicle.m_path at position
    /// index m_pathPositionIndex >> 1, step through PathUnit.m_positionCount positions, then
    /// continue at m_nextPathUnit from index 0. A walk stops when the segment is found, the chain
    /// ends, after MaxHopsPerVehicle units, or when the global path-unit budget is spent.
    /// </summary>
    public static class SegmentRouteShare
    {
        public const int DefaultLimit = 10;
        public const int MaxLimit = 50;
        public const int DefaultBudget = 150000;
        public const int MaxBudget = 1000000;
        public const float DefaultRadius = 500f;
        public const int MaxHopsPerVehicle = 64;

        private const string Meaning = "vehicles whose CURRENT remaining route includes this segment at this instant; not throughput";

        public static string Validate(int segment, float x, float z, float radius)
        {
            if (segment > 0)
            {
                return null;
            }
            if (float.IsNaN(x) || float.IsNaN(z))
            {
                return "Pass segment=<id> or x=<float>&z=<float>[&radius=<m>] (the busiest road segment in the radius is used).";
            }
            if (float.IsNaN(radius) || radius <= 0f)
            {
                return "radius must be positive.";
            }
            return null;
        }

        public static CommandResult BuildJson(int segmentParam, float x, float z, float radius, int limit, int budget)
        {
            if (limit < 1) limit = 1;
            if (limit > MaxLimit) limit = MaxLimit;
            if (budget < 1) budget = 1;
            if (budget > MaxBudget) budget = MaxBudget;

            NetManager net = Singleton<NetManager>.instance;
            NetSegment[] segments = net.m_segments.m_buffer;

            ushort target;
            if (segmentParam > 0)
            {
                if (segmentParam >= segments.Length || !IsLiveSegment(segments[segmentParam]))
                {
                    return CommandResult.Fail("Segment " + segmentParam + " does not exist.");
                }
                target = (ushort)segmentParam;
            }
            else
            {
                target = BusiestRoadSegment(segments, x, z, radius);
                if (target == 0)
                {
                    return CommandResult.Fail("No road segment within " + radius.ToString("0.#", CultureInfo.InvariantCulture) + " m of (" +
                        x.ToString("0.#", CultureInfo.InvariantCulture) + "," + z.ToString("0.#", CultureInfo.InvariantCulture) + ").");
                }
            }

            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            Vehicle[] vehicles = vehicleManager.m_vehicles.m_buffer;
            PathUnit[] units = Singleton<PathManager>.instance.m_pathUnits.m_buffer;
            BuildingManager buildingManager = Singleton<BuildingManager>.instance;
            DistrictManager districtManager = Singleton<DistrictManager>.instance;

            Dictionary<string, int> byClass = new Dictionary<string, int>();
            byClass["passengerCar"] = 0;
            byClass["cargoTruck"] = 0;
            byClass["transit"] = 0;
            byClass["service"] = 0;
            Dictionary<string, int> pairs = new Dictionary<string, int>();
            Dictionary<ushort, string> areaCache = new Dictionary<ushort, string>();

            int length = vehicles.Length;
            int offset = length > 1 ? new System.Random().Next(1, length) : 0;
            int totalActive = 0;
            int scanned = 0;
            int hopCapped = 0;
            int matched = 0;
            int outsideToOutside = 0;
            int fromOutside = 0;
            int toOutside = 0;
            long walked = 0;
            bool budgetSpent = false;

            for (int n = 0; n < length; n++)
            {
                int i = (offset + n) % length;
                if (i == 0)
                {
                    continue;
                }
                Vehicle.Flags flags = vehicles[i].m_flags;
                if ((flags & Vehicle.Flags.Created) == 0 || (flags & Vehicle.Flags.Deleted) != 0)
                {
                    continue;
                }
                // Leading vehicles only: trailers and cargo carried inside a ship or train share the leader's route.
                if (vehicles[i].m_leadingVehicle != 0 || vehicles[i].m_cargoParent != 0)
                {
                    continue;
                }
                uint path = vehicles[i].m_path;
                if (path == 0 || (flags & Vehicle.Flags.WaitingPath) != 0)
                {
                    continue;
                }
                totalActive++;
                if (budgetSpent)
                {
                    continue;
                }

                bool found = false;
                bool complete = true;
                uint unit = path;
                int index = vehicles[i].m_pathPositionIndex >> 1;
                int hops = 0;
                while (unit != 0 && unit < units.Length && hops < MaxHopsPerVehicle)
                {
                    if (walked >= budget)
                    {
                        complete = false;
                        budgetSpent = true;
                        break;
                    }
                    walked++;
                    hops++;
                    int count = units[unit].m_positionCount;
                    for (int p = index; p < count; p++)
                    {
                        PathUnit.Position position;
                        if (units[unit].GetPosition(p, out position) && position.m_segment == target)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                    unit = units[unit].m_nextPathUnit;
                    index = 0;
                }
                if (!found && complete && unit != 0 && hops >= MaxHopsPerVehicle)
                {
                    // The route goes on past the per-vehicle hop cap: this vehicle was NOT fully
                    // walked, so it must not count as scanned (the review's 65-unit path case).
                    complete = false;
                    hopCapped++;
                }
                if (!complete)
                {
                    continue;
                }
                scanned++;
                if (!found)
                {
                    continue;
                }

                matched++;
                string vehicleClass = ClassOf(vehicles[i].Info);
                int classCount;
                byClass.TryGetValue(vehicleClass, out classCount);
                byClass[vehicleClass] = classCount + 1;

                string from = AreaOf(vehicles[i].m_sourceBuilding, buildingManager, districtManager, areaCache);
                string to = AreaOf(vehicles[i].m_targetBuilding, buildingManager, districtManager, areaCache);
                bool dummy = (flags & Vehicle.Flags.DummyTraffic) != 0;
                if (dummy || (from == "outside" && to == "outside"))
                {
                    outsideToOutside++;
                }
                else if (from == "outside")
                {
                    fromOutside++;
                }
                else if (to == "outside")
                {
                    toOutside++;
                }

                string key = from + "\n" + to;
                int pairCount;
                pairs.TryGetValue(key, out pairCount);
                pairs[key] = pairCount + 1;
            }

            bool truncated = scanned < totalActive;

            List<KeyValuePair<string, int>> pairList = new List<KeyValuePair<string, int>>(pairs);
            pairList.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
            {
                int byCount = b.Value.CompareTo(a.Value);
                return byCount != 0 ? byCount : string.CompareOrdinal(a.Key, b.Key);
            });

            NetSegment seg = segments[target];
            Vector3 start = net.m_nodes.m_buffer[seg.m_startNode].m_position;
            Vector3 end = net.m_nodes.m_buffer[seg.m_endNode].m_position;
            NetInfo info = seg.Info;

            StringBuilder json = new StringBuilder(1024);
            json.Append("{\"ok\":true,\"segment\":{\"id\":").Append(target);
            json.Append(",\"prefab\":\"").Append(JsonUtil.Escape(info == null ? "" : info.name)).Append("\"");
            json.Append(",\"density\":").Append(seg.m_trafficDensity);
            json.Append(",\"start\":").Append(Point(start));
            json.Append(",\"end\":").Append(Point(end)).Append("}");
            json.Append(",\"meaning\":\"").Append(Meaning).Append("\"");
            json.Append(",\"scanned\":").Append(scanned);
            json.Append(",\"hopCapped\":").Append(hopCapped);
            json.Append(",\"totalActive\":").Append(totalActive);
            json.Append(",\"truncated\":").Append(JsonUtil.Bool(truncated));
            json.Append(",\"sample\":").Append(JsonUtil.Bool(truncated));
            json.Append(",\"pathUnitsWalked\":").Append(walked);
            json.Append(",\"budget\":").Append(budget);
            json.Append(",\"matched\":").Append(matched);
            json.Append(",\"byClass\":{");
            json.Append("\"passengerCar\":").Append(byClass["passengerCar"]);
            json.Append(",\"cargoTruck\":").Append(byClass["cargoTruck"]);
            json.Append(",\"transit\":").Append(byClass["transit"]);
            json.Append(",\"service\":").Append(byClass["service"]);
            List<string> otherKeys = new List<string>();
            foreach (KeyValuePair<string, int> entry in byClass)
            {
                if (entry.Key.StartsWith("other(", StringComparison.Ordinal))
                {
                    otherKeys.Add(entry.Key);
                }
            }
            otherKeys.Sort(StringComparer.Ordinal);
            for (int k = 0; k < otherKeys.Count; k++)
            {
                json.Append(",\"").Append(JsonUtil.Escape(otherKeys[k])).Append("\":").Append(byClass[otherKeys[k]]);
            }
            json.Append("}");
            json.Append(",\"outsideToOutside\":").Append(outsideToOutside);
            json.Append(",\"fromOutside\":").Append(fromOutside);
            json.Append(",\"toOutside\":").Append(toOutside);
            json.Append(",\"topPairs\":[");
            int emitted = Mathf.Min(limit, pairList.Count);
            for (int k = 0; k < emitted; k++)
            {
                string pairKey = pairList[k].Key;
                int split = pairKey.IndexOf('\n');
                if (k > 0) json.Append(",");
                json.Append("{\"from\":\"").Append(JsonUtil.Escape(pairKey.Substring(0, split)));
                json.Append("\",\"to\":\"").Append(JsonUtil.Escape(pairKey.Substring(split + 1)));
                json.Append("\",\"count\":").Append(pairList[k].Value).Append("}");
            }
            json.Append("]");
            json.Append(",\"pairsTotal\":").Append(pairList.Count);
            json.Append(",\"pairsTruncated\":").Append(pairList.Count - emitted);
            json.Append("}");
            return CommandResult.FromJson(json.ToString());
        }

        private static bool IsLiveSegment(NetSegment segment)
        {
            NetSegment.Flags flags = segment.m_flags;
            return (flags & NetSegment.Flags.Created) != NetSegment.Flags.None &&
                (flags & (NetSegment.Flags.Deleted | NetSegment.Flags.Collapsed)) == NetSegment.Flags.None;
        }

        /// <summary>Highest m_trafficDensity RoadBaseAI segment whose middle is in the radius; ties to the lowest id; 0 when none.</summary>
        private static ushort BusiestRoadSegment(NetSegment[] segments, float x, float z, float radius)
        {
            ushort best = 0;
            int bestDensity = -1;
            float radiusSq = radius * radius;
            for (int i = 1; i < segments.Length; i++)
            {
                if (!IsLiveSegment(segments[i]))
                {
                    continue;
                }
                NetInfo info = segments[i].Info;
                if (info == null || !(info.m_netAI is RoadBaseAI))
                {
                    continue;
                }
                Vector3 middle = segments[i].m_middlePosition;
                float dx = middle.x - x;
                float dz = middle.z - z;
                if (dx * dx + dz * dz > radiusSq)
                {
                    continue;
                }
                int density = segments[i].m_trafficDensity;
                if (density > bestDensity)
                {
                    bestDensity = density;
                    best = (ushort)i;
                }
            }
            return best;
        }

        private static string ClassOf(VehicleInfo info)
        {
            if (info == null || info.m_vehicleAI == null)
            {
                return "other(none)";
            }
            VehicleAI ai = info.m_vehicleAI;
            if (ai is PassengerCarAI) return "passengerCar";
            if (ai is CargoTruckAI) return "cargoTruck";
            if (ai is BusAI || ai is TramAI || ai is PassengerTrainAI || ai is TrolleybusAI ||
                ai is PassengerFerryAI || ai is PassengerShipAI || ai is PassengerPlaneAI || ai is PassengerBlimpAI ||
                ai is PassengerHelicopterAI || ai is CableCarAI || ai is TaxiAI)
            {
                // PassengerTrainAI covers MetroTrainAI (a subclass) and the monorail (no separate AI class).
                return "transit";
            }
            if (ai is FireTruckAI || ai is FireCopterAI || ai is PoliceCarAI || ai is PoliceCopterAI ||
                ai is AmbulanceAI || ai is AmbulanceCopterAI || ai is HearseAI || ai is GarbageTruckAI ||
                ai is MaintenanceTruckAI || ai is PostVanAI || ai is SnowTruckAI || ai is DisasterResponseVehicleAI ||
                ai is DisasterResponseCopterAI || ai is ParkMaintenanceVehicleAI || ai is WaterTruckAI || ai is BankVanAI)
            {
                return "service";
            }
            return "other(" + ai.GetType().Name + ")";
        }

        private static string AreaOf(ushort buildingId, BuildingManager buildings, DistrictManager districts, Dictionary<ushort, string> cache)
        {
            if (buildingId == 0)
            {
                return "unknown";
            }
            string area;
            if (cache.TryGetValue(buildingId, out area))
            {
                return area;
            }
            Building building = buildings.m_buildings.m_buffer[buildingId];
            BuildingInfo info = building.Info;
            if (info != null && info.m_buildingAI is OutsideConnectionAI)
            {
                area = "outside";
            }
            else
            {
                byte district = districts.GetDistrict(building.m_position);
                if (district == 0)
                {
                    area = "none";
                }
                else
                {
                    string name = districts.GetDistrictName(district);
                    area = (name == null || name.Length == 0) ? "district " + district : name;
                }
            }
            cache[buildingId] = area;
            return area;
        }

        private static string Point(Vector3 p)
        {
            return "{\"x\":" + p.x.ToString("0.0", CultureInfo.InvariantCulture) + ",\"z\":" + p.z.ToString("0.0", CultureInfo.InvariantCulture) + "}";
        }
    }
}
