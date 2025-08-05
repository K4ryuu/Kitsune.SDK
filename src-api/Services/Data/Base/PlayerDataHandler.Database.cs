using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Dapper;
using Kitsune.SDK.Core.Models.Events.Enums;
using Kitsune.SDK.Services.Data.Storage;
using CounterStrikeSharp.API.Core;
using Kitsune.SDK.Utilities;
using Kitsune.SDK.Core.Base;
using Kitsune.SDK.Core.Models.Events.Args;
using CounterStrikeSharp.API;

namespace Kitsune.SDK.Services.Data.Base
{
    /// <summary>
    /// Database operations for PlayerDataHandler
    /// </summary>
    public abstract partial class PlayerDataHandler
    {
        #region Database Configuration

        /// <summary>
        /// Gets the table name, loading it from config if not already loaded
        /// </summary>
        private string GetTableName()
        {
            // If table name is already loaded, return it
            if (_tableName != null)
                return _tableName;

            var configKey = _dataType == DataType.Storage ? "storage_table" : "settings_table";
            var fallbackValue = _dataType == DataType.Storage ? "kitsune_player_storage" : "kitsune_player_settings";

            try
            {
                _tableName = _plugin.Config.GetLocalValue<string>(configKey, "database");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to get table name from config, using default: {Error}", ex.Message);
                _tableName = fallbackValue;
            }

            return _tableName ?? fallbackValue;
        }

        #endregion

        #region Database Structure

        /// <summary>
        /// Initializes the database structure (table and columns) for this handler
        /// </summary>
        public async Task InitializeDatabaseStructureAsync()
        {
            try
            {
                using var connection = await CreateConnectionAsync();

                // Create table if it doesn't exist
                await CreateTableIfNotExists(connection);

                // Create column if it doesn't exist
                await CreateColumnIfNotExists(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize database structure for {DataType} handler: {ErrorMessage}", _dataType, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Creates a table if it doesn't exist
        /// </summary>
        private async Task CreateTableIfNotExists(MySqlConnection? existingConnection = null)
        {
            try
            {
                var connection = existingConnection ?? await CreateConnectionAsync();
                bool shouldDispose = existingConnection == null;

                var tableName = TableName;
                var createTableQuery = $@"
                    CREATE TABLE IF NOT EXISTS `{StringEx.ValidateSqlIdentifier(tableName)}` (
                        `steam_id` VARCHAR(32) NOT NULL,
                        `name` VARCHAR(64) NOT NULL DEFAULT 'Unknown',
                        `last_online` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        PRIMARY KEY (`steam_id`),
                        INDEX `idx_last_online` (`last_online`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

                await RetryHelper.ExecuteWithRetryAsync(
                    async () => await connection.ExecuteAsync(createTableQuery),
                    $"CreateTable_{tableName}",
                    _logger);

                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create table {Table}", TableName);
                throw;
            }
        }

        /// <summary>
        /// Creates a column if it doesn't exist
        /// </summary>
        private async Task CreateColumnIfNotExists(MySqlConnection? existingConnection = null)
        {
            try
            {
                var connection = existingConnection ?? await CreateConnectionAsync();
                bool shouldDispose = existingConnection == null;

                const string columnExistsQuery = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @TableName
                    AND COLUMN_NAME = @ColumnName";

                var tableName = TableName;
                var columnExists = await RetryHelper.ExecuteWithRetryAsync(
                    async () => await connection.ExecuteScalarAsync<int>(columnExistsQuery,
                        new { TableName = tableName, ColumnName = _columnName }),
                    $"CheckColumnExists_{tableName}_{_columnName}",
                    _logger) > 0;

                if (!columnExists)
                {
                    var addColumnQuery = $@"
                        ALTER TABLE `{StringEx.ValidateSqlIdentifier(tableName)}`
                        ADD COLUMN `{StringEx.ValidateSqlIdentifier(_columnName)}` JSON NULL";

                    await RetryHelper.ExecuteWithRetryAsync(
                        async () => await connection.ExecuteAsync(addColumnQuery),
                        $"AddColumn_{tableName}_{_columnName}",
                        _logger);
                }

                if (shouldDispose)
                {
                    connection.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create column {Column} in table {Table}", _columnName, TableName);
            }
        }

        /// <summary>
        /// Creates and opens a database connection
        /// </summary>
        protected async Task<MySqlConnection> CreateConnectionAsync()
        {
            // Don't try to create connections during disposal
            if (_isDisposing)
            {
                throw new ObjectDisposedException($"PlayerDataHandler for {_ownerPlugin}",
                    "Cannot create database connections during disposal");
            }

            try
            {
                var connection = new MySqlConnection(_plugin.Config.GetConnectionString());
                await connection.OpenAsync();
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create database connection for plugin {Plugin}: {ErrorMessage}", _ownerPlugin, ex.Message);
                throw;
            }
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Save all handler data for a player in a single batch transaction
        /// </summary>
        public static async Task SaveAllPlayerDataBatchAsync(ulong steamId, IList<PlayerDataHandler> handlers)
        {
            if (handlers.Count == 0)
                return;

            // Group handlers by plugin to get a single database connection
            var firstHandler = handlers.First();

            try
            {
                using var connection = await firstHandler.CreateConnectionAsync();
                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    int savedCount = 0;

                    foreach (var handler in handlers)
                    {
                        try
                        {
                            // Fire pre-save event
                            if (!handler.FireEvent(steamId, EventType.PlayerDataSave, HookMode.Pre))
                                continue;

                            // Synchronize tracked properties before saving (for StorageHandler)
                            if (handler is StorageHandler storageHandler && !handler._isDisposing)
                            {
                                await storageHandler.SynchronizeTrackedProperties(steamId);
                            }

                            // Prepare data to save
                            var cache = GetPlayerCacheStatic(steamId, handler.HandlerDataType);

                            // Debug: Show cache contents for this handler
                            if (handler.OwnerPlugin == "K4-Zenith-Stats")
                            {
                                var statsEntries = cache.Where(kvp => kvp.Key.StartsWith("K4-Zenith-Stats:", StringComparison.Ordinal)).ToList();
                            }

                            var dataToSave = handler.PrepareDataForSaveFromCache(cache);

                            // Skip if no data to save
                            if (dataToSave.Count == 0)
                            {
                                continue;
                            }

                            // Save to database within the transaction
                            var json = JsonSerializer.Serialize(dataToSave);
                            var tableName = handler.TableName;


                            var query = $@"
                                INSERT INTO `{StringEx.ValidateSqlIdentifier(tableName)}` (`steam_id`, `{StringEx.ValidateSqlIdentifier(handler._columnName)}`, `last_online`)
                                VALUES (@SteamID, @Data, CURRENT_TIMESTAMP)
                                ON DUPLICATE KEY UPDATE
                                    `{StringEx.ValidateSqlIdentifier(handler._columnName)}` = @Data,
                                    `last_online` = CURRENT_TIMESTAMP";

                            await connection.ExecuteAsync(query, new { SteamID = steamId.ToString(), Data = json }, transaction);

                            handler.UpdateSaveTime(steamId);

                            // Fire post-save event
                            handler.FireEvent(steamId, EventType.PlayerDataSave, HookMode.Post);

                            savedCount++;
                        }
                        catch
                        {
                            throw;
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Safely converts a JsonElement number to the appropriate .NET type
        /// </summary>
        private static object ConvertJsonNumber(JsonElement jsonElement)
        {
            // Try to get as different numeric types, starting with most specific
            if (jsonElement.TryGetInt32(out int intValue))
                return intValue;

            if (jsonElement.TryGetInt64(out long longValue))
                return longValue;

            if (jsonElement.TryGetDouble(out double doubleValue))
                return doubleValue;

            // Fallback to decimal for very precise numbers
            if (jsonElement.TryGetDecimal(out decimal decimalValue))
                return decimalValue;

            // If all else fails, return the raw number as string
            return jsonElement.GetRawText();
        }

        #endregion

        #region Database Operations

        /// <summary>
        /// Load player data from database - simple single query
        /// </summary>
        public async Task LoadPlayerDataAsync(ulong steamId)
        {
            try
            {

                using var connection = await CreateConnectionAsync();

                var tableName = TableName;
                var query = $@"
                    SELECT `{StringEx.ValidateSqlIdentifier(_columnName)}`
                    FROM `{StringEx.ValidateSqlIdentifier(tableName)}`
                    WHERE `steam_id` = @SteamID";

                var result = await connection.ExecuteScalarAsync<string>(query, new { SteamID = steamId.ToString() });

                if (result != null)
                {
                    var cache = GetPlayerCache(steamId, _dataType);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(result);

                    if (data != null)
                    {
                        foreach (var (key, value) in data)
                        {
                            var fullKey = BuildFullKey(key);

                            // Convert JsonElement to proper type
                            object? finalValue = value;
                            if (value is JsonElement jsonElement)
                            {
                                // Convert JsonElement based on its ValueKind
                                finalValue = jsonElement.ValueKind switch
                                {
                                    JsonValueKind.Number => ConvertJsonNumber(jsonElement),
                                    JsonValueKind.String => jsonElement.GetString(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    JsonValueKind.Null => null,
                                    _ => jsonElement.ToString()
                                };
                            }

                            cache[fullKey] = finalValue;
                        }
                    }

                    // Update last_online timestamp
                    var updateQuery = $@"
                        UPDATE `{StringEx.ValidateSqlIdentifier(tableName)}`
                        SET `last_online` = CURRENT_TIMESTAMP
                        WHERE `steam_id` = @SteamID";

                    await connection.ExecuteAsync(updateQuery, new { SteamID = steamId.ToString() });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading {DataType} data for player {SteamId}", _dataType, steamId);
            }
        }

        /// <summary>
        /// Save player data to database - simple single query
        /// </summary>
        public async Task SavePlayerDataAsync(ulong steamId)
        {

            if (!CanSavePlayer(steamId))
            {
                return;
            }

            try
            {
                // Synchronize tracked properties before saving (for StorageHandler)
                if (this is StorageHandler storageHandler && !_isDisposing)
                {
                    await storageHandler.SynchronizeTrackedProperties(steamId);
                }

                // Prepare data to save
                var cache = GetPlayerCache(steamId, _dataType);

                // Debug: Show what's in cache before save
                var cacheForPlugin = cache.Where(kvp => kvp.Key.StartsWith($"{_ownerPlugin}:", StringComparison.Ordinal)).ToList();

                var dataToSave = PrepareDataForSaveFromCache(cache);

                // Skip if no data to save
                if (dataToSave.Count == 0)
                {
                    return;
                }

                // Save to database
                var connection = new MySqlConnection(_plugin.Config.GetConnectionString());
                await connection.OpenAsync(); // Synchronous open

                var json = JsonSerializer.Serialize(dataToSave);
                var tableName = TableName;

                var query = $@"
                    INSERT INTO `{StringEx.ValidateSqlIdentifier(tableName)}` (`steam_id`, `{StringEx.ValidateSqlIdentifier(_columnName)}`, `last_online`)
                    VALUES (@SteamID, @Data, CURRENT_TIMESTAMP)
                    ON DUPLICATE KEY UPDATE
                        `{StringEx.ValidateSqlIdentifier(_columnName)}` = @Data,
                        `last_online` = CURRENT_TIMESTAMP";

                await connection.ExecuteAsync(query, new { SteamID = steamId.ToString(), Data = json }); // Synchronous execute

                UpdateSaveTime(steamId);

                // Fire post-saving event
                Server.NextWorldUpdate(() =>
                {
                    FireEvent(steamId, EventType.PlayerDataSave, HookMode.Post);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving {DataType} data for player {SteamId}", _dataType, steamId);
            }
        }

        /// <summary>
        /// Load data for multiple players in one query (bulk load)
        /// </summary>
        public async Task LoadMultiplePlayersAsync(IEnumerable<ulong> steamIds)
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0) return;

            try
            {
                using var connection = await CreateConnectionAsync();

                // Build IN clause
                var steamIdParams = string.Join(",", steamIdList.Select((_, index) => $"@SteamID{index}"));
                var tableName = TableName;

                // Use alias for the data column to ensure consistent naming
                var query = $@"
                    SELECT `steam_id`,
                        `{StringEx.ValidateSqlIdentifier(_columnName)}` AS `data`
                    FROM `{StringEx.ValidateSqlIdentifier(tableName)}`
                    WHERE `steam_id` IN ({steamIdParams})";

                // Build parameters
                var parameters = new DynamicParameters();
                for (int i = 0; i < steamIdList.Count; i++)
                {
                    parameters.Add($"SteamID{i}", steamIdList[i].ToString());
                }

                var results = await connection.QueryAsync(query, parameters);

                // Process results
                foreach (var row in results)
                {
                    var rowDict = (IDictionary<string, object>)row;
                    var steamIdStr = rowDict["steam_id"]?.ToString();
                    var jsonData = rowDict["data"]?.ToString(); // Most már mindig "data" néven lesz

                    if (ulong.TryParse(steamIdStr, out var steamId) && !string.IsNullOrEmpty(jsonData))
                    {
                        var cache = GetPlayerCache(steamId, _dataType);
                        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonData);

                        if (data != null)
                        {
                            foreach (var (key, value) in data)
                            {
                                var fullKey = BuildFullKey(key);
                                cache[fullKey] = value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk loading {DataType} data for {Count} players", _dataType, steamIdList.Count);

                // Fallback: load individually
                foreach (var steamId in steamIdList)
                {
                    await LoadPlayerDataAsync(steamId);
                }
            }
        }

        public static async Task LoadMultiplePlayersForPluginAsync(IEnumerable<ulong> steamIds, SdkPlugin plugin)
        {
            var steamIdList = steamIds.ToList();
            if (steamIdList.Count == 0)
                return;

            var pluginName = Path.GetFileNameWithoutExtension(plugin.ModulePath);
            var relevantHandlers = new List<PlayerDataHandler>();

            // Csak az adott plugin handler-eit gyűjtjük össze
            foreach (var (key, handler) in _handlers)
            {
                if (key.StartsWith($"{pluginName}:"))
                {
                    relevantHandlers.Add(handler);
                }
            }

            if (relevantHandlers.Count == 0)
                return;

            try
            {
                // Bulk load minden handler típushoz
                var loadTasks = relevantHandlers.Select(handler => handler.LoadMultiplePlayersAsync(steamIdList));
                await Task.WhenAll(loadTasks);

                // Storage handler-ek esetén tároljuk az eredeti értékeket
                foreach (var handler in relevantHandlers.OfType<StorageHandler>())
                {
                    foreach (var steamId in steamIdList)
                    {
                        handler.StoreOriginalValues(steamId);
                    }
                }

                // Fire events minden játékoshoz
                foreach (var steamId in steamIdList)
                {
                    FirePluginPlayerDataLoadEvent(steamId, plugin);
                }
            }
            catch (Exception ex)
            {
                var logger = relevantHandlers.FirstOrDefault()?._logger;
                logger?.LogError(ex, "Error in plugin-specific bulk loading for {Count} players, plugin {Plugin}", steamIdList.Count, plugin.ModuleName);
            }
        }

        private static void FirePluginPlayerDataLoadEvent(ulong steamId, SdkPlugin plugin)
        {
            var eventManager = plugin.Events;
            if (eventManager != null)
            {
                // Only fire the event if the player is loaded
                var player = Core.Base.Player.Find(steamId);
                if (player?.IsLoaded == true)
                {
                    var eventArgs = new PlayerDataEventArgs(steamId, plugin.ModuleName, EventType.PlayerDataLoad);
                    eventManager.Dispatch(eventArgs, HookMode.Post);
                }
            }
        }

        /// <summary>
        /// Save data for multiple players in one transaction (bulk save)
        /// </summary>
        public async Task SaveMultiplePlayersAsync(IEnumerable<ulong> steamIds)
        {
            var playersToSave = steamIds.Where(CanSavePlayer).ToList();
            if (playersToSave.Count == 0) return;

            MySqlConnection? connection = null;
            try
            {
                connection = await CreateConnectionAsync();
                using var transaction = await connection.BeginTransactionAsync();

                foreach (var steamId in playersToSave)
                {
                    if (!FireEvent(steamId, EventType.PlayerDataSave, HookMode.Pre))
                        continue;

                    try
                    {
                        // Synchronize tracked properties before saving (for StorageHandler)
                        if (this is StorageHandler storageHandler && !_isDisposing)
                        {
                            await storageHandler.SynchronizeTrackedProperties(steamId);
                        }

                        var cache = GetPlayerCache(steamId, _dataType);
                        var dataToSave = PrepareDataForSaveFromCache(cache);

                        // Skip if no data to save
                        if (dataToSave.Count == 0)
                            continue;

                        var json = JsonSerializer.Serialize(dataToSave);
                        var tableName = TableName;

                        var query = $@"
                            INSERT INTO `{StringEx.ValidateSqlIdentifier(tableName)}` (`steam_id`, `{StringEx.ValidateSqlIdentifier(_columnName)}`)
                            VALUES (@SteamID, @Data)
                            ON DUPLICATE KEY UPDATE `{StringEx.ValidateSqlIdentifier(_columnName)}` = @Data";

                        await connection.ExecuteAsync(query, new { SteamID = steamId.ToString(), Data = json }, transaction);

                        UpdateSaveTime(steamId);
                        FireEvent(steamId, EventType.PlayerDataSave, HookMode.Post);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving data for player {SteamId} in bulk operation", steamId);
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk saving {DataType} data for {Count} players", _dataType, playersToSave.Count);
            }
            finally
            {
                connection?.Dispose();
            }
        }

        /// <summary>
        /// Save all online players efficiently
        /// </summary>
        public async Task SaveAllOnlinePlayerDataAsync()
        {
            var onlinePlayers = Player.List.Values;
            if (onlinePlayers.Count != 0)
            {
                await SaveMultiplePlayersAsync(onlinePlayers.Select(p => p.SteamID));
            }
        }

        /// <summary>
        /// Prepare data for save from cache
        /// </summary>
        public Dictionary<string, object?> PrepareDataForSaveFromCache(ConcurrentDictionary<string, object?> cache)
        {
            var dataToSave = new Dictionary<string, object?>();

            // Get cache data for this plugin
            foreach (var (fullKey, value) in cache)
            {
                if (fullKey.StartsWith($"{_ownerPlugin}:", StringComparison.Ordinal))
                {
                    string keyWithoutPrefix = fullKey[(_ownerPlugin.Length + 1)..];
                    dataToSave[keyWithoutPrefix] = value;
                }
            }

            return dataToSave;
        }

        /// <summary>
        /// Cleanup inactive players from the database
        /// </summary>
        public async Task CleanupInactivePlayersAsync(int retentionDays)
        {
            if (retentionDays <= 0)
                return;

            try
            {
                using var connection = await CreateConnectionAsync();
                var tableName = TableName;

                // Delete players who haven't been online in X days
                var deleteQuery = $@"
                    DELETE FROM `{StringEx.ValidateSqlIdentifier(tableName)}`
                    WHERE `last_online` < DATE_SUB(NOW(), INTERVAL @RetentionDays DAY)";

                var deletedCount = await connection.ExecuteAsync(deleteQuery, new { RetentionDays = retentionDays });

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} inactive players from {Table} (older than {Days} days)",
                        deletedCount, tableName, retentionDays);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup inactive players from {Table}", TableName);
            }
        }

        #endregion
    }
}