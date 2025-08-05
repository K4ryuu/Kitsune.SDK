using System.Collections.Concurrent;

namespace Kitsune.SDK.Core.Models.Config
{
	/// <summary>
	/// Module configuration structure
	/// </summary>
	public class ModuleConfig
	{
		public required string ModuleName { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.Now;
		public DateTime LastUpdated { get; set; } = DateTime.Now;
		public ConcurrentDictionary<string, ConfigGroup> Groups { get; set; } = [];
	}
}