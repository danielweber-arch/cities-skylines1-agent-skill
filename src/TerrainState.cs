using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// GET /state/terrain: terrain and water heights at up to 64 points, so an agent can see
    /// where water is before it plans a grid, road or building.
    /// </summary>
    public static class TerrainState
    {
        public const int MaxPoints = 64;
        private const float ShoreSearchDistance = 100f;

        /// <summary>
        /// Parses either x/z (single point) or "points=x1,z1;x2,z2;...". Returns an error message
        /// for a 400, or null with the points filled in.
        /// </summary>
        public static string ParsePoints(string pointsParam, float x, float z, out List<Vector2> points)
        {
            points = new List<Vector2>();
            if (pointsParam != null && pointsParam.Length > 0)
            {
                string[] pairs = pointsParam.Split(';');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string pair = pairs[i].Trim();
                    if (pair.Length == 0)
                    {
                        continue;
                    }
                    string[] parts = pair.Split(',');
                    float px;
                    float pz;
                    if (parts.Length != 2 ||
                        !float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out px) ||
                        !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out pz))
                    {
                        return "points must be x1,z1;x2,z2;... with numeric values (bad entry: '" + pair + "').";
                    }
                    points.Add(new Vector2(px, pz));
                }
                if (points.Count == 0)
                {
                    return "points was empty. Pass points=x1,z1;x2,z2 or x=..&z=..";
                }
                if (points.Count > MaxPoints)
                {
                    return "At most " + MaxPoints + " points per request (got " + points.Count + "). Split the request.";
                }
                return null;
            }

            if (float.IsNaN(x) || float.IsNaN(z))
            {
                return "Pass x=<float>&z=<float> or points=x1,z1;x2,z2;... (at most " + MaxPoints + " points).";
            }
            points.Add(new Vector2(x, z));
            return null;
        }

        public static CommandResult BuildTerrainJson(List<Vector2> points)
        {
            TerrainManager terrain = TerrainManager.instance;
            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true,\"count\":").Append(points.Count).Append(",\"samples\":[");
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = new Vector3(points[i].x, 0f, points[i].y);
                float terrainY;
                float waterY;
                bool hasWater = WaterGuard.PointHasWater(p, out terrainY, out waterY);

                Vector3 shorePos;
                Vector3 shoreDir;
                float shoreWaterHeight;
                bool hasShore = terrain.GetShorePos(p, ShoreSearchDistance, out shorePos, out shoreDir, out shoreWaterHeight);
                float shoreDistance = 0f;
                if (hasShore)
                {
                    Vector3 delta = shorePos - p;
                    delta.y = 0f;
                    shoreDistance = delta.magnitude;
                }

                if (i > 0)
                {
                    json.Append(",");
                }
                json.Append("{\"x\":").Append(JsonUtil.Number(p.x))
                    .Append(",\"z\":").Append(JsonUtil.Number(p.z))
                    .Append(",\"terrainHeight\":").Append(JsonUtil.Number(terrainY))
                    .Append(",\"waterHeight\":").Append(JsonUtil.Number(waterY))
                    .Append(",\"hasWater\":").Append(JsonUtil.Bool(hasWater))
                    .Append(",\"waterDepth\":").Append(JsonUtil.Number(Mathf.Max(0f, waterY - terrainY)))
                    .Append(",\"shoreDistance\":").Append(hasShore ? JsonUtil.Number(shoreDistance) : "null")
                    .Append(",\"shoreWaterHeight\":").Append(hasShore ? JsonUtil.Number(shoreWaterHeight) : "null")
                    .Append("}");
            }
            json.Append("]}");
            return CommandResult.FromJson(json.ToString());
        }
    }
}
