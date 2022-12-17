using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using NAudio.Wave;
using System.Threading.Tasks;

namespace JerpDoesBots
{

    public class soundCommandDef
    {
        public string name { get; set; }
        // public string path { get; set; }
        public List<string> paths { get; set; }
        public long lastUsed;
        public float volume { get; set; }
        public bool isValidForPointReward { get; set; }
        public bool isMandatoryReward { get; set; }
        public bool existsOnTwitch { get; set; }
        public string rewardID { get; set; }
        public string source { get; set; }
        public string character { get; set; }
        public string description { get; set; }
        // public Dictionary<string, long> userLastUsed;

    }

    public class soundCommandConfig
    {
        public List<soundCommandDef> soundList { get; set; }
        public bool enabled { get; set; }
        public int useDevice { get; set; }
        public float globalVolume { get; set; }
        public int pointRewardCountMax { get; set; } // Maximum amount of reward slots to allocate for sounds.  Selects mandatory first and then fills with a random selection from the rest.
        public int pointRewardCostDefault { get; set; }

        public soundCommandConfig()
        {
            useDevice = -1;
            globalVolume = 1.0f;
            pointRewardCountMax = 0;
            pointRewardCostDefault = 50;
        }
    }

    class soundCommands : botModule
    {
        public const long COOLDOWN_GLOBAL_ALL       = 7500; // Entire system
        public const long COOLDOWN_GLOBAL_PERSOUND  = 17500; // Force some sound variety by allowing other sounds to be played
        public const long COOLDOWN_PERUSER          = 30000;
        private const int CUSTOM_REWARDS_MAX        = 50; // TODO: Move elsehwere

        private soundCommandConfig m_Config;
        private soundCommandDef m_lastSound;
        private WaveOutEvent m_OutputEvent;
        private int m_DeviceNumber = -1;

        private float m_GlobalVolume = 1.0f;
        private bool m_IsEnabled = false;

        private List<soundCommandDef> currentPointRewards;

        public bool isEnabled { get { return m_IsEnabled; } set { m_IsEnabled = value; } }


        public TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest getCreateRewardRequest(soundCommandDef aCurDef)
        {
            TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest newRewardRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest();
            newRewardRequest.Title = "Play Sound - " + aCurDef.name;
            newRewardRequest.Cost = m_Config.pointRewardCostDefault;

            if (!string.IsNullOrEmpty(aCurDef.description))
            {
                if (!string.IsNullOrEmpty(aCurDef.source))
                {
                    newRewardRequest.Prompt = "From "+aCurDef.source+":\n"+aCurDef.description;
                }
                else
                {
                    newRewardRequest.Prompt = aCurDef.description;
                }
            }
            else
            {
                newRewardRequest.Prompt = "Play a sound!";
            }

            newRewardRequest.BackgroundColor = "#222222";

            newRewardRequest.GlobalCooldownSeconds = (int)Math.Ceiling((double)(COOLDOWN_GLOBAL_ALL / 1000));
            newRewardRequest.IsGlobalCooldownEnabled = true;

            newRewardRequest.ShouldRedemptionsSkipRequestQueue = false;
            newRewardRequest.IsUserInputRequired = false;

            newRewardRequest.IsEnabled = true;

            return newRewardRequest;
        }

        private bool onCooldown(soundCommandDef aSound, userEntry commandUser)
        {
            if (m_OutputEvent.PlaybackState == PlaybackState.Playing)
                return true;

            if (commandUser.isBroadcaster)
                return false;

            if (
                (m_BotBrain.actionTimer.ElapsedMilliseconds + COOLDOWN_GLOBAL_ALL > aSound.lastUsed) &&
                (aSound != m_lastSound || m_BotBrain.actionTimer.ElapsedMilliseconds + COOLDOWN_GLOBAL_PERSOUND > aSound.lastUsed)
            )
            {
                return false;
            }

            return true;
        }

        private bool updateSoundRewardRedemptionStatus(string aRewardID, string aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus aStatus)
        {

            List<string> redemptionIDs = new List<string>();
            redemptionIDs.Add(aRedemptionID);

            TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest updateRequest = new TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus.UpdateCustomRewardRedemptionStatusRequest();
            updateRequest.Status = aStatus;

            try
            {
                Task<TwitchLib.Api.Helix.Models.ChannelPoints.UpdateRedemptionStatus.UpdateRedemptionStatusResponse> refundRedemptionTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.UpdateRedemptionStatusAsync(m_BotBrain.ownerUserID, aRewardID, redemptionIDs, updateRequest);
                refundRedemptionTask.Wait();

                if (refundRedemptionTask.Result != null)
                {
                    return true;
                }
                else
                {
                    m_BotBrain.logWarningsErrors.writeAndLog("Failed channel point redemption refund request (API)");
                    return false;
                }
            }
            catch (Exception e)
            {
                m_BotBrain.logWarningsErrors.writeAndLog("Failed channel point redemption refund request (exception): " + e.Message);
            }

            return false;
        }

        public override void onChannelPointRedemption(userEntry aMessageUser, string aRewardTitle, int aRewardCost, string aRewardUserInput, string aRewardID, string aRedemptionID)
        {
            bool needRefund = false;
            if (isEnabled)
            {
                soundCommandDef foundSound = null;
                foreach (soundCommandDef curSound in m_Config.soundList)
                {
                    if (curSound.existsOnTwitch && curSound.rewardID == aRewardID)
                    {
                        foundSound = curSound;
                        break;
                    }
                }

                if (foundSound != null)
                {
                    if (playSoundInternal(aMessageUser, foundSound))
                    {
                        m_BotBrain.logGeneral.writeAndLog("Sound reward redemption by " + aMessageUser.Nickname + " - " + foundSound.name);
                        if (!updateSoundRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED))
                        {
                            //Error state since I couldn't mark fulfilled
                        }
                    }
                }
                else
                {
                    needRefund = true;
                }
            }
            else
            {
                needRefund = true;
            }

            if (needRefund)
            {
                // TODO: Fail reason/output (see raffle)
                if (!updateSoundRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED))
                {
                    // m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("raffleRewardRedeemStatusCanceledFail"), aMessageUser.Nickname));
                }
            }
        }

        private bool playSoundInternal(userEntry commandUser, soundCommandDef curSound, bool isRandom = false)
        {
            int pathCount = curSound.paths.Count;
            if (pathCount > 0 && !onCooldown(curSound, commandUser))
            {
                string baseSoundPath;
                if (pathCount > 1)
                {
                    int soundIndex = m_BotBrain.randomizer.Next(0, pathCount);

                    baseSoundPath = curSound.paths[soundIndex];
                }
                else
                {
                    baseSoundPath = curSound.paths[0];
                }

                string soundPath = System.IO.Path.Combine(jerpBot.storagePath, "sounds\\" + baseSoundPath);

                if (File.Exists(soundPath))
                {
                    
                    AudioFileReader audioFile = new AudioFileReader(soundPath);
                    m_OutputEvent.DeviceNumber = m_DeviceNumber;
                    m_OutputEvent.Init(audioFile);

                    float soundVolume = m_GlobalVolume;
                    if (curSound.volume > 0)
                        soundVolume *= curSound.volume;

                    m_OutputEvent.Volume = Math.Min(soundVolume, 1.0f);
                    m_OutputEvent.Play();

                    curSound.lastUsed = m_BotBrain.actionTimer.ElapsedMilliseconds;
                    m_lastSound = curSound;

                    if (isRandom)
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("soundPlayRandom"), curSound.name));

                    return true;
                }
            }

            return false;
        }

        public bool soundExists(string aSound)
        {
            if (!m_IsEnabled)
                return false;

            soundCommandDef curSound;
            for (int i = 0; i < m_Config.soundList.Count; i++)
            {
                curSound = m_Config.soundList[i];
                if (curSound.name == aSound)
                {
                    return true;
                }
            }
            return false;
        }

        public void playSound(userEntry commandUser, string argumentString)
        {
            if (!m_IsEnabled)
                return;

            soundCommandDef curSound;
            for (int i=0; i < m_Config.soundList.Count; i++)
            {
                curSound = m_Config.soundList[i];
                if (curSound.name == argumentString)
                {
                    playSoundInternal(commandUser, curSound);
                    break;
                }
            }
        }

        // This is for people like Jerp who haven't updated all of their rigs to Win10
        public void setDevice(userEntry commandUser, string argumentString)
        {
            bool success = false;
            int deviceNumber;
            if (Int32.TryParse(argumentString, out deviceNumber))
            {
                if (deviceNumber >= -1 && deviceNumber < WaveOut.DeviceCount)
                {
                    m_DeviceNumber = deviceNumber;
                    m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("soundSetDeviceNumberSuccess"), deviceNumber, WaveOut.GetCapabilities(deviceNumber).ProductName));
                    success = true;
                }
            }

            if (!success)
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("soundSetDeviceNumberFailRange"), WaveOut.DeviceCount - 1));
        }

        public void getDeviceList(userEntry commandUser, string argumentString)
        {
            for (int i=0; i < WaveOut.DeviceCount; i++)
            {
                WaveOutCapabilities curDevice = WaveOut.GetCapabilities(i);
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("soundDeviceListEntry"), i, curDevice.ProductName));
            }
        }

        public void setVolume(userEntry commandUser, string argumentString)
        {
            float newVolume;
            if (float.TryParse(argumentString, out newVolume))
            {
                newVolume = Math.Min(newVolume, 1.0f);
                m_GlobalVolume = newVolume;
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.localizer.getString("soundSetGlobalVolume"), newVolume));
            }
        }

        public void enable(userEntry commandUser, string argumentString)
        {
            m_IsEnabled = true;

            m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("soundEnabled"));
        }

        public void disable(userEntry commandUser, string argumentString)
        {
            m_IsEnabled = false;

            m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("soundDisabled"));
        }

        public void getList(userEntry commandUser, string argumentString)
        {
            m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("soundList"));
        }

        private bool attemptAddReward(soundCommandDef aCurSound)
        {
            try
            {
                TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsRequest createRewardRequest = getCreateRewardRequest(aCurSound);
                Task<TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward.CreateCustomRewardsResponse> createRewardTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.CreateCustomRewardsAsync(m_BotBrain.ownerUserID, createRewardRequest);
                createRewardTask.Wait();

                if (createRewardTask.Result == null)
                {
                    m_BotBrain.logWarningsErrors.writeAndLog("Failed to create channel point reward named: " + aCurSound.name);
                    return false;
                }
                else
                {
                    aCurSound.rewardID = createRewardTask.Result.Data[0].Id;
                    aCurSound.existsOnTwitch = true;
                    return true;    // Successfully created
                }
            }
            catch (Exception e)
            {
                m_BotBrain.logWarningsErrors.writeAndLog(string.Format("Exception when trying to create channel point reward named: \"{0}\": {1}", aCurSound.name, e.Message));
                return false;
            }
        }

        private bool loadSounds()
        {
            currentPointRewards = new List<soundCommandDef>();
            List<soundCommandDef> pointRewardAddQueue = new List<soundCommandDef>();

            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_sounds.json");
            if (File.Exists(configPath))
            {
                string configFileString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(configFileString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<soundCommandConfig>(configFileString);


                    int curSoundRewardCount = 0;
                    foreach (soundCommandDef curSound in m_Config.soundList)    // Collect mandatory sounds first
                    {
                        if (curSound.isMandatoryReward && curSoundRewardCount < m_Config.pointRewardCountMax && curSoundRewardCount < CUSTOM_REWARDS_MAX)
                        {
                            pointRewardAddQueue.Add(curSound);
                            curSoundRewardCount++;
                        }
                    }

                    // Randomize before collecting non-mandatory sounds
                    m_Config.soundList.Sort(delegate (soundCommandDef a, soundCommandDef b)
                    {
                        return m_BotBrain.randomizer.Next() - m_BotBrain.randomizer.Next();
                    });

                    foreach (soundCommandDef curSound in m_Config.soundList)    // Collect a set of non-mandatory sounds
                    {
                        if (curSound.isValidForPointReward && curSoundRewardCount < m_Config.pointRewardCountMax && curSoundRewardCount < CUSTOM_REWARDS_MAX)
                        {
                            pointRewardAddQueue.Add(curSound);
                            curSoundRewardCount++;
                        }
                    }

                    // Get current list of rewards
                    Task<TwitchLib.Api.Helix.Models.ChannelPoints.GetCustomReward.GetCustomRewardsResponse> getRewardsTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.GetCustomRewardAsync(m_BotBrain.ownerUserID);
                    getRewardsTask.Wait();

                    if (getRewardsTask.Result != null)
                    {
                        int totalRewardCount = getRewardsTask.Result.Data.Length;
                        // Check existing rewards
                        foreach (TwitchLib.Api.Helix.Models.ChannelPoints.CustomReward curReward in getRewardsTask.Result.Data)
                        {
                            if (curReward.Title.StartsWith("Play Sound - "))
                            {
                                bool foundRewardInAddQueue = false;
                                foreach (soundCommandDef curSound in pointRewardAddQueue)
                                {
                                    if (curReward.Title == "Play Sound - " + curSound.name)
                                    {
                                        curSound.existsOnTwitch = true;
                                        curSound.rewardID = curReward.Id;
                                        foundRewardInAddQueue = true;
                                        break;
                                    }
                                }

                                if (!foundRewardInAddQueue)
                                {
                                    try
                                    {
                                        Task removeRewardTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.DeleteCustomRewardAsync(m_BotBrain.ownerUserID, curReward.Id);
                                        removeRewardTask.Wait();
                                        totalRewardCount--;
                                    }
                                    catch (Exception e)
                                    {
                                        m_BotBrain.logWarningsErrors.writeAndLog(string.Format("Exception when trying to remove channel point reward named: \"{0}\": {1}", curReward.Title, e.Message));
                                    }
                                }
                            }
                        }

                        int rewardsOnTwitch = 0;
                        // Add new rewards until we can't fit any more
                        foreach (soundCommandDef curSound in pointRewardAddQueue)
                        {
                            if (curSound.existsOnTwitch)
                            {
                                if (rewardsOnTwitch > m_Config.pointRewardCountMax || rewardsOnTwitch > CUSTOM_REWARDS_MAX)
                                {
                                    try
                                    {
                                        Task removeRewardTask = m_BotBrain.twitchAPI.Helix.ChannelPoints.DeleteCustomRewardAsync(m_BotBrain.ownerUserID, curSound.rewardID);
                                        removeRewardTask.Wait();
                                        rewardsOnTwitch--;
                                        curSound.existsOnTwitch = false;
                                    }
                                    catch (Exception e)
                                    {
                                        m_BotBrain.logWarningsErrors.writeAndLog(string.Format("Exception when trying to remove channel point reward named: \"{0}\": {1}", curSound.name, e.Message));
                                    }
                                }
                            }
                            else if (!curSound.existsOnTwitch && rewardsOnTwitch < m_Config.pointRewardCountMax && rewardsOnTwitch < CUSTOM_REWARDS_MAX)
                            {
                                if (attemptAddReward(curSound))
                                {
                                    rewardsOnTwitch++;
                                }
                            }
                        }
                    }

                    if (m_Config.useDevice >= -1 && m_Config.useDevice < WaveOut.DeviceCount)
                    {
                        m_DeviceNumber = m_Config.useDevice;
                    }

                    m_GlobalVolume = Math.Min(m_Config.globalVolume, 1.0f);

                    m_IsEnabled = m_Config.enabled;

                    return true;
                }
            }

            return false;
        }

        public void reloadSounds(userEntry commandUser, string argumentString)
        {
            if (loadSounds())
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("soundReloadSuccess"));
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.localizer.getString("soundReloadFail"));
        }

        public void playRandom(userEntry commandUser, string argumentString)
        {
            int soundID = m_BotBrain.randomizer.Next(0, m_Config.soundList.Count - 1);
            playSoundInternal(commandUser, m_Config.soundList[soundID], true);
        }

        public soundCommands(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            if (loadSounds())
            {
                isEnabled = m_Config.enabled;

                chatCommandDef tempDef = new chatCommandDef("sound", playSound, true, true);
                tempDef.addSubCommand(new chatCommandDef("volume", setVolume, false, false));
                tempDef.addSubCommand(new chatCommandDef("list", getList, true, true));
                tempDef.addSubCommand(new chatCommandDef("enable", enable, true, false));
                tempDef.addSubCommand(new chatCommandDef("disable", disable, true, false));
                tempDef.addSubCommand(new chatCommandDef("reload", reloadSounds, false, false));
                tempDef.addSubCommand(new chatCommandDef("random", playRandom, true, true));
                tempDef.addSubCommand(new chatCommandDef("getdevices", getDeviceList, false, false));
                tempDef.addSubCommand(new chatCommandDef("setdevice", setDevice, false, false));

                m_BotBrain.addChatCommand(tempDef);
                m_OutputEvent = new WaveOutEvent();
            }
        }
    }
}
