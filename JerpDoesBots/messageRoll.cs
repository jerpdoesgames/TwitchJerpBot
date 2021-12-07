using System;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    class messageRoll : botModule
	{
        private messageRollConfig m_Config;
        private bool m_Loaded = false;
        private int m_MessageIndex = -1;

        private throttler m_Throttler;

        private bool isValidTags(int aIndex)
        {
            if (m_Config.messageList[m_MessageIndex].tags == null) // No tags to worry about
            {
                return true;
            }
            else
            {
                for (int i = 0; i < m_Config.messageList[aIndex].tags.Count; i++)
                {
                    if (!m_BotBrain.tagInList(m_Config.messageList[aIndex].tags[i], m_BotBrain.tags))
                        return false;
                }
            }

            return true;
        }

        private bool isValidMessage(int aIndex)
        {
            if (m_MessageIndex < m_Config.messageList.Count)
            {
                if (
                    (m_Config.messageList[m_MessageIndex].games == null || m_Config.messageList[m_MessageIndex].games.Contains(m_BotBrain.game)) &&
                    isValidTags(m_MessageIndex)
                   )
                {
                    return true;
                }
            }
            return false;
        }

        public void nextMessageIndex()
        {
            m_MessageIndex++;
            if (m_MessageIndex > m_Config.messageList.Count)
                m_MessageIndex = 0;
        }


        public string getNextMessage()
        {
            messageRollEntry newMessage = null;
            int messagesChecked = 0;
            while (messagesChecked < m_Config.messageList.Count)
            {
                nextMessageIndex();
                if (isValidMessage(m_MessageIndex))
                {
                    newMessage = m_Config.messageList[m_MessageIndex];
                    break;
                }
                messagesChecked++;
            }

            if (newMessage != null)
            {
                return newMessage.text;
            }
            else
            {
                m_Loaded = false;
            }

            return null;
        }

		public override void frame()
		{
            if (m_Loaded && m_Throttler.isReady)
            {
                sendNextMessage();
            }
		}

        public bool loadConfig()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_messages.json");
            if (File.Exists(configPath))
            {
                string messageConfigString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(messageConfigString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<messageRollConfig>(messageConfigString);
                    return true;
                }
            }
            return false;
        }

        private void sendNextMessage()
        {
            string messageToSend = getNextMessage();

            if (!String.IsNullOrEmpty(messageToSend))
            {

                if (messageToSend.IndexOf('!') == 0)
                {
                    userEntry jerpUser = m_BotBrain.checkCreateUser("jerp");

                    m_BotBrain.processUserCommand(jerpUser, messageToSend);
                }
                else
                {
                    m_BotBrain.sendDefaultChannelMessage(messageToSend);
                }


                m_Throttler.trigger();
            }
        }

        public void forceNext(userEntry commandUser, string argumentString)
        {
            sendNextMessage();
        }

        public messageRoll(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            if (loadConfig())
            {
                m_Loaded = true;

                m_Throttler = new throttler(aJerpBot);
                m_Throttler.waitTimeMax = 900000;
                m_Throttler.lineCountReduction = 23333;
                m_Throttler.lineCountReductionMax = 30;

                chatCommandDef tempDef = new chatCommandDef("message", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("next", forceNext, false, false));

                m_BotBrain.addChatCommand(tempDef);

            }
		}
	}
}
