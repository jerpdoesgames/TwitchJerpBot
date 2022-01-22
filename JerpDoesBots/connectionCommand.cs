namespace JerpDoesBots
{

	public class connectionCommand
	{
		public enum types : int {
			privateMessage,
			quit,
			channelMessage,
			joinChannel,
			partChannel,
			partAllChannels
		};

		private types commandType;

		private string target;
		private string message;

		public	types	getCommandType()	{ return commandType; }
		public	string	getMessage()		{ return message; }
		public	string	getTarget()			{ return target; }

		public	void	setTarget(string commandTarget)	{ target = commandTarget; }
		public	void	setMessage(string messageToSet)	{ message = messageToSet; }

		public connectionCommand(types newCommandType)
		{
			commandType = newCommandType;
		}

	}
}
