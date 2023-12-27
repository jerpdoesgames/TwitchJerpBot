using System;
using System.Collections.Generic;

namespace JerpDoesBots
{
    internal class channelCondition
    {
        public List<string> allowedGames { get; set; }
        public List<string> barredGames { get; set; }
        public List<string> requiredTags { get; set; }
        public List<string> barredTags { get; set; }

        private Nullable<DateTime> m_DateTimeStart;
        private Nullable<DateTime> m_DateTimeEnd;
        public string timeStart { set { m_DateTimeStart = DateTime.Parse(value); } }
        public string timeEnd { set { m_DateTimeEnd = DateTime.Parse(value); } }

        public bool isValidDateTime()
        {
            DateTime curTime = DateTime.Now;

            return ((m_DateTimeStart == null || curTime >= m_DateTimeStart) && (m_DateTimeEnd == null || curTime <= m_DateTimeEnd));
        }

        public bool validGame(string aOverrideGame = null)
        {
            string useGame = !string.IsNullOrEmpty(aOverrideGame) ? aOverrideGame : jerpBot.instance.game;

            if (allowedGames != null && allowedGames.Count > 0)
            {
                bool foundGame = false;
                foreach (string curGame in allowedGames)
                {
                    if (curGame == useGame)
                    {
                        foundGame = true;
                        break;
                    }
                }

                if (!foundGame)
                {
                    return false;
                }
            }

            if (barredGames != null && barredGames.Count > 0)
            {
                foreach (string curGame in barredGames)
                {
                    if (useGame == curGame)
                    {
                        return false;
                    }
                }
            }

            return true;
        }


        public bool validTags(string[] aOverrideTags = null)
        {
            string[] useTags = aOverrideTags != null && aOverrideTags.Length > 0 ? aOverrideTags : jerpBot.instance.tags;

            if (requiredTags != null && requiredTags.Count > 0)
            {
                bool missingTag = false;
                foreach (string curTag in requiredTags)
                {
                    if (!jerpBot.instance.tagInList(curTag, useTags))
                    {
                        missingTag = true;
                        break;
                    }
                }

                if (missingTag)
                {
                    return false;
                }
            }

            if (barredTags != null && barredTags.Count > 0)
            {
                foreach (string curTag in useTags)
                {
                    if (jerpBot.instance.tagInList(curTag, barredTags.ToArray()))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public virtual bool isMet(string aOverrideGame = null, string[] aOverrideTags = null)
        {
            if (!validGame(aOverrideGame))
                return false;

            if (!validTags(aOverrideTags))
                return false;

            if (!isValidDateTime())
                return false;

            return true;
        }
    }

    internal class streamCondition : channelCondition
    {
        public int viewersMin { get; set; }
        public int viewersMax { get; set; }
        public float followPercentMin { get; set; }
        public float followPercentMax { get; set; }
        public float subPercentMin { get; set; }
        public float subPercentMax { get; set; }

        private bool isValidFollowPercentage()
        {
            int totalChatters;
            int totalFollowers = jerpBot.instance.getNumChattersFollowing(out totalChatters);
            float followPercent = totalChatters > 0 && totalFollowers > 0 ? (totalFollowers / totalChatters) : 0f;

            if (followPercentMin >= 0 && followPercentMax >= 0)
            {
                return followPercent <= followPercentMax && followPercent >= followPercentMin;
            }
            else if (followPercentMin >= 0)
            {
                return followPercent >= followPercentMin;
            }
            else if (followPercentMax >= 0)
            {
                return followPercent <= followPercentMax;
            }
            else
            {
                return true;
            }
        }

        private bool isValidSubscriberPercentage()
        {
            int totalChatters;
            int totalSubscribers = jerpBot.instance.getNumChattersSubscribed(out totalChatters);
            float subPercent = totalChatters > 0 && totalSubscribers > 0 ? (totalSubscribers / totalChatters) : 0f;

            if (subPercentMin >= 0 && subPercentMax >= 0)
            {
                return subPercent <= subPercentMax && subPercent >= subPercentMin;
            }
            else if (subPercentMin >= 0)
            {
                return subPercent >= subPercentMin;
            }
            else if (subPercentMax >= 0)
            {
                return subPercent <= subPercentMax;
            }
            else
            {
                return true;
            }
        }

        public bool isMet(string aOverrideGame = null, string[] aOverrideTags = null, int aOverrideViewerCount = -1)
        {
            int useViewCount = aOverrideViewerCount != -1 ? aOverrideViewerCount : jerpBot.instance.viewersLast;

            if ((viewersMax > 0 && useViewCount > viewersMax) || (viewersMin > 0 && useViewCount < viewersMin))
            {
                return false;
            }

            if (!isValidFollowPercentage())
                return false;

            if (!isValidSubscriberPercentage())
                return false;

            if (!base.isMet(aOverrideGame, aOverrideTags))
                return false;

            return true;
        }

        public streamCondition() : base()
        {
            subPercentMin = -1;
            subPercentMax = -1;
            followPercentMin = -1;
            followPercentMax = -1;
            viewersMin = -1;
            viewersMax = -1;
        }
    }


    internal class adCondition : streamCondition
    {
        public int adTimeSecondsMin { get; set; }
        public int adTimeSecondsMax { get; set; }

        public bool isMet(string aOverrideGame = null, string[] aOverrideTags = null, int aOverrideViewerCount = -1, int aCommercialLengthSeconds = -1)
        {
            if (!base.isMet(aOverrideGame, aOverrideTags, aOverrideViewerCount))
                return false;

            if ((adTimeSecondsMax > 0 && aCommercialLengthSeconds > adTimeSecondsMax) || (adTimeSecondsMin > 0 && aCommercialLengthSeconds < adTimeSecondsMin))
            {
                return false;
            }

            return true;
        }
    }
}
