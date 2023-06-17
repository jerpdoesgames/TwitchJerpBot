using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    class hostMessages : botModule
	{
        private bool m_IsLoaded;
        hostMessagesConfig m_Config;

        private bool loadConfig()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_hostmessages.json");
            if (File.Exists(configPath))
            {
                string messageConfigString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(messageConfigString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<hostMessagesConfig>(messageConfigString);
                    return true;
                }
            }
            return false;
        }

        public void reloadMessages(userEntry commandUser, string argumentString)
        {
            if (loadConfig())
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("hostMessageLoadSuccess"));
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("hostMessageLoadFail"));
        }

        public override void onRaid(string aHostName, int aViewerCount)
        {

            for (int i = 0; i < m_Config.thresholds.Count; i++)
            {
                if (aViewerCount >= m_Config.thresholds[i].viewers)
                {
                    foreach (string curMessage in m_Config.thresholds[i].messages)
                    {
                        m_BotBrain.sendDefaultChannelMessage(string.Format(curMessage, aHostName));
                    }
                    break;
                }
            }
        }

        public hostMessages(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("hostmessages", null, true, true);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadMessages, false, false));

                m_BotBrain.addChatCommand(tempDef);
            }
        }
	}
}
