using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerpDoesBots
{
    // Frickin' stay hydrated bot couldn't find me when I tried to sign up, so I'll write my own thing
    class hydrateReminder : botModule
    {
        private const float OZ_TO_ML = 29.5735f;
        private throttler m_Throttler;
        private long m_OzPerHour = 4;
        private long m_LastHoursPassed = 0;
        private int m_HoursPassedOffset = 0;

        public int hoursPassedOffset
        {
            get { return m_HoursPassedOffset; }
            set { m_HoursPassedOffset = value; }
        }

        private int getHoursPassed()
        {

            if (m_BotBrain.IsLive)
            {
                TimeSpan tempTimeSinceLive = m_BotBrain.timeSinceLive;
                return (int)tempTimeSinceLive.TotalHours + m_HoursPassedOffset;
            }
            else
            {
                return 0;
            }
        }

        private string getDrinkMessage(long aTimePassed)
        {
            int hoursPassed = getHoursPassed();
            if (hoursPassed > 0)
            {
                int ozToDrink = (int)(m_OzPerHour * hoursPassed);
                string mlToDrink = string.Format("{0:n0}", (int)(ozToDrink * OZ_TO_ML));

                return hoursPassed + " hours have passed, you should have had at least " + (m_OzPerHour * hoursPassed) + "oz (" + mlToDrink + "ml) to drink.";
            }
            
            return "No drink reminders yet - but feel free to have a swig anyways!";
        }

        public override void frame()
        {
            if (m_Throttler.isReady)
            {
                m_Throttler.trigger();
                int curHoursPassed = getHoursPassed();
                if (curHoursPassed > m_LastHoursPassed)
                {
                    m_LastHoursPassed = curHoursPassed;
                    m_BotBrain.sendDefaultChannelMessage(getDrinkMessage(m_BotBrain.actionTimer.ElapsedMilliseconds));
                }
            }
        }

        public void current(userEntry commandUser, string argumentString)
        {
            m_BotBrain.sendDefaultChannelMessage(getDrinkMessage(m_BotBrain.actionTimer.ElapsedMilliseconds));
        }

        public void setOffset(userEntry commandUser, string argumentString)
        {
            int offsetVal;

            if (Int32.TryParse(argumentString, out offsetVal))
            {
                m_HoursPassedOffset = offsetVal;
                m_BotBrain.sendDefaultChannelMessage("Hours passed offset is now " + m_HoursPassedOffset);
            }

        }

        public hydrateReminder(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            m_Throttler = new throttler(aJerpBot);
            m_Throttler.requiresUserMessages = false;
            m_Throttler.messagesReduceTimer = false;
            m_Throttler.waitTimeMax = 30000;
            chatCommandDef tempDef = new chatCommandDef("hydrate", null, false, false);
            tempDef.addSubCommand(new chatCommandDef("current", current, true, true));
            tempDef.addSubCommand(new chatCommandDef("offset", setOffset, false, false));

            m_BotBrain.addChatCommand(tempDef);
        }
    }

}
