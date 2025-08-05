using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Core.Models.Events.Args;
using Microsoft.Extensions.Logging;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Services.Events;
using Kitsune.SDK.Core.Models.Events;

namespace Kitsune.SDK.Services.Events
{
    /// <summary>
    /// Implementation of the SDK event manager optimized for game performance.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the SdkEventManager class.
    /// </remarks>
    /// <param name="logger">Optional logger for error handling.</param>
    public sealed class SdkEventManager(SdkPlugin plugin) : ISdkEventManager, IDisposable
    {
        // Compact record struct to minimize memory overhead and improve cache locality
        private readonly record struct Subscription(Guid Id, EventType EventType, HookMode HookMode, Func<SdkEventArgs, HookResult> Callback, Type EventArgsType, string? CustomEventName = null);

        // Use arrays for fast iteration and better memory layout
        private Subscription[] _subscriptions = [];
        private readonly object _lock = new();
        private readonly ILogger? _logger = plugin.Logger;

        // Event type to subscriptions index map for fast lookup
        // Using ReadOnly arrays for fast and allocation-free iteration
        private readonly ConcurrentDictionary<(EventType, HookMode), Subscription[]> _subscriptionCache = new();

        // Store registered custom events with their source plugin
        private readonly ConcurrentBag<string> _registeredCustomEvents = [];

        // Debug mode flag for exception propagation
        private static bool _debugMode;
        public static bool DebugMode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _debugMode;
            set => _debugMode = value;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid Subscribe(EventType eventType, Func<SdkEventArgs, HookResult> callback, HookMode hookMode = HookMode.Post)
        {
            ArgumentNullException.ThrowIfNull(callback);

            var id = Guid.NewGuid();
            var subscription = new Subscription(id, eventType, hookMode, callback, typeof(SdkEventArgs));

            lock (_lock)
            {
                var newArray = new Subscription[_subscriptions.Length + 1];
                Array.Copy(_subscriptions, newArray, _subscriptions.Length);
                newArray[^1] = subscription;
                _subscriptions = newArray;
                _subscriptionCache.Clear(); // Invalidate cache when subscriptions change
            }

            return id;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid Subscribe<T>(EventType eventType, Func<T, HookResult> callback, HookMode hookMode = HookMode.Post) where T : SdkEventArgs
        {
            ArgumentNullException.ThrowIfNull(callback);

            // Inline the wrapper for better performance
            HookResult WrappedCallback(SdkEventArgs e) => e is T typedArgs ? callback(typedArgs) : HookResult.Continue;

            var id = Guid.NewGuid();
            var subscription = new Subscription(id, eventType, hookMode, WrappedCallback, typeof(T));

            lock (_lock)
            {
                var newArray = new Subscription[_subscriptions.Length + 1];
                Array.Copy(_subscriptions, newArray, _subscriptions.Length);
                newArray[^1] = subscription;
                _subscriptions = newArray;
                _subscriptionCache.Clear(); // Invalidate cache when subscriptions change
            }

            return id;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsubscribe(Guid subscriptionId)
        {
            lock (_lock)
            {
                for (int i = 0; i < _subscriptions.Length; i++)
                {
                    if (_subscriptions[i].Id == subscriptionId)
                    {
                        // Create new array without this subscription
                        var newArray = new Subscription[_subscriptions.Length - 1];

                        if (i > 0)
                            Array.Copy(_subscriptions, 0, newArray, 0, i);

                        if (i < _subscriptions.Length - 1)
                            Array.Copy(_subscriptions, i + 1, newArray, i, _subscriptions.Length - i - 1);

                        _subscriptions = newArray;
                        _subscriptionCache.Clear(); // Invalidate cache
                        return true;
                    }
                }
            }

            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Dispatch(SdkEventArgs eventArgs, HookMode hookMode)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);

            var eventType = eventArgs.EventType;
            var cacheKey = (eventType, hookMode);

            // For custom events, also check the event name
            string? customEventName = null;
            if (eventType == EventType.Custom && eventArgs is CustomEventArgs customEventArgs)
            {
                customEventName = customEventArgs.EventName;
            }

            // Get matching subscriptions from cache or create new
            if (!_subscriptionCache.TryGetValue(cacheKey, out var matchingSubscriptions))
            {
                // Create a snapshot of the subscriptions for thread safety
                var snapshot = _subscriptions;

                // First count matching subscriptions
                int matchCount = 0;
                for (int i = 0; i < snapshot.Length; i++)
                {
                    if (snapshot[i].EventType == eventType && snapshot[i].HookMode == hookMode)
                    {
                        // For custom events, also check the event name
                        if (eventType == EventType.Custom && customEventName != null)
                        {
                            if (snapshot[i].CustomEventName == customEventName)
                            {
                                matchCount++;
                            }
                        }
                        else
                        {
                            matchCount++;
                        }
                    }
                }

                // Allocate exactly the right size array
                matchingSubscriptions = new Subscription[matchCount];

                // Fill the array with matching subscriptions
                if (matchCount > 0)
                {
                    int index = 0;
                    for (int i = 0; i < snapshot.Length; i++)
                    {
                        if (snapshot[i].EventType == eventType && snapshot[i].HookMode == hookMode)
                        {
                            // For custom events, also check the event name
                            if (eventType == EventType.Custom && customEventName != null)
                            {
                                if (snapshot[i].CustomEventName == customEventName)
                                {
                                    matchingSubscriptions[index++] = snapshot[i];
                                }
                            }
                            else
                            {
                                matchingSubscriptions[index++] = snapshot[i];
                            }
                        }
                    }
                }

                // Only cache non-custom events for performance
                if (eventType != EventType.Custom)
                {
                    _subscriptionCache[cacheKey] = matchingSubscriptions;
                }
            }
            else if (eventType == EventType.Custom && customEventName != null)
            {
                // For custom events, we need to filter the cached subscriptions by event name
                var tempList = new List<Subscription>();

                foreach (var sub in matchingSubscriptions)
                {
                    if (sub.CustomEventName == customEventName)
                    {
                        tempList.Add(sub);
                    }
                }

                matchingSubscriptions = tempList.ToArray();
            }

            // Early exit for common case - no subscriptions
            if (matchingSubscriptions.Length == 0)
            {
                return hookMode != HookMode.Pre || eventArgs.Result == HookResult.Continue;
            }

            // Process subscriptions
            bool continueProcessing = true;
            Type eventArgsType = eventArgs.GetType();

            for (int i = 0; i < matchingSubscriptions.Length; i++)
            {
                ref readonly var sub = ref matchingSubscriptions[i];

                // Skip type check if we know it's the base type
                if (sub.EventArgsType == typeof(SdkEventArgs) || sub.EventArgsType.IsAssignableFrom(eventArgsType))
                {
                    try
                    {
                        // Call the callback and update the event result
                        HookResult result = sub.Callback(eventArgs);
                        eventArgs.Result = result;

                        // Optimize for the common case (Continue)
                        if (result == HookResult.Continue)
                            continue;

                        // Handle special hook results
                        switch (result)
                        {
                            case HookResult.Stop:
                                // Stop processing completely
                                return hookMode != HookMode.Pre;

                            case HookResult.Handled:
                                // For Pre hooks, this means block the action
                                continueProcessing = false;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in event handler for {EventType}", eventType);

                        if (_debugMode)
                            throw;
                    }
                }
            }

            // For Pre hooks, determine if original action should proceed
            return hookMode != HookMode.Pre || continueProcessing;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RegisterCustom(string eventName)
        {
            ArgumentException.ThrowIfNullOrEmpty(eventName);

            // Check if the event name already exists
            if (_registeredCustomEvents.Contains(eventName))
            {
                _logger?.LogWarning("Custom event '{EventName}' is already registered", eventName);
                return false;
            }

            // Register the custom event
            _registeredCustomEvents.Add(eventName);
            return true;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid SubscribeCustom(string eventName, Func<CustomEventArgs, HookResult> callback, HookMode hookMode = HookMode.Post)
        {
            ArgumentException.ThrowIfNullOrEmpty(eventName);
            ArgumentNullException.ThrowIfNull(callback);

            // Check if the event name is registered
            if (!_registeredCustomEvents.Contains(eventName))
            {
                _logger?.LogWarning("Cannot subscribe to unregistered custom event '{EventName}'", eventName);
                return Guid.Empty;
            }

            // Inline the wrapper for better performance
            HookResult WrappedCallback(SdkEventArgs e) => e is CustomEventArgs customArgs && customArgs.EventName == eventName
                ? callback(customArgs)
                : HookResult.Continue;

            var id = Guid.NewGuid();
            var subscription = new Subscription(id, EventType.Custom, hookMode, WrappedCallback, typeof(CustomEventArgs), eventName);

            lock (_lock)
            {
                var newArray = new Subscription[_subscriptions.Length + 1];
                Array.Copy(_subscriptions, newArray, _subscriptions.Length);
                newArray[^1] = subscription;
                _subscriptions = newArray;
                _subscriptionCache.Clear(); // Invalidate cache when subscriptions change
            }

            return id;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TriggerCustom(string eventName, object? data = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(eventName);

            // Check if the event name is registered
            if (!_registeredCustomEvents.Contains(eventName))
            {
                _logger?.LogWarning("Cannot trigger unregistered custom event '{EventName}'", eventName);
                return false;
            }

            // Create custom event args
            var eventArgs = new CustomEventArgs(eventName, plugin.ModuleName, data);

            // First dispatch Pre hooks
            bool preResult = Dispatch(eventArgs, HookMode.Pre);

            // If Pre hooks weren't blocked, dispatch Post hooks
            if (preResult)
            {
                Dispatch(eventArgs, HookMode.Post);
            }

            return preResult;
        }

        /// <summary>
        /// Disposes the event manager and cleans up all subscriptions.
        /// </summary>
        public void Dispose()
        {
            _subscriptions = [];
            _subscriptionCache.Clear();
            _registeredCustomEvents.Clear();
            GC.SuppressFinalize(this);
        }
    }
}