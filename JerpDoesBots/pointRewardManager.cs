using System.IO;
using System.Web.Script.Serialization;
using TwitchLib.PubSub.Events;

namespace JerpDoesBots
{
    internal class pointRewardManager : botModule
    {

        public pointRewardManager(jerpBot aJerpBot) : base(aJerpBot, true, true, false)
        {
            chatCommandDef tempDef = new chatCommandDef("points", null, false, false);
            // tempDef.addSubCommand(new chatCommandDef("reload", reloadConfig, false, false));
            m_BotBrain.addChatCommand(tempDef);
        }
    }
}
