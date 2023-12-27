using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerpDoesBots
{
    /// <summary>
    /// Built by the configuration json for the autoExec system.
    /// </summary>
    internal class autoExecConfig
    {
        private int m_TimerIntervalSeconds = 5;
        /// <summary>
        /// How long to wait when checking whether to execute any activateOnTimer commands.
        /// </summary>
        public int timerIntervalSeconds { get { return m_TimerIntervalSeconds; } set { m_TimerIntervalSeconds = value; } }
        public List<autoExecConfigEntry> entries { get; set; }
    }

    /// <summary>
    /// Entry for the autoExec system - each represents a command or set of commands that are executed when the requirements are met.
    /// </summary>
    internal class autoExecConfigEntry
    {
        public const int COOLDOWN_TIME_MIN_SECONDS = 5; // For safety sake
        /// <summary>
        /// List of messages and/or commands to execute when conditions are met for this entry.
        /// </summary>
        public List<string> commands { get; set; }

        /// <summary>
        /// Requirements to meet for this entry to be considered (alongside cooldownTimeSeconds if activateOnTimer is true).
        /// </summary>
        public streamCondition requirements { get; set; }

        /// <summary>
        /// Whether this entry can be considered for activation at regular intervals.  False by default.
        /// </summary>
        public bool activateOnTimer { get; set; }

        /// <summary>
        /// Whether this entry can be considered for activation when the bot first loads.  False by default.
        /// </summary>
        public bool activateOnBotLoad { get; set; }

        /// <summary>
        /// Whether this entry can be considered for activation when the category changes.  False by default.
        /// </summary>
        public bool activateOnCategoryChange { get; set; }

        private int m_cooldownTimeSeconds = COOLDOWN_TIME_MIN_SECONDS;
        /// <summary>
        /// How long to wait before this entry can be considered after it was last activated (minimum of COOLDOWN_TIME_MIN_SECONDS).
        /// </summary>
        public int cooldownTimeSeconds { get { return m_cooldownTimeSeconds; } set { m_cooldownTimeSeconds = Math.Max(COOLDOWN_TIME_MIN_SECONDS, value); } }
        private double m_LastActivationTimeMS = -1;
        public double lastActivationTimeMS { get { return m_LastActivationTimeMS; } set { m_LastActivationTimeMS = value; } }

        /// <summary>
        /// Whether this entry can be activated with a message containing one or more specific terms (trigger words/phrases).  False by default.  Requires at least one string in messageTermsToCheck.  Uses cooldownTimeSeconds to prevent message spam.
        /// </summary>
        public bool activateOnMessageTerm { get; set; }

        /// <summary>
        /// Specific terms or phrases that can cause this entry to activate (by default, all terms must be included in one message).  Requires activateOnMessageTerm to be true.
        /// </summary>
        public List<string> messageTermsToCheck { get; set; }

        /// <summary>
        /// Allows any individual message term to be valid for activating this entry.  Defaults to false.  Requires at least one string in messageTermsToCheck and activateOnMessageTerm must be true.
        /// </summary>
        public bool messageTermsUseORCheck { get; set; }
    }
}
