using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace JerpDoesBots
{
	class userEntry
	{
		private bool m_NeedsUpdate = false; // Whether we need to update this in the database
		private long m_LastUpdate = 0;  // Last time we've updated this in the database
										// private int		level			= 0;
		private bool m_Initialized = false;
		private bool m_InChannel = false;
		private int m_ViewerID;
		private string m_Nickname;                  // ^[a-zA-Z0-9_]{4,25}$
		public string Nickname { get { return m_Nickname; } }

		private int m_Loyalty = 0;
		private int m_Points = 0;
		private bool m_IsFollower = false;
		private bool m_isVIP = false;
		private bool m_isPartner = false;

		private bool m_IsSubscriber = false;
		private bool m_IsTurbo = false;
		private bool m_IsModerator = false;
		private bool m_IsBroadcaster = false;
		private bool m_IsHosting = false;
		private int m_SessionMessageCount = 0;
		private int m_SessionCommandCount = 0;
		private bool m_IsBrb = false;
		private string m_TwitchUserID;

		private SQLiteConnection botDatabase;

		public void incrementMessageCount()
		{
			m_SessionMessageCount++;
			m_NeedsUpdate = true;
		}

		public void incrementCommandCount()
		{
			m_SessionCommandCount++;
			m_NeedsUpdate = true;
		}

		public bool inChannel { get { return m_InChannel; } set { m_InChannel = value; } }
		public bool needsUpdate { get { return m_NeedsUpdate; } }
		public bool isSubscriber { get { return m_IsSubscriber; } set { m_IsSubscriber = value; } }
		public bool isFollower { get { return m_IsFollower; } set { m_IsFollower = value; } }
		public bool isVIP { get { return m_isVIP; } set { m_isVIP = value; } }
		public bool isPartner { get { return m_isPartner; } set { m_isPartner = value; } }

		public bool isTurbo { get { return m_IsTurbo; } }
		public bool isModerator { get { return m_IsModerator; } set { m_IsModerator = value; } }

		public bool isBrb { get { return m_IsBrb; } set { m_IsBrb = value; } }

		public string twitchUserID { get { return m_TwitchUserID; } set { m_TwitchUserID = value; } }

		private DateTime m_LastFollowCheckTime;
		public DateTime lastFollowCheckTime { get; set; }
 
		public bool isHosting		{
			get { return m_IsHosting; }
			set { m_IsHosting = value; }
		}

		public bool isBroadcaster {
			get { return m_IsBroadcaster; }
			set { m_IsBroadcaster = value; }
		}

		public void addPoints(int pointsToAdd)
		{
			m_Points += pointsToAdd;
		}

		public void addLoyalty(int loyaltyToAdd)
		{
			m_Loyalty += loyaltyToAdd;
		}

		public void doUpdate(long updateTime)
		{
			string updateRowQuery = "UPDATE viewers SET loyalty = @param1, points = @param2 WHERE name = @param3 LIMIT 1";
			SQLiteCommand updateRowCommand = new SQLiteCommand(updateRowQuery, botDatabase);
			updateRowCommand.Parameters.Add(new SQLiteParameter("@param1", m_Loyalty));
			updateRowCommand.Parameters.Add(new SQLiteParameter("@param2", m_Points));
			updateRowCommand.Parameters.Add(new SQLiteParameter("@param3", m_Nickname));
			updateRowCommand.ExecuteNonQuery();

			m_LastUpdate = updateTime;
		}

		private bool createUser(string aUsername)
		{
			string createViewerRowQuery = "INSERT INTO viewers (name, loyalty, points) values (@param1, @param2, @param3)";

			SQLiteCommand createViewerRowCommand = new SQLiteCommand(createViewerRowQuery, botDatabase);

			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param1", aUsername));
			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param2", (object)0));
			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param3", (object)0));

			if (createViewerRowCommand.ExecuteNonQuery() > 0)
			{
				return true;
			}
			return false;
		}

		private bool loadUser(string aUsername)
		{
			string getViewerRowQuery = "SELECT * FROM viewers WHERE name = @param1 LIMIT 1";
			SQLiteCommand getViewerRowCommand = new SQLiteCommand(getViewerRowQuery, botDatabase);
			getViewerRowCommand.Parameters.Add(new SQLiteParameter("@param1", aUsername));
			SQLiteDataReader viewerRowReader = getViewerRowCommand.ExecuteReader();

			if (viewerRowReader.HasRows && viewerRowReader.Read())
			{
				m_Loyalty = Convert.ToInt32(viewerRowReader["loyalty"]);
				m_Points = Convert.ToInt32(viewerRowReader["points"]);
				return true;
			}

			return false;
		}

		public userEntry(string aNickname, SQLiteConnection aBotData)
		{
			if (!String.IsNullOrEmpty(aNickname))
			{
				botDatabase = aBotData;

				if (loadUser(aNickname))
					m_Initialized = true;
				else if (createUser(aNickname))
					m_Initialized = true;
				
				if (m_Initialized)
					m_Nickname	= aNickname;
			}
		}
	}
}
