using System;

namespace JerpDoesBots
{
    // TODO: Eventually replace all the existing throttle behavior
    class throttler
    {
        private long m_WaitTimeMax = 900000;    // Maximum amount of time to wait before sending out next message, assuming the minimum lines has been met.
        private long m_LastLineCount = 0;   // How many lines had passed when the last message went out.
        private bool m_Initialized = false;    // Will initialize when first checked - that way we can have it wait from roughly the first frame post connection rather than 0.
        private int m_LineCountMinimum = 6; // How many lines need to pass before the next message can go out (even if the throttle is up).
        private int m_LineCountReductionMax = 30;   // How many lines can reduce the time between messages
        private long m_LineCountReduction = 23333;  // How much time to reduce the message delay per line
        private long m_MessageTimeLast = 0;
        private bool m_RequiresUserMessages = true; // Require a minimum amount of chat messages to pass before sending its next message.
        private bool m_MessagesReduceTimer = true;

        private jerpBot m_BotBrain;

        public int lineCountReductionMax
        {
            get { return m_LineCountReductionMax; }
            set { m_LineCountReductionMax = value; }
        }

        public bool requiresUserMessages
        {
            get { return m_RequiresUserMessages; }
            set { m_RequiresUserMessages = value; }
        }

        public bool messagesReduceTimer
        {
            get { return m_MessagesReduceTimer; }
            set { m_MessagesReduceTimer = value; }
        }

        public long waitTimeMax
        {
            get { return m_WaitTimeMax; }
            set { m_WaitTimeMax = value; }
        }

        public long lineCountReduction
        {
            get { return m_LineCountReduction; }
            set { m_LineCountReduction = value; }
        }

        public int lineCountMinimum
        {
            get { return m_LineCountMinimum; }
            set { m_LineCountMinimum = value; }
        }

        public long adjustedThrottleTime
        {
            get
            {

                long messageCountReduction = 0;

                if (m_MessagesReduceTimer)
                    messageCountReduction = (Math.Min(linesSinceLastMessage, m_LineCountReductionMax));

                return m_WaitTimeMax - messageCountReduction;
            }
        }

        public bool isTimeUp
        {
            get
            {
                return (m_BotBrain.ActionTimer.ElapsedMilliseconds > (m_MessageTimeLast + adjustedThrottleTime));
            }
        }

        public long linesSinceLastMessage
        {
            get
            {
                return Math.Min(m_BotBrain.LineCount - m_LastLineCount, m_LineCountReductionMax);
            }
        }

        public bool isWaitingOnLines
        {
            get
            {
                return m_RequiresUserMessages && (m_LineCountMinimum >= linesSinceLastMessage);
            }
        }

        public bool isReady
        {
            get
            {
                if (!m_Initialized)
                {
                    m_MessageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
                    m_Initialized = true;
                }

                return (!isWaitingOnLines && isTimeUp);
            }
        }

        public void trigger()
        {
            m_MessageTimeLast = m_BotBrain.ActionTimer.ElapsedMilliseconds;
            m_LastLineCount = m_BotBrain.LineCount;
        }

        public throttler(jerpBot aBotBrain)
        {
            m_BotBrain = aBotBrain;
        }
    }
}
