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
            m_CommercialStartTimeMS = m_BotBrain.actionTimer.ElapsedMilliseconds;
            m_CommercialLengthSeconds = aCommercialArgs.Length;
            m_CommercialStartGame = m_BotBrain.game;
            m_CommercialStartViewerCount = m_BotBrain.viewersLast;
            m_CommercialStartTags = m_BotBrain.tags;
            
            m_IsCommercialActive = true;

            m_NextAdAt = null;

            if (m_Config.announceCommercialStart)
            {
                int adTimeSeconds = aCommercialArgs.Length % 60;
                int adTimeMinutes = aCommercialArgs.Length / 60;    // Truncation is expected
                string adTimeString = (adTimeMinutes > 0 ? adTimeMinutes + "m" : "") + (adTimeSeconds > 0 ? adTimeSeconds + "s" : "");
                m_BotBrain.sendDefaultChannelAnnounce(string.Format(m_BotBrain.localizer.getString("adManagerCommercialStart"), adTimeString));
            }

            if (m_Config.commercialStartCommands != null && m_Config.commercialStartCommands.Count > 0)
            {
                userEntry ownerUser = m_BotBrain.checkCreateUser(m_BotBrain.ownerUsername);
                foreach (adManagerConfigCommandEntry curCommand in m_Config.commercialStartCommands)
                {
                    if (isValidAdCondition(curCommand.requirements))
                    {
                        m_BotBrain.processUserCommand(ownerUser, curCommand.commandString);
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
            if (m_IsCommercialActive && m_BotBrain.actionTimer.ElapsedMilliseconds - m_CommercialStartTimeMS > commercialLengthMS)
            {
                m_IsCommercialActive = false;

                if (m_Config.announceCommercialEnd)
                {
                    m_BotBrain.sendDefaultChannelAnnounce(m_BotBrain.localizer.getString("adManagerCommercialEnd"));
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
                            m_BotBrain.messageOrCommand(curCommand.commandString);
                        }
                    }
                }

                getAdScheduleData();
            }
            else
            {
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
                                    m_BotBrain.messageOrCommand(curWarning.commandString);
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
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("adManagerSnoozeCountOutput"), m_AvailableSnoozes));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("adManagerSnoozeCountOutputMoreIncoming"), m_AvailableSnoozes, getNextSnoozeTimeString()));
            }
        }

        public void reloadConfig(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("adManagerReloadSuccess"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("adManagerReloadFail"));
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
                        Task<TwitchLib.Api.Helix.Models.Channels.SnoozeNextAd.SnoozeNextAdResponse> snoozeResponse = m_BotBrain.twitchAPI.Helix.Channels.SnoozeNextAd(m_BotBrain.ownerUserID);
                        snoozeResponse.Wait();

                        if (snoozeResponse.Result != null && snoozeResponse.Result.Data.Length > 0)
                        {
                            SnoozeNextAd snoozeData = snoozeResponse.Result.Data[0];
                            m_NextAdAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(snoozeData.NextAdAt)).DateTime.ToLocalTime();
                            m_SnoozeRefreshAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(snoozeData.SnoozeRefreshAt)).DateTime.ToLocalTime();
                            m_AvailableSnoozes = snoozeData.SnoozeCount;

                            if (m_AvailableSnoozes > 0)
                                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("adManagerSnoozeSuccess"), m_AvailableSnoozes));
                            else
                                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("adManagerSnoozeSuccessNoneLeft"), getNextSnoozeTimeString()));
                        }
                    }
                    catch (Exception e)
                    {
                        m_BotBrain.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("adManagerSnoozeFailUnknownReason"));
                        Console.WriteLine("Unable to snooze ad: " + e.Message);
                    }
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("adManagerSnoozeFailNoSnoozesLeft"), getNextSnoozeTimeString()));
                }


            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("adManagerSnoozeFailCommercialActive"));
            }
        }

        /// <summary>
        /// Initialize command entries for the ad manager.
        /// </summary>
        /// <param name="aJerpBot"></param>
        public adManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("ad", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
                tempDef.addSubCommand(new chatCommandDef("snooze", snoozeAd, true, false));
                tempDef.addSubCommand(new chatCommandDef("count", outputSnoozeInfo, true, false));
                m_BotBrain.addChatCommand(tempDef);
            }
        }
    }
}
