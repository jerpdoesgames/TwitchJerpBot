using TwitchLib.PubSub.Events;

namespace JerpDoesBots
{
    class botModule
	{
		/// <summary>
		/// Occurs every time the program's main loop runs.
		/// </summary>
		public virtual void onFrame() {}
		/// <summary>
		/// Occurs when the bot has fully loaded, including all modules and successfully connecting to chat.
		/// </summary>
        public virtual void onBotFullyLoaded() { }
		// TODO: onCategoryIDChanged while offline.
		/// <summary>
		/// Occurs when the category changes.  Typically only triggers when swapping categories while live.
		/// </summary>
		public virtual void onCategoryIDChanged() { }
		/// <summary>
		/// Occurs when the stream transitions from offline to live.  This includes if the stream is live when the bot first loads.
		/// </summary>
        public virtual void onStreamLive() { }
        /// <summary>
        /// Occurs when the stream transitions from live to offline.  Bot loads assuming the stream is offline, so onBotFullyLoaded() should be used instead if working on activate-on-load features.
        /// </summary>
        public virtual void onStreamOffline() { }
		/// <summary>
		/// Occurs on any user message - not commands and not messages from the bot itself (though the owner's messages will still be checked).
		/// </summary>
		/// <param name="aUser">User who sent the message.</param>
		/// <param name="aMessage">Message being sent.</param>
        public virtual void onUserMessage(userEntry aUser, string aMessage) {}
		/// <summary>
		/// Occurs whenever a user joins the channel (really, any channels the bot is in).
		/// </summary>
		/// <param name="aUser">User who joined.</param>
        public virtual void onUserJoin(userEntry aUser) {}
		/// <summary>
		/// Unused - should occur when the bot receives a private message.
		/// </summary>
		/// <param name="aUser">User who sent the message.</param>
		/// <param name="aMessage">Message sent by the user.</param>
        public virtual void onPrivateMessage(userEntry aUser, string aMessage) {}
		/// <summary>
		/// Occurs whenever a user redeems a channel point reward on Twitch.
		/// </summary>
		/// <param name="aUser">User who redeemed the reward.</param>
		/// <param name="aRewardTitle">Title text for the reward.</param>
		/// <param name="aRewardCost">Point cost of the reward.</param>
		/// <param name="aRewardUserInput">Any text the user has entered as part of redeeming the reward.</param>
		/// <param name="aRewardID">The ID of the reward being redeemed.</param>
		/// <param name="aRedemptionID">The ID of the redemption instance for this reward.</param>
		public virtual void onChannelPointRedemption(userEntry aUser, string aRewardTitle, int aRewardCost, string aRewardUserInput, string aRewardID, string aRedemptionID) { }
		/// <summary>
		/// Occurs when an ad starts on Twitch.
		/// </summary>
		/// <param name="aCommercialArgs">Data about the commercial being played including its length.</param>
		public virtual void onCommercialStart(OnCommercialArgs aCommercialArgs) { }
		
		public virtual void onRaid(string aHostName, int aViewerCount) { }

		private bool m_RequiresConnection	= true;
		private bool m_RequiresChannel		= true;
		private bool m_RequiresPM			= false;

		public bool requiresConnection { get { return m_RequiresConnection; } }
		public bool requiresChannel { get { return m_RequiresChannel; } }
		public bool requiresPM { get { return m_RequiresPM; } }

		protected jerpBot m_BotBrain;

		public botModule(jerpBot aJerpBot, bool aRequiresConnection = true, bool aRequiresChannel = true, bool aRequiresPM = false)
		{
			m_BotBrain				= aJerpBot;
			m_RequiresConnection	= aRequiresConnection;
			m_RequiresChannel		= aRequiresChannel;
			m_RequiresPM			= aRequiresPM;

			m_BotBrain.addModule(this);
		} 
	}
}
