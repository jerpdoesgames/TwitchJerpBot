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

        /// <summary>
        /// Whether the specified allowed/barred category is set for the channel.
        /// </summary>
        /// <param name="aOverrideCategory">Used to fix a specific category in place (typically used when the same conditions need to be met when an ad starts/ends).</param>
        /// <returns></returns>
        public bool isValidCategory(string aOverrideCategory = null)
        {
            string useGame = !string.IsNullOrEmpty(aOverrideCategory) ? aOverrideCategory : jerpBot.instance.game;

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

        /// <summary>
        /// Whether the specified allowed/barred tags are in/not in use by the channel.
        /// </summary>
        /// <param name="aOverrideTags"></param>
        /// <returns>Used to fix specific tags in place (typically used when the same conditions need to be met when an ad starts/ends).</returns>
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

        /// <summary>
        /// Whether all conditions are met by the channel.
        /// </summary>
        /// <param name="aOverrideCategory">Used to fix a specific category in place (typically used when the same conditions need to be met when an ad starts/ends).</param>
        /// <param name="aOverrideTags">Used to fix specific tags in place (typically used when the same conditions need to be met when an ad starts/ends).</param>
        /// <returns></returns>
        public virtual bool isMet(string aOverrideCategory = null, string[] aOverrideTags = null)
        {
            if (!isValidCategory(aOverrideCategory))
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

        /// <summary>
        /// Whether the specified min/max follower count is true for the stream.
        /// </summary>
        /// <returns></returns>
        private bool isValidFollowPercentage()
        {
            if (followPercentMin == -1 && followPercentMax == -1)
            {
                return true;
            }
            else
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
            }

            return true;
        }

        /// <summary>
        /// Whether the specified min/max subscriber count is true for the stream.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Whether all conditions are met by the stream.
        /// </summary>
        /// <param name="aOverrideCategory">Used to fix a specific category in place (typically used when the same conditions need to be met when an ad starts/ends).</param>
        /// <param name="aOverrideTags">Used to fix specific tags in place (typically used when the same conditions need to be met when an ad starts/ends).</param>
        /// <param name="aOverrideViewerCount">Used to fix a specific viewer count in place (typically used when the same conditions need to be met when an ad starts/ends).</param>
        /// <returns></returns>
        public bool isMet(string aOverrideCategory = null, string[] aOverrideTags = null, int aOverrideViewerCount = -1)
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

            if (!base.isMet(aOverrideCategory, aOverrideTags))
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

    /// <summary>
    /// Extended version of streamCondition to check ad-specific data such as the duration of an ad.
    /// </summary>
    internal class adCondition : streamCondition
    {
        public int adTimeSecondsMin { get; set; }
        public int adTimeSecondsMax { get; set; }

        public bool isMet(string aOverrideCategory = null, string[] aOverrideTags = null, int aOverrideViewerCount = -1, int aCommercialLengthSeconds = -1)
        {
            if (!base.isMet(aOverrideCategory, aOverrideTags, aOverrideViewerCount))
                return false;

            if ((adTimeSecondsMax > 0 && aCommercialLengthSeconds > adTimeSecondsMax) || (adTimeSecondsMin > 0 && aCommercialLengthSeconds < adTimeSecondsMin))
            {
                return false;
            }

            return true;
        }
    }
}
