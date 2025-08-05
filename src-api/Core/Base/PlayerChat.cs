using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;
using Kitsune.SDK.Core.Models.Player;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Services.Config;
using Kitsune.SDK.Services;

namespace Kitsune.SDK.Core.Base
{
	public partial class Player
	{
		// Store priority-based chat options directly rather than using tuples
		public bool EnableChatModifiers = true;
		public bool EnableScoreboardModifiers = true;

		private PriorityValue<string>? _clanTag = null;
		private PriorityValue<string>? _nameTag = null;
		private PriorityValue<string>? _nameColor = null;
		private PriorityValue<string>? _chatColor = null;
		private PriorityValue<bool>? _mute = null;
		private PriorityValue<bool>? _gag = null;

		/// <summary>
		/// A simple record to store a value with a priority for efficient access
		/// </summary>
		/// <typeparam name="T">The type of value stored</typeparam>
		private readonly record struct PriorityValue<T>(T Value, ActionPriority Priority);

		/// <summary>
		/// Is the player muted (voice chat blocked)
		/// </summary>
		public bool IsMuted => _mute?.Value ?? false;

		/// <summary>
		/// Is the player gagged (text chat blocked)
		/// </summary>
		public bool IsGagged => _gag?.Value ?? false;

		/// <summary>
		/// Set mute status for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetMute(bool mute, ActionPriority priority = ActionPriority.Normal)
		{
			// Check if priority allows this change
			if (_mute != null && priority < _mute.Value.Priority)
				return;

			if (mute)
			{
				if (!Controller.VoiceFlags.HasFlag(VoiceFlags.Muted))
					Controller.VoiceFlags |= VoiceFlags.Muted;

				_mute = new PriorityValue<bool>(mute, priority);
			}
			else
			{
				if (Controller.VoiceFlags.HasFlag(VoiceFlags.Muted))
					Controller.VoiceFlags &= ~VoiceFlags.Muted;

				_mute = null;
			}
		}

		/// <summary>
		/// Set gag status for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetGag(bool gag, ActionPriority priority = ActionPriority.Normal)
		{
			// Check if priority allows this change
			if (_gag != null && priority < _gag.Value.Priority)
				return;

			_gag = gag ? new PriorityValue<bool>(true, priority) : null;
		}

		/// <summary>
		/// Set clan tag for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetClanTag(string? tag, ActionPriority priority = ActionPriority.Normal)
		{
			if (tag == null)
			{
				_clanTag = null;
				return;
			}

			// Check if priority allows this change
			if (_clanTag != null && priority < _clanTag.Value.Priority)
				return;

			_clanTag = new PriorityValue<string>(tag, priority);

			// Apply to the player controller
			Controller.Clan = tag;
			CounterStrikeSharp.API.Utilities.SetStateChanged(Controller, "CCSPlayerController", "m_szClan");
		}

		/// <summary>
		/// Get clan tag for a player with default from SDK config
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string GetClanTag()
		{
			if (_clanTag?.Value != null)
				return _clanTag.Value.Value;

			// Get default from SDK config
			string defaultTag = SdkInternalConfig.GetValue("default_clan_tag", "chatprocessor", string.Empty);
			if (!string.IsNullOrEmpty(defaultTag))
			{
				// Replace placeholders in default tag
				return PlaceholderHandler.ReplacePlayerPlaceholders(defaultTag, Controller);
			}

			return string.Empty;
		}

		/// <summary>
		/// Set name tag for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetNameTag(string? tag, ActionPriority priority = ActionPriority.Normal)
		{
			if (tag == null)
			{
				_nameTag = null;
				return;
			}

			// Check if priority allows this change
			if (_nameTag != null && priority < _nameTag.Value.Priority)
				return;

			_nameTag = new PriorityValue<string>(tag, priority);
		}

		/// <summary>
		/// Get name tag for a player with default from SDK config
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string GetNameTag()
		{
			if (_nameTag?.Value != null)
				return _nameTag.Value.Value;

			// Get default from SDK config
			string defaultTag = SdkInternalConfig.GetValue("default_chat_tag", "chatprocessor", string.Empty);
			if (!string.IsNullOrEmpty(defaultTag))
			{
				// Replace placeholders in default tag
				return PlaceholderHandler.ReplacePlayerPlaceholders(defaultTag, Controller);
			}

			return string.Empty;
		}

		/// <summary>
		/// Set name color for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetNameColor(string? color, ActionPriority priority = ActionPriority.Normal)
		{
			if (color == null)
			{
				_nameColor = null;
				return;
			}

			// Check if priority allows this change
			if (_nameColor != null && priority < _nameColor.Value.Priority)
				return;

			_nameColor = new PriorityValue<string>(color, priority);
		}

		/// <summary>
		/// Get name color for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public char GetNameColor() =>
			_nameColor != null ? ChatColor.GetValue(_nameColor.Value.Value, Controller) : ChatColors.ForPlayer(Controller);

		/// <summary>
		/// Set chat color for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetChatColor(string? color, ActionPriority priority = ActionPriority.Normal)
		{
			if (color == null)
			{
				_chatColor = null;
				return;
			}

			// Check if priority allows this change
			if (_chatColor != null && priority < _chatColor.Value.Priority)
				return;

			_chatColor = new PriorityValue<string>(color, priority);
		}

		/// <summary>
		/// Get chat color for a player
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public char GetChatColor() =>
			_chatColor != null ? ChatColor.GetValue(_chatColor.Value.Value, Controller) : ChatColors.Default;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrintToChat(string message)
			=> Controller.PrintToChat(message.StartsWith(' ') ? message : " " + message);
	}
}