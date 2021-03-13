using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Script.Serialization;
using System.IO;

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
                            m_Entries.Add(new delaySendEntry(m_BotBrain.ActionTimer.ElapsedMilliseconds + delayMS, commandUser, argumentList[1]));
                        }
                        else if (delayMS < MIN_DELAY_TIME)
                        {
                            m_BotBrain.sendDefaultChannelMessage("Delay must be at least " + MIN_DELAY_TIME + " milliseconds (for safety sake).");
                        }
                        else
                        {
                            m_BotBrain.sendDefaultChannelMessage("Delay exceeds the maximum time of " + MAX_DELAY_TIME + " milliseconds.");
                        }
                        
                    }
                }
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage("Queue is full at " + m_Entries.Count + "entries.  Not adding another for safety sake.");
            }
        }

        public void purgeEntries(userEntry commandUser, string argumentString)
        {
            int prevEntryCount = m_Entries.Count;
            m_Entries.Clear();

            if (prevEntryCount > 0)
            {
                m_BotBrain.sendDefaultChannelMessage("Delayed message queue cleared.");
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage("No delayed messages to clear.");
            }

        }
        
        public void getCount(userEntry commandUser, string argumentString)
        {
            m_BotBrain.sendDefaultChannelMessage("Delayed message queue has " + m_Entries.Count + " entries.");
        }

        public override void frame()
        {
            List<delaySendEntry> removedEntries = new List<delaySendEntry>();

            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (m_BotBrain.ActionTimer.ElapsedMilliseconds >= m_Entries[i].sendTime)
                {
                    string messageToSend = m_Entries[i].message;
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
            tempDef.UseGlobalCooldown = false;
            m_BotBrain.addChatCommand(tempDef);
        }

    }
}
