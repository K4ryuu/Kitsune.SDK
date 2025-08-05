using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Kitsune.SDK.Utilities
{
    /// <summary>
    /// Helper class for retry logic with exponential backoff
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Executes an async action with retry logic
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, ILogger? logger = null, int maxRetries = 3, int initialDelayMs = 100)
        {
            for (int retry = 0; retry <= maxRetries; retry++)
            {
                try
                {
                    return await operation();
                }
                catch (MySqlException ex) when (IsTransientError(ex))
                {
                    if (retry == maxRetries)
                    {
                        logger?.LogError($"Operation {operationName} failed after {maxRetries} retries: {ex.Message}");
                        throw;
                    }

                    int delay = initialDelayMs; // Simple linear delay
                    logger?.LogWarning($"Transient error in {operationName}, retrying in {delay}ms (attempt {retry + 1}/{maxRetries})");
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Non-retryable error in {operationName}: {ex.Message}");
                    throw;
                }
            }

            throw new InvalidOperationException($"Operation {operationName} failed to complete");
        }

        /// <summary>
        /// Executes an async action with retry logic (void return)
        /// </summary>
        public static async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName, ILogger? logger = null, int maxRetries = 3, int initialDelayMs = 100)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await operation();
                return true;
            }, operationName, logger, maxRetries, initialDelayMs);
        }

        /// <summary>
        /// Determines if a MySQL exception is transient and should be retried
        /// </summary>
        private static bool IsTransientError(MySqlException ex)
        {
            switch (ex.Number)
            {
                case 1040: // Too many connections
                case 1053: // Server shutdown in progress
                case 1205: // Lock wait timeout exceeded
                case 1213: // Deadlock found when trying to get lock
                case 1226: // User has exceeded the resource
                case 1689: // Wait timeout expired
                case 2002: // Can't connect to server
                case 2003: // Can't connect to MySQL server
                case 2006: // MySQL server has gone away
                case 2013: // Lost connection to MySQL server during query
                case 2020: // Got packet bigger than 'max_allowed_packet' bytes
                    return true;
                default:
                    return false;
            }
        }
    }
}