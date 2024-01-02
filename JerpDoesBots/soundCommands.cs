using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace JerpDoesBots
{
    internal class soundCommandDef
    {
        public string name { get; set; }
        public List<string> paths { get; set; }
        public float volume { get; set; }
        public bool isValidForPointReward { get; set; }
        /// <summary>
        /// Primarily used to filter out sounds that should only be accessible for internal use.  Defaults to true.
        /// </summary>
        public bool isValidForCommand { get; set; }
        public int pointRewardCost { get; set; }
        public bool isMandatoryReward { get; set; }
        public string source { get; set; }
        public string character { get; set; }
        public string description { get; set; }
        public channelCondition requirements { get; set; }
        public long lastUsed;
        public pointReward reward { get; set; }
        // public Dictionary<string, long> userLastUsed;
        public soundCommandDef()
        {
            pointRewardCost = 0;
            isValidForCommand = true;
        }
    }

    internal class soundCommandConfig
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

        public bool isEnabled { get { return m_IsEnabled; } set { m_IsEnabled = value; } }

        public pointReward createPointRewardFromSoundDef(soundCommandDef aSoundDef)
        {
            pointReward newReward = new pointReward();

            newReward.title = "Play Sound - " + aSoundDef.name;

            if (aSoundDef.pointRewardCost > 0)
                newReward.cost = aSoundDef.pointRewardCost;
            else
                newReward.cost = m_Config.pointRewardCostDefault;

            if (!string.IsNullOrEmpty(aSoundDef.description))
            {
                if (!string.IsNullOrEmpty(aSoundDef.source))
                {
                    newReward.description = "From " + aSoundDef.source + ":\n" + aSoundDef.description; // TODO: LOCALIZE
                }
                else
                {
                    newReward.description = aSoundDef.description;
                }
            }
            else
            {
                newReward.description = "Play a sound!"; // TODO: LOCALIZE
            }

            newReward.backgroundColor = "#222222"; // TODO: Make configurable
            newReward.globalCooldownSeconds = (int)Math.Ceiling((double)(COOLDOWN_GLOBAL_ALL / 1000));
            newReward.autoFulfill = false;
            newReward.requireUserInput = false;

            return newReward;
        }

        private bool onCooldown(soundCommandDef aSound, userEntry commandUser)
        {
            if (m_OutputEvent.PlaybackState == PlaybackState.Playing)
                return true;

            if (commandUser.isBroadcaster)
                return false;

            if (
                (jerpBot.instance.actionTimer.ElapsedMilliseconds + COOLDOWN_GLOBAL_ALL > aSound.lastUsed) &&
                (aSound != m_lastSound || jerpBot.instance.actionTimer.ElapsedMilliseconds + COOLDOWN_GLOBAL_PERSOUND > aSound.lastUsed)
            )
            {
                return false;
            }

            return true;
        }

        public override void onChannelPointRedemption(userEntry aMessageUser, string aRewardTitle, int aRewardCost, string aRewardUserInput, string aRewardID, string aRedemptionID)
        {
            bool needRefund = false;
            if (isEnabled)
            {
                soundCommandDef foundSound = m_Config.soundList.Find(x => x.reward.shouldExistOnTwitch && x.reward.rewardID == aRewardID);

                if (foundSound != null)
                {
                    if (playSoundInternal(aMessageUser, foundSound))
                    {
                        jerpBot.instance.logGeneral.writeAndLog("Sound reward redemption by " + aMessageUser.Nickname + " - " + foundSound.name);
                        if (!pointRewardManager.updateRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.FULFILLED))
                        {
                            //Error state since I couldn't mark fulfilled
                        }
                    }
                    else
                    {
                        needRefund = true;
                    }
                }
            }

            if (needRefund)
            {
                // TODO: Fail reason/output (see raffle)
                if (!pointRewardManager.updateRewardRedemptionStatus(aRewardID, aRedemptionID, TwitchLib.Api.Core.Enums.CustomRewardRedemptionStatus.CANCELED))
                {
                    // jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("raffleRewardRedeemStatusCanceledFail"), aMessageUser.Nickname));
                }
            }
        }

        private bool playSoundInternal(userEntry aUser, soundCommandDef curSound, bool aIsRandom = false, bool aOutputErrors = false, bool aSilentMode = false, bool aFromShoutout = false)
        {
            int pathCount = curSound.paths.Count;
            if (pathCount > 0)
            {
                if (curSound.requirements == null || curSound.requirements.isValidCategory())
                {
                    if (curSound.requirements == null || curSound.requirements.validTags())
                    {
                        if (!onCooldown(curSound, aUser))
                        {
                            string baseSoundPath;
                            if (pathCount > 1)
                            {
                                int soundIndex = jerpBot.instance.randomizer.Next(0, pathCount);

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

                                curSound.lastUsed = jerpBot.instance.actionTimer.ElapsedMilliseconds;
                                m_lastSound = curSound;

                                if (aIsRandom && !aSilentMode)
                                    jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundPlayRandom"), curSound.name));

                                return true;
                            }
                        }
                        else
                        {
                            if (aOutputErrors)
                            {
                                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundPlayErrorOnCooldown"), curSound.name));
                            }
                        }
                    }
                    else
                    {
                        jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundPlayErrorInvalidTags"), curSound.name));
                    }
                }
                else
                {
                    if (aOutputErrors)
                    {
                        jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundPlayErrorInvalidGame"), curSound.name, jerpBot.instance.game));
                    }
                    
                }
            }
            else
            {
                // No sound paths defined (no error here?)
            }

            return false;
        }

        public bool soundExists(string aSound, bool aIsCommand = true)
        {
            if (m_IsEnabled)
            {
                soundCommandDef checkSound = getSoundDef(aSound);
                if (checkSound != null && (!aIsCommand || checkSound.isValidForCommand))
                {
                    return true;
                }
            }

            return m_IsEnabled && getSoundDef(aSound) != null;
        }

        public soundCommandDef getSoundDef(string aSound, bool aIsCommand = true)
        {
            List<soundCommandDef> useSoundList = aIsCommand ? getValidCommandSounds() : m_Config.soundList;
            return useSoundList.Find(x => x.name.ToLower() == aSound.ToLower());
        }

        public void playSoundCommand(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (!m_IsEnabled)
                return;

            soundCommandDef useSound = getSoundDef(argumentString, true);
            if (useSound != null)
            {
                playSoundInternal(commandUser, useSound, false, true, aSilent);
            }
        }

        public void setDevice(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            bool success = false;
            int deviceNumber;
            if (Int32.TryParse(argumentString, out deviceNumber))
            {
                if (deviceNumber >= -1 && deviceNumber < WaveOut.DeviceCount)
                {
                    m_DeviceNumber = deviceNumber;
                    if (!aSilent)
                    {
                        jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundSetDeviceNumberSuccess"), deviceNumber, WaveOut.GetCapabilities(deviceNumber).ProductName));
                    }
                    success = true;
                }
            }

            if (!success)
                jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundSetDeviceNumberFailRange"), WaveOut.DeviceCount - 1));
        }

        public void getDeviceList(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            for (int i=0; i < WaveOut.DeviceCount; i++)
            {
                WaveOutCapabilities curDevice = WaveOut.GetCapabilities(i);

                Console.WriteLine(string.Format(jerpBot.instance.localizer.getString("soundDeviceListEntry"), i, curDevice.ProductName));
            }

            if (!aSilent)
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("soundDeviceListOutput"));
        }

        public void setVolume(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            float newVolume;
            if (float.TryParse(argumentString, out newVolume))
            {
                newVolume = Math.Min(newVolume, 1.0f);
                m_GlobalVolume = newVolume;
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(string.Format(jerpBot.instance.localizer.getString("soundSetGlobalVolume"), newVolume));
            }
        }

        public void enable(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsEnabled = true;

            if (!aSilent)
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("soundEnabled"));
        }

        public void disable(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            m_IsEnabled = false;

            if (!aSilent)
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("soundDisabled"));
        }

        public void getList(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("soundList"));
        }

        private bool loadSounds()
        {
            string configPath = System.IO.Path.Combine(jerpBot.storagePath, "config\\jerpdoesbots_sounds.json");
            if (File.Exists(configPath))
            {
                string configFileString = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(configFileString))
                {
                    m_Config = new JavaScriptSerializer().Deserialize<soundCommandConfig>(configFileString);

                    foreach (soundCommandDef curSound in m_Config.soundList)
                    {
                        curSound.reward = createPointRewardFromSoundDef(curSound);
                        curSound.reward = pointRewardManager.addUpdatePointReward(curSound.reward);
                    }

                    int curSoundRewardCount = 0;
                    foreach (soundCommandDef curSound in m_Config.soundList)    // Collect mandatory sounds first
                    {
                        if (curSound.isMandatoryReward && (curSound.requirements == null || curSound.requirements.isMet()) && curSoundRewardCount < m_Config.pointRewardCountMax && curSoundRewardCount < CUSTOM_REWARDS_MAX)
                        {
                            curSound.reward.enabled = m_Config.enabled;
                            curSound.reward.shouldExistOnTwitch = m_Config.enabled;
                            curSoundRewardCount++;
                        }
                    }

                    // Randomize before collecting non-mandatory sounds
                    m_Config.soundList.Sort(delegate (soundCommandDef a, soundCommandDef b)
                    {
                        return jerpBot.instance.randomizer.Next() - jerpBot.instance.randomizer.Next();
                    });

                    foreach (soundCommandDef curSound in m_Config.soundList)    // Collect a set of non-mandatory sounds
                    {
                        if (curSound.isValidForPointReward && (curSound.requirements == null || curSound.requirements.isMet()) && curSoundRewardCount < m_Config.pointRewardCountMax && curSoundRewardCount < CUSTOM_REWARDS_MAX)
                        {
                            curSound.reward.enabled = m_Config.enabled;
                            curSound.reward.shouldExistOnTwitch = m_Config.enabled;
                            curSoundRewardCount++;
                        }
                    }

                    pointRewardManager.updateRemoteRewardsFromLocalData();

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

        public void reloadSounds(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            if (loadSounds())
            {
                if (!aSilent)
                    jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("soundReloadSuccess"));
            }
                
            else
                jerpBot.instance.sendDefaultChannelMessage(jerpBot.instance.localizer.getString("soundReloadFail"));
        }


        public List<soundCommandDef> getValidCommandSounds()
        {
            return m_Config.soundList.FindAll(x => x.isValidForCommand);
        }

        public void playRandom(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            List<soundCommandDef> validSounds = getValidCommandSounds();
            int soundID = jerpBot.instance.randomizer.Next(0, validSounds.Count - 1);
            playSoundInternal(commandUser, validSounds[soundID], true, false, aSilent);
        }

        public soundCommands() : base(true, true, false)
        {
            if (loadSounds())
            {
                isEnabled = m_Config.enabled;

                chatCommandDef tempDef = new chatCommandDef("sound", playSoundCommand, true, true);
                tempDef.addSubCommand(new chatCommandDef("volume", setVolume, false, false));
                tempDef.addSubCommand(new chatCommandDef("list", getList, true, true));
                tempDef.addSubCommand(new chatCommandDef("enable", enable, true, false));
                tempDef.addSubCommand(new chatCommandDef("disable", disable, true, false));
                tempDef.addSubCommand(new chatCommandDef("reload", reloadSounds, false, false));
                tempDef.addSubCommand(new chatCommandDef("random", playRandom, true, true));
                tempDef.addSubCommand(new chatCommandDef("getdevices", getDeviceList, false, false));
                tempDef.addSubCommand(new chatCommandDef("setdevice", setDevice, false, false));

                jerpBot.instance.addChatCommand(tempDef);
                m_OutputEvent = new WaveOutEvent();
            }
        }
    }
}
