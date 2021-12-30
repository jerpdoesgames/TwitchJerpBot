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
			Task<GetPredictionsResponse> lastPredictionTask = Task.Run(() => m_BotBrain.twitchAPI.Helix.Predictions.GetPredictionsAsync(m_BotBrain.OwnerID, null, null, 1));
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

		public void displayLast(userEntry commandUser, string argumentString)
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
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionDisplayLast"), lastPrediction.Title, winningOutcome.Title));
						}
						else
                        {
							m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionDisplayLastNoOutcome"), lastPrediction.Title));
						}
						break;

					case PredictionStatus.ACTIVE:
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionDisplayLastActive"), lastPrediction.Title));
						break;

					case PredictionStatus.LOCKED:
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionDisplayLastLocked"), lastPrediction.Title));
						break;

					case PredictionStatus.CANCELED:
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionDisplayLastCanceled"), lastPrediction.Title));
						break;

					default:
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionDisplayLastFail"));
						break;
				}
            }
			else
            {
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionDisplayLastFail"));
			}
        }


		public void cancel(userEntry commandUser, string argumentString)
        {
			try
            {
				Prediction lastPrediction = getLastPrediction();
				if (lastPrediction != null && (lastPrediction.Status == PredictionStatus.ACTIVE || lastPrediction.Status == PredictionStatus.LOCKED))
				{
					Task cancelTask = Task.Run(() => m_BotBrain.twitchAPI.Helix.Predictions.EndPredictionAsync(m_BotBrain.OwnerID, lastPrediction.Id, PredictionEndStatus.CANCELED));
					cancelTask.Wait();

					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCancelSuccess"));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCancelFailStatus"));
				}
			}
			catch (Exception e)
            {
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCancelFail"));
			}
        }

		public void close(userEntry commandUser, string argumentString)
        {
			try
			{
				Prediction lastPrediction = getLastPrediction();
				if (lastPrediction != null && lastPrediction.Status == PredictionStatus.ACTIVE)
				{
					Task closeTask = Task.Run(() => m_BotBrain.twitchAPI.Helix.Predictions.EndPredictionAsync(m_BotBrain.OwnerID, lastPrediction.Id, PredictionEndStatus.LOCKED));
					closeTask.Wait();

					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCloseSuccess"));
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCloseFailStatus"));
				}
			}
			catch (Exception e)
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCloseFail"));
			}
		}

		public void decide(userEntry commandUser, string argumentString)
        {
			try
			{
				Prediction lastPrediction = getLastPrediction();
				if (lastPrediction != null && lastPrediction.Status == PredictionStatus.LOCKED)
				{
					int outcomeIndex;

					if  (Int32.TryParse(argumentString, out outcomeIndex) && (outcomeIndex == 1 || outcomeIndex == 2) && lastPrediction.Outcomes.Length == 2)
                    {
						Task decideTask = Task.Run(() => m_BotBrain.twitchAPI.Helix.Predictions.EndPredictionAsync(m_BotBrain.OwnerID, lastPrediction.Id, PredictionEndStatus.RESOLVED, lastPrediction.Outcomes[outcomeIndex - 1].Id));
						decideTask.Wait();

						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionDecideSuccess"), lastPrediction.Outcomes[outcomeIndex - 1].Title));
					}
					else
                    {
						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionDecideFailOutcomeInvalid"));
					}
				}
				else
				{
					m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionDecideFailStatus"));
				}
			}
			catch (Exception e)
			{
				m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionDecideFail"));
			}
		}

		private bool isValidOutcome(string aOutcome)
        {
			return (!string.IsNullOrEmpty(aOutcome) && aOutcome.Length > 0 && aOutcome.Length <= 25);
        }

		public void create(userEntry commandUser, string argumentString)
        {
			string[] argArray = argumentString.Split('|');
			if (argArray.Length >= 5)
            {
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionCreateFail"), m_BotBrain.Localizer.getString("predictionCreateFailOutcomeCount")));
			}
			else if (argArray.Length == 4)
            {
				string failReasons = "";

				int duration;
				bool durationValid = false;
				if (Int32.TryParse(argArray[0], out duration) && duration >= 1 && duration <= 1800)
					durationValid = true;
				else
					failReasons += m_BotBrain.Localizer.getString("predictionCreateFailArgCount");

				string title = argArray[1];
				bool titleValid = false;

				if (!string.IsNullOrEmpty(title) && title.Length > 1 && title.Length <= 45)
					titleValid = true;
				else
					failReasons += m_BotBrain.Localizer.getString("predictionCreateFailTitleLength");

				string outcome1 = argArray[2];
				bool outcome1Valid = false;

				if (isValidOutcome(outcome1))
					outcome1Valid = true;
				else
					failReasons += string.Format(m_BotBrain.Localizer.getString("predictionCreateFailOutcomeInvalid"), 1);

				string outcome2 = argArray[3];
				bool outcome2Valid = false;

				if (isValidOutcome(outcome2))
					outcome2Valid = true;
				else
					failReasons += string.Format(m_BotBrain.Localizer.getString("predictionCreateFailOutcomeInvalid"), 2);

				if (durationValid && titleValid && outcome1Valid && outcome2Valid)
                {
					try
					{
						TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome outcome1Object = new TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome() { Title = outcome1 };
						TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome outcome2Object = new TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome() { Title = outcome2 };
						TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome[] outcomeList = { outcome1Object, outcome2Object };

						CreatePredictionRequest newPredictionRequest = new CreatePredictionRequest()
						{
							BroadcasterId = m_BotBrain.OwnerID,
							Title = title,
							PredictionWindowSeconds = duration,
							Outcomes = outcomeList
						};

						Task<CreatePredictionResponse> createTask = Task.Run(() => m_BotBrain.twitchAPI.Helix.Predictions.CreatePredictionAsync(newPredictionRequest));
						createTask.Wait();

						m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("predictionCreateSuccess"));
					}
					catch (Exception e)
                    {
						m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionCreateFail"), m_BotBrain.Localizer.getString("predictionCreateFailUnknown")));
					}
                }
				else
                {
					m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionCreateFail"), failReasons));
				}
			}
			else
            {
				m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("predictionCreateFail"), m_BotBrain.Localizer.getString("predictionCreateFailArgCount")));
			}
        }

		public predictionManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
		{
			chatCommandDef tempDef = new chatCommandDef("prediction", null, false, false);
			tempDef.addSubCommand(new chatCommandDef("last", displayLast, true, true));
			tempDef.addSubCommand(new chatCommandDef("create", create, true, false));
			tempDef.addSubCommand(new chatCommandDef("close", close, true, false));
			tempDef.addSubCommand(new chatCommandDef("cancel", cancel, true, false));
			tempDef.addSubCommand(new chatCommandDef("decide", decide, true, false));
			m_BotBrain.addChatCommand(tempDef);
		}
	}
}