using System;

namespace JerpDoesBots
{
    class throttler
    {
        private long m_WaitTimeMax = 60000;    // Maximum amount of time to wait before the throttler is ready, assuming the minimum lines has been met.
        private long m_LastLineCount = 0;   // How many lines had passed when the last time this throttler was triggered.
        private bool m_Initialized = false;    // Will initialize when first checked - that way we can have it wait from roughly the first frame post connection rather than 0.
        private int m_LineCountMinimum = 6; // How many lines need to pass before the next message can go out (even if the throttle is up).
        private int m_LineCountReductionMax = 15;   // How many lines can reduce the time between messages
        private long m_LineCountReduction = 2500;  // How much time to reduce the message delay per line
        private long m_MessageTimeLast = 0;
        private bool m_RequiresUserMessages = true; // Require a minimum amount of chat messages to pass before sending its next message.
        private bool m_MessagesReduceTimer = true;

        private jerpBot m_BotBrain;

        /// <summary>Max amount of lines that can reduce the wait time (requires messagesReduceTimer).</summary>
        public int lineCountReductionMax
        {
            get { return m_LineCountReductionMax; }
            set { m_LineCountReductionMax = value; }
        }

        /// <summary>Whether you need to meet lineCountMinimum before the throttler becomes ready. </summary>
        public bool requiresUserMessages
        {
            get { return m_RequiresUserMessages; }
            set { m_RequiresUserMessages = value; }
        }

        /// <summary>Whether lines sent reduce the timer</summary>
        public bool messagesReduceTimer
        {
            get { return m_MessagesReduceTimer; }
            set { m_MessagesReduceTimer = value; }
        }

        /// <summary>Maximum wait time before the throttler is ready (before lines sent possibly reduce this timer)</summary>
        public long waitTimeMax
        {
            get { return m_WaitTimeMax; }
            set { m_WaitTimeMax = value; }
        }

        /// <summary>How much time is subtracted from waitTimeMax per line (requires messagesReduceTimer).</summary>
        public long lineCountReduction
        {
            get { return m_LineCountReduction; }
            set { m_LineCountReduction = value; }
        }

        /// <summary>Minimum amount of lines before the throttler becomes ready (requires requiresUserMessages)</summary>
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
                return (m_BotBrain.actionTimer.ElapsedMilliseconds > (m_MessageTimeLast + adjustedThrottleTime));
            }
        }

        public long linesSinceLastMessage
        {
            get
            {
                return Math.Min(m_BotBrain.lineCount - m_LastLineCount, m_LineCountReductionMax);
            }
        }

        public bool isWaitingOnLines
        {
            get
            {
                return m_RequiresUserMessages && (m_LineCountMinimum >= linesSinceLastMessage);
            }
        }

        /// <summary>Whether all requirements have been met.</summary>
        public bool isReady
        {
            get
            {
                if (!m_Initialized)
                {
                    m_MessageTimeLast = m_BotBrain.actionTimer.ElapsedMilliseconds;
                    m_Initialized = true;
                }

                return (!isWaitingOnLines && isTimeUp);
            }
        }

        /// <summary>Logs that the desired throttled action occurred and to begin waiting for more lines/time before becoming ready.</summary>
        public void trigger()
        {
            m_MessageTimeLast = m_BotBrain.actionTimer.ElapsedMilliseconds;
            m_LastLineCount = m_BotBrain.lineCount;
        }

        /// <summary>All times are in Milliseconds</summary>
        public throttler(jerpBot aBotBrain)
        {
            m_BotBrain = aBotBrain;
        }
    }
}
