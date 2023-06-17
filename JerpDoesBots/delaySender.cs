using System;
using System.Collections.Generic;

namespace JerpDoesBots
{

    class delaySender : botModule
    {
        private List<delaySendEntry> m_Entries;
        const long MAX_DELAY_TIME = 4 * 60 * 60 * 1000; // 4h
        const long MIN_DELAY_TIME = 5 * 1000;
        const int MAX_ENTRIES = 20;

        private struct delaySendEntry
        {
            public string message { get; set; }
            public long sendTime { get; set; }
            public userEntry commandUser { get; set; }

            public delaySendEntry(long aSendTime, userEntry aUser, string aMessage)
            {
                message = aMessage;
                sendTime = aSendTime;
                commandUser = aUser;
            }
        }

        public void addEntry(userEntry commandUser, string argumentString)
        {
            if (m_Entries.Count < MAX_ENTRIES)
            {
                string[] argumentList = argumentString.Split(new[] { ' ' }, 2);
                if (argumentList.Length == 2)
                {

                    long delayMS;
                    if (long.TryParse(argumentList[0], out delayMS))
                    {
                        delayMS *= 1000;
                        if (delayMS <= MAX_DELAY_TIME && delayMS >= MIN_DELAY_TIME)
                        {
                            m_Entries.Add(new delaySendEntry(m_BotBrain.actionTimer.ElapsedMilliseconds + delayMS, commandUser, argumentList[1]));
                        }
                        else if (delayMS < MIN_DELAY_TIME)
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("delayTimeShort"), MIN_DELAY_TIME));
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("delayTimeLong"), MAX_DELAY_TIME));
                        }
                        
                    }
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("delayQueueMax"), m_Entries.Count));
            }
        }

        public void purgeEntries(userEntry commandUser, string argumentString)
        {
            int prevEntryCount = m_Entries.Count;
            m_Entries.Clear();

            if (prevEntryCount > 0)
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("delayQueueClearSuccess"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("delayQueueClearFail"));
            }

        }
        
        public void getCount(userEntry commandUser, string argumentString)
        {
            m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("delayQueueCount"), m_Entries.Count));
        }

        public override void frame()
        {
            List<delaySendEntry> removedEntries = new List<delaySendEntry>();

            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_BotBrain.actionTimer.ElapsedMilliseconds >= m_Entries[i].sendTime)
                {
                    string messageToSend = m_Entries[i].message;
                    if (!String.IsNullOrEmpty(messageToSend))
                    {

                        if (messageToSend.IndexOf('!') == 0)
                        {
                            userEntry botOwnerUser = m_BotBrain.checkCreateUser(m_BotBrain.ownerUsername);

                            m_BotBrain.processUserCommand(botOwnerUser, messageToSend);
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage(messageToSend);
                        }
                    }

                    removedEntries.Add(m_Entries[i]);
                }
            }

            for (int i = 0; i < removedEntries.Count; i++)
            {
                m_Entries.Remove(removedEntries[i]);
            }
        }

        public delaySender(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            m_Entries = new List<delaySendEntry>();

            chatCommandDef tempDef = new chatCommandDef("delay", addEntry, true, false);
            tempDef.addSubCommand(new chatCommandDef("purge", purgeEntries, false, false));
            tempDef.addSubCommand(new chatCommandDef("count", getCount, false, false));
            tempDef.useGlobalCooldown = false;
            m_BotBrain.addChatCommand(tempDef);
        }

    }
}
