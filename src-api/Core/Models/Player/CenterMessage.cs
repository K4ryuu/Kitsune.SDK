namespace Kitsune.SDK.Core.Models.Player
{
	public struct CenterMessage(string message, float duration, ActionPriority priority = ActionPriority.Normal)
	{
		public string Message { get; set; } = message;
		public float Duration { get; set; } = duration;
		public ActionPriority Priority { get; set; } = priority;
	}
}