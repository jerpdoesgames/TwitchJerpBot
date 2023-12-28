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
		public string name { get; set; }
		public string shoutMessage { get; set; }
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
				userEntry userToShout = m_BotBrain.checkCreateUser(aChannelName);

                if (userToShout.lastShoutoutTimeMS == -1 || m_BotBrain.actionTimer.ElapsedMilliseconds > (userToShout.lastShoutoutTimeMS + m_ShoutThrottleMS))	// On global cooldown?
				{
					bool apiShoutAvailable = (
                        (
                            m_APILastShoutMS == -1 ||
                            m_BotBrain.actionTimer.ElapsedMilliseconds > m_APILastShoutMS + m_APIShoutThrottleMS
                        ) &&
                        (
                            m_LastShoutedNickname != userToShout.Nickname ||
                            m_BotBrain.actionTimer.ElapsedMilliseconds > userToShout.lastShoutoutTimeMS + m_APIShoutThrottlePerUserMS
                        )
                    );
                    autoShoutoutUser shoutUser = configData.users.Find(x => x.name.ToLower() == aChannelName.ToLower());
                    bool isMessageOnly = (shoutUser != null && shoutUser.type == autoShoutUserType.messageOnly);

					TwitchLib.Api.Helix.Models.Channels.GetChannelInformation.ChannelInformation channelInfo = null;

					if (!isMessageOnly)
						channelInfo = m_BotBrain.getSingleChannelInfoByName(userToShout.Nickname);

					bool didAPIShoutout = false;

                    if (m_APIShoutEnabled && apiShoutAvailable && channelInfo != null && (shoutUser == null || shoutUser.type != autoShoutUserType.messageOnly))
					{
						try
						{
                            Task shoutTask = Task.Run(() => m_BotBrain.twitchAPI.Helix.Chat.SendShoutoutAsync(m_BotBrain.ownerUserID, channelInfo.BroadcasterId, m_BotBrain.ownerUserID));
                            shoutTask.Wait();
							didAPIShoutout = true;
                            m_LastShoutedNickname = userToShout.Nickname;
                            userToShout.lastShoutoutTimeMS = m_BotBrain.actionTimer.ElapsedMilliseconds;
                            m_APILastShoutMS = m_BotBrain.actionTimer.ElapsedMilliseconds;

                            if (shoutUser != null && !string.IsNullOrEmpty(shoutUser.shoutMessage) && shoutUser.type == autoShoutUserType.streamer) // Streamer check unncessary for now, but keeping in case other types are added later.
                            {
                                m_BotBrain.sendDefaultChannelAnnounce(string.Format(m_BotBrain.localizer.getString("shoutoutMessageCustomAPI"), channelInfo.BroadcasterName, shoutUser.shoutMessage));
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
                                m_BotBrain.logWarningsErrors.writeAndLog(string.Format("AggregateException when trying to shoutout user: \"{0}\": {1}", userToShout.Nickname, contentString));
                            }
							else
							{
                                m_BotBrain.logWarningsErrors.writeAndLog(string.Format("AggregateException when trying to shoutout user: \"{0}\": {1}", userToShout.Nickname, e.Message));
                            }
                        }
						catch(Exception e)
						{
                            m_BotBrain.logWarningsErrors.writeAndLog(string.Format("General exception when trying to shoutut user: \"{0}\": {1}", userToShout.Nickname, e.Message));
                            if (e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message))
                            {
                                m_BotBrain.logWarningsErrors.writeAndLog(string.Format("Inner Exception: {0}", e.InnerException.Message));
                            }
                        }
                    }

					if (!didAPIShoutout)
					{
                        string lastGame = "";

                        if (channelInfo != null && !string.IsNullOrEmpty(channelInfo.GameName))
                            lastGame = "  " + string.Format(m_BotBrain.localizer.getString("shoutoutLastPlaying"), channelInfo.GameName);
                        
						if (shoutUser != null && !string.IsNullOrEmpty(shoutUser.shoutMessage))
                        {
                            switch (shoutUser.type)
                            {
                                case autoShoutUserType.messageOnly:
                                    m_BotBrain.sendDefaultChannelAnnounce(shoutUser.shoutMessage);
                                    break;

                                case autoShoutUserType.streamer:
                                    m_BotBrain.sendDefaultChannelAnnounce(string.Format(m_BotBrain.localizer.getString("shoutoutMessageCustom"), channelInfo.BroadcasterName, shoutUser.shoutMessage, channelInfo.BroadcasterName.ToLower()) + lastGame);
                                    break;
                            }
                        }
						else
						{
                            m_BotBrain.sendDefaultChannelAnnounce(string.Format(m_BotBrain.localizer.getString("shoutoutMessage"), channelInfo.BroadcasterName, channelInfo.BroadcasterName.ToLower()) + lastGame);
                        }
                        m_LastShoutedNickname = userToShout.Nickname;
                        userToShout.lastShoutoutTimeMS = m_BotBrain.actionTimer.ElapsedMilliseconds;
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
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("shoutoutAPIEnabled"));
        }

        public void disableAPI(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_APIShoutEnabled = false;
            if (!aSilent)
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("shoutoutAPIDisabled"));
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
                    m_BotBrain.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("shoutoutReloadSuccess"));
            }
            else
            {
                m_BotBrain.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("shoutoutReloadFail"));
            }
        }

        public autoShoutout(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
            loaded = load();

            if (loaded)
            {
                chatCommandDef tempDef = new chatCommandDef("shoutout", shoutout, true, false);
                tempDef.addSubCommand(new chatCommandDef("enableapi", enableAPI, true, false));
                tempDef.addSubCommand(new chatCommandDef("disableapi", disableAPI, true, false));
                tempDef.addSubCommand(new chatCommandDef("reload", reload, false, false));

                m_BotBrain.addChatCommand(tempDef);
            }
        }
	}
}
