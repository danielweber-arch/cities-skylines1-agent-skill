using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using ColossalFramework;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Which city is loaded, so an agent keeps per-city notes apart. The id is
    /// SimulationMetaData.m_gameInstanceIdentifier, which the game keeps across saves of the
    /// same city. Population is district 0 (the whole city) m_populationData.m_finalCount.
    /// lastSaveName: the game's own SavePanel.m_LastSaveName (private static, read by reflection;
    /// SavePanel.SaveRoutine sets it on save and LoadPanel through SavePanel.lastLoadedName on
    /// load), else the last save requested through the bridge in this process, else null.
    /// SimulationMetaData has no save-name field.
    /// </summary>
    public static class CityIdentity
    {
        private static string lastBridgeSaveName;
        // The instance id the bridge save was made in, so a save in city A is never reported
        // as city B's last save after a load (the fallback is process-global otherwise).
        private static string lastBridgeSaveCityId;
        private static readonly object SaveNameLock = new object();

        /// <summary>Called by SaveCommands after SavePanel accepted a save request.</summary>
        public static void RememberSave(string name)
        {
            string cityId = null;
            try
            {
                SimulationMetaData meta = SimulationManager.instance.m_metaData;
                if (meta != null)
                {
                    cityId = meta.m_gameInstanceIdentifier;
                }
            }
            catch (Exception)
            {
            }
            lock (SaveNameLock)
            {
                lastBridgeSaveName = name;
                lastBridgeSaveCityId = cityId;
            }
        }

        /// <summary>Compact JSON object, or the literal null when the metadata is not available.</summary>
        public static string BuildCityJson()
        {
            try
            {
                SimulationManager simulation = Singleton<SimulationManager>.instance;
                if (simulation == null || simulation.m_metaData == null)
                {
                    return "null";
                }
                SimulationMetaData meta = simulation.m_metaData;

                int population = 0;
                DistrictManager districts = Singleton<DistrictManager>.instance;
                if (districts != null && districts.m_districts != null && districts.m_districts.m_buffer != null)
                {
                    population = (int)districts.m_districts.m_buffer[0].m_populationData.m_finalCount;
                }

                StringBuilder json = new StringBuilder();
                json.Append("{\"id\":").Append(StringOrNull(meta.m_gameInstanceIdentifier));
                json.Append(",\"name\":").Append(StringOrNull(meta.m_CityName));
                json.Append(",\"map\":").Append(StringOrNull(meta.m_MapName));
                json.Append(",\"environment\":").Append(StringOrNull(meta.m_environment));
                json.Append(",\"gameDate\":\"").Append(meta.m_currentDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("\"");
                json.Append(",\"population\":").Append(population);
                json.Append(",\"lastSaveName\":").Append(StringOrNull(LastSaveName(meta.m_gameInstanceIdentifier)));
                json.Append("}");
                return json.ToString();
            }
            catch (Exception)
            {
                return "null";
            }
        }

        private static string LastSaveName(string currentCityId)
        {
            // The game field first: SavePanel.SaveRoutine and LoadPanel both set it, so it follows
            // a city loaded after a bridge save. The bridge's own record is the fallback when the
            // field is still empty (no load or save seen yet) or the reflection read fails.
            try
            {
                FieldInfo field = typeof(SavePanel).GetField("m_LastSaveName", BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                {
                    string name = field.GetValue(null) as string;
                    if (name != null && name.Length > 0)
                    {
                        return name;
                    }
                }
            }
            catch (Exception)
            {
            }
            lock (SaveNameLock)
            {
                if (lastBridgeSaveName != null && lastBridgeSaveName.Length > 0 &&
                    lastBridgeSaveCityId != null && lastBridgeSaveCityId == currentCityId)
                {
                    return lastBridgeSaveName;
                }
            }
            return null;
        }

        private static string StringOrNull(string value)
        {
            if (value == null || value.Length == 0)
            {
                return "null";
            }
            return "\"" + JsonUtil.Escape(value) + "\"";
        }
    }
}
