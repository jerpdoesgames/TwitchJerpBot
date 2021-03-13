using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace JerpDoesBots
{
	class userEntry
	{
		private bool	needsUpdate	= false;	// Whether we need to update this in the database
		private long	lastUpdate		= 0;	// Last time we've updated this in the database
		// private int		level			= 0;
		private bool	initialized = false;
		private bool	inChannel = false;
		private int		viewerID;

		private string	nickname;					// ^[a-zA-Z0-9_]{4,25}$
		public string	Nickname { get { return nickname; } }

		private int		loyalty					= 0;
		private int		points					= 0;
		private bool	isFollower				= false;
		private bool	isSubscriber			= false;
		private bool	isTurbo					= false;
		private bool	isModerator				= false;
		private bool	isBroadcaster			= false;
		private bool	isHosting				= false;
		private int		sessionMessageCount		= 0;
		private int		sessionCommandCount		= 0;

		SQLiteConnection botDatabase;

		public void incrementMessageCount()
		{
			sessionMessageCount++;
			needsUpdate = true;
		}

		public void incrementCommandCount()
		{
			sessionCommandCount++;
			needsUpdate = true;
		}

		public bool InChannel		{ get { return inChannel; } set { inChannel = value; } }
		public bool NeedsUpdate		{ get { return needsUpdate; } }
		public bool IsSubscriber	{ get { return isSubscriber; } set { isSubscriber = value; } }
		public bool IsFollower		{ get { return isFollower; } }
		public bool IsTurbo			{ get { return isTurbo; } }
		public bool IsModerator		{ get { return isModerator; } set { isModerator = value; } }
		public bool IsHosting		{
			get { return isHosting; }
			set { isHosting = value; }
		}

		public bool IsBroadcaster {
			get { return isBroadcaster; }
			set { isBroadcaster = value; }
		}

		public int Level			{ get { return calculateLevel(loyalty, nickname); } }

		public void addPoints(int pointsToAdd)
		{
			points += pointsToAdd;
		}

		public void addLoyalty(int loyaltyToAdd)
		{
			loyalty += loyaltyToAdd;
		}

		public void doUpdate(long updateTime)
		{
			string updateRowQuery = "UPDATE viewers SET loyalty = " + loyalty + ", points = " + points + " WHERE name = '" + nickname + "' LIMIT 1";
			SQLiteCommand updateRowCommand = new SQLiteCommand(updateRowQuery, botDatabase);
			updateRowCommand.ExecuteNonQuery();

			lastUpdate = updateTime;
		}

		private bool convertTagBool(string tagValue)
		{
			if (!string.IsNullOrEmpty(tagValue))
			{
				int tagInt = int.Parse(tagValue);
				return Convert.ToBoolean(tagInt);
			}

			return false;
		}

		public void processTags(string newTags)
		{
			Dictionary<string, string> tagList = new Dictionary<string, string>();
			string[] tagSplit = newTags.Split(';');
			string[] tagPieces;
			if (tagSplit != null && tagSplit.Length > 0)
			{
				for (int tagIndex = 0; tagIndex < tagSplit.Length; tagIndex++)
				{
					tagPieces = tagSplit[tagIndex].Split('=');
					if (tagPieces.Length >= 2)
						tagList.Add(tagPieces[0], tagPieces[1]);
				}
			}

			// user-id=26627520;
			// badges=broadcaster/1,subscriber/0,premium/1;
			// nameColor		= tagList["color"];
			// TODO handle keys not found (whenever someone subs, especially)

			

			isModerator		= tagList.ContainsKey("mod") ? convertTagBool(tagList["mod"]) : false ;
			isTurbo			= tagList.ContainsKey("turbo") ? convertTagBool(tagList["turbo"]) : false;
			isSubscriber	= tagList.ContainsKey("subscriber") ? convertTagBool(tagList["subscriber"]) : false;
		}

		public static int calculateLevel(int userScore, string username = "")
		{
			// TODO: decide on a formula for level

			if (username.ToLower() == "jerp")
				return 9999;

			if (userScore >= 2500)
				return 5;

			if (userScore >= 1750)
				return 4;

			if (userScore >= 1000)
				return 3;

			if (userScore >= 250)
				return 2;

			if (userScore >= 100)
				return 1;

			return 0;
		}

		private bool createUser(string newUsername)
		{
			string createViewerRowQuery = "INSERT INTO viewers (name, loyalty, points) values (@param1, @param2, @param3)";

			SQLiteCommand createViewerRowCommand = new SQLiteCommand(createViewerRowQuery, botDatabase);

			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param1", newUsername));
			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param2", (object)0));
			createViewerRowCommand.Parameters.Add(new SQLiteParameter("@param3", (object)0));

			if (createViewerRowCommand.ExecuteNonQuery() > 0)
			{
				return true;
			}
			return false;
		}

		private bool loadUser(string newUsername)
		{
			string getViewerRowQuery = "SELECT * FROM viewers WHERE name = @param1 LIMIT 1";
			SQLiteCommand getViewerRowCommand = new SQLiteCommand(getViewerRowQuery, botDatabase);
			getViewerRowCommand.Parameters.Add(new SQLiteParameter("@param1", newUsername));
			SQLiteDataReader viewerRowReader = getViewerRowCommand.ExecuteReader();

			if (viewerRowReader.HasRows && viewerRowReader.Read())
			{
				loyalty = Convert.ToInt32(viewerRowReader["loyalty"]);
				points = Convert.ToInt32(viewerRowReader["points"]);
				return true;
			}

			return false;
		}

		public userEntry(string newUsername, SQLiteConnection botData)
		{
			if (!String.IsNullOrEmpty(newUsername))
			{
				botDatabase = botData;

				if (loadUser(newUsername))
					initialized = true;
				else if (createUser(newUsername))
					initialized = true;
				
				if (initialized)
					nickname	= newUsername;
			}
		}
	}
}
