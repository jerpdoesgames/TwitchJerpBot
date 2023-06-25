using System.Collections.Generic;

namespace JerpDoesBots
{
    internal class adManagerIncomingAdWarning
    {
        private bool m_NotifiedSinceLastAd;
        public void setNotifyTriggered()
        {
            m_NotifiedSinceLastAd = true;
        }

        public void resetNotifiedStatus()
        {
            m_NotifiedSinceLastAd = false;
        }

        public bool notifiedSinceLastAd { get { return m_NotifiedSinceLastAd; } }

        public int timeBeforeAdSeconds { get; set; }
        public adCondition requirements { get; set; }
        public string commandString { get; set; }
    }
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
        public List<adManagerIncomingAdWarning> incomingAdWarnings { get; set; }
    }
}
