using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Core;

namespace Kitsune.SDK.Utilities
{
	/// <summary>
	/// SDK Translation utility for handling localized strings
	/// </summary>
	public sealed class SdkTranslations : IStringLocalizer
	{
		private static readonly Lazy<SdkTranslations> _instance = new(() => new SdkTranslations());
		public static SdkTranslations Instance => _instance.Value;

		private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations = new();
		private readonly string _translationsPath;

		private SdkTranslations()
		{
			_translationsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "translations");
			LoadTranslations();
		}

		/// <summary>
		/// Gets the string resource with the given name for CoreLanguage
		/// </summary>
		/// <param name="name">The name of the string resource</param>
		/// <returns>The string resource as a LocalizedString</returns>
		public LocalizedString this[string name]
			=> GetLocalizedString(name, CoreConfig.ServerLanguage);

		/// <summary>
		/// Gets the string resource with the given name and formatted with the supplied arguments for CoreLanguage
		/// </summary>
		/// <param name="name">The name of the string resource</param>
		/// <param name="arguments">The values to format the string with</param>
		/// <returns>The formatted string resource as a LocalizedString</returns>
		public LocalizedString this[string name, params object[] arguments]
			=> GetLocalizedString(name, CoreConfig.ServerLanguage, arguments);

		/// <summary>
		/// Gets all string resources
		/// </summary>
		/// <param name="includeParentCultures">A Boolean indicating whether to include strings from parent cultures</param>
		/// <returns>The strings</returns>
		public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
		{
			var results = new List<LocalizedString>();

			foreach (var culture in _translations.Keys)
			{
				foreach (var translation in _translations[culture])
				{
					results.Add(new LocalizedString(translation.Key, translation.Value));
				}
			}

			return results;
		}

		private LocalizedString GetLocalizedString(string name, string culture, params object[] arguments)
		{
			if (_translations.TryGetValue(culture, out var translations) && translations.TryGetValue(name, out var value))
			{
				try
				{
					string formattedValue = arguments.Length > 0 ? string.Format(value, arguments) : value;
					return new LocalizedString(name, formattedValue, false);
				}
				catch (FormatException)
				{
					// If formatting fails, return the raw value
					return new LocalizedString(name, value, false);
				}
			}

			// Fallback to English if key not found in requested culture
			if (culture != "en" && _translations.TryGetValue("en", out var englishTranslations) && englishTranslations.TryGetValue(name, out var englishValue))
			{
				try
				{
					string formattedValue = arguments.Length > 0 ? string.Format(englishValue, arguments) : englishValue;
					return new LocalizedString(name, formattedValue, false);
				}
				catch (FormatException)
				{
					return new LocalizedString(name, englishValue, false);
				}
			}

			// Return the key if no translation found
			return new LocalizedString(name, name, true);
		}

		private void LoadTranslations()
		{
			if (!Directory.Exists(_translationsPath))
			{
				Directory.CreateDirectory(_translationsPath);
				CreateDefaultTranslations();
				return;
			}

			var translationFiles = Directory.GetFiles(_translationsPath, "*.json");

			foreach (var file in translationFiles)
			{
				try
				{
					string culture = Path.GetFileNameWithoutExtension(file);
					string content = File.ReadAllText(file);
					var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(content);

					if (translations == null)
						continue;

					foreach (var key in translations.Keys)
					{
						translations[key] = ChatColor.ReplaceColors(translations[key]);
					}

					if (translations != null)
					{
						_translations[culture] = translations;
					}
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException($"Failed to load translation file {file}.", ex);
				}
			}

			// Ensure we have at least English translations
			if (!_translations.ContainsKey("en"))
			{
				CreateDefaultTranslations();
			}
		}

		private void CreateDefaultTranslations()
		{
			var defaultTranslations = new Dictionary<string, string>
			{
				["kitsune.sdk.general.prefix"] = "{silver}[{lightred}KITSUNE.SDK{silver}]",
				["kitsune.sdk.general.no-permission"] = "{red}You don't have permission to use this command.",
				["kitsune.sdk.general.player-not-found"] = "{red}Player not found.",
				["kitsune.sdk.general.invalid-arguments"] = "{red}Invalid arguments provided.",
				["kitsune.sdk.command.client-only"] = "{lightred}This command can only be used by clients.",
				["kitsune.sdk.command.server-only"] = "{lightred}This command can only be used by the server.",
				["kitsune.sdk.command.no-permission"] = "{lightred}You do not have permission to use this command.",
				["kitsune.sdk.command.help"] = "{silver}Expected Usage: {lime}!{0} {1}",
				["kitsune.sdk.tag.separator"] = "{white}: ",
				["kitsune.sdk.tag.dead"] = "{grey}[DEAD] ",
				["kitsune.sdk.tag.team.spectator"] = "{white}[SPEC] ",
				["kitsune.sdk.tag.team.t"] = "{yellow}[T] ",
				["kitsune.sdk.tag.team.ct"] = "{lightblue}[CT] ",
				["kitsune.sdk.tag.team.unassigned"] = "{white}[UNASSIGNED] "
			};

			_translations["en"] = defaultTranslations;

			// Save to file
			try
			{
				string filePath = Path.Combine(_translationsPath, "en.json");
				var options = new JsonSerializerOptions { WriteIndented = true };
				string json = JsonSerializer.Serialize(defaultTranslations, options);
				File.WriteAllText(filePath, json);
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException("Failed to create default translations file.", ex);
			}
		}
	}
}
