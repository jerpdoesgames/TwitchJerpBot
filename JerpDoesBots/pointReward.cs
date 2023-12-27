using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace JerpDoesBots
{
    internal class pointReward
    {
        public string title { get; set; }
        public string description { get; set; }
        public int cost { get; set; }
        public int maxPerStream { get; set; }
        public int maxPerUserPerStream { get; set; }
        public string backgroundColor { get; set; }
        public int globalCooldownSeconds { get; set; }
        public bool requireUserInput { get; set; }
        public bool autoFulfill { get; set; }
        public bool enabled { get; set; }
        public string rewardID { get; set; }    // ID on Twitch
        public bool shouldExistOnTwitch { get; set; }

        public pointReward()
        {
            cost = 1;
            maxPerStream = -1;
            globalCooldownSeconds = -1;
            maxPerUserPerStream = -1;
            enabled = true;
            autoFulfill = false;    // For clarity sake
        }
    }
}
