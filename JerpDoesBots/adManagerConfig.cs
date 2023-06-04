using System.Collections.Generic;

namespace JerpDoesBots
{
    internal class adManagerConfigCommandEntry
    {
        public string commandString { get; set; }
        public List<string> allowedGames { get; set; }
        public List<string> barredGames { get; set; }
        public List<string> requiredTags { get; set; }
        public List<string> barredTags { get; set; }
        public int viewersMin { get; set; }
        public int viewersMax { get; set; }
        public int adTimeSecondsMin { get; set; }
        public int adTimeSecondsMax { get; set; }

        public adManagerConfigCommandEntry()
        {
            allowedGames = new List<string>();
            barredGames = new List<string>();
            requiredTags = new List<string>();
            barredTags = new List<string>();
        }
    }

    internal class adManagerConfig
    {
        public bool announceCommercialStart { get; set; }
        public bool announceCommercialEnd { get; set; } 
        public List<adManagerConfigCommandEntry> commercialStartCommands { get; set; }
        public List<adManagerConfigCommandEntry> commercialEndCommands { get; set; }
    }
}
