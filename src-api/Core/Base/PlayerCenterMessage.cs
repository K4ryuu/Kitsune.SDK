using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
using Kitsune.SDK.Core.Models.Player;

namespace Kitsune.SDK.Core.Base
{
	public partial class Player
	{
		private readonly List<CenterMessage> _centerMessages = new(32);
		private CenterMessage? _cachedTopMessage;
		private float _lastUpdateTime;
		private bool _cacheInvalidated = false;
		private bool _wasShowingMessage = false;

		private const float MESSAGE_REFRESH_INTERVAL = 0.25f;

		/// <summary>
		/// Show the current center message if any
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ShowCenterMessage()
		{
			// Fast path for no messages
			if (_centerMessages.Count == 0)
			{
				if (_cachedTopMessage.HasValue && _wasShowingMessage)
				{
					Controller.PrintToCenterHtml("");

					_cachedTopMessage = null;
					_cacheInvalidated = false;
					_wasShowingMessage = false;
				}

				return;
			}

			float currentTime = Server.CurrentTime;

			// Avoid updating the cache too frequently
			bool needsUpdate = !_cachedTopMessage.HasValue || _cacheInvalidated || _lastUpdateTime + MESSAGE_REFRESH_INTERVAL < currentTime || (_cachedTopMessage.HasValue && _cachedTopMessage.Value.Duration <= currentTime);

			if (needsUpdate)
			{
				UpdateCachedMessage(currentTime);
				_cacheInvalidated = false;
			}

			// Display message if we have one
			if (_cachedTopMessage.HasValue)
			{
				Controller.PrintToCenterHtml(_cachedTopMessage.Value.Message);
				_wasShowingMessage = true;
			}
		}

		/// <summary>
		/// Update the cached top message
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void UpdateCachedMessage(float currentTime)
		{
			// Remove expired messages first
			for (int i = _centerMessages.Count - 1; i >= 0; i--)
			{
				if (_centerMessages[i].Duration <= currentTime)
				{
					_centerMessages.RemoveAt(i);
				}
			}

			// No messages after cleanup
			if (_centerMessages.Count == 0)
			{
				_cachedTopMessage = null;
				return;
			}

			// Find highest priority message
			CenterMessage topMessage = _centerMessages[0];

			for (int i = 1; i < _centerMessages.Count; i++)
			{
				var message = _centerMessages[i];

				if (message.Priority > topMessage.Priority || (message.Priority == topMessage.Priority && message.Duration < topMessage.Duration))
				{
					topMessage = message;
				}
			}

			_cachedTopMessage = topMessage;
			_lastUpdateTime = currentTime;
		}

		/// <summary>
		/// Print a message to the center of the screen
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PrintToCenter(string message, int duration = 3, ActionPriority priority = ActionPriority.Low)
		{
			if (string.IsNullOrEmpty(message))
				return;

			float currentTime = Server.CurrentTime;
			float messageEndTime = currentTime + duration;

			// Check if this message would be completely blocked by higher priority messages
			// We check against all messages (including expired ones) since expired cleanup
			// happens in UpdateCachedMessage to avoid duplicate work
			bool wouldBeBlocked = false;

			for (int i = 0; i < _centerMessages.Count; i++)
			{
				var existingMessage = _centerMessages[i];

				// Skip expired messages
				if (existingMessage.Duration <= currentTime)
					continue;

				// If there's a higher priority message that completely covers our message duration
				if (existingMessage.Priority > priority && existingMessage.Duration >= messageEndTime)
				{
					wouldBeBlocked = true;
					break;
				}
			}

			// Only add the message if it won't be completely blocked
			if (!wouldBeBlocked)
			{
				_centerMessages.Add(new CenterMessage(message, messageEndTime, priority));
				_cacheInvalidated = true;
			}
		}
	}
}