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

        public void addEntry(userEntry commandUser, string argumentString, bool aSilent = false)
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
                            m_Entries.Add(new delaySendEntry(jerpBot.instance.actionTimer.ElapsedMilliseconds + delayMS, commandUser, argumentList[1]));
                        }
                        else if (delayMS < MIN_DELAY_TIME)
                        {
                            jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("delayTimeShort"), MIN_DELAY_TIME));
                        }
                        else
                        {
                            jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("delayTimeLong"), MAX_DELAY_TIME));
                        }
                        
                    }
                }
            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("delayQueueMax"), m_Entries.Count));
            }
        }

        public void purgeEntries(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            int prevEntryCount = m_Entries.Count;
            m_Entries.Clear();

            if (prevEntryCount > 0)
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("delayQueueClearSuccess"));
            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("delayQueueClearFail"));
            }

        }
        
        public void getCount(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("delayQueueCount"), m_Entries.Count));
        }

        public override void onFrame()
        {
            List<delaySendEntry> removedEntries = new List<delaySendEntry>();

            for (int i = 0; i < m_Entries.Count; i++)
            {
                if (jerpBot.instance.actionTimer.ElapsedMilliseconds >= m_Entries[i].sendTime)
                {
                    string messageToSend = m_Entries[i].message;
                    if (!String.IsNullOrEmpty(messageToSend))
                    {

                        if (jerpBot.instance.isValidCommandFormat(messageToSend))
                        {
                            userEntry botOwnerUser = jerpBot.instance.checkCreateUser(jerpBot.instance.ownerUsername);

                            jerpBot.instance.processUserCommand(botOwnerUser, messageToSend);
                        }
                        else
                        {
                            jerpBot.instance.sendDefaultChannelMessage(messageToSend);
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

        public delaySender() : base(true, true, false)
        {
            m_Entries = new List<delaySendEntry>();

            chatCommandDef tempDef = new chatCommandDef("delay", addEntry, true, false);
            tempDef.addSubCommand(new chatCommandDef("purge", purgeEntries, false, false));
            tempDef.addSubCommand(new chatCommandDef("count", getCount, false, false));
            tempDef.useGlobalCooldown = false;
            jerpBot.instance.addChatCommand(tempDef);
        }

    }
}
