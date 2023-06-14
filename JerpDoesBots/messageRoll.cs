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

        private bool isValidTags(messageRollEntry aMessage)
        {
            if (m_Config.messageList[m_MessageIndex].tags == null) // No tags to worry about
            {
                return true;
            }
            else
            {
                for (int i = 0; i < aMessage.tags.Count; i++)
                {
                    if (!m_BotBrain.tagInList(aMessage.tags[i], m_BotBrain.tags))
                        return false;
                }
            }

            return true;
        }

        private bool isValidViewerCount(int viewersMin = -1, int viewersMax = -1)
        {
            if (viewersMin >= 0 && viewersMax >= 0)
            {
                return m_BotBrain.viewersLast <= viewersMax && m_BotBrain.viewersLast >= viewersMin;
            }
            else if (viewersMin >= 0)
            {
                return m_BotBrain.viewersLast >= viewersMin;
            }
            else if (viewersMax >= 0)
            {
                return m_BotBrain.viewersLast <= viewersMax;
            }
            else
            {
                return true;
            }
        }

        private bool isValidFollowPercentage(float aFollowerPercentMin = -1, float aFollowerPercentMax = -1)
        {
            int totalChatters;
            int totalFollowers = m_BotBrain.getNumChattersFollowing(out totalChatters);
            float followPercent = totalChatters > 0 && totalFollowers > 0 ? (totalFollowers / totalChatters) : 0f;

            if (aFollowerPercentMin >= 0 && aFollowerPercentMax >= 0)
            {
                return followPercent <= aFollowerPercentMax && followPercent >= aFollowerPercentMin;
            }
            else if (aFollowerPercentMin >= 0)
            {
                return followPercent >= aFollowerPercentMin;
            }
            else if (aFollowerPercentMax >= 0)
            {
                return followPercent <= aFollowerPercentMax;
            }
            else
            {
                return true;
            }
        }

        private bool isValidGame(messageRollEntry aMessage)
        {
            return aMessage.games == null || aMessage.games.Contains(m_BotBrain.game);
        }

        private bool isValidMessage(int aIndex)
        {
            if (m_MessageIndex < m_Config.messageList.Count)
            {
                messageRollEntry curMessage = m_Config.messageList[m_MessageIndex];
                if (
                    curMessage != null &&
                    isValidGame(curMessage) &&
                    isValidTags(curMessage) &&
                    isValidFollowPercentage(curMessage.followPercentMin, curMessage.followPercentMax) &&
                    isValidViewerCount(curMessage.viewersMin, curMessage.viewersMax)
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
            messageRollEntry messageToSend = getNextMessage();

            if (messageToSend != null && !String.IsNullOrEmpty(messageToSend.text))
            {
                if (messageToSend.text.IndexOf('!') == 0)
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

        public void forceNext(userEntry commandUser, string argumentString)
        {
            if (m_Loaded)
                sendNextMessage();
        }

        public void reload(userEntry commandUser, string argumentString)
        {
            m_Loaded = loadConfig();
            if (m_Loaded)
            {
                m_MessageIndex = -1;
                m_BotBrain.sendDefaultChannelMessage("Successfully reloaded automated messages.");
            }
            else
                m_BotBrain.sendDefaultChannelMessage("Failed to reload automated messages.");

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
                tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));

                m_BotBrain.addChatCommand(tempDef);

            }
		}
	}
}
