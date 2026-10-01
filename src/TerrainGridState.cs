using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// GET /state/terrain/grid: a compact character map of an area (dry, steep, water, mixed shore)
    /// so an agent can see where ground pieces fit before planning. Water uses
    /// WaterGuard.PointHasWater (the same test the builders use), never a height threshold.
    /// Flow per water region is TerrainManager.SampleWaterData's velocity (it takes the
    /// WaterSimulation BeginRead/EndRead lock itself): instantaneous and indicative only.
    /// </summary>
    public static class TerrainGridState
    {
        public const float DefaultRadius = 512f;
        public const float MaxRadius = 2048f;
        public const float DefaultCell = 64f;
        public const float MinCell = 16f;
        public const float DefaultSteep = 8f;
        public const int MaxSide = 64;
        public const int MaxFlowRegions = 8;

        /// <summary>
        /// Mean speed below this reads as still. The unit is the game's water-simulation velocity
        /// as SampleWaterData returns it (int16 cell velocity scaled by about 1/4369), not
        /// calibrated metres per second.
        /// </summary>
        public const float StillSpeed = 0.1f;

        private const string FlowNote = "instantaneous surface velocity, indicative only; a bay or lake reads still and sewage there reaches intakes";

        /// <summary>Returns an error message for a 400, or null when the parameters are usable.</summary>
        public static string Validate(float x, float z, float radius, float cell, float steep, out int side)
        {
            side = 0;
            if (float.IsNaN(x) || float.IsNaN(z))
            {
                return "Pass x=<float>&z=<float> (the grid centre).";
            }
            if (float.IsNaN(radius) || radius <= 0f || radius > MaxRadius)
            {
                return "radius must be > 0 and <= " + MaxRadius.ToString(CultureInfo.InvariantCulture) + " m.";
            }
            if (float.IsNaN(cell) || cell < MinCell)
            {
                return "cell must be >= " + MinCell.ToString(CultureInfo.InvariantCulture) + " m.";
            }
            if (float.IsNaN(steep) || steep <= 0f)
            {
                return "steep must be a positive grade in percent.";
            }
            side = Mathf.CeilToInt(2f * radius / cell);
            if (side > MaxSide)
            {
                side = 0;
                return "Grid would be " + Mathf.CeilToInt(2f * radius / cell) + " cells per side (max " + MaxSide + "): reduce radius or raise cell.";
            }
            if (side < 1)
            {
                side = 1;
            }
            return null;
        }

        public static CommandResult BuildGridJson(float x, float z, float radius, float cell, float steep, int side)
        {
            TerrainManager terrain = TerrainManager.instance;
            float originX = x - radius;
            float originZ = z - radius;
            float half = cell * 0.5f;
            float cornerDistance = Mathf.Sqrt(2f) * half;

            char[,] grid = new char[side, side];
            float minHeight = float.MaxValue;
            float maxHeight = float.MinValue;
            int dry = 0;
            int steepCount = 0;
            int water = 0;
            int mixed = 0;

            float[] sx = new float[5];
            float[] sz = new float[5];
            for (int r = 0; r < side; r++)
            {
                float cz = originZ + 2f * radius - (r + 0.5f) * cell;
                for (int c = 0; c < side; c++)
                {
                    float cx = originX + (c + 0.5f) * cell;
                    sx[0] = cx; sz[0] = cz;
                    sx[1] = cx - half; sz[1] = cz - half;
                    sx[2] = cx + half; sz[2] = cz - half;
                    sx[3] = cx - half; sz[3] = cz + half;
                    sx[4] = cx + half; sz[4] = cz + half;

                    int wet = 0;
                    float centreHeight = 0f;
                    float maxGrade = 0f;
                    for (int s = 0; s < 5; s++)
                    {
                        float terrainY;
                        float waterY;
                        if (WaterGuard.PointHasWater(new Vector3(sx[s], 0f, sz[s]), out terrainY, out waterY))
                        {
                            wet++;
                        }
                        if (terrainY < minHeight) minHeight = terrainY;
                        if (terrainY > maxHeight) maxHeight = terrainY;
                        if (s == 0)
                        {
                            centreHeight = terrainY;
                        }
                        else if (cornerDistance > 0f)
                        {
                            float grade = Mathf.Abs(terrainY - centreHeight) / cornerDistance * 100f;
                            if (grade > maxGrade) maxGrade = grade;
                        }
                    }

                    char ch;
                    if (wet == 5)
                    {
                        ch = '~';
                        water++;
                    }
                    else if (wet > 0)
                    {
                        ch = '?';
                        mixed++;
                    }
                    else if (maxGrade > steep)
                    {
                        ch = '^';
                        steepCount++;
                    }
                    else
                    {
                        ch = '.';
                        dry++;
                    }
                    grid[r, c] = ch;
                }
            }

            // 4-connected regions of '~' cells, largest first.
            List<FlowRegion> regions = new List<FlowRegion>();
            bool[,] seen = new bool[side, side];
            int[] queueR = new int[side * side];
            int[] queueC = new int[side * side];
            for (int r = 0; r < side; r++)
            {
                for (int c = 0; c < side; c++)
                {
                    if (grid[r, c] != '~' || seen[r, c])
                    {
                        continue;
                    }
                    FlowRegion region = new FlowRegion();
                    int head = 0;
                    int tail = 0;
                    queueR[tail] = r; queueC[tail] = c; tail++;
                    seen[r, c] = true;
                    while (head < tail)
                    {
                        int qr = queueR[head];
                        int qc = queueC[head];
                        head++;
                        region.Cells++;
                        float px = originX + (qc + 0.5f) * cell;
                        float pz = originZ + 2f * radius - (qr + 0.5f) * cell;
                        if (region.Cells == 1)
                        {
                            region.MinX = px; region.MaxX = px; region.MinZ = pz; region.MaxZ = pz;
                        }
                        else
                        {
                            if (px < region.MinX) region.MinX = px;
                            if (px > region.MaxX) region.MaxX = px;
                            if (pz < region.MinZ) region.MinZ = pz;
                            if (pz > region.MaxZ) region.MaxZ = pz;
                        }
                        float th;
                        float wh;
                        Vector3 velocity;
                        Vector3 normal;
                        if (terrain.SampleWaterData(new Vector2(px, pz), out th, out wh, out velocity, out normal))
                        {
                            region.SumX += velocity.x;
                            region.SumZ += velocity.z;
                            // Mean of the speeds, not the speed of the mean: two opposing currents
                            // average to a zero vector and would otherwise read as still.
                            region.SumSpeed += Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
                            region.Sampled++;
                        }
                        Enqueue(grid, seen, side, qr - 1, qc, queueR, queueC, ref tail);
                        Enqueue(grid, seen, side, qr + 1, qc, queueR, queueC, ref tail);
                        Enqueue(grid, seen, side, qr, qc - 1, queueR, queueC, ref tail);
                        Enqueue(grid, seen, side, qr, qc + 1, queueR, queueC, ref tail);
                    }
                    regions.Add(region);
                }
            }
            regions.Sort(delegate(FlowRegion a, FlowRegion b) { return b.Cells.CompareTo(a.Cells); });

            if (minHeight == float.MaxValue)
            {
                minHeight = 0f;
                maxHeight = 0f;
            }

            StringBuilder json = new StringBuilder(side * (side + 3) + 1024);
            json.Append("{\"ok\":true,\"center\":{\"x\":").Append(D1(x)).Append(",\"z\":").Append(D1(z)).Append("}");
            json.Append(",\"radius\":").Append(D1(radius));
            json.Append(",\"cell\":").Append(D1(cell));
            json.Append(",\"cols\":").Append(side);
            json.Append(",\"rowCount\":").Append(side);
            json.Append(",\"origin\":{\"x\":").Append(D1(originX)).Append(",\"z\":").Append(D1(originZ)).Append("}");
            json.Append(",\"rowOrder\":\"row 0 = north (max z), col 0 = west (min x); char (r,c) centre = origin + ((c+0.5)*cell, 2*radius-(r+0.5)*cell)\"");
            json.Append(",\"legend\":{\".\":\"dry, grade <= ").Append(D1(steep)).Append("%\",\"^\":\"dry, grade > ").Append(D1(steep))
                .Append("%\",\"~\":\"water (all 5 samples)\",\"?\":\"mixed shore (some samples wet)\"}");
            json.Append(",\"rows\":[");
            char[] line = new char[side];
            for (int r = 0; r < side; r++)
            {
                for (int c = 0; c < side; c++)
                {
                    line[c] = grid[r, c];
                }
                if (r > 0) json.Append(",");
                json.Append("\"").Append(line).Append("\"");
            }
            json.Append("]");
            json.Append(",\"minHeight\":").Append(D1(minHeight));
            json.Append(",\"maxHeight\":").Append(D1(maxHeight));
            json.Append(",\"counts\":{\"dry\":").Append(dry).Append(",\"steep\":").Append(steepCount)
                .Append(",\"water\":").Append(water).Append(",\"mixed\":").Append(mixed).Append("}");
            json.Append(",\"flow\":[");
            int emitted = Mathf.Min(regions.Count, MaxFlowRegions);
            for (int i = 0; i < emitted; i++)
            {
                FlowRegion region = regions[i];
                float mx = region.Sampled == 0 ? 0f : region.SumX / region.Sampled;
                float mz = region.Sampled == 0 ? 0f : region.SumZ / region.Sampled;
                float meanSpeed = region.Sampled == 0 ? 0f : region.SumSpeed / region.Sampled;
                float halfCell = cell * 0.5f;
                if (i > 0) json.Append(",");
                json.Append("{\"id\":").Append(i + 1);
                json.Append(",\"cells\":").Append(region.Cells);
                json.Append(",\"sampled\":").Append(region.Sampled);
                json.Append(",\"bbox\":{\"minX\":").Append(D1(region.MinX - halfCell)).Append(",\"maxX\":").Append(D1(region.MaxX + halfCell))
                    .Append(",\"minZ\":").Append(D1(region.MinZ - halfCell)).Append(",\"maxZ\":").Append(D1(region.MaxZ + halfCell)).Append("}");
                json.Append(",\"meanVelocity\":{\"x\":").Append(D1(mx)).Append(",\"z\":").Append(D1(mz)).Append("}");
                json.Append(",\"speed\":").Append(D1(meanSpeed));
                // still is null when no cell could be sampled: unknown is not still.
                json.Append(",\"still\":").Append(region.Sampled == 0 ? "null" : JsonUtil.Bool(meanSpeed < StillSpeed));
                json.Append("}");
            }
            json.Append("]");
            json.Append(",\"flowNote\":\"").Append(FlowNote).Append("; speed = mean of per-cell speeds; still = speed < ").Append(StillSpeed.ToString("0.0#", CultureInfo.InvariantCulture)).Append(" (game water-sim units)\"");
            json.Append(",\"flowTruncated\":").Append(regions.Count - emitted);
            json.Append("}");
            return CommandResult.FromJson(json.ToString());
        }

        private static void Enqueue(char[,] grid, bool[,] seen, int side, int r, int c, int[] queueR, int[] queueC, ref int tail)
        {
            if (r < 0 || c < 0 || r >= side || c >= side)
            {
                return;
            }
            if (seen[r, c] || grid[r, c] != '~')
            {
                return;
            }
            seen[r, c] = true;
            queueR[tail] = r;
            queueC[tail] = c;
            tail++;
        }

        private static string D1(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private class FlowRegion
        {
            public int Cells;
            public int Sampled;
            public float SumX;
            public float SumZ;
            public float SumSpeed;
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;
        }
    }
}
