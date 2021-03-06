using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace JerpDoesBots
{
	class userEntry
	{
		private bool	m_NeedsUpdate	= false;	// Whether we need to update this in the database
		private long	m_LastUpdate		= 0;	// Last time we've updated this in the database
		// private int		level			= 0;
		private bool	m_Initialized = false;
		private bool	m_InChannel = false;
		private int		m_ViewerID;
		private string	m_Nickname;					// ^[a-zA-Z0-9_]{4,25}$
		public string	Nickname { get { return m_Nickname; } }

		private int		m_Loyalty					= 0;
		private int		m_Points					= 0;
		private bool	m_IsFollower				= false;
		private bool	m_IsSubscriber			= false;
		private bool	m_IsTurbo					= false;
		private bool	m_IsModerator				= false;
		private bool	m_IsBroadcaster			= false;
		private bool	m_IsHosting				= false;
		private int		m_SessionMessageCount		= 0;
		private int		m_SessionCommandCount		= 0;

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

		public bool inChannel		{ get { return m_InChannel; } set { m_InChannel = value; } }
		public bool needsUpdate		{ get { return m_NeedsUpdate; } }
		public bool isSubscriber	{ get { return m_IsSubscriber; } set { m_IsSubscriber = value; } }
		public bool isFollower		{ get { return m_IsFollower; } }
		public bool isTurbo			{ get { return m_IsTurbo; } }
		public bool isModerator		{ get { return m_IsModerator; } set { m_IsModerator = value; } }
		public bool isHosting		{
			get { return m_IsHosting; }
			set { m_IsHosting = value; }
		}

		public bool isBroadcaster {
			get { return m_IsBroadcaster; }
			set { m_IsBroadcaster = value; }
		}

		public int level			{ get { return calculateLevel(m_Loyalty, m_Nickname); } }

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

		public static int calculateLevel(int aUserScore, string aUsername = "")
		{
			// TODO: decide on a formula for level

			if (aUsername.ToLower() == "jerp")
				return 9999;

			if (aUserScore >= 2500)
				return 5;

			if (aUserScore >= 1750)
				return 4;

			if (aUserScore >= 1000)
				return 3;

			if (aUserScore >= 250)
				return 2;

			if (aUserScore >= 100)
				return 1;

			return 0;
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

		public userEntry(string aUsername, SQLiteConnection aBotData)
		{
			if (!String.IsNullOrEmpty(aUsername))
			{
				botDatabase = aBotData;

				if (loadUser(aUsername))
					m_Initialized = true;
				else if (createUser(aUsername))
					m_Initialized = true;
				
				if (m_Initialized)
					m_Nickname	= aUsername;
			}
		}
	}
}
