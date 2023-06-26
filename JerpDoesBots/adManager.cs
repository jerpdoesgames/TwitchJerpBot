using System;
using System.IO;
using System.Web.Script.Serialization;
using TwitchLib.PubSub.Events;

namespace JerpDoesBots
{
    internal class adManager : botModule
    {
        private const int PREROLL_FREE_TIME_PER_AD_MINUTE = 20;
        private const int AD_SNOOZE_DURATION_SECONDS = 300;
        private bool m_IsLoaded;
        private adManagerConfig m_Config;
        private long m_CommercialStartTimeMS = 0;
        private bool m_IsCommercialActive = false;
        private int m_CommercialLengthSeconds = 0;

        private string m_CommercialStartGame = "";
        private string[] m_CommercialStartTags;
        private int m_CommercialStartViewerCount = 0;
        private int m_SnoozeCountSinceLastAd = 0;

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

        public override void onCommercialStart(OnCommercialArgs aCommercialArgs)
        {
            m_CommercialStartTimeMS = m_BotBrain.actionTimer.ElapsedMilliseconds;
            m_CommercialLengthSeconds = aCommercialArgs.Length;
            m_CommercialStartGame = m_BotBrain.game;
            m_CommercialStartViewerCount = m_BotBrain.viewersLast;
            m_CommercialStartTags = m_BotBrain.tags;
            
            m_IsCommercialActive = true;

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

        private bool isValidAdCondition(adCondition aCondition)
        {
            return aCondition == null || aCondition.isMet(m_CommercialStartGame, m_CommercialStartTags, m_CommercialStartViewerCount, m_CommercialLengthSeconds);
        }

        public override void frame()
        {
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

                m_SnoozeCountSinceLastAd = 0;

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
            }
            else
            {
                if (!m_IsCommercialActive && m_CommercialStartTimeMS != 0 && m_Config.incomingAdWarnings != null && m_Config.incomingAdWarnings.Count > 0)
                {
                    foreach (adManagerIncomingAdWarning curWarning in m_Config.incomingAdWarnings)
                    {
                        if (!curWarning.notifiedSinceLastAd && m_BotBrain.actionTimer.ElapsedMilliseconds > m_CommercialStartTimeMS + (m_SnoozeCountSinceLastAd * AD_SNOOZE_DURATION_SECONDS * 1000) + (m_CommercialLengthSeconds * 1000 * PREROLL_FREE_TIME_PER_AD_MINUTE) - (curWarning.timeBeforeAdSeconds * 1000))
                        {
                            if (isValidAdCondition(curWarning.requirements))
                            {
                                m_BotBrain.messageOrCommand(curWarning.commandString);
                                curWarning.setNotifyTriggered();
                            }
                        }
                    }
                }
            }
        }

        public void reloadConfig(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("adManagerCommercialReloadSuccess"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("adManagerCommercialReloadFail"));
            }
        }

        public void setSnoozeCount(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            int snoozeCount;
            if (Int32.TryParse(argumentString, out snoozeCount))
            {
                m_SnoozeCountSinceLastAd = snoozeCount;
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("adManagerSnoozeCountSet"), snoozeCount));
            }
        }

        public adManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("ad", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
                tempDef.addSubCommand(new chatCommandDef("snooze", setSnoozeCount, false, false));
                m_BotBrain.addChatCommand(tempDef);
            }
        }
    }
}
