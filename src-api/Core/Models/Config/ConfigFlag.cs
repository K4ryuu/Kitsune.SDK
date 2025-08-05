namespace Kitsune.SDK.Core.Models.Config
{
	[Flags]
	public enum ConfigFlag
	{
		None = 0,
		Global = 1,         // Allow all other modules to access this config value
		Protected = 2,      // Prevent this config value from retrieving the value (hidden)
		Locked = 4          // Prevent this config value from being changed (read-only)
	}
}