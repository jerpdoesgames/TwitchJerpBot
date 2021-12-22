using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using NAudio.Wave;

namespace JerpDoesBots
{

    public class soundCommandDef
    {
        public string name { get; set; }
        // public string path { get; set; }
        public List<string> paths { get; set; }
        public long lastUsed;
        public float volume { get; set; }
        // public Dictionary<string, long> userLastUsed;
    }

    public class soundCommandConfig
    {
        public List<soundCommandDef> soundList { get; set; }
        public bool enabled { get; set; }
    }

    class soundCommands : botModule
    {
        public const long COOLDOWN_GLOBAL_ALL       = 7500; // Entire system
        public const long COOLDOWN_GLOBAL_PERSOUND  = 17500; // Force some sound variety by allowing other sounds to be played
        public const long COOLDOWN_PERUSER          = 30000;

        private soundCommandConfig m_Config;
        private soundCommandDef m_lastSound;
        private WaveOutEvent m_OutputDevice;

        private float m_GlobalVolume = 0.5f;
        private bool m_IsEnabled = false;

        public bool isEnabled { get { return m_IsEnabled; } set { m_IsEnabled = value; } }

        private bool onCooldown(soundCommandDef aSound, userEntry commandUser)
        {
            if (m_OutputDevice.PlaybackState == PlaybackState.Playing)
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

        private void playSoundInternal(userEntry commandUser, soundCommandDef curSound, bool isRandom = false)
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
                    m_OutputDevice.Init(audioFile);

                    float soundVolume = m_GlobalVolume;
                    if (curSound.volume > 0)
                        soundVolume *= curSound.volume;

                    m_OutputDevice.Volume = Math.Min(soundVolume, 1.0f);
                    m_OutputDevice.Play();
                    curSound.lastUsed = m_BotBrain.actionTimer.ElapsedMilliseconds;
                    m_lastSound = curSound;

                    if (isRandom)
                        m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("soundPlayRandom"), curSound.name));
                }
            }
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

        public void setVolume(userEntry commandUser, string argumentString)
        {
            float newVolume;
            if (float.TryParse(argumentString, out newVolume))
            {
                newVolume = Math.Min(newVolume, 1.0f);
                m_GlobalVolume = newVolume;
                m_BotBrain.sendDefaultChannelMessage(string.Format(m_BotBrain.Localizer.getString("soundSetGlobalVolume"), newVolume));
            }
        }

        public void enable(userEntry commandUser, string argumentString)
        {
            m_IsEnabled = true;

            m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("soundEnabled"));
        }

        public void disable(userEntry commandUser, string argumentString)
        {
            m_IsEnabled = false;

            m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("soundDisabled"));
        }

        public void getList(userEntry commandUser, string argumentString)
        {
            m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("soundList"));
        }

        public void makeList(userEntry commandUser, string argumentString)
        {
            if (!m_IsEnabled)
                return;

            List<string> soundNames = new List<string>();

            for (int i = 0; i < m_Config.soundList.Count; i++)
                soundNames.Add(m_Config.soundList[i].name);

            string soundNameString = string.Join(", ", soundNames);

            Console.WriteLine("Available sounds are: " + soundNameString);  // TODO: This should probably be removed or changed since I don't want to localize something going to the console and this is too long for a chat message
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
                    return true;
                }
            }

            return false;
        }

        public void reloadSounds(userEntry commandUser, string argumentString)
        {
            if (loadSounds())
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("soundReloadSuccess"));
            else
                m_BotBrain.sendDefaultChannelMessage(m_BotBrain.Localizer.getString("soundReloadFail"));
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
                tempDef.addSubCommand(new chatCommandDef("buildlist", makeList, false, false));
                tempDef.addSubCommand(new chatCommandDef("enable", enable, true, false));
                tempDef.addSubCommand(new chatCommandDef("disable", disable, true, false));
                tempDef.addSubCommand(new chatCommandDef("reload", reloadSounds, false, false));
                tempDef.addSubCommand(new chatCommandDef("random", playRandom, true, true));

                m_BotBrain.addChatCommand(tempDef);
                m_OutputDevice = new WaveOutEvent();
            }
        }
    }
}
