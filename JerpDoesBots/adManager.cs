using System.IO;
using System.Web.Script.Serialization;
using TwitchLib.Api.Helix.Models.Charity;
using TwitchLib.PubSub.Events;

namespace JerpDoesBots
{
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

            if (m_Config.commercialStartCommands.Count > 0)
            {
                userEntry ownerUser = m_BotBrain.checkCreateUser(m_BotBrain.ownerUsername);
                foreach (adManagerConfigCommandEntry curCommand in m_Config.commercialStartCommands)
                {
                    if (isValidCommand(curCommand))
                    {
                        m_BotBrain.processUserCommand(ownerUser, curCommand.commandString);
                    }
                }
            }
        }

        private bool isValidCommand(adManagerConfigCommandEntry aCommand)
        {
            if (aCommand.allowedGames.Count > 0)
            {
                bool foundGame = false;
                foreach (string curGame in aCommand.allowedGames)
                {
                    if (curGame == m_CommercialStartGame)
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

            if (aCommand.barredGames.Count > 0)
            {
                foreach (string curGame in aCommand.barredGames)
                {
                    if (m_CommercialStartGame == curGame)
                    {
                        return false;
                    }
                }
            }

            if ((aCommand.viewersMax > 0 && m_CommercialStartViewerCount > aCommand.viewersMax) || (aCommand.viewersMin > 0 && m_CommercialStartViewerCount < aCommand.viewersMin))
            {
                return false;
            }

            if ((aCommand.adTimeSecondsMax > 0 && m_CommercialLengthSeconds > aCommand.adTimeSecondsMax) || (aCommand.adTimeSecondsMin > 0 && m_CommercialLengthSeconds < aCommand.adTimeSecondsMin))
            {
                return false;
            }

            if (aCommand.requiredTags != null && aCommand.requiredTags.Count > 0)
            {
                bool missingTag = false;
                foreach (string curTag in aCommand.requiredTags)
                {
                    if (!m_BotBrain.tagInList(curTag, m_CommercialStartTags))
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

            if (aCommand.barredTags != null && aCommand.barredTags.Count > 0)
            {
                foreach (string curTag in m_CommercialStartTags)
                {
                    if (m_BotBrain.tagInList(curTag, aCommand.barredTags.ToArray()))
                    {
                        return false;
                    }
                }
            }

            return true;
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

                if (m_Config.commercialEndCommands.Count > 0)
                {
                    userEntry ownerUser = m_BotBrain.checkCreateUser(m_BotBrain.ownerUsername);
                    foreach (adManagerConfigCommandEntry curCommand in m_Config.commercialEndCommands)
                    {
                        if (isValidCommand(curCommand))
                        {
                            m_BotBrain.processUserCommand(ownerUser, curCommand.commandString);
                        }
                    }
                }
            }
        }

        public void reloadConfig(userEntry commandUser, string argumentString)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("adManagerCommercialReloadSuccess"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("adManagerCommercialReloadFail"));
            }
        }

        public adManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("ad", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
                m_BotBrain.addChatCommand(tempDef);
            }
        }
    }
}
