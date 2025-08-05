using System.Net;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using MaxMind.GeoIP2;
using Kitsune.SDK.Core.Base;

namespace Kitsune.SDK.Utilities
{
	/// <summary>
	/// Static GeoIP service using MaxMind's GeoLite2 database
	/// Provides global access to GeoIP lookup functionality
	/// </summary>
	public static class GeoIP
	{
		// Result type definition for clarity and performance
		public readonly record struct CountryInfo(string ShortName, string LongName);

		// Default country values
		private static readonly CountryInfo _defaultCountry = new("??", "Unknown");

		// Lazy initialization - database loads only on first use
		private static readonly Lazy<DatabaseReader?> _reader = new(LoadDatabase);

		// Database load status flag
		private static bool _databaseLoadFailed;

		// Initialization status flag
		private static bool _placeholdersRegistered;

		/// <summary>
		/// Initialize GeoIP placeholders for a plugin
		/// </summary>
		/// <param name="plugin">The plugin to register placeholders for</param>
		public static void Initialize(SdkPlugin plugin)
		{
			if (_placeholdersRegistered)
				return;

			// Register country_short placeholder
			plugin.Placeholders.RegisterPlayer("{country_short}", player =>
			{
				var country = GetPlayerCountry(player);
				return country.ShortName;
			});

			// Register country_long placeholder
			plugin.Placeholders.RegisterPlayer("{country_long}", player =>
			{
				var country = GetPlayerCountry(player);
				return country.LongName;
			});

			_placeholdersRegistered = true;
		}

		/// <summary>
		/// Loads the GeoIP database
		/// </summary>
		private static DatabaseReader? LoadDatabase()
		{
			try
			{
				string databasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "GeoLite2-Country.mmdb");

				if (!File.Exists(databasePath))
				{
					_databaseLoadFailed = true;
					return null;
				}

				return new DatabaseReader(databasePath);
			}
			catch
			{
				_databaseLoadFailed = true;
				return null;
			}
		}

		/// <summary>
		/// Get a player's country based on their IP address
		/// </summary>
		/// <param name="player">The player controller</param>
		/// <returns>CountryInfo with country code and name</returns>
		public static CountryInfo GetPlayerCountry(CCSPlayerController? player)
		{
			// Early exit if player is invalid
			if (player?.IsValid != true || string.IsNullOrEmpty(player.IpAddress))
				return _defaultCountry;

			// Extract IP from player's address (format: IP:PORT)
			int colonIndex = player.IpAddress.IndexOf(':');
			string ipAddress = colonIndex > 0 ? player.IpAddress[..colonIndex] : player.IpAddress;

			return GetIPCountry(ipAddress);
		}

		/// <summary>
		/// Get country information for an IP address
		/// </summary>
		/// <param name="ipAddress">The IP address to lookup</param>
		/// <returns>CountryInfo with country code and name</returns>
		public static CountryInfo GetIPCountry(string? ipAddress)
		{
			// Early exit for null/empty input
			if (string.IsNullOrEmpty(ipAddress))
				return _defaultCountry;

			// Early exit if database failed to load previously
			if (_databaseLoadFailed)
				return _defaultCountry;

			try
			{
				// Get database reader (loads on first access)
				var reader = _reader.Value;
				if (reader == null)
					return _defaultCountry;

				// Perform the lookup
				var response = reader.Country(ipAddress);

				// Handle case when country info is missing
				if (response.Country.IsoCode == null || response.Country.Name == null)
					return _defaultCountry;

				// Return valid country info
				return new CountryInfo(response.Country.IsoCode, response.Country.Name);
			}
			catch
			{
				// Return default on any error
				return _defaultCountry;
			}
		}
	}
}