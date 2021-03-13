using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;

using System.Web.Script.Serialization;
using System.Diagnostics;

using System.Web;

namespace JerpDoesBots
{


	public class api_streams_entry
	{

	}

	public class api_streams_response
	{
		public List<api_streams_entry> streams { get; set; }
	}

	public class hosts_hosterEntry
	{
		public uint host_id { get; set; }
		public uint target_id { get; set; }
		public string host_login { get; set; }
		public string host_display_name { get; set; }
		public string target_display_name { get; set; }
	}

	public class hostedList
	{
		public List<hosts_hosterEntry> hosts { get; set; }
	}

	// =====================

	class twitchAPI	// Kraken API request manager
	{
		private Queue<twitchAPIRequest> requestQueue;
		private Stopwatch				requestTimer;
		private long					requestQueueThrottle	= 2500;
		private long					requestTimeLast			= 0;
		private jerpBot					botBrain;
		private logger					APILog;
		private string					clientID;
        private int                     m_ChannelID;

        private void getChannelInfo(userEntry infoUser)
        {
            string infoNickname = infoUser.Nickname;

            WebClient newClient = new WebClient();
            newClient.Headers.Add("Client-ID", clientID);
            newClient.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");
            string jsonString = newClient.DownloadString("https://api.twitch.tv/kraken/channels/" + m_ChannelID); // TODO: System.Net.WebException
            if (!String.IsNullOrEmpty(jsonString))
            {
                twitchAPIResults.channelInfo infoResult = new JavaScriptSerializer().Deserialize<twitchAPIResults.channelInfo>(jsonString);

                if (infoNickname == botBrain.getConnectionUser())
                {
                    if (!string.IsNullOrEmpty(infoResult.game))
                        botBrain.Game = infoResult.game;

                    if (!string.IsNullOrEmpty(infoResult.status))
                        botBrain.Title = infoResult.status;
                }
            }
        }

		private void getChannelStatus(userEntry statusUser)
		{
			string statusNickname = statusUser.Nickname;

			if (!String.IsNullOrEmpty(statusNickname))
			{
				WebClient newClient = new WebClient();
				newClient.Headers.Add("Client-ID", clientID);
                newClient.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");    // ye olde...
                string jsonString = newClient.DownloadString("https://api.twitch.tv/kraken/streams/" + m_ChannelID); // TODO: System.Net.WebException: 'The remote server returned an error: (500) Internal Server Error.'
				if (!String.IsNullOrEmpty(jsonString))
				{
                    twitchAPIResults.channelStatus statusResult = new JavaScriptSerializer().Deserialize<twitchAPIResults.channelStatus>(jsonString);

                    if (statusNickname == botBrain.getConnectionUser())
                    {
                        if (statusResult.stream != null)
                        {
                            botBrain.setViewerCount(statusResult.stream.viewers);

                            string statusMessage = "Recorded viewers for user " + statusNickname + " is " + statusResult.stream.viewers;

                            if (!String.IsNullOrEmpty(statusResult.stream.game))
                                statusMessage += ".  Playing " + statusResult.stream.game;

                            APILog.write(statusMessage);
                            botBrain.setLive(true);
                        }
                        else
                        {
                            botBrain.setLive(false);
                            botBrain.setViewerCount(0);
                        }
                    }
				}
			}
		}

		public string getChannelChatServer(string channelName)
		{
			if (!String.IsNullOrEmpty(channelName))
			{
				WebClient newClient = new WebClient();
				// newClient.Headers.Add("Client-ID", clientID);
				string jsonString = newClient.DownloadString("http://tmi.twitch.tv/servers?channel=" + channelName);
				if (!String.IsNullOrEmpty(jsonString))
				{
					JObject resultObject = JObject.Parse(jsonString);
					if (!string.IsNullOrEmpty((string)resultObject["servers"][0]))
					{
						string serverFirst = (string)resultObject["servers"][0];

						if (!string.IsNullOrEmpty(serverFirst))	// Will this ever need to connect to a server other than the first?
							return serverFirst;
					}
				}
			}
			return "";
		}

		public List<string> getChannelHosters(string channelName)	// TODO: Make this support more than one channel
		{
			if (!String.IsNullOrEmpty(channelName))
			{
				WebClient newClient = new WebClient();
				// newClient.Headers.Add("Client-ID", clientID);
				string jsonString = newClient.DownloadString("https://tmi.twitch.tv/hosts?include_logins=1&target=26627520"); // TODO: System.Net.WebException
				if (!String.IsNullOrEmpty(jsonString))
				{
					hostedList hostsResult = new JavaScriptSerializer().Deserialize<hostedList>(jsonString);
					if (hostsResult.hosts != null)
					{
						List<string> hosterList = new List<string>();
						hosts_hosterEntry hostCheck;
						for (int i=0; i < hostsResult.hosts.Count; i++)
						{
							hostCheck = hostsResult.hosts[i];
							if (!string.IsNullOrEmpty(hostCheck.host_login))
								hosterList.Add(hostCheck.host_login);
						}
						return hosterList;
					}
				}
			}
			return null;
		}

		private void executeRequest(twitchAPIRequest requestToExecute)
		{
            userEntry requestUser = botBrain.checkCreateUser(requestToExecute.getTarget());
            switch (requestToExecute.getRequestType())
			{
                case twitchAPIRequest.types.channelInfo:
                    if (requestUser != null)
                    {
                        getChannelInfo(requestUser);
                    }
                    return;
                case twitchAPIRequest.types.channelStatus:
                    if (requestUser != null)
                    {
                        getChannelStatus(requestUser);
                    }
                    return;
                case twitchAPIRequest.types.hostedList:
					if (!string.IsNullOrEmpty(requestToExecute.getTarget()))
					{
						List<string> hostedList = getChannelHosters(requestToExecute.getTarget());
						foreach (userEntry hostingUser in botBrain.UserList.Values)
							hostingUser.IsHosting = hostedList.Contains(hostingUser.Nickname);
						/*
						if (hostedList.Count > 0)
							botBrain.sendDefaultChannelMessage("Being hosted by " + string.Join(", ", hostedList));
						*/
					}
					return;

			}
		}

		private void processRequestQueue()
		{
			if (requestQueue.Count > 0)
			{
				if (requestTimer.ElapsedMilliseconds > requestTimeLast + requestQueueThrottle)
				{
					twitchAPIRequest requestToExecute = requestQueue.Dequeue();
					executeRequest(requestToExecute);
					requestTimeLast = requestTimer.ElapsedMilliseconds;
				}
			}
		}

		public void queueRequest(twitchAPIRequest requestToExecute)
		{
			requestQueue.Enqueue(requestToExecute);
		}

		public void frame()
		{
			processRequestQueue();
		}

		public twitchAPI(jerpBot newBotBrain, logger newAPILog, string newClientID, int aChannelID)
		{
			botBrain		= newBotBrain;
			APILog			= newAPILog;
			requestQueue	= new Queue<twitchAPIRequest>();
			requestTimer	= Stopwatch.StartNew();
			clientID		= newClientID;
            m_ChannelID     = aChannelID;
		}
	}
}
