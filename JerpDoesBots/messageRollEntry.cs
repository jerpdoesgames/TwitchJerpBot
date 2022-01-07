using System.Collections.Generic;

namespace JerpDoesBots
{
    class messageRollEntry
    {
        public string text { get; set; }
        public List<string> games { get; set; }
        public List<string> tags { get; set; }
        public float followPercentMin { get; set; }
        public float followPercentMax { get; set; }
        public int viewersMin { get; set; }
        public int viewersMax { get; set; }

        public messageRollEntry ()
        {
            followPercentMin = -1;
            followPercentMax = -1;
            viewersMin = -1;
            viewersMax = -1;
        }
    }
}
