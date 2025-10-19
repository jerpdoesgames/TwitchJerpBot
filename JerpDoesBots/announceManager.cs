using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JerpDoesBots
{
    public class announceAction
    {
        public bool allowSimpleMessage = false;
        public string message;
        public string channel;

        public announceAction(string aChannel, string aMessage, bool aAllowSimpleMessage)
        {
            channel = aChannel;
            message = aMessage;
            allowSimpleMessage = aAllowSimpleMessage;
        }
    }

    internal class announceManager : botModule
    {
        private List<announceAction> m_Queue;
        private throttler m_Throttler;
        private const long API_ANNOUNCE_THROTTLE_MS = 2500; // 2000, but a little extra for safety

        private void send(announceAction aAnnounce)
        {
            connectionCommand newCommand = new connectionCommand(connectionCommand.types.channelAnnouncement);
            newCommand.setTarget(aAnnounce.channel);
            newCommand.setMessage(aAnnounce.message);

            jerpBot.instance.queueAction(newCommand);
            m_Throttler.trigger();
        }

        public void sendOrQueue(string aChannel, string aMessage, bool aUseQueue = true)
        {
            if (m_Throttler.isReady)
            {
                announceAction newAnnounce = new announceAction(aChannel, aMessage, aUseQueue);
                send(newAnnounce);
            }
            else if (!aUseQueue)
            {
                jerpBot.instance.sendDefaultChannelMessage(aMessage);
            }
            else
            {
                announceAction newAnnounce = new announceAction(aChannel, aMessage, aUseQueue);
                m_Queue.Add(newAnnounce);
            }
        }

        public override void onFrame()
        {
            if (m_Throttler.isReady && m_Queue.Count > 0)
            {
                // Do announce
                announceAction curAnnounce = m_Queue[0];
                m_Queue.RemoveAt(0);
                send(curAnnounce);
            }
        }

        public void announce(userEntry commandUser, string argumentString, bool aSilent = false)
        {
            jerpBot.instance.sendDefaultChannelAnnounce(argumentString);
        }

        public announceManager() : base(true, true, false)
        {
            jerpBot.instance.announceModule = this;
            m_Queue = new List<announceAction>();

            m_Throttler = new throttler(true);
            m_Throttler.requiresUserMessages = false;
            m_Throttler.messagesReduceTimer = false;
            m_Throttler.waitTimeMSMax = API_ANNOUNCE_THROTTLE_MS;

            chatCommandDef tempDef = new chatCommandDef("announce", announce, true, false);
            jerpBot.instance.addChatCommand(tempDef);
        }
    }
}
