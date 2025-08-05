using CounterStrikeSharp.API.Core;

namespace Kitsune.SDK.Core.Models.Placeholders
{
	/// <summary>
	/// Represents a placeholder that can be used in messages without player context
	/// </summary>
	public class ServerPlaceholder
	{
		/// <summary>
		/// The module that registered this placeholder
		/// </summary>
		public required BasePlugin Module;

		/// <summary>
		/// The placeholder text to be replaced (without {{}})
		/// </summary>
		public required string Placeholder;

		/// <summary>
		/// The callback function that returns the replacement text for the placeholder
		/// </summary>
		public required Func<string> Callback;
	}
}