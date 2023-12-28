using System;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    class messageRoll : botModule
	{
        private messageRollConfig m_Config;
        private bool m_IsLoaded = false;
        private int m_MessageIndex = -1;
        private throttler m_Throttler;

        private bool isValidMessage(int aIndex)
        {
            if (m_MessageIndex < m_Config.messageList.Count)
            {
                messageRollEntry curMessage = m_Config.messageList[m_MessageIndex];
                if (
                    curMessage != null &&
                    (curMessage.requirements == null || curMessage.requirements.isMet())
                )
                {
                    return true;
                }
            }
            return false;
        }

        public void nextValidMessageIndex()
        {
            bool looped = false;
            int originalMessageIndex = m_MessageIndex;
            bool validMessageIndex = false;
            do
            {
                m_MessageIndex++;
                if (m_MessageIndex >= m_Config.messageList.Count)
                    m_MessageIndex = 0;

                if (m_MessageIndex == originalMessageIndex)
                    looped = true;

                if (isValidMessage(m_MessageIndex))
                    validMessageIndex = true;

            } while (!looped && !validMessageIndex);
        }

        public messageRollEntry getNextMessage()
        {
            nextValidMessageIndex();

            if (isValidMessage(m_MessageIndex))
            {
                messageRollEntry newMessage = m_Config.messageList[m_MessageIndex];
                return newMessage;
            }

            return null;
        }

		public override void onFrame()
		{
            if (m_IsLoaded && m_Throttler.isReady)
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
            messageRollEntry messageToSend = getNextMessage();

            if (messageToSend != null && !String.IsNullOrEmpty(messageToSend.text))
            {
                if (m_BotBrain.isValidCommandFormat(messageToSend.text))
                {
                    userEntry ownerUser = m_BotBrain.checkCreateUser(m_BotBrain.ownerUsername);
                    m_BotBrain.processUserCommand(ownerUser, messageToSend.text);
                }
                else
                {
                    if (messageToSend.isAnnounce)
                        m_BotBrain.sendDefaultChannelAnnounce(messageToSend.text);
                    else
                        m_BotBrain.sendDefaultChannelMessage(messageToSend.text);
                }
            }

            m_Throttler.trigger();
        }

        public void forceNext(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (m_IsLoaded)
                sendNextMessage();
        }

        public void reload(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsLoaded = loadConfig();
            if (m_IsLoaded)
            {
                m_MessageIndex = -1;
                if (!aSilent)
                    m_BotBrain.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("messageRollReloadSuccess"));
            }
            else
                m_BotBrain.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("messageRollReloadFail"));

        }

        public messageRoll(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            if (loadConfig())
            {
                m_IsLoaded = true;

                m_Throttler = new throttler(aJerpBot);
                m_Throttler.waitTimeMSMax = 900000;
                m_Throttler.lineCountReductionMS = 23333;
                m_Throttler.lineCountReductionMax = 30;

                chatCommandDef tempDef = new chatCommandDef("message", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("next", forceNext, false, false));
                tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));

                m_BotBrain.addChatCommand(tempDef);

            }
		}
	}
}
