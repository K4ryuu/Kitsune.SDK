using System.Runtime.CompilerServices;
using System.Reflection;
using Kitsune.SDK.Services.Data.Storage;
using Kitsune.SDK.Core.Attributes.Data;

namespace Kitsune.SDK.Services
{
	/// <summary>
	/// Base class for storage models that provides automatic SDK cache integration
	/// </summary>
	public abstract class StorageBase
	{
		private Dictionary<string, StorageAttribute>? _attributeCache;

		/// <summary>
		/// Gets a value from SDK storage
		/// </summary>
		protected T Get<T>([CallerMemberName] string propertyName = "")
		{
			var context = StorageHandler.GetContext(this);
			if (context == null)
				return default!;

			var attr = GetStorageAttribute(propertyName);
			if (attr == null)
				return default!;

			return context.Value.Handler.GetStorageValue<T>(context.Value.SteamId, attr.Name) ?? default!;
		}

		/// <summary>
		/// Sets a value in SDK storage
		/// </summary>
		protected void Set<T>(T value, [CallerMemberName] string propertyName = "")
		{
			var context = StorageHandler.GetContext(this);
			if (context == null)
				return;

			var attr = GetStorageAttribute(propertyName);
			if (attr == null)
				return;

			context.Value.Handler.SetStorageValue(
				context.Value.SteamId, attr.Name, value);
		}

		/// <summary>
		/// Gets the storage attribute for a property
		/// </summary>
		private StorageAttribute? GetStorageAttribute(string propertyName)
		{
			// Lazy init cache
			if (_attributeCache == null)
			{
				_attributeCache = new Dictionary<string, StorageAttribute>();

				foreach (var prop in GetType().GetProperties())
				{
					var attr = prop.GetCustomAttribute<StorageAttribute>();
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