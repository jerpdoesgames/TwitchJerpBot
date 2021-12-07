using System.Collections.Generic;

namespace JerpDoesBots
{
    public class hostMessageEntry
    {
        public int viewers { get; set; }
        public List<string> messages { get; set; }
    }

    class hostMessagesConfig
    {
        public List<hostMessageEntry> thresholds { get; set; }
    }
}
