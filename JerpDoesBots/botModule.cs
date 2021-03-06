using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JerpDoesBots
{
	class botModule
	{
		public virtual void frame() {}
		public virtual void onUserMessage(userEntry aUser, string aMessage) {}
        public virtual void onUserJoin(userEntry aUser) {}
        public virtual void onPrivateMessage(userEntry aUser, string aMessage) {}

		private bool m_RequiresConnection	= true;
		private bool m_RequiresChannel		= true;
		private bool m_RequiresPM			= false;

		public bool requiresConnection { get { return m_RequiresConnection; } }
		public bool requiresChannel { get { return m_RequiresChannel; } }
		public bool requiresPM { get { return m_RequiresPM; } }

		protected jerpBot m_BotBrain;

		public botModule(jerpBot aJerpBot, bool aRequiresConnection = true, bool aRequiresChannel = true, bool aRequiresPM = false)
		{
			m_BotBrain				= aJerpBot;
			m_RequiresConnection	= aRequiresConnection;
			m_RequiresChannel		= aRequiresChannel;
			m_RequiresPM			= aRequiresPM;

			m_BotBrain.addModule(this);
		} 
	}
}
