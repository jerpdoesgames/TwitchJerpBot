using System;
using System.Threading.Tasks;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions.CreatePrediction;
using TwitchLib.Api.Helix.Models.Predictions.GetPredictions;
using TwitchLib.Api.Helix.Models.Predictions;

namespace JerpDoesBots
{
	class predictionManager : botModule
	{
		public Prediction getLastPrediction()
		{
			Task<GetPredictionsResponse> lastPredictionTask = Task.Run(() => jerpBot.instance.twitchAPI.Helix.Predictions.GetPredictionsAsync(jerpBot.instance.ownerUserID, null, null, 1));
			lastPredictionTask.Wait();

			if (lastPredictionTask.Result != null)
            {
				return lastPredictionTask.Result.Data[0];
            }

			return null;
        }

		public TwitchLib.Api.Helix.Models.Predictions.Outcome getOutcomeById(Prediction aPrediction, string aOutcomeID)
        {
			for(int i = 0; i < aPrediction.Outcomes.Length; i++)
            {
				if (aPrediction.Outcomes[i].Id == aOutcomeID)
					return aPrediction.Outcomes[i];
            }

			return null;
        }

		/// <summary>
		/// Display last prediction and its outcome.
		/// </summary>
		/// <param name="commandUser">User attempting to display the last prediction.</param>
		/// <param name="argumentString"></param>
		/// <param name="aSilent"></param>
		public void displayLast(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			Prediction lastPrediction = getLastPrediction();
			if (lastPrediction != null)
            {
                switch (lastPrediction.Status)
				{
					case PredictionStatus.RESOLVED:
						TwitchLib.Api.Helix.Models.Predictions.Outcome winningOutcome = getOutcomeById(lastPrediction, lastPrediction.WinningOutcomeId);

						if (winningOutcome != null)
						{
							jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionDisplayLast"), lastPrediction.Title, winningOutcome.Title));
						}
						else
                        {
							jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionDisplayLastNoOutcome"), lastPrediction.Title));
						}
						break;

					case PredictionStatus.ACTIVE:
						jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionDisplayLastActive"), lastPrediction.Title));
						break;

					case PredictionStatus.LOCKED:
						jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionDisplayLastLocked"), lastPrediction.Title));
						break;

					case PredictionStatus.CANCELED:
						jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionDisplayLastCanceled"), lastPrediction.Title));
						break;

					default:
						jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionDisplayLastFail"));
						break;
				}
            }
			else
            {
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionDisplayLastFail"));
			}
        }

		/// <summary>
		/// Cancels an active/locked prediction.
		/// </summary>
		/// <param name="commandUser">User attempting to cancel this prediction.</param>
		/// <param name="argumentString">Unused.</param>
		/// <param name="aSilent">Whether to output a message on success.</param>
		public void cancel(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			try
            {
				Prediction lastPrediction = getLastPrediction();
				if (lastPrediction != null && (lastPrediction.Status == PredictionStatus.ACTIVE || lastPrediction.Status == PredictionStatus.LOCKED))
				{
					Task cancelTask = Task.Run(() => jerpBot.instance.twitchAPI.Helix.Predictions.EndPredictionAsync(jerpBot.instance.ownerUserID, lastPrediction.Id, PredictionEndStatus.CANCELED));
					cancelTask.Wait();

					if (!aSilent)
						jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCancelSuccess"));
				}
				else
				{
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCancelFailStatus"));
				}
			}
			catch (Exception e)
            {
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCancelFail") + " : " + e.Message);
			}
        }

		/// <summary>
		/// Close (lock) an prediction so no additional votes can be made.
		/// </summary>
		/// <param name="commandUser">User closing (locking) this prediction.</param>
		/// <param name="argumentString">Unused.</param>
		/// <param name="aSilent">Whether to output a message when the prediction is successfully closed (locked).</param>
		public void close(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			try
			{
				Prediction lastPrediction = getLastPrediction();
				if (lastPrediction != null && lastPrediction.Status == PredictionStatus.ACTIVE)
				{
					Task closeTask = Task.Run(() => jerpBot.instance.twitchAPI.Helix.Predictions.EndPredictionAsync(jerpBot.instance.ownerUserID, lastPrediction.Id, PredictionEndStatus.LOCKED));
					closeTask.Wait();

					if (!aSilent)
						jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCloseSuccess"));
				}
				else
				{
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCloseFailStatus"));
				}
			}
			catch (Exception e)
			{
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCloseFail") + " : " + e.Message);
			}
		}

		/// <summary>
		/// Announce the outcome of a prediction.
		/// </summary>
		/// <param name="commandUser">User announcing the oucome of a prediction.</param>
		/// <param name="argumentString">Integer index of the outcome which won (1 or 2).</param>
		/// <param name="aSilent">Whether to output a message on success.</param>
		public void decide(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			try
			{
				Prediction lastPrediction = getLastPrediction();
				if (lastPrediction != null && lastPrediction.Status == PredictionStatus.LOCKED)
				{
					int outcomeIndex;

					if  (Int32.TryParse(argumentString, out outcomeIndex) && (outcomeIndex == 1 || outcomeIndex == 2) && lastPrediction.Outcomes.Length == 2)
                    {
						Task decideTask = Task.Run(() => jerpBot.instance.twitchAPI.Helix.Predictions.EndPredictionAsync(jerpBot.instance.ownerUserID, lastPrediction.Id, PredictionEndStatus.RESOLVED, lastPrediction.Outcomes[outcomeIndex - 1].Id));
						decideTask.Wait();

						if (!aSilent)
							jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionDecideSuccess"), lastPrediction.Outcomes[outcomeIndex - 1].Title));
					}
					else
                    {
						jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionDecideFailOutcomeInvalid"));
					}
				}
				else
				{
					jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionDecideFailStatus"));
				}
			}
			catch (Exception e)
			{
				jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionDecideFail") + " : " + e.Message);
			}
		}

		private bool isValidOutcome(string aOutcome)
        {
			return (!string.IsNullOrEmpty(aOutcome) && aOutcome.Length > 0 && aOutcome.Length <= 25);
        }

		/// <summary>
		/// Create a new prediction using the Twitch API.
		/// </summary>
		/// <param name="commandUser">User creating the prediction.</param>
		/// <param name="argumentString">List of arguments (separated by slash [/]) required to start a prediction: duration in seconds/title/outcome 1/outcome 2.</param>
		/// <param name="aSilent">Whether to output on success.</param>
		public void create(userEntry commandUser, string argumentString, bool aSilent = false)
        {
			string[] argArray = argumentString.Split('/');
			if (argArray.Length >= 5)
            {
				jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionCreateFail"), jerpBot.instance.localizer.getString("predictionCreateFailOutcomeCount")));
			}
			else if (argArray.Length == 4)
            {
				string failReasons = "";

				int duration;
				bool durationValid = false;
				if (Int32.TryParse(argArray[0], out duration) && duration >= 1 && duration <= 1800)
					durationValid = true;
				else
					failReasons += jerpBot.instance.localizer.getString("predictionCreateFailArgCount");

				string title = argArray[1];
				bool titleValid = false;

				if (!string.IsNullOrEmpty(title) && title.Length > 1 && title.Length <= 45)
					titleValid = true;
				else
					failReasons += jerpBot.instance.localizer.getString("predictionCreateFailTitleLength");

				string outcome1 = argArray[2];
				bool outcome1Valid = false;

				if (isValidOutcome(outcome1))
					outcome1Valid = true;
				else
					failReasons += string.Format(jerpBot.instance.localizer.getString("predictionCreateFailOutcomeInvalid"), 1);

				string outcome2 = argArray[3];
				bool outcome2Valid = false;

				if (isValidOutcome(outcome2))
					outcome2Valid = true;
				else
					failReasons += string.Format(jerpBot.instance.localizer.getString("predictionCreateFailOutcomeInvalid"), 2);

				if (durationValid && titleValid && outcome1Valid && outcome2Valid)
                {
					try
					{
						TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome outcome1Object = new TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome() { Title = outcome1 };
						TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome outcome2Object = new TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome() { Title = outcome2 };
						TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome[] outcomeList = { outcome1Object, outcome2Object };

						CreatePredictionRequest newPredictionRequest = new CreatePredictionRequest()
						{
							BroadcasterId = jerpBot.instance.ownerUserID,
							Title = title,
							PredictionWindowSeconds = duration,
							Outcomes = outcomeList
						};

						Task<CreatePredictionResponse> createTask = Task.Run(() => jerpBot.instance.twitchAPI.Helix.Predictions.CreatePredictionAsync(newPredictionRequest));
						createTask.Wait();

						if (!aSilent)
							jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("predictionCreateSuccess"));
					}
					catch (Exception e)
                    {
						jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionCreateFail"), jerpBot.instance.localizer.getString("predictionCreateFailUnknown") + " : " +  e.Message));
					}
                }
				else
                {
					jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionCreateFail"), failReasons));
				}
			}
			else
            {
				jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("predictionCreateFail"), jerpBot.instance.localizer.getString("predictionCreateFailArgCount")));
			}
        }

		/// <summary>
		/// Module for interacting with Twitch's predictions API (a "gamba"/etc. type system where people vote for outcomes using channel points).
		/// </summary>
		/// <param name="aJerpBot">Will eventually be removed - reference to jerpBot.</param>
		public predictionManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			chatCommandDef tempDef = new chatCommandDef("prediction", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("last", displayLast, true, true));
			tempDef.addSubCommand(new chatCommandDef("create", create, true, false));
			tempDef.addSubCommand(new chatCommandDef("close", close, true, false));
			tempDef.addSubCommand(new chatCommandDef("cancel", cancel, true, false));
			tempDef.addSubCommand(new chatCommandDef("decide", decide, true, false));
			jerpBot.instance.addChatCommand(tempDef);
		}
	}
}