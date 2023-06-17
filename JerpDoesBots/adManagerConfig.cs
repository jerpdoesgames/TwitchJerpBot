using System.Collections.Generic;

namespace JerpDoesBots
{
    internal class adManagerConfigCommandEntry
    {
        public string commandString { get; set; }
        public adCondition requirements { get; set; }
    }

    internal class adManagerConfig
    {
        public bool announceCommercialStart { get; set; }
        public bool announceCommercialEnd { get; set; } 
        public List<adManagerConfigCommandEntry> commercialStartCommands { get; set; }
        public List<adManagerConfigCommandEntry> commercialEndCommands { get; set; }
    }
}
