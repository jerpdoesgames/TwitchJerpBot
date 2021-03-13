using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace JerpDoesBots
{
	class messageRoll : botModule
	{
        private messageRollConfig m_Config;
        private bool m_Loaded = false;
        private int m_MessageIndex = -1;
		private long m_MessageTimeLast = 0;
        private bool m_RequiresUserMessages = true;     // Requires a minimum amount of chat messages to pass before sending its next message.

        private long messageThrottle		= 900000;	// Maximum amount of time to wait before sending out next message, assuming the minimum lines has been met.
        private long lastLineCount			= 0;		// How many lines had passed when the last message went out.
        private int lineCountMinimum		= 6;		// How many lines need to pass before the next message can go out (even if the throttle is up).
        private int lineCountReductionMax	= 30;		// How many lines can reduce the time between messages
        private long lineTimeReduction		= 23333;    // How much time to reduce the message delay per line

        private bool m_FirstFrame = false;

        public long messageTimeLast { set { m_MessageTimeLast = value; } get { return m_MessageTimeLast; } }

        private bool isValidMessage(int aIndex)
        {
            if (m_MessageIndex < m_Config.messageList.Count)
            {
                if (m_Config.messageList[m_MessageIndex].games == null || m_Config.messageList[m_MessageIndex].games.Contains(m_BotBrain.Game))
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
            if (m_Loaded)
            {
                if (!m_FirstFrame)
                {
                    messageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                    m_FirstFrame = true;
                }

                long linesSince = Math.Min(m_BotBrain.LineCount - lastLineCount, lineCountReductionMax);
                if (!m_RequiresUserMessages || (m_Config.messageList.Count > 0 && linesSince >= lineCountMinimum))
                {
                    long messageThrottleReduction = m_RequiresUserMessages ? Math.Max(linesSince * lineTimeReduction, 0) : 0;

                    if (m_BotBrain.ActionTimer.ElapsedMilliseconds > (messageTimeLast + messageThrottle - messageThrottleReduction))
                    {
                        sendNextMessage();
                    }
                }
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

        public void nowait(userEntry commandUser, string argumentString)
        {
            m_RequiresUserMessages = false;

            m_BotBrain.sendDefaultChannelMessage("Auto messages will no-longer wait for people to chat.");
        }

        public void wait(userEntry commandUser, string argumentString)
        {

            m_RequiresUserMessages = true;

            m_BotBrain.sendDefaultChannelMessage("Auto messages will wait for people to chat first.");
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

                
                m_MessageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                lastLineCount = m_BotBrain.LineCount;
            }
        }

        public void forceNext(userEntry commandUser, string argumentString)
        {
            sendNextMessage();
        }

        public void throttle(userEntry commandUser, string argumentString)
        {
            int secondsToWait;
            if (int.TryParse(argumentString, out secondsToWait))
            {
                if (secondsToWait >= 60)
                {
                    messageThrottle = secondsToWait * 1000;
                    m_BotBrain.sendDefaultChannelMessage("Auto message throttle set to " + secondsToWait + " seconds.");
                }
            }
        }

        public messageRoll(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            if (loadConfig())
            {
                m_Loaded = true;

                chatCommandDef tempDef = new chatCommandDef("message", null, false, false);
                tempDef.addSubCommand(new chatCommandDef("wait", wait, false, false));
                tempDef.addSubCommand(new chatCommandDef("nowait", nowait, false, false));
                tempDef.addSubCommand(new chatCommandDef("throttle", throttle, false, false));
                tempDef.addSubCommand(new chatCommandDef("next", forceNext, false, false));

                m_BotBrain.addChatCommand(tempDef);

            }
		}
	}
}
