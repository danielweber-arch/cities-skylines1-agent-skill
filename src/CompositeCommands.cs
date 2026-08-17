using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Multi-step operations that would otherwise cost the agent dozens of round trips and a
    /// lot of coordinate arithmetic. Each one is queued onto the game thread by ApiServer, is
    /// bounded, rolls back what it created on failure, and is idempotent by opId.
    /// </summary>
    public static class CompositeCommands
    {
        /// <summary>
        /// CS1 zoning cells are 8m and zoneable depth is 4 cells (32m) per side, so 80m of
        /// spacing gives full-depth zoning on both sides of a road with 16m left for the road
        /// itself. Wider spacing leaves an unzoneable dead strip down the middle of every block.
        /// </summary>
        public const float DefaultSpacing = 80f;

        private const float MinSpacing = 32f;
        private const float MaxSpacing = 256f;
        private const int MaxCells = 400;

        // Zoning every block is the expensive half, so a neighborhood is capped lower than a
        // bare grid. 144 blocks of SetZone already holds the game thread for a few seconds.
        private const int MaxNeighborhoodCells = 144;

        // ---------------------------------------------------------------- build-grid

        public static CommandResult BuildGrid(string body)
        {
            string opId = JsonUtil.GetString(body, "opId", "");
            string cached;
            if (OpCache.TryGet(opId, out cached))
            {
                return CommandResult.FromJson(cached);
            }

            GridRequest request;
            string error = GridRequest.Parse(body, out request);
            if (error != null)
            {
                return CommandResult.Fail(error);
            }

            if (request.DryRun)
            {
                return CommandResult.FromJson(DescribeGridPlan(request));
            }

            GridResult grid;
            try
            {
                grid = BuildLattice(request);
            }
            catch (Exception ex)
            {
                return CommandResult.Fail(Describe(ex));
            }

            string json = GridJson(request, grid, null);
            OpCache.Store(opId, json);
            return CommandResult.FromJson(json);
        }

        // ------------------------------------------------------------------- connect

        public static CommandResult Connect(string body)
        {
            string opId = JsonUtil.GetString(body, "opId", "");
            string cached;
            if (OpCache.TryGet(opId, out cached))
            {
                return CommandResult.FromJson(cached);
            }

            string prefabName = JsonUtil.GetString(body, "roadPrefab", "Basic Road");
            string serviceName = JsonUtil.GetString(body, "toService", "Road");
            float maxDistance = JsonUtil.GetNumber(body, "maxDistance", 200f);
            float snapDistance = NodeHelper.ClampSnapDistance(JsonUtil.GetNumber(body, "snapDistance", NodeHelper.DefaultSnapDistance));
            bool dryRun = JsonUtil.GetBool(body, "dryRun", false);
            Vector3 from = ReadPoint(body, "from");

            NetInfo prefab = PrefabCollection<NetInfo>.FindLoaded(prefabName);
            if (prefab == null)
            {
                return CommandResult.Fail("Network prefab was not found: " + prefabName + ". List valid names with GET /prefabs/roads.");
            }

            ItemClass.Service service;
            if (!TryParseService(serviceName, out service))
            {
                return CommandResult.Fail("Unsupported toService: " + serviceName);
            }

            if (maxDistance <= 0f || maxDistance > 1000f)
            {
                return CommandResult.Fail("maxDistance must be between 0 and 1000.");
            }

            ushort target = NodeHelper.FindNearestNodeOfService(from, maxDistance, service);
            if (target == 0)
            {
                return CommandResult.Fail("No " + serviceName + " node was found within " +
                    JsonUtil.Number(maxDistance) + "m of the given point. Build a road nearer first, or raise maxDistance.");
            }

            Vector3 targetPosition = NetManager.instance.m_nodes.m_buffer[target].m_position;
            float distance = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(targetPosition.x, 0f, targetPosition.z));

            if (dryRun)
            {
                return CommandResult.FromJson("{\"ok\":true,\"dryRun\":true" +
                    ",\"targetNodeId\":" + target +
                    ",\"targetPosition\":" + PointJson(targetPosition) +
                    ",\"distance\":" + JsonUtil.Number(distance) + "}");
            }

            List<ushort> createdNodes = new List<ushort>();
            List<ushort> createdSegments = new List<ushort>();

            try
            {
                bool created;
                ushort fromNode = NodeHelper.FindOrCreateNode(from, prefab, snapDistance, out created);
                if (created)
                {
                    createdNodes.Add(fromNode);
                }

                if (fromNode == target)
                {
                    string already = "{\"ok\":true,\"dryRun\":false,\"alreadyConnected\":true" +
                        ",\"nodeId\":" + fromNode +
                        ",\"segmentIds\":[]" +
                        ",\"distance\":" + JsonUtil.Number(distance) + "}";
                    OpCache.Store(opId, already);
                    return CommandResult.FromJson(already);
                }

                ushort segment = NodeHelper.CreateSegment(fromNode, target, prefab, JsonUtil.GetString(body, "name", ""));
                if (segment != 0)
                {
                    createdSegments.Add(segment);
                }

                string json = "{\"ok\":true,\"dryRun\":false,\"alreadyConnected\":" + JsonUtil.Bool(segment == 0) +
                    ",\"nodeId\":" + fromNode +
                    ",\"targetNodeId\":" + target +
                    ",\"targetPosition\":" + PointJson(targetPosition) +
                    ",\"segmentIds\":" + IdArray(createdSegments) +
                    ",\"createdNodeIds\":" + IdArray(createdNodes) +
                    ",\"distance\":" + JsonUtil.Number(distance) + "}";

                OpCache.Store(opId, json);
                return CommandResult.FromJson(json);
            }
            catch (Exception ex)
            {
                NodeHelper.Rollback(createdSegments, createdNodes);
                return CommandResult.Fail(Describe(ex) + " (rolled back " + createdSegments.Count + " segments, " + createdNodes.Count + " nodes)");
            }
        }

        // -------------------------------------------------------- build-neighborhood

        public static CommandResult BuildNeighborhood(string body)
        {
            string opId = JsonUtil.GetString(body, "opId", "");
            string cached;
            if (OpCache.TryGet(opId, out cached))
            {
                return CommandResult.FromJson(cached);
            }

            GridRequest request;
            string error = GridRequest.ParseNeighborhood(body, out request);
            if (error != null)
            {
                return CommandResult.Fail(error);
            }

            List<ZoneShare> mix;
            error = ParseZoneMix(body, out mix);
            if (error != null)
            {
                return CommandResult.Fail(error);
            }

            string placement = JsonUtil.GetString(body, "commercialPlacement", "perimeter");
            if (placement != "perimeter" && placement != "core" && placement != "corners")
            {
                return CommandResult.Fail("commercialPlacement must be perimeter, core, or corners.");
            }

            bool hasConnectTo = HasPoint(body, "connectTo");
            Vector3 connectTo = ReadPoint(body, "connectTo");
            float connectMaxDistance = JsonUtil.GetNumber(body, "connectMaxDistance", 400f);

            if (request.DryRun)
            {
                return CommandResult.FromJson(DescribeGridPlan(request));
            }

            GridResult grid;
            try
            {
                grid = BuildLattice(request);
            }
            catch (Exception ex)
            {
                return CommandResult.Fail(Describe(ex));
            }

            // Step 2: join the lattice to the existing network before zoning, so a failure
            // here still leaves a rollback-able grid rather than a zoned orphan island.
            StringBuilder connection = new StringBuilder();
            if (hasConnectTo)
            {
                try
                {
                    ConnectLattice(request, grid, connectTo, connectMaxDistance, connection);
                }
                catch (Exception ex)
                {
                    NodeHelper.Rollback(grid.Segments, grid.CreatedNodes);
                    return CommandResult.Fail("Grid built but connection failed, so the grid was rolled back: " + Describe(ex));
                }
            }

            // Step 3: zoning. Zone failures are reported per block rather than rolled back —
            // repainting is cheap and non-destructive, bulldozing a built grid is not.
            string zoning = ApplyZoneMix(request, grid, mix, placement, body);

            string json = GridJson(request, grid,
                ",\"connection\":" + (connection.Length == 0 ? "null" : connection.ToString()) +
                ",\"zoning\":" + zoning);

            OpCache.Store(opId, json);
            return CommandResult.FromJson(json);
        }

        // ---------------------------------------------------------------- the lattice

        private sealed class GridResult
        {
            public ushort[,] Nodes;
            public List<ushort> AllNodes = new List<ushort>();
            public List<ushort> CreatedNodes = new List<ushort>();
            public List<ushort> Segments = new List<ushort>();
            public int ReusedNodes;
            public int SkippedSegments;
        }

        /// <summary>
        /// Builds every node first, then every segment. Interleaving them means a segment can
        /// be created against a node that a later snap would have merged away.
        /// </summary>
        private static GridResult BuildLattice(GridRequest request)
        {
            GridResult result = new GridResult();
            result.Nodes = new ushort[request.Cols + 1, request.Rows + 1];

            try
            {
                for (int i = 0; i <= request.Cols; i++)
                {
                    for (int j = 0; j <= request.Rows; j++)
                    {
                        Vector3 world = request.LatticePoint(i, j);
                        bool created;
                        ushort node = NodeHelper.FindOrCreateNode(world, request.Prefab, request.SnapDistance, out created);
                        result.Nodes[i, j] = node;
                        result.AllNodes.Add(node);
                        if (created)
                        {
                            result.CreatedNodes.Add(node);
                        }
                        else
                        {
                            result.ReusedNodes++;
                        }
                    }
                }

                for (int i = 0; i <= request.Cols; i++)
                {
                    for (int j = 0; j <= request.Rows; j++)
                    {
                        if (i < request.Cols)
                        {
                            AddSegment(result, result.Nodes[i, j], result.Nodes[i + 1, j], request);
                        }
                        if (j < request.Rows)
                        {
                            AddSegment(result, result.Nodes[i, j], result.Nodes[i, j + 1], request);
                        }
                    }
                }
            }
            catch (Exception)
            {
                NodeHelper.Rollback(result.Segments, result.CreatedNodes);
                throw;
            }

            return result;
        }

        private static void AddSegment(GridResult result, ushort a, ushort b, GridRequest request)
        {
            ushort segment = NodeHelper.CreateSegment(a, b, request.Prefab, request.Name);
            if (segment != 0)
            {
                result.Segments.Add(segment);
            }
            else
            {
                result.SkippedSegments++;
            }
        }

        private static void ConnectLattice(GridRequest request, GridResult grid, Vector3 connectTo, float maxDistance, StringBuilder json)
        {
            ushort target = NodeHelper.FindNearestNodeOfService(connectTo, maxDistance, ItemClass.Service.Road);
            if (target == 0)
            {
                throw new BridgeException("No existing road was found within " + JsonUtil.Number(maxDistance) +
                    "m of connectTo. The neighborhood would have been an orphan island.");
            }

            Vector3 targetPosition = NetManager.instance.m_nodes.m_buffer[target].m_position;

            // Pick the lattice node closest to the existing road, not to connectTo itself —
            // that is the shortest possible link and the least likely to cross other geometry.
            ushort nearest = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < grid.AllNodes.Count; i++)
            {
                ushort candidate = grid.AllNodes[i];
                if (candidate == 0 || candidate == target)
                {
                    continue;
                }
                Vector3 delta = NetManager.instance.m_nodes.m_buffer[candidate].m_position - targetPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < bestSq)
                {
                    bestSq = delta.sqrMagnitude;
                    nearest = candidate;
                }
            }

            if (nearest == 0)
            {
                throw new BridgeException("The grid produced no node that could be connected.");
            }

            ushort segment = NodeHelper.CreateSegment(nearest, target, request.Prefab, request.Name);
            if (segment != 0)
            {
                grid.Segments.Add(segment);
            }

            json.Append("{\"fromNodeId\":").Append(nearest)
                .Append(",\"toNodeId\":").Append(target)
                .Append(",\"toPosition\":").Append(PointJson(targetPosition))
                .Append(",\"segmentId\":").Append(segment)
                .Append(",\"alreadyConnected\":").Append(JsonUtil.Bool(segment == 0))
                .Append(",\"length\":").Append(JsonUtil.Number(Mathf.Sqrt(bestSq)))
                .Append("}");
        }

        // ------------------------------------------------------------------- zoning

        private sealed class ZoneShare
        {
            public string Name;
            public float Weight;
        }

        private static string ParseZoneMix(string body, out List<ZoneShare> mix)
        {
            mix = new List<ZoneShare>();

            string[] known = new string[]
            {
                "ResidentialLow", "ResidentialHigh", "CommercialLow", "CommercialHigh",
                "Industrial", "Office", "Unzoned"
            };

            int start = body == null ? -1 : body.IndexOf("\"zoneMix\"");
            if (start < 0)
            {
                return "zoneMix is required, e.g. {\"ResidentialLow\":0.7,\"CommercialLow\":0.3}.";
            }

            int brace = body.IndexOf('{', start);
            if (brace < 0)
            {
                return "zoneMix must be an object of zone name to weight.";
            }
            int close = body.IndexOf('}', brace);
            if (close < 0)
            {
                return "zoneMix object is not terminated.";
            }

            string inner = body.Substring(brace, close - brace + 1);
            float total = 0f;

            for (int i = 0; i < known.Length; i++)
            {
                float weight = JsonUtil.GetNumber(inner, known[i], -1f);
                if (weight >= 0f)
                {
                    if (weight > 0f)
                    {
                        ZoneShare share = new ZoneShare();
                        share.Name = known[i];
                        share.Weight = weight;
                        mix.Add(share);
                        total += weight;
                    }
                }
            }

            if (mix.Count == 0)
            {
                return "zoneMix contained no recognised zone. Valid zones: " + string.Join(", ", known) + ". " +
                    "Note that CS1 has no plain \"Commercial\" or \"Residential\" zone — use CommercialLow/CommercialHigh/ResidentialLow/ResidentialHigh.";
            }

            if (total <= 0f)
            {
                return "zoneMix weights must add up to more than zero.";
            }

            for (int i = 0; i < mix.Count; i++)
            {
                mix[i].Weight = mix[i].Weight / total;
            }

            mix.Sort(delegate(ZoneShare a, ZoneShare b) { return b.Weight.CompareTo(a.Weight); });
            return null;
        }

        private static string ApplyZoneMix(GridRequest request, GridResult grid, List<ZoneShare> mix, string placement, string body)
        {
            bool preserveOccupied = JsonUtil.GetBool(body, "preserveOccupied", true);
            float zoneRadius = JsonUtil.GetNumber(body, "zoneRadius", request.Spacing * 0.5f);

            int blockCount = request.Cols * request.Rows;
            List<int> order = OrderBlocks(request, placement);
            string[] assignment = new string[blockCount];

            // The heaviest share is the background; the rest are placed first, into whichever
            // blocks the placement rule favours.
            int cursor = 0;
            for (int i = 1; i < mix.Count; i++)
            {
                int count = Mathf.RoundToInt(mix[i].Weight * blockCount);
                for (int n = 0; n < count && cursor < order.Count; n++, cursor++)
                {
                    assignment[order[cursor]] = mix[i].Name;
                }
            }
            for (int i = 0; i < blockCount; i++)
            {
                if (assignment[i] == null)
                {
                    assignment[i] = mix[0].Name;
                }
            }

            StringBuilder json = new StringBuilder();
            json.Append("{\"zoneRadius\":").Append(JsonUtil.Number(zoneRadius));
            json.Append(",\"preserveOccupied\":").Append(JsonUtil.Bool(preserveOccupied));
            json.Append(",\"blocks\":[");

            int changedCellsTotal = 0;
            int failures = 0;

            for (int index = 0; index < blockCount; index++)
            {
                int col = index % request.Cols;
                int row = index / request.Cols;
                Vector3 center = request.BlockCenter(col, row);

                string zoneBody = "{\"zone\":\"" + assignment[index] + "\"" +
                    ",\"preserveOccupied\":" + JsonUtil.Bool(preserveOccupied) +
                    ",\"radius\":" + JsonUtil.Number(zoneRadius) +
                    ",\"center\":" + PointJson(center) + "}";

                CommandResult result;
                try
                {
                    result = ZoneCommands.SetZone(zoneBody);
                }
                catch (Exception ex)
                {
                    result = CommandResult.Fail(Describe(ex));
                }

                int changed = (int)JsonUtil.GetNumber(result.Json, "changedCells", 0f);
                changedCellsTotal += changed;
                if (!result.Ok)
                {
                    failures++;
                }

                if (index > 0)
                {
                    json.Append(",");
                }
                json.Append("{\"index\":").Append(index)
                    .Append(",\"col\":").Append(col)
                    .Append(",\"row\":").Append(row)
                    .Append(",\"center\":").Append(PointJson(center))
                    .Append(",\"zone\":\"").Append(assignment[index]).Append("\"")
                    .Append(",\"ok\":").Append(JsonUtil.Bool(result.Ok))
                    .Append(",\"changedCells\":").Append(changed)
                    .Append("}");
            }

            json.Append("],\"changedCells\":").Append(changedCellsTotal);
            json.Append(",\"failedBlocks\":").Append(failures);
            json.Append("}");
            return json.ToString();
        }

        /// <summary>
        /// Block indices ordered by how strongly the placement rule prefers them for the
        /// minority zones. Deterministic: the same request always paints the same blocks.
        /// </summary>
        private static List<int> OrderBlocks(GridRequest request, string placement)
        {
            List<int> corners = new List<int>();
            List<int> perimeter = new List<int>();
            List<int> interior = new List<int>();

            for (int row = 0; row < request.Rows; row++)
            {
                for (int col = 0; col < request.Cols; col++)
                {
                    int index = row * request.Cols + col;
                    bool edgeCol = col == 0 || col == request.Cols - 1;
                    bool edgeRow = row == 0 || row == request.Rows - 1;

                    if (edgeCol && edgeRow)
                    {
                        corners.Add(index);
                    }
                    else if (edgeCol || edgeRow)
                    {
                        perimeter.Add(index);
                    }
                    else
                    {
                        interior.Add(index);
                    }
                }
            }

            List<int> order = new List<int>();
            if (placement == "corners")
            {
                order.AddRange(corners);
                order.AddRange(perimeter);
                order.AddRange(interior);
            }
            else if (placement == "core")
            {
                order.AddRange(interior);
                order.AddRange(perimeter);
                order.AddRange(corners);
            }
            else
            {
                order.AddRange(perimeter);
                order.AddRange(corners);
                order.AddRange(interior);
            }
            return order;
        }

        // ------------------------------------------------------------------ request

        private sealed class GridRequest
        {
            public NetInfo Prefab;
            public string PrefabName;
            public string Name;
            public Vector3 Origin;
            public int Cols;
            public int Rows;
            public float Spacing;
            public float RotationDegrees;
            public float SnapDistance;
            public bool DryRun;

            private Quaternion rotation;

            public void Init()
            {
                rotation = Quaternion.Euler(0f, RotationDegrees, 0f);
            }

            public Vector3 LatticePoint(int i, int j)
            {
                return Origin + rotation * new Vector3(i * Spacing, 0f, j * Spacing);
            }

            public Vector3 BlockCenter(int col, int row)
            {
                return Origin + rotation * new Vector3((col + 0.5f) * Spacing, 0f, (row + 0.5f) * Spacing);
            }

            public static string Parse(string body, out GridRequest request)
            {
                request = null;
                GridRequest r = new GridRequest();

                r.PrefabName = JsonUtil.GetString(body, "roadPrefab", "Basic Road");
                r.Name = JsonUtil.GetString(body, "name", "");
                r.Cols = (int)JsonUtil.GetNumber(body, "cols", 4f);
                r.Rows = (int)JsonUtil.GetNumber(body, "rows", 4f);
                r.Spacing = JsonUtil.GetNumber(body, "spacing", DefaultSpacing);
                r.RotationDegrees = JsonUtil.GetNumber(body, "rotationDegrees", 0f);
                r.SnapDistance = NodeHelper.ClampSnapDistance(JsonUtil.GetNumber(body, "snapDistance", NodeHelper.DefaultSnapDistance));
                r.DryRun = JsonUtil.GetBool(body, "dryRun", false);
                r.Origin = ReadPoint(body, "origin");

                string error = r.Validate(MaxCells);
                if (error != null)
                {
                    return error;
                }

                r.Init();
                request = r;
                return null;
            }

            public static string ParseNeighborhood(string body, out GridRequest request)
            {
                request = null;
                GridRequest r = new GridRequest();

                r.PrefabName = JsonUtil.GetString(body, "roadPrefab", "Basic Road");
                r.Name = JsonUtil.GetString(body, "name", "");

                // radiusOrCols is the single-number form; cols/rows override it when present.
                int square = (int)JsonUtil.GetNumber(body, "radiusOrCols", 4f);
                r.Cols = (int)JsonUtil.GetNumber(body, "cols", square);
                r.Rows = (int)JsonUtil.GetNumber(body, "rows", square);
                r.Spacing = JsonUtil.GetNumber(body, "spacing", DefaultSpacing);
                r.RotationDegrees = JsonUtil.GetNumber(body, "rotationDegrees", 0f);
                r.SnapDistance = NodeHelper.ClampSnapDistance(JsonUtil.GetNumber(body, "snapDistance", NodeHelper.DefaultSnapDistance));
                r.DryRun = JsonUtil.GetBool(body, "dryRun", false);

                string error = r.Validate(MaxNeighborhoodCells);
                if (error != null)
                {
                    return error;
                }

                r.Init();

                // "center" names the middle of the neighborhood; the lattice grows from a corner.
                Vector3 center = ReadPoint(body, "center");
                Vector3 halfExtent = Quaternion.Euler(0f, r.RotationDegrees, 0f) *
                    new Vector3(r.Cols * r.Spacing * 0.5f, 0f, r.Rows * r.Spacing * 0.5f);
                r.Origin = center - halfExtent;

                request = r;
                return null;
            }

            private string Validate(int maxCells)
            {
                Prefab = PrefabCollection<NetInfo>.FindLoaded(PrefabName);
                if (Prefab == null)
                {
                    return "Network prefab was not found: " + PrefabName + ". List valid names with GET /prefabs/roads.";
                }

                if (Cols < 1 || Rows < 1)
                {
                    return "cols and rows must both be at least 1.";
                }

                if (Cols * Rows > maxCells)
                {
                    return "cols * rows must be at most " + maxCells + " (asked for " + (Cols * Rows) +
                        "). Build the district in several calls instead.";
                }

                if (Spacing < MinSpacing || Spacing > MaxSpacing)
                {
                    return "spacing must be between " + JsonUtil.Number(MinSpacing) + " and " +
                        JsonUtil.Number(MaxSpacing) + " metres. " + JsonUtil.Number(DefaultSpacing) +
                        " gives full-depth zoning on both sides of the road.";
                }

                return null;
            }
        }

        // --------------------------------------------------------------- json output

        private static string DescribeGridPlan(GridRequest request)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true,\"dryRun\":true");
            json.Append(",\"roadPrefab\":\"").Append(JsonUtil.Escape(request.PrefabName)).Append("\"");
            json.Append(",\"cols\":").Append(request.Cols);
            json.Append(",\"rows\":").Append(request.Rows);
            json.Append(",\"spacing\":").Append(JsonUtil.Number(request.Spacing));
            json.Append(",\"plannedNodes\":").Append((request.Cols + 1) * (request.Rows + 1));
            json.Append(",\"plannedSegments\":").Append(request.Cols * (request.Rows + 1) + request.Rows * (request.Cols + 1));
            json.Append(",\"bbox\":").Append(BboxJson(request));
            json.Append(",\"blockCenters\":").Append(BlockCentersJson(request));
            json.Append("}");
            return json.ToString();
        }

        private static string GridJson(GridRequest request, GridResult grid, string extra)
        {
            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true,\"dryRun\":false");
            json.Append(",\"roadPrefab\":\"").Append(JsonUtil.Escape(request.PrefabName)).Append("\"");
            json.Append(",\"cols\":").Append(request.Cols);
            json.Append(",\"rows\":").Append(request.Rows);
            json.Append(",\"spacing\":").Append(JsonUtil.Number(request.Spacing));
            json.Append(",\"nodeIds\":").Append(IdArray(grid.AllNodes));
            json.Append(",\"createdNodeIds\":").Append(IdArray(grid.CreatedNodes));
            json.Append(",\"reusedNodes\":").Append(grid.ReusedNodes);
            json.Append(",\"segmentIds\":").Append(IdArray(grid.Segments));
            json.Append(",\"skippedSegments\":").Append(grid.SkippedSegments);
            json.Append(",\"bbox\":").Append(BboxJson(request));
            json.Append(",\"blockCenters\":").Append(BlockCentersJson(request));
            if (extra != null)
            {
                json.Append(extra);
            }
            json.Append("}");
            return json.ToString();
        }

        private static string BboxJson(GridRequest request)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i <= request.Cols; i++)
            {
                for (int j = 0; j <= request.Rows; j++)
                {
                    Vector3 p = request.LatticePoint(i, j);
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.z < minZ) minZ = p.z;
                    if (p.z > maxZ) maxZ = p.z;
                }
            }

            return "{\"minX\":" + JsonUtil.Number(minX) +
                ",\"maxX\":" + JsonUtil.Number(maxX) +
                ",\"minZ\":" + JsonUtil.Number(minZ) +
                ",\"maxZ\":" + JsonUtil.Number(maxZ) + "}";
        }

        private static string BlockCentersJson(GridRequest request)
        {
            StringBuilder json = new StringBuilder("[");
            for (int row = 0; row < request.Rows; row++)
            {
                for (int col = 0; col < request.Cols; col++)
                {
                    if (json.Length > 1)
                    {
                        json.Append(",");
                    }
                    json.Append(PointJson(request.BlockCenter(col, row)));
                }
            }
            json.Append("]");
            return json.ToString();
        }

        private static string PointJson(Vector3 point)
        {
            return "{\"x\":" + JsonUtil.Number(point.x) + ",\"z\":" + JsonUtil.Number(point.z) + "}";
        }

        private static string IdArray(List<ushort> ids)
        {
            StringBuilder json = new StringBuilder("[");
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(",");
                }
                json.Append(ids[i]);
            }
            json.Append("]");
            return json.ToString();
        }

        private static bool HasPoint(string body, string name)
        {
            return body != null && body.IndexOf("\"" + name + "\"") >= 0;
        }

        private static Vector3 ReadPoint(string body, string name)
        {
            float x = JsonUtil.GetPointNumber(body, name, "x", 0f);
            float z = JsonUtil.GetPointNumber(body, name, "z", 0f);
            float y = JsonUtil.GetPointNumber(body, name, "y", 0f);
            return new Vector3(x, y, z);
        }

        private static bool TryParseService(string value, out ItemClass.Service service)
        {
            service = ItemClass.Service.Road;
            if (value == null || value.Length == 0 || value == "Road")
            {
                return true;
            }

            try
            {
                service = (ItemClass.Service)Enum.Parse(typeof(ItemClass.Service), value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Describe(Exception ex)
        {
            if (ex is BridgeException)
            {
                return ex.Message;
            }
            return ex.GetType().Name + ": " + ex.Message;
        }
    }
}
