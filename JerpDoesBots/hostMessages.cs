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

        public void reloadMessages(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (loadConfig())
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("hostMessageLoadSuccess"));
            }
                
            else
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("hostMessageLoadFail"));
        }

        public override void onRaidReceived(string aHostName, int aViewerCount)
        {
            for (int i = 0; i < m_Config.thresholds.Count; i++)
            {
                if (aViewerCount >= m_Config.thresholds[i].viewers)
                {
                    foreach (string curMessage in m_Config.thresholds[i].messages)
                    {
                        jerpBot.instance.sendDefaultChannelMessage(string.Format(curMessage, aHostName));
                    }
                    break;
                }
            }
        }

        public hostMessages() : base(true, true, false)
		{
            m_IsLoaded = loadConfig();

            if (m_IsLoaded)
            {
                chatCommandDef tempDef = new chatCommandDef("hostmessages", null, true, true);
                tempDef.addSubCommand(new chatCommandDef("reload", reloadMessages, false, false));

                jerpBot.instance.addChatCommand(tempDef);
            }
        }
	}
}
