using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TwitchLib.Api.Core.Exceptions;

namespace JerpDoesBots
{
    public enum autoShoutUserType : int
    {
        streamer,            // Default
        messageOnly
    };

    class autoShoutoutUser
	{
        /// <summary>
        /// Name of the user being shouted out (case-insitive to search, will match the case of the name listed in the shoutout config.
        /// </summary>
		public string name { get; set; }
        /// <summary>
        /// Message to display when shouting out the user.
        /// </summary>
		public string shoutMessage { get; set; }
        /// <summary>
        /// Commands to execute (or additional messages to send) when shouting out the user.
        /// </summary>
        public List<string> shoutCommands { get; set; }
        /// <summary>
        /// Used to define whether this includes a link to a stream or it's a message-only shoutout.
        /// </summary>
        public autoShoutUserType type { get; set; }
	}

	class autoShoutoutConfig
	{
		public List<autoShoutoutUser> users { get; set; }
	}

	class autoShoutout : botModule
	{
		private autoShoutoutConfig configData;
		public bool loaded = false;
		private long m_ShoutThrottleMS = 1000 * 60 * 60 * 2;  // 2 hours (MS/S * S * M * S)  Should ideally be longer than the API times
		private long m_APIShoutThrottleMS = (1000 * 60 * 2) + 30000;    // 2 minutes (MS/S * S * M) - plus an extra 30s as a buffer
		private long m_APIShoutThrottlePerUserMS = (1000 * 60 * 60) + 300000; // 60 minutes + 5 as a buffer (MS/S * S * M) + 300000
		private string m_LastShoutedNickname = string.Empty;
		private bool m_APIShoutEnabled = true;
        private long m_APILastShoutMS = -1;

		public override void onUserMessage(userEntry shoutUserEntry, string aMessage)
		{
            autoShoutoutUser shoutUser = configData.users.Find(x => x.name.ToLower() == shoutUserEntry.Nickname.ToLower());
            if (shoutUser != null)
            {
                shoutoutInternal(shoutUserEntry.Nickname);
            }
        }

		private void shoutoutInternal(string aChannelName, bool aAnnounceErrors = false)	// TODO: Actually announce errors
		{
			if (!string.IsNullOrEmpty(aChannelName))
			{
				userEntry userToShout = jerpBot.instance.checkCreateUser(aChannelName);

                if (userToShout.lastShoutoutTimeMS == -1 || jerpBot.instance.actionTimer.ElapsedMilliseconds > (userToShout.lastShoutoutTimeMS + m_ShoutThrottleMS))	// On global cooldown?
				{
					bool apiShoutAvailable = (
                        (
                            m_APILastShoutMS == -1 ||
                            jerpBot.instance.actionTimer.ElapsedMilliseconds > m_APILastShoutMS + m_APIShoutThrottleMS
                        ) &&
                        (
                            m_LastShoutedNickname != userToShout.Nickname ||
                            jerpBot.instance.actionTimer.ElapsedMilliseconds > userToShout.lastShoutoutTimeMS + m_APIShoutThrottlePerUserMS
                        )
                    );
                    autoShoutoutUser shoutUser = configData.users.Find(x => x.name.ToLower() == aChannelName.ToLower());
                    bool isMessageOnly = (shoutUser != null && shoutUser.type == autoShoutUserType.messageOnly);

					TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation channelInfo = null;

					if (!isMessageOnly)
						channelInfo = jerpBot.instance.getSingleChannelInfoByName(userToShout.Nickname);

					bool didAPIShoutout = false;

                    if (m_APIShoutEnabled && apiShoutAvailable && channelInfo != null && (shoutUser == null || shoutUser.type != autoShoutUserType.messageOnly))
					{
						try
						{
                            Task shoutTask = Task.Run(() => jerpBot.instance.twitchAPI.Helix.Chat.SendShoutoutAsync(jerpBot.instance.ownerUserID, channelInfo.BroadcasterId, jerpBot.instance.ownerUserID));
                            shoutTask.Wait();
							didAPIShoutout = true;
                            m_LastShoutedNickname = userToShout.Nickname;
                            userToShout.lastShoutoutTimeMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
                            m_APILastShoutMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;

                            if (shoutUser != null && !string.IsNullOrEmpty(shoutUser.shoutMessage) && shoutUser.type == autoShoutUserType.streamer) // Streamer check unncessary for now, but keeping in case other types are added later.
                            {
                                jerpBot.instance.sendDefaultChannelAnnounce(string.Format(jerpBot.instance.localizer.getString("shoutoutMessageCustomAPI"), channelInfo.BroadcasterName, shoutUser.shoutMessage));
                            }
                        }
						catch (AggregateException e) // TODO: make exeption handling less stupid
						{
							if (e.InnerException is HttpResponseException)
							{
								HttpResponseException eResponse = (HttpResponseException) e.InnerException;
                                Task<string> contentTask = Task.Run(() => eResponse.HttpResponse.Content.ReadAsStringAsync());
    
                                contentTask.Wait();

                                string contentString = contentTask.Result;
                                jerpBot.instance.logWarningsErrors.writeAndLog(string.Format("AggregateException when trying to shoutout user: \"{0}\": {1}", userToShout.Nickname, contentString));
                            }
							else
							{
                                jerpBot.instance.logWarningsErrors.writeAndLog(string.Format("AggregateException when trying to shoutout user: \"{0}\": {1}", userToShout.Nickname, e.Message));
                            }
                        }
						catch(Exception e)
						{
                            jerpBot.instance.logWarningsErrors.writeAndLog(string.Format("General exception when trying to shoutut user: \"{0}\": {1}", userToShout.Nickname, e.Message));
                            if (e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message))
                            {
                                jerpBot.instance.logWarningsErrors.writeAndLog(string.Format("Inner Exception: {0}", e.InnerException.Message));
                            }
                        }
                    }

					if (!didAPIShoutout)
					{
                        string lastGame = "";

                        if (channelInfo != null && !string.IsNullOrEmpty(channelInfo.GameName))
                            lastGame = "  " + string.Format(jerpBot.instance.localizer.getString("shoutoutLastPlaying"), channelInfo.GameName);
                        
						if (shoutUser != null && !string.IsNullOrEmpty(shoutUser.shoutMessage))
                        {
                            switch (shoutUser.type)
                            {
                                case autoShoutUserType.messageOnly:
                                    jerpBot.instance.sendDefaultChannelAnnounce(shoutUser.shoutMessage);
                                    break;

                                case autoShoutUserType.streamer:
                                    jerpBot.instance.sendDefaultChannelAnnounce(string.Format(jerpBot.instance.localizer.getString("shoutoutMessageCustom"), channelInfo.BroadcasterName, shoutUser.shoutMessage, channelInfo.BroadcasterName.ToLower()) + lastGame);
                                    break;
                            }
                        }
						else
						{
                            jerpBot.instance.sendDefaultChannelAnnounce(string.Format(jerpBot.instance.localizer.getString("shoutoutMessage"), channelInfo.BroadcasterName, channelInfo.BroadcasterName.ToLower()) + lastGame);
                        }
                        m_LastShoutedNickname = userToShout.Nickname;
                        userToShout.lastShoutoutTimeMS = jerpBot.instance.actionTimer.ElapsedMilliseconds;
                    }

                    if (shoutUser != null && shoutUser.shoutCommands != null && shoutUser.shoutCommands.Count > 0)
                    {
                        foreach (string curCommand in shoutUser.shoutCommands)
                        {
                            jerpBot.instance.messageOrCommand(curCommand);
                        }
                    }
                    
                }
			}
		}

        public void shoutout(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            string nickname = jerpBot.getFirstTokenString(argumentString);
			shoutoutInternal(nickname, true);
        }

        public void enableAPI(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			m_APIShoutEnabled = true;
            if (aSilent)
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("shoutoutAPIEnabled"));
        }

        public void disableAPI(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_APIShoutEnabled = false;
            if (!aSilent)
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("shoutoutAPIDisabled"));
        }
        private bool load()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_shoutouts.json");
            if (File.Exists(configPath))
            {
                string configFileString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(configFileString))
                {
                    configData = new JavaScriptSerializer().Deserialize<autoShoutoutConfig>(configFileString);
                    return true;
                }
            }

            return false;
        }

        public void reload(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            loaded = load();
            if (loaded)
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("shoutoutReloadSuccess"));
            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("shoutoutReloadFail"));
            }
        }

        public autoShoutout() : base(true, true, false)
		{
            loaded = load();

            if (loaded)
            {
                chatCommandDef tempDef = new chatCommandDef("shoutout", shoutout, true, false);
                tempDef.addSubCommand(new chatCommandDef("enableapi", enableAPI, true, false));
                tempDef.addSubCommand(new chatCommandDef("disableapi", disableAPI, true, false));
                tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));

                jerpBot.instance.addChatCommand(tempDef);
            }
        }
	}
}
