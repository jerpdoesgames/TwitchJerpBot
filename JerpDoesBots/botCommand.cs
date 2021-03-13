
namespace JerpDoesBots
{
	class botCommand
	{

		public enum types : int {
			postChannelStatus
		};

		private types commandType;

		public types getCommandType() { return commandType; }
		private string target;

		public void setTarget(string newTarget)
		{
			target = newTarget;
		}

		public botCommand(types newCommandType)
		{
			commandType = newCommandType;
		}
	}
}
