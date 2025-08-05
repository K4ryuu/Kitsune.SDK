using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Kitsune.SDK.Utilities
{
	/// <summary>
	/// Optimized utility class for handling chat colors
	/// </summary>
	public static partial class ChatColor
	{
		// Use FrozenDictionary for faster lookups and less memory usage
		private static readonly FrozenDictionary<string, char> _colorMap;

		// Keep a hashset of color values for fast contains checks
		private static readonly HashSet<char> _colorValues;

		// Regex for color pattern matching (created at initialization)
		private static readonly Regex _colorPattern;

		// Special keys for dynamic colors
		private const string TEAM_KEY = "team";
		private const string RANDOM_KEY = "random";

		// Static constructor to initialize color map and pattern once
		static ChatColor()
		{
			// Build color map from the CSS ChatColors
			var colors = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);

			// Reflection to get all color constants from ChatColors
			foreach (var field in typeof(ChatColors).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (field.FieldType == typeof(char) && field.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length == 0)
				{
					string colorName = field.Name.ToLowerInvariant();
					char colorChar = (char)field.GetValue(null)!;
					colors[colorName] = colorChar;
				}
			}

			// Create immutable dictionary for better performance
			_colorMap = colors.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

			// Create hashset of all values for fast lookups
			_colorValues = [.. _colorMap.Values];

			// Build regex pattern using the color names
			string pattern = string.Join('|', _colorMap.Keys.Select(k => $@"\{{{k}\}}|{k}"));

			// Use compiled regex for better performance
			_colorPattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
		}

		/// <summary>
		/// Applies color codes to a string, replacing {colorname} with the actual color character
		/// </summary>
		/// <param name="msg">The message to process</param>
		/// <param name="player">Optional player for team color</param>
		/// <returns>The colorized string</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ReplaceColors(string msg, CCSPlayerController? player = null)
		{
			// Early return for empty messages
			if (string.IsNullOrEmpty(msg))
				return msg;

			// Process standard color tags
			msg = _colorPattern.Replace(msg, match =>
			{
				string key = match.Value.Trim('{', '}').ToLowerInvariant();
				return _colorMap.TryGetValue(key, out char color) ? color.ToString() : match.Value;
			});

			// Handle special tags more efficiently
			if (player != null && msg.Contains("{team}", StringComparison.OrdinalIgnoreCase))
			{
				msg = msg.Replace("{team}", ChatColors.ForPlayer(player).ToString(), StringComparison.OrdinalIgnoreCase);
			}

			if (msg.Contains("{random}", StringComparison.OrdinalIgnoreCase))
			{
				char randomColor = _colorMap.Values.ElementAt(Random.Shared.Next(0, _colorMap.Count));
				msg = msg.Replace("{random}", randomColor.ToString(), StringComparison.OrdinalIgnoreCase);
			}

			return msg;
		}

		/// <summary>
		/// Gets the character value for a named chat color
		/// </summary>
		/// <param name="colorName">The name of the color</param>
		/// <param name="player">Optional player for team color</param>
		/// <returns>The color character</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char GetValue(string colorName, CCSPlayerController? player = null)
		{
			if (string.IsNullOrEmpty(colorName))
				return ChatColors.Default;

			// Handle special colors
			if (colorName.Equals(TEAM_KEY, StringComparison.OrdinalIgnoreCase))
				return player?.IsValid == true ? ChatColors.ForPlayer(player) : ChatColors.Default;

			if (colorName.Equals(RANDOM_KEY, StringComparison.OrdinalIgnoreCase))
				return _colorMap.Values.ElementAt(Random.Shared.Next(0, _colorMap.Count));

			// Fast lookup in frozen dictionary
			return _colorMap.TryGetValue(colorName, out char color) ? color : ChatColors.Default;
		}

		/// <summary>
		/// Removes color tags from a string
		/// </summary>
		/// <param name="msg">The text to clean</param>
		/// <returns>The cleaned text without color tags</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string RemoveColorKeys(string msg)
		{
			if (string.IsNullOrEmpty(msg))
				return msg;

			// Remove all color tags in one regex operation
			return _colorPattern.Replace(msg, string.Empty)
				.Replace("{team}", string.Empty, StringComparison.OrdinalIgnoreCase)
				.Replace("{random}", string.Empty, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Removes color characters from a string
		/// </summary>
		/// <param name="msg">The text to clean</param>
		/// <returns>The cleaned text without color characters</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string RemoveColorValues(string msg)
		{
			if (string.IsNullOrEmpty(msg))
				return msg;

			// Create a StringBuilder with capacity matching the original string
			var result = new StringBuilder(msg.Length);

			// Scan through the string only once
			foreach (char c in msg)
			{
				// Skip if it's a color code
				if (!_colorValues.Contains(c))
				{
					result.Append(c);
				}
			}

			return result.ToString();
		}
	}
}