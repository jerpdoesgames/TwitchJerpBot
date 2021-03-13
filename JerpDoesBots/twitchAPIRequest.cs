namespace JerpDoesBots
{
	class twitchAPIRequest
	{
		public enum types : int {
			userFollowingChannel,			// https://api.twitch.tv/kraken/users/jerp/follows/channels/tehcannonfodder
			followerList,					// https://api.twitch.tv/kraken/channels/jerp/follows (default limit is... 20?)
											// https://api.twitch.tv/kraken/channels/jerp/follows?limit=5
			channelChatServer,              // http://tmi.twitch.tv/servers?channel=jerp
			hostedList,                     // https://tmi.twitch.tv/hosts?include_logins=1&target=[channelID]
                                            // Channel ID is numeric, and NOT THE USERNAME for the channel
            channelInfo,                    // https://api.twitch.tv/kraken/channels/jerp
            channelStatus                   // TODO: Deprecated by December 2018 (all Kraken stuff)
        };

		private botCommand postRequestCommand;
		private types requestType;
		private string target;

		public	types	getRequestType()			{ return requestType; }
		public	string	getTarget()					{ return target; }
		public	void	setTarget(string newTarget)	{ target = newTarget; }

		public twitchAPIRequest(types newRequestType, botCommand newPostRequestCommand = null)
		{
			if (newPostRequestCommand != null)
			{
				postRequestCommand = newPostRequestCommand;
			}

			requestType = newRequestType;
		}
	}
}
