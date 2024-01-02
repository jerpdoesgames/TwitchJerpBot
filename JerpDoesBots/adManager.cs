using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TwitchLib.Api.Helix.Models.Channels.GetAdSchedule;
using TwitchLib.Api.Helix.Models.Channels.SnoozeNextAd;
using TwitchLib.PubSub.Events;

namespace JerpDoesBots
{
    /// <summary>
    /// Module for managing ads on Twitch.
    /// </summary>
    internal class adManager : botModule
    {
        private bool m_IsLoaded;
        private adManagerConfig m_Config;
        private long m_CommercialStartTimeMS = 0;
        private bool m_IsCommercialActive = false;
        private int m_CommercialLengthSeconds = 0;

        private string m_CommercialStartGame = "";
        private string[] m_CommercialStartTags;
        private int m_CommercialStartViewerCount = 0;

        private int m_AvailableSnoozes = 0;
        private Nullable<DateTime> m_SnoozeRefreshAt;
        private int m_SnoozeRefreshThrottleSeconds = 30;
        private int m_SnoozeRefreshCheckBufferSeconds = 1;  // Just to be sure it should have updated when we next check for snoozes.
        private Nullable<DateTime> m_NextAdAt;
        private const int SNOOZE_COUNT_MAX = 3;

        private long commercialLengthMS { get { return m_CommercialLengthSeconds * 1000; } }

        private bool loadConfig()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_admanager.json");
            if (File.Exists(configPath))
            {
                string configFileString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(configFileString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<adManagerConfig>(configFileString);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Request and store data from the channel's ad schedule, including snooze data and when the next ad will occur.
        /// </summary>
        private void getAdScheduleData()
        {
            Task<GetAdScheduleResponse> getAdScheduleTask = jerpBot.instance.twitchAPI.Helix.Channels.GetAdScheduleAsync(jerpBot.instance.ownerUserID);
            getAdScheduleTask.Wait();

            if (getAdScheduleTask.Result != null && getAdScheduleTask.Result.Data.Length >= 0)
            {
                AdSchedule adScheduleData = getAdScheduleTask.Result.Data[0];
                m_NextAdAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(adScheduleData.NextAdAt)).DateTime.ToLocalTime();
                m_SnoozeRefreshAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(adScheduleData.SnoozeRefreshAt)).DateTime.ToLocalTime();
                m_AvailableSnoozes = adScheduleData.SnoozeCount;
            }
        }

        public override void onStreamLive()
        {
            getAdScheduleData();
        }

        /// <summary>
        /// Occurs when a commercial begins.
        /// </summary>
        /// <param name="aCommercialArgs">Information about the commercial being played.</param>
        public override void onCommercialStart(OnCommercialArgs aCommercialArgs)
        {
            m_CommercialStartTimeMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
            m_CommercialLengthSeconds = aCommercialArgs.Length;
            m_CommercialStartGame = jerpBot.instance.game;
            m_CommercialStartViewerCount = jerpBot.instance.viewersLast;
            m_CommercialStartTags = jerpBot.instance.tags;
            
            m_IsCommercialActive = true;

            m_NextAdAt = null;

            if (m_Config.announceCommercialStart)
            {
                int adTimeSeconds = aCommercialArgs.Length % 60;
                int adTimeMinutes = aCommercialArgs.Length / 60;    // Truncation is expected
                string adTimeString = (adTimeMinutes > 0 ? adTimeMinutes + "m" : "") + (adTimeSeconds > 0 ? adTimeSeconds + "s" : "");
                jerpBot.instance.sendDefaultChannelAnnounce(string.Format(jerpBot.instance.localizer.getString("adManagerCommercialStart"), adTimeString));
            }

            if (m_Config.commercialStartCommands != null && m_Config.commercialStartCommands.Count > 0)
            {
                userEntry ownerUser = jerpBot.instance.checkCreateUser(jerpBot.instance.ownerUsername);
                foreach (adManagerConfigCommandEntry curCommand in m_Config.commercialStartCommands)
                {
                    if (isValidAdCondition(curCommand.requirements))
                    {
                        jerpBot.instance.processUserCommand(ownerUser, curCommand.commandString);
                    }
                }
            }
        }

        /// <summary>
        /// Whether the condition for this ad is met (also true if the condition is null).
        /// </summary>
        /// <param name="aCondition">The condition to check.</param>
        /// <returns></returns>
        private bool isValidAdCondition(adCondition aCondition)
        {
            return aCondition == null || aCondition.isMet(m_CommercialStartGame, m_CommercialStartTags, m_CommercialStartViewerCount, m_CommercialLengthSeconds);
        }

        public override void onFrame()
        {
            // Commercial completed
            if (m_IsCommercialActive && jerpBot.instance.actionTimer.ElapsedMilliseconds - m_CommercialStartTimeMS > commercialLengthMS)
            {
                m_IsCommercialActive = false;

                if (m_Config.announceCommercialEnd)
                {
                    jerpBot.instance.sendDefaultChannelAnnounce(jerpBot.instance.localizer.getString("adManagerCommercialEnd"));
                }

                if (!m_IsCommercialActive && m_Config.incomingAdWarnings != null && m_Config.incomingAdWarnings.Count > 0)
                {
                    foreach (adManagerIncomingAdWarning curWarning in m_Config.incomingAdWarnings)
                    {
                        curWarning.resetNotifiedStatus();
                    }
                }

                if (m_Config.commercialEndCommands != null && m_Config.commercialEndCommands.Count > 0)
                {
                    foreach (adManagerConfigCommandEntry curCommand in m_Config.commercialEndCommands)
                    {
                        if (isValidAdCondition(curCommand.requirements))
                        {
                            jerpBot.instance.messageOrCommand(curCommand.commandString);
                        }
                    }
                }

                getAdScheduleData();
            }
            else
            {
                if (!m_IsCommercialActive && m_SnoozeRefreshAt != null)
                {
                    // Check next snooze
                    TimeSpan timeUntilSnoozeAvailable = m_SnoozeRefreshAt.Value.Subtract(DateTime.Now);
                    if (timeUntilSnoozeAvailable.Seconds + m_SnoozeRefreshCheckBufferSeconds <= 0)
                    {
                        m_SnoozeRefreshAt = DateTime.Now.AddSeconds(m_SnoozeRefreshThrottleSeconds);    // Enforcing a throttle in case it fails, otherwise this will be overwritten with the actual time.
                        getAdScheduleData();
                    }
                }

                // Check whether to display ad warnings
                if (!m_IsCommercialActive && m_NextAdAt != null && m_Config.incomingAdWarnings != null && m_Config.incomingAdWarnings.Count > 0)
                {
                    TimeSpan timeUntilNextAd = m_NextAdAt.Value.Subtract(DateTime.Now);
                    double secondsUntilNextAd = timeUntilNextAd.TotalSeconds;

                    if (secondsUntilNextAd > 0)
                    {
                        foreach (adManagerIncomingAdWarning curWarning in m_Config.incomingAdWarnings)
                        {
                            if (!curWarning.notifiedSinceLastAd && secondsUntilNextAd <= curWarning.timeBeforeAdSeconds)
                            {
                                if (isValidAdCondition(curWarning.requirements))
                                {
                                    curWarning.setNotifyTriggered();
                                    jerpBot.instance.messageOrCommand(curWarning.commandString);
                                }
                            }
                            else if (curWarning.notifiedSinceLastAd && secondsUntilNextAd > curWarning.timeBeforeAdSeconds) // Catches snoozes
                            {
                                curWarning.resetNotifiedStatus();
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Time until snooze in "#m#s" format.
        /// </summary>
        /// <returns></returns>
        public string getNextSnoozeTimeString()
        {
            string output = "??m??s";

            if (m_SnoozeRefreshAt != null)
            {
                TimeSpan timeUntilNextSnooze = m_SnoozeRefreshAt.Value.Subtract(DateTime.Now);
                if (timeUntilNextSnooze.TotalSeconds > 0)
                {
                    output = timeUntilNextSnooze.Minutes + "m" + timeUntilNextSnooze.Seconds + "s";
                }
            }

            return output;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="commandUser">Outputs how many snoozes are available as a string.  Will also output the time until another snooze is available if the count is less than SNOOZE_COUNT_MAX.</param>
        /// <param name="argumentString">Unused.</param>
        /// <param name="aSilent">Unused.</param>
        public void outputSnoozeInfo(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (m_AvailableSnoozes >= SNOOZE_COUNT_MAX)
            {
                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("adManagerSnoozeCountOutput"), m_AvailableSnoozes));
            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("adManagerSnoozeCountOutputMoreIncoming"), m_AvailableSnoozes, getNextSnoozeTimeString()));
            }
        }

        public void reloadConfig(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("adManagerReloadSuccess"));
            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("adManagerReloadFail"));
            }
        }

        /// <summary>
        /// Attempt to snooze an ad using the Twitch API.
        /// </summary>
        /// <param name="commandUser">User who's attempting to snooze an ad.</param>
        /// <param name="argumentString">Unused.</param>
        /// <param name="aSilent">Whether to output on success.</param>
        public void snoozeAd(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!m_IsCommercialActive)
            {
                if (m_AvailableSnoozes > 0)
                {
                    try
                    {
                        Task<TwitchLib.Api.Helix.Models.Channels.SnoozeNextAd.SnoozeNextAdResponse> snoozeResponse = jerpBot.instance.twitchAPI.Helix.Channels.SnoozeNextAd(jerpBot.instance.ownerUserID);
                        snoozeResponse.Wait();

                        if (snoozeResponse.Result != null && snoozeResponse.Result.Data.Length > 0)
                        {
                            SnoozeNextAd snoozeData = snoozeResponse.Result.Data[0];
                            m_NextAdAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(snoozeData.NextAdAt)).DateTime.ToLocalTime();
                            m_SnoozeRefreshAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(snoozeData.SnoozeRefreshAt)).DateTime.ToLocalTime();
                            m_AvailableSnoozes = snoozeData.SnoozeCount;

                            if (m_AvailableSnoozes > 0)
                                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("adManagerSnoozeSuccess"), m_AvailableSnoozes));
                            else
                                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("adManagerSnoozeSuccessNoneLeft"), getNextSnoozeTimeString()));
                        }
                    }
                    catch (Exception e)
                    {
                        jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("adManagerSnoozeFailUnknownReason"));
                        Console.WriteLine("Unable to snooze ad: " + e.Message);
                    }
                }
                else
                {
                    jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("adManagerSnoozeFailNoSnoozesLeft"), getNextSnoozeTimeString()));
                }


            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("adManagerSnoozeFailCommercialActive"));
            }
        }

        /// <summary>
        /// Initialize command entries for the ad manager.
        /// </summary>
        public adManager() : base(true, true, false)
        {
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("ad", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
                tempDef.addSubCommand(new chatCommandDef("snooze", snoozeAd, true, false));
                tempDef.addSubCommand(new chatCommandDef("count", outputSnoozeInfo, true, false));
                jerpBot.instance.addChatCommand(tempDef);
            }
        }
    }
}
