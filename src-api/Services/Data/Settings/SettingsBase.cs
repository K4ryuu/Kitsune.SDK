using System.Runtime.CompilerServices;
using System.Reflection;
using Kitsune.SDK.Services.Data.Settings;
using Kitsune.SDK.Core.Attributes.Data;

namespace Kitsune.SDK.Services
{
	/// <summary>
	/// Base class for settings models that provides automatic SDK cache integration
	/// </summary>
	public abstract class SettingsBase
	{
		private Dictionary<string, SettingAttribute>? _attributeCache;

		/// <summary>
		/// Gets a value from SDK settings
		/// </summary>
		protected T Get<T>([CallerMemberName] string propertyName = "")
		{
			var context = SettingsHandler.GetContext(this);
			if (context == null)
				return default(T)!;

			var attr = GetSettingAttribute(propertyName);
			if (attr == null)
				return default(T)!;

			return context.Value.Handler.GetSettingValue<T>(
				context.Value.SteamId, attr.Name) ?? default(T)!;
		}

		/// <summary>
		/// Sets a value in SDK settings
		/// </summary>
		protected void Set<T>(T value, [CallerMemberName] string propertyName = "")
		{
			var context = SettingsHandler.GetContext(this);
			if (context == null)
				return;

			var attr = GetSettingAttribute(propertyName);
			if (attr == null)
				return;

			context.Value.Handler.SetSettingValue(
				context.Value.SteamId, attr.Name, value);
		}

		/// <summary>
		/// Gets the setting attribute for a property
		/// </summary>
		private SettingAttribute? GetSettingAttribute(string propertyName)
		{
			// Lazy init cache
			if (_attributeCache == null)
			{
				_attributeCache = new Dictionary<string, SettingAttribute>();

				foreach (var prop in GetType().GetProperties())
				{
					var attr = prop.GetCustomAttribute<SettingAttribute>();
					if (attr != null)
					{
						_attributeCache[prop.Name] = attr;
					}
				}
			}

			return _attributeCache.TryGetValue(propertyName, out var attribute) ? attribute : null;
		}
	}
}