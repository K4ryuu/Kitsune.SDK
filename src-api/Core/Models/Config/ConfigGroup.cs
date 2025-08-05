using System.Collections.Concurrent;

namespace Kitsune.SDK.Core.Models.Config
{
	/// <summary>
	/// Configuration group structure
	/// </summary>
	public class ConfigGroup
	{
		public required string Name { get; set; }
		public ConcurrentDictionary<string, ConfigItem> Items { get; set; } = [];
	}
}