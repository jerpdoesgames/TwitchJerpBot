using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.PubSub.Models.Responses.Messages.Redemption;

namespace JerpDoesBots
{
    internal class pointRewardManager : botModule
    {
        private static List<pointReward> m_Rewards;
        private static int m_LastUpdateRewardsAdded = 0;
        private static int m_LastUpdateRewardsRemoved = 0;
        private static int m_LastUpdateRewardsUpdated = 0;

        public static int lastUpdateRewardsAdded { get { return m_LastUpdateRewardsAdded; } }
        public static int lastUpdateRewardsRemoved { get { return m_LastUpdateRewardsRemoved; } }
        public static int lastUpdateRewardsUpdated { get { return m_LastUpdateRewardsUpdated; } }

        public static bool updateRewardRedemptionStatus(string aRewardID, string aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus aStatus)
        {
            List<string> redemptionIDs = new List<string>() { aRedemptionID };

            TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest updateRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest();
            updateRequest.Status = aStatus;

            try
            {
                Task<TwitchLib.Api.Helix.Models.ChannelPoints.UpdateRedemptionStatus.UpdateRedemptionStatusResponse> refundRedemptionTask = jerpBot.instance.twitchAPI.Helix.ChannelPoints.UpdateRedemptionStatusAsync(jerpBot.instance.ownerUserID, aRewardID, redemptionIDs, updateRequest);
                refundRedemptionTask.Wait();

                if (refundRedemptionTask.Result != null)
                {
                    return true;
                }
                else
                {
                    jerpBot.instance.logWarningsErrors.writeAndLog("Failed channel point redemption refund request (API)");
                    return false;
                }
            }
            catch (Exception e)
            {
                jerpBot.instance.logWarningsErrors.writeAndLog("Failed channel point redemption refund request (exception): " + e.Message);
            }

            return false;
        }

        public static void updateRemoteRewardsFromLocalData()
        {
            int rewardsAdded = 0;
            int rewardsRemoved = 0;
            int rewardsUpdated = 0;

            CustomReward[] remoteRewards = getRemoteRewardData();

            foreach (pointReward curReward in m_Rewards)
            {
                bool hasRewardID = !string.IsNullOrEmpty(curReward.rewardID);
                if (!hasRewardID && curReward.shouldExistOnTwitch)
                {
                    addReward(curReward);
                    rewardsAdded++;
                }
                else if (hasRewardID && !curReward.shouldExistOnTwitch)
                {
                    removeReward(curReward);
                    rewardsRemoved++;
                }
                else if (hasRewardID && curReward.shouldExistOnTwitch)
                {
                    CustomReward foundRemoteReward = remoteRewards.ToList().Find(rewardtoCheck => rewardtoCheck.Id == curReward.rewardID);
                    if (foundRemoteReward != null)
                    {
                        if (checkUpdateReward(curReward, foundRemoteReward))
                        {
                            rewardsUpdated++;
                        }
                    }
                }
            }

            m_LastUpdateRewardsAdded = rewardsAdded;
            m_LastUpdateRewardsRemoved = rewardsRemoved;
            m_LastUpdateRewardsUpdated = rewardsUpdated;
        }

        private static bool checkUpdateReward(pointReward aLocalData, CustomReward aRemoteData)
        {
            bool wasUpdated = false;
            bool needsUpdate = false;

            TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest updateRewardRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardRequest();

            if (aLocalData.title != aRemoteData.Title)
            {
                updateRewardRequest.Title = aLocalData.title;
                needsUpdate = true;
            }
                

            if (aLocalData.description != aRemoteData.Prompt)
            {
                updateRewardRequest.Title = !string.IsNullOrEmpty(aLocalData.title) ? aLocalData.title : "";
                needsUpdate = true;
            }
                

            if (aLocalData.cost != aRemoteData.Cost)
            {
                updateRewardRequest.Cost = aLocalData.cost;
                needsUpdate = true;
            }
                

            if ((aLocalData.maxPerStream != -1 && !aRemoteData.MaxPerStreamSetting.IsEnabled) || (aLocalData.maxPerStream == -1 && aRemoteData.MaxPerStreamSetting.IsEnabled))
            {
                updateRewardRequest.IsMaxPerStreamEnabled = aLocalData.maxPerStream != -1;
                if (aLocalData.maxPerStream != -1)
                    updateRewardRequest.MaxPerStream = aLocalData.maxPerStream;

                needsUpdate = true;
            }

            if ((aLocalData.maxPerUserPerStream != -1 && !aRemoteData.MaxPerUserPerStreamSetting.IsEnabled) || (aLocalData.maxPerUserPerStream == -1 && aRemoteData.MaxPerUserPerStreamSetting.IsEnabled))
            {
                updateRewardRequest.IsMaxPerUserPerStreamEnabled = aLocalData.maxPerUserPerStream != -1;
                if (aLocalData.maxPerUserPerStream != -1)
                    updateRewardRequest.MaxPerUserPerStream = aLocalData.maxPerUserPerStream;

                needsUpdate = true;
            }

            if (aLocalData.backgroundColor != aRemoteData.BackgroundColor)
            {
                updateRewardRequest.BackgroundColor = aLocalData.backgroundColor;
                needsUpdate = true;
            }

            if ((aLocalData.globalCooldownSeconds != -1 && !aRemoteData.GlobalCooldownSetting.IsEnabled) || (aLocalData.globalCooldownSeconds == -1 && aRemoteData.GlobalCooldownSetting.IsEnabled))
            {
                updateRewardRequest.IsGlobalCooldownEnabled = aLocalData.globalCooldownSeconds != -1;
                if (aLocalData.globalCooldownSeconds != -1)
                    updateRewardRequest.GlobalCooldownSeconds = aLocalData.globalCooldownSeconds;

                needsUpdate = true;
            }

            if (aLocalData.requireUserInput != aRemoteData.IsUserInputRequired)
            {
                updateRewardRequest.IsUserInputRequired = aLocalData.requireUserInput;
                needsUpdate = true;
            }

            if (aLocalData.autoFulfill != aRemoteData.ShouldRedemptionsSkipQueue)
            {
                updateRewardRequest.ShouldRedemptionsSkipRequestQueue = aLocalData.autoFulfill;
                needsUpdate = true;
            }

            if (aLocalData.enabled != aRemoteData.IsEnabled)
            {
                updateRewardRequest.IsEnabled = aLocalData.enabled;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                try
                {
                    Task<TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward.UpdateCustomRewardResponse> updateRewardTask = jerpBot.instance.twitchAPI.Helix.ChannelPoints.UpdateCustomRewardAsync(jerpBot.instance.ownerUserID, aLocalData.rewardID, updateRewardRequest);
                    updateRewardTask.Wait();
                }
                catch (Exception e)
                {
                    jerpBot.instance.logWarningsErrors.writeAndLog("Failed to update channel point reward named: " + aLocalData.title + " | " + e.Message);
                }

                wasUpdated = true;
            }

            return wasUpdated;
        }

        public static pointReward addUpdatePointReward(pointReward aReward)
        {
            pointReward existingReward = m_Rewards.Find(checkReward => checkReward.title == aReward.title);
            if (existingReward != null)
            {
                existingReward.title = aReward.title;
                existingReward.description = aReward.description;
                existingReward.cost = aReward.cost;
                existingReward.maxPerStream = aReward.maxPerStream;
                existingReward.maxPerUserPerStream = aReward.maxPerUserPerStream;
                existingReward.backgroundColor = aReward.backgroundColor;
                existingReward.globalCooldownSeconds = aReward.globalCooldownSeconds;
                existingReward.requireUserInput = aReward.requireUserInput;
                existingReward.autoFulfill = aReward.autoFulfill;
                existingReward.enabled = aReward.enabled;
                existingReward.shouldExistOnTwitch = aReward.shouldExistOnTwitch;
                return existingReward;
            }
            else
            {
                m_Rewards.Add(aReward);
                return aReward;
            }
        }

        public static TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest getCreateRequest(pointReward aReward)
        {
            TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest newRewardRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest();
            newRewardRequest.Title = aReward.title;
            newRewardRequest.Cost = aReward.cost;

            if (!string.IsNullOrEmpty(aReward.description))
                newRewardRequest.Prompt = aReward.description;

            if (!string.IsNullOrEmpty(aReward.backgroundColor))
                newRewardRequest.BackgroundColor = aReward.backgroundColor;

            if (aReward.maxPerUserPerStream >= 1)
            {
                newRewardRequest.MaxPerUserPerStream = aReward.maxPerUserPerStream;
                newRewardRequest.IsMaxPerUserPerStreamEnabled = true;
            }

            if (aReward.maxPerStream >= 1)
            {
                newRewardRequest.MaxPerStream = aReward.maxPerStream;
                newRewardRequest.IsMaxPerStreamEnabled = true;
            }

            if (aReward.globalCooldownSeconds >= 1)
            {
                newRewardRequest.GlobalCooldownSeconds = aReward.globalCooldownSeconds;
                newRewardRequest.IsGlobalCooldownEnabled = true;
            }

            newRewardRequest.ShouldRedemptionsSkipRequestQueue = aReward.autoFulfill;
            newRewardRequest.IsUserInputRequired = aReward.requireUserInput;

            newRewardRequest.IsEnabled = aReward.enabled;

            return newRewardRequest;
        }

        private static void addReward(pointReward aRewardToAdd)
        {
            try
            {
                TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest createRewardRequest = getCreateRequest(aRewardToAdd);
                Task<TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsResponse> createRewardTask = jerpBot.instance.twitchAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(jerpBot.instance.ownerUserID, createRewardRequest);
                createRewardTask.Wait();

                if (createRewardTask.Result == null)
                {
                    jerpBot.instance.logWarningsErrors.writeAndLog("Failed to create channel point reward named: " + aRewardToAdd.title);
                }
                else
                {
                    aRewardToAdd.rewardID = createRewardTask.Result.Data[0].Id;
                }
            }
            catch (Exception e)
            {
                jerpBot.instance.logWarningsErrors.writeAndLog(string.Format("Exception when trying to create channel point reward named: \"{0}\": {1}", aRewardToAdd.title, e.Message));
            }
        }

        private static void removeReward(pointReward aRewardToRemove)
        {
            try
            {
                Task removeRewardTask = jerpBot.instance.twitchAPI.Helix.ChannelPoints.DeleteCustomRewardAsync(jerpBot.instance.ownerUserID, aRewardToRemove.rewardID);
                removeRewardTask.Wait();

                aRewardToRemove.rewardID = null;
            }
            catch (Exception e)
            {
                jerpBot.instance.logWarningsErrors.writeAndLog(string.Format("Exception when trying to remove channel point reward named: \"{0}\": {1}", aRewardToRemove.rewardID, e.Message));
            }
        }

        private static void removeReward(pointReward[] aRewardsToRemove)
        {
            foreach (pointReward curReward in aRewardsToRemove)
            {
                removeReward(curReward);
            }
        }

        private static void addReward(pointReward[] aRewardsToAdd)
        {
            foreach (pointReward curReward in aRewardsToAdd)
            {
                addReward(curReward);
            }
        }

        private static CustomReward[] getRemoteRewardData()
        {
            Task<TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward.GetCustomRewardsResponse> getRewardsTask = jerpBot.instance.twitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(jerpBot.instance.ownerUserID);
            getRewardsTask.Wait();

            if (getRewardsTask.Result != null)
            {
                return getRewardsTask.Result.Data;
            }
            else
            {
                jerpBot.instance.logWarningsErrors.writeAndLog("pointRewardManager.getRemoteRewardData - Null result from getRewardsTask");
                return null;
            }
        }

        private static bool updateLocalRewardsFromRemoteData()
        {
            CustomReward[] rewardData = getRemoteRewardData();

            if (rewardData != null)
            {
                foreach (CustomReward curReward in rewardData)
                {
                    pointReward existingReward = m_Rewards.Find(checkReward => checkReward.rewardID == curReward.Id);
                    pointReward createUpdateReward = existingReward != null ? existingReward : new pointReward();

                    createUpdateReward.title = curReward.Title;
                    createUpdateReward.description = curReward.Prompt;
                    createUpdateReward.cost = curReward.Cost;
                    createUpdateReward.maxPerStream = curReward.MaxPerStreamSetting.IsEnabled ? curReward.MaxPerStreamSetting.MaxPerStream : -1;
                    createUpdateReward.maxPerUserPerStream = curReward.MaxPerUserPerStreamSetting.IsEnabled ? curReward.MaxPerUserPerStreamSetting.MaxPerUserPerStream: -1;
                    createUpdateReward.backgroundColor = curReward.BackgroundColor;
                    createUpdateReward.globalCooldownSeconds = curReward.GlobalCooldownSetting.IsEnabled ? curReward.GlobalCooldownSetting.GlobalCooldownSeconds : -1;
                    createUpdateReward.requireUserInput = curReward.IsUserInputRequired;
                    createUpdateReward.autoFulfill = curReward.ShouldRedemptionsSkipQueue;
                    createUpdateReward.enabled = curReward.IsEnabled;
                    createUpdateReward.rewardID = curReward.Id;
                    createUpdateReward.shouldExistOnTwitch = true;  // Assume it should be there.

                    if (existingReward == null)
                    {
                        m_Rewards.Add(createUpdateReward);
                    }
                }
                return true;
            }
            return false;
        }

        public void updatelocal(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (updateLocalRewardsFromRemoteData())
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("channelPointRewardsUpdateLocalFromRemoteSuccess"));
            }
            else
            {
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("channelPointRewardsUpdateLocalFromRemoteFail"));
            }
        }

        public pointRewardManager() : base(true, true, false)
        {
            m_Rewards = new List<pointReward>();
            chatCommandDef tempDef = new chatCommandDef("points", null, false, false);
            tempDef.addSubCommand(new chatCommandDef("updatelocal", updatelocal, false, false));
            jerpBot.instance.addChatCommand(tempDef);

            updateLocalRewardsFromRemoteData();
        }
    }
}
