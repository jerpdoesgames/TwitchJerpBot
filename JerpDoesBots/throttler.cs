using System;

namespace JerpDoesBots
{
    /// <summary>
    /// System for delaying actions based on timers and/or messages passed.
    /// </summary>
    class throttler
    {
        private long m_WaitTimeMSMax = 60000;    // Maximum amount of time to wait before the throttler is ready, assuming the minimum lines has been met.
        private long m_LastLineCount = 0;   // How many lines had passed when the last time this throttler was triggered.
        private bool m_Initialized = false;    // Will initialize when first checked - that way we can have it wait from roughly the first frame post connection rather than 0.
        private int m_LineCountMinimum = 6; // How many lines need to pass before the next message can go out (even if the throttle is up).
        private int m_LineCountReductionMax = 15;   // How many lines can reduce the time between messages
        private long m_LineCountReductionMS = 2500;  // How much time to reduce the message delay per line
        private long m_MessageTimeLastMS = 0;
        private bool m_RequiresUserMessages = true; // Require a minimum amount of chat messages to pass before sending its next message.
        private bool m_MessagesReduceTimer = true;

        /// <summary>Max amount of lines that can reduce the wait time (requires messagesReduceTimer)  Defaults to 15.</summary>
        public int lineCountReductionMax
        {
            get { return m_LineCountReductionMax; }
            set { m_LineCountReductionMax = value; }
        }

        /// <summary>Whether you need to meet lineCountMinimum before the throttler becomes ready.  True by default.</summary>
        public bool requiresUserMessages
        {
            get { return m_RequiresUserMessages; }
            set { m_RequiresUserMessages = value; }
        }

        /// <summary>Whether lines sent reduce the timer.  True by default.</summary>
        public bool messagesReduceTimer
        {
            get { return m_MessagesReduceTimer; }
            set { m_MessagesReduceTimer = value; }
        }

        /// <summary>Maximum wait time before the throttler is ready (before lines sent possibly reduce this timer).  Defaults to 60000.</summary>
        public long waitTimeMSMax
        {
            get { return m_WaitTimeMSMax; }
            set { m_WaitTimeMSMax = value; }
        }

        /// <summary>How much time (ms) is subtracted from m_WaitTimeMSMax per line (requires messagesReduceTimer).  Defaults to 2500.</summary>
        public long lineCountReductionMS
        {
            get { return m_LineCountReductionMS; }
            set { m_LineCountReductionMS = value; }
        }

        /// <summary>Minimum amount of lines before the throttler becomes ready (requires requiresUserMessages).  Defaults to 6.</summary>
        public int lineCountMinimum
        {
            get { return m_LineCountMinimum; }
            set { m_LineCountMinimum = value; }
        }

        /// <summary>
        /// Amount of time that's assumed to have passed since throttler was last ready (includes reduction for messages sent, if messagesReduceTimer is true).
        /// </summary>
        public long adjustedThrottleTimeMS
        {
            get
            {
                long messageCountReduction = 0;

                if (m_MessagesReduceTimer)
                    messageCountReduction = (Math.Min(linesSinceLastTrigger, m_LineCountReductionMax));

                return m_WaitTimeMSMax - messageCountReduction;
            }
        }

        /// <summary>
        /// Whether the throttle is still waiting for time to pass (including time reduced for messages sent, if messagesReduceTimer is true).
        /// </summary>
        public bool isTimeUp
        {
            get
            {
                return (jerpBot.instance.actionTimer.ElapsedMilliseconds > (m_MessageTimeLastMS + adjustedThrottleTimeMS));
            }
        }

        /// <summary>
        /// Amount of lines that have passed since the throttler was last triggered.
        /// </summary>
        public long linesSinceLastTrigger
        {
            get
            {
                return Math.Min(jerpBot.instance.lineCount - m_LastLineCount, m_LineCountReductionMax);
            }
        }

        /// <summary>
        /// Whether the throttler is waiting on additional messages to be sent before becoming ready (requires requiresUserMessages to be true).
        /// </summary>
        public bool isWaitingOnLines
        {
            get
            {
                return m_RequiresUserMessages && (m_LineCountMinimum >= linesSinceLastTrigger);
            }
        }

        /// <summary>Whether all requirements have been met.</summary>
        public bool isReady
        {
            get
            {
                if (!m_Initialized)
                {
                    m_MessageTimeLastMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
                    m_Initialized = true;
                }

                return (!isWaitingOnLines && isTimeUp);
            }
        }

        /// <summary>Logs that the desired throttled action occurred and begins to wait for more lines/time before becoming ready.</summary>
        public void trigger()
        {
            m_MessageTimeLastMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
            m_LastLineCount = jerpBot.instance.lineCount;
        }
    }
}
