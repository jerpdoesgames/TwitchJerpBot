using System.IO;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace JerpDoesBots
{
    class localizer
    {
        private jerpBot m_BotBrain;
        private localizerConfig m_Config;
        private bool m_Loaded = false;

        class localizerConfig
        {
            public Dictionary<string, string> entries { get; set; }
        }

        /// <summary>
        /// Get a string from the localization database using its key.
        /// </summary>
        /// <param name="aKey">Key for the string to return</param>
        /// <returns></returns>
        public string getString(string aKey)
        {
            string output;

            if (m_Config.entries.TryGetValue(aKey, out output))
                return output;
            else
                return "INVALID LOC KEY: " + aKey;
        }

        public bool loadConfig()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_localization.json");
            if (File.Exists(configPath))
            {
                string localizerConfigString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(localizerConfigString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<localizerConfig>(localizerConfigString);
                    return true;
                }
            }
            return false;
        }

        public localizer(jerpBot aJerpBot)
        {
            m_BotBrain = aJerpBot;
            if (loadConfig())
            {
                m_Loaded = true;
            }
        }

    }
}
