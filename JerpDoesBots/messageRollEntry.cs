using System.Collections.Generic;

namespace JerpDoesBots
{
    class messageRollEntry
    {
        public string text { get; set; }
        public bool isAnnounce { get; set; }
        public streamCondition requirements { get; set; }
    }
}
