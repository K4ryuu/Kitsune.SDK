namespace Kitsune.SDK.Core.Models.Config
{
	/// <summary>
	/// Configuration item structure
	/// </summary>
	public class ConfigItem
	{
		public string GroupName { get; set; } = "default";
		public required string Name { get; set; }
		public required string Description { get; set; }
		public required object DefaultValue { get; set; }
		public required object Value { get; set; }
		public ConfigFlag Flags { get; set; }
	}
}