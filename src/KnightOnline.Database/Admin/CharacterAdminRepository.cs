using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Diagnostics;

namespace KnightOnline.Database.Admin;

/// <summary>
/// MariaDB implementation of <see cref="ICharacterAdminRepository"/>.
///
/// Schema resilience: until the real KO_GAME export is converted, column sets
/// can vary between installs. The repository therefore probes
/// INFORMATION_SCHEMA once and only uses columns that exist (missing ones
/// surface in CharacterDetails.MissingColumns and become read-only in the
/// UI). strItem/strSerial are mandatory - without them there is nothing to
/// edit. strItemTime (and WAREHOUSE.WarehouseDataTime) are OPTIONAL: the
/// real KN_Online dump (Oct 2026) has neither column - it predates item
/// expiration support - so expirations simply read as 0 and are not written
/// back when the columns are absent.
/// </summary>
public sealed class CharacterAdminRepository : ICharacterAdminRepository
{
    private readonly string _gameConnectionString;
    private readonly string _accountConnectionString;
    private readonly SemaphoreSlim _probeGate = new(1, 1);
    private IReadOnlySet<string>? _userDataColumns;
    private IReadOnlySet<string>? _warehouseColumns;
    private bool? _hasItemTable;
    private bool? _hasCurrentUser;

    private static readonly string[] UserDataWishList =
    {
        "strUserID", "Nation", "Race", "Class", "Level", "Exp", "Loyalty", "Gold",
        "Zone", "Knights", "Fame", "Authority", "Hp", "Mp", "Sp",
        "Strong", "Sta", "Dex", "Intel", "Cha", "Points",
        "strItem", "strSerial", "strItemTime",
    };

    public CharacterAdminRepository(string gameConnectionString, string accountConnectionString)
    {
        _gameConnectionString = gameConnectionString;
        _accountConnectionString = accountConnectionString;
    }

    private async Task<MySqlConnection> OpenGameAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_gameConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private async Task<MySqlConnection> OpenAccountAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(_accountConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        if (_userDataColumns is not null)
            return;

        await _probeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_userDataColumns is not null)
                return;

            await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);
            _userDataColumns = await LoadColumnsAsync(connection, "USERDATA", cancellationToken).ConfigureAwait(false);
            _warehouseColumns = await LoadColumnsAsync(connection, "WAREHOUSE", cancellationToken).ConfigureAwait(false);
            _hasItemTable = await TableExistsAsync(connection, "ITEM", cancellationToken).ConfigureAwait(false);

            try
            {
                await using var account = await OpenAccountAsync(cancellationToken).ConfigureAwait(false);
                _hasCurrentUser = await TableExistsAsync(account, "CURRENTUSER", cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _hasCurrentUser = false; // account DB may be the same DB; GetOnline falls back to game DB
            }

            if (_userDataColumns.Count == 0)
                KoLog.Warn("[admin] USERDATA table not found in the game database - has the DB been converted yet?");
            else if (!_userDataColumns.Contains("strItem"))
                KoLog.Warn("[admin] USERDATA has no strItem column - unexpected schema.");
        }
        finally
        {
            _probeGate.Release();
        }
    }

    private static async Task<IReadOnlySet<string>> LoadColumnsAsync(MySqlConnection connection, string table, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table";
        command.Parameters.AddWithValue("@table", table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table";
        command.Parameters.AddWithValue("@table", table);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result) > 0;
    }

    private bool Has(string column) => _userDataColumns?.Contains(column) == true;

    // Item-expiration blobs: present in newer schemas, absent in the real
    // KN_Online dump. Everything expiration-shaped is conditional on these.
    private bool HasItemTime => Has("strItemTime");
    private bool HasWarehouseTime => _warehouseColumns?.Contains("WarehouseDataTime") == true;

    private string ExistingColumns(IEnumerable<string> wishlist)
        => string.Join(", ", wishlist.Where(Has).Select(c => $"`{c}`"));

    public async Task<AdminStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        long accounts = 0, characters = 0, online = 0, items = 0;
        var accountOk = true;
        var gameOk = true;
        string? error = null;

        try
        {
            await ProbeAsync(cancellationToken).ConfigureAwait(false);
            await using var game = await OpenGameAsync(cancellationToken).ConfigureAwait(false);
            if (_userDataColumns!.Count > 0)
                characters = await CountAsync(game, "SELECT COUNT(*) FROM `USERDATA`", cancellationToken).ConfigureAwait(false);
            if (_hasItemTable == true)
                items = await CountAsync(game, "SELECT COUNT(*) FROM `ITEM`", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            gameOk = false;
            error = $"Game DB: {ex.Message}";
        }

        try
        {
            await using var account = await OpenAccountAsync(cancellationToken).ConfigureAwait(false);
            if (await TableExistsAsync(account, "TB_USER", cancellationToken).ConfigureAwait(false))
                accounts = await CountAsync(account, "SELECT COUNT(*) FROM `TB_USER`", cancellationToken).ConfigureAwait(false);
            if (await TableExistsAsync(account, "CURRENTUSER", cancellationToken).ConfigureAwait(false))
                online = await CountAsync(account, "SELECT COUNT(*) FROM `CURRENTUSER`", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            accountOk = false;
            error = (error is null ? "" : error + " ") + $"Account DB: {ex.Message}";
        }

        return new AdminStats(accounts, characters, online, items, accountOk, gameOk, error);
    }

    private static async Task<long> CountAsync(MySqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<CharacterSummary>> SearchCharactersAsync(string term, int limit = 100, CancellationToken cancellationToken = default)
    {
        await ProbeAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<CharacterSummary>();
        if (_userDataColumns!.Count == 0)
            return results;

        var columns = ExistingColumns(new[] { "strUserID", "Nation", "Class", "Level", "Zone", "Knights", "Gold" });

        await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {columns} FROM `USERDATA` WHERE `strUserID` LIKE @term ORDER BY `strUserID` LIMIT @limit";
        command.Parameters.AddWithValue("@term", $"%{term.Trim()}%");
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 500));

        var names = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.GetString("strUserID");
                names.Add(name);
                results.Add(new CharacterSummary(
                    name,
                    null,
                    ReadInt(reader, "Nation"),
                    ReadInt(reader, "Class"),
                    ReadInt(reader, "Level"),
                    ReadInt(reader, "Zone"),
                    ReadInt(reader, "Knights"),
                    ReadLong(reader, "Gold"),
                    false));
            }
        }

        // Resolve account + online status in one extra round-trip each.
        var accounts = await ResolveAccountsAsync(connection, names, cancellationToken).ConfigureAwait(false);
        var online = await GetOnlineNamesAsync(cancellationToken).ConfigureAwait(false);

        return results
            .Select(r => r with
            {
                AccountId = accounts.GetValueOrDefault(r.Name),
                IsOnline = online.Contains(r.Name),
            })
            .ToList();
    }

    private static int ReadInt(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static long ReadLong(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static byte[] ReadBlob(MySqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return Array.Empty<byte>();
        return reader.GetValue(ordinal) as byte[] ?? Array.Empty<byte>();
    }

    /// <summary>Like <see cref="ReadBlob"/>, but returns empty when the SELECT
    /// did not include the column (schema lacks it) instead of throwing.</summary>
    private static byte[] ReadOptionalBlob(MySqlDataReader reader, string column)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), column, StringComparison.OrdinalIgnoreCase))
                return ReadBlob(reader, column);
        }
        return Array.Empty<byte>();
    }

    /// <summary>ACCOUNT_CHAR maps an account to its (up to) 3 character names.</summary>
    private async Task<Dictionary<string, string>> ResolveAccountsAsync(MySqlConnection gameConnection, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0 || !await TableExistsAsync(gameConnection, "ACCOUNT_CHAR", cancellationToken).ConfigureAwait(false))
            return map;

        await using var command = gameConnection.CreateCommand();
        var parameters = names.Select((_, i) => $"@n{i}").ToList();
        command.CommandText =
            $"SELECT strAccountID, strCharID1, strCharID2, strCharID3 FROM `ACCOUNT_CHAR` " +
            $"WHERE strCharID1 IN ({string.Join(",", parameters)}) OR strCharID2 IN ({string.Join(",", parameters)}) OR strCharID3 IN ({string.Join(",", parameters)})";
        for (var i = 0; i < names.Count; i++)
            command.Parameters.AddWithValue($"@n{i}", names.ElementAt(i));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var account = reader.GetString(0);
            for (var i = 1; i <= 3; i++)
            {
                if (!reader.IsDBNull(i))
                {
                    var character = reader.GetString(i);
                    if (character.Length > 0)
                        map[character] = account;
                }
            }
        }
        return map;
    }

    private async Task<HashSet<string>> GetOnlineNamesAsync(CancellationToken cancellationToken)
    {
        var online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // CURRENTUSER normally lives in the account DB; hybrid installs keep it in the game DB.
        foreach (var open in new Func<CancellationToken, Task<MySqlConnection>>[] { OpenAccountAsync, OpenGameAsync })
        {
            try
            {
                await using var connection = await open(cancellationToken).ConfigureAwait(false);
                if (!await TableExistsAsync(connection, "CURRENTUSER", cancellationToken).ConfigureAwait(false))
                    continue;

                var columns = await LoadColumnsAsync(connection, "CURRENTUSER", cancellationToken).ConfigureAwait(false);
                var column = columns.Contains("strCharID") ? "strCharID" : columns.Contains("strUserID") ? "strUserID" : null;
                if (column is null)
                    continue;

                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT `{column}` FROM `CURRENTUSER`";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!reader.IsDBNull(0))
                        online.Add(reader.GetString(0));
                }
                if (online.Count > 0)
                    break;
            }
            catch
            {
                // try the other database
            }
        }

        return online;
    }

    public async Task<CharacterDetails?> GetCharacterAsync(string name, CancellationToken cancellationToken = default)
    {
        await ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (_userDataColumns!.Count == 0 || !Has("strItem") || !Has("strSerial"))
            return null;

        var columns = ExistingColumns(UserDataWishList);

        await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);

        byte[] itemBlob, serialBlob, itemTimeBlob;
        CharacterDetails details;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT {columns} FROM `USERDATA` WHERE `strUserID` = @name";
            command.Parameters.AddWithValue("@name", name);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            itemBlob = ReadBlob(reader, "strItem");
            serialBlob = ReadBlob(reader, "strSerial");
            itemTimeBlob = ReadOptionalBlob(reader, "strItemTime");

            details = new CharacterDetails
            {
                Name = reader.GetString("strUserID"),
                AccountId = null,
                Nation = ReadInt(reader, "Nation"),
                Race = ReadInt(reader, "Race"),
                ClassId = ReadInt(reader, "Class"),
                Level = ReadInt(reader, "Level"),
                Exp = ReadLong(reader, "Exp"),
                Loyalty = ReadLong(reader, "Loyalty"),
                Gold = ReadLong(reader, "Gold"),
                Zone = ReadInt(reader, "Zone"),
                KnightsId = ReadInt(reader, "Knights"),
                Fame = ReadInt(reader, "Fame"),
                Authority = ReadInt(reader, "Authority"),
                Hp = ReadInt(reader, "Hp"),
                Mp = ReadInt(reader, "Mp"),
                Stats = new[]
                {
                    ReadInt(reader, "Strong"), ReadInt(reader, "Sta"), ReadInt(reader, "Dex"),
                    ReadInt(reader, "Intel"), ReadInt(reader, "Cha"),
                },
                StatPoints = ReadInt(reader, "Points"),
                Inventory = ItemBlobs.Parse(itemBlob, serialBlob, itemTimeBlob, InventorySlots.Total),
                Warehouse = Enumerable.Repeat(ItemSlotData.Empty, InventorySlots.WarehouseTotal).ToArray(),
                BankCoins = 0,
                IsOnline = false,
                MissingColumns = UserDataWishList.Where(c => !Has(c)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StoredInventorySlots = Math.Min(InventorySlots.Total, itemBlob.Length / ItemBlobs.ItemEntrySize),
                ConcurrencyToken = "",
            };
        }

        var accounts = await ResolveAccountsAsync(connection, new[] { details.Name }, cancellationToken).ConfigureAwait(false);
        var accountId = accounts.GetValueOrDefault(details.Name);

        byte[] warehouseItems = Array.Empty<byte>(), warehouseSerials = Array.Empty<byte>(), warehouseTimes = Array.Empty<byte>();
        long bankCoins = 0;

        if (accountId is not null && _warehouseColumns is { Count: > 0 } &&
            _warehouseColumns.Contains("WarehouseData"))
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `nMoney`, `WarehouseData`, `strSerial`" +
                (HasWarehouseTime ? ", `WarehouseDataTime`" : "") +
                " FROM `WAREHOUSE` WHERE `strAccountID` = @account";
            command.Parameters.AddWithValue("@account", accountId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                bankCoins = ReadLong(reader, "nMoney");
                warehouseItems = ReadBlob(reader, "WarehouseData");
                warehouseSerials = ReadBlob(reader, "strSerial");
                warehouseTimes = ReadOptionalBlob(reader, "WarehouseDataTime");
            }
        }

        var online = await GetOnlineNamesAsync(cancellationToken).ConfigureAwait(false);

        details.AccountId = accountId;
        details.Warehouse = ItemBlobs.Parse(warehouseItems, warehouseSerials, warehouseTimes, InventorySlots.WarehouseTotal);
        details.BankCoins = bankCoins;
        details.IsOnline = online.Contains(details.Name);
        details.ConcurrencyToken = BlobToken(itemBlob, serialBlob, itemTimeBlob, warehouseItems, warehouseSerials, warehouseTimes);
        return details;
    }

    // CharacterDetails is a class with init-only members; this helper rebuilds
    // the few post-load fields without a second query.
    private static string BlobToken(params byte[][] blobs)
    {
        using var sha = SHA256.Create();
        foreach (var blob in blobs)
        {
            sha.TransformBlock(blob, 0, blob.Length, null, 0);
            var length = BitConverter.GetBytes(blob.Length);
            sha.TransformBlock(length, 0, length.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    public async Task<string> SaveCharacterAsync(CharacterDetails character, string adminUser, CancellationToken cancellationToken = default)
    {
        await ProbeAsync(cancellationToken).ConfigureAwait(false);

        var online = await GetOnlineNamesAsync(cancellationToken).ConfigureAwait(false);
        if (online.Contains(character.Name))
            throw new AdminEditRejectedException($"{character.Name} is online - refusing to edit item blobs (the logout save would clobber the edit).");

        // Preserve the stored number of item slots (47 for this codebase's
        // SaveUserData, 73 max) rather than silently growing the blob.
        var storedSlots = character.StoredInventorySlots > 0
            ? character.StoredInventorySlots
            : InventorySlots.Total;
        var inventoryToStore = character.Inventory.Take(storedSlots).ToList();
        var (items, serials, itemTimes) = ItemBlobs.Serialize(inventoryToStore);
        var (wItems, wSerials, wTimes) = ItemBlobs.Serialize(character.Warehouse);

        await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        byte[] oldItems, oldSerials, oldTimes, oldWItems = Array.Empty<byte>(), oldWSerials = Array.Empty<byte>(), oldWTimes = Array.Empty<byte>();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT `strItem`, `strSerial`" +
                (HasItemTime ? ", `strItemTime`" : "") +
                " FROM `USERDATA` WHERE `strUserID` = @name FOR UPDATE";
            command.Parameters.AddWithValue("@name", character.Name);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new AdminEditRejectedException($"Character {character.Name} no longer exists.");

            oldItems = ReadBlob(reader, "strItem");
            oldSerials = ReadBlob(reader, "strSerial");
            oldTimes = ReadOptionalBlob(reader, "strItemTime");
        }

        if (character.AccountId is not null && _warehouseColumns is { Count: > 0 } && _warehouseColumns.Contains("WarehouseData"))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT `WarehouseData`, `strSerial`" +
                (HasWarehouseTime ? ", `WarehouseDataTime`" : "") +
                " FROM `WAREHOUSE` WHERE `strAccountID` = @account FOR UPDATE";
            command.Parameters.AddWithValue("@account", character.AccountId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                oldWItems = ReadBlob(reader, "WarehouseData");
                oldWSerials = ReadBlob(reader, "strSerial");
                oldWTimes = ReadOptionalBlob(reader, "WarehouseDataTime");
            }
        }

        var currentToken = BlobToken(oldItems, oldSerials, oldTimes, oldWItems, oldWSerials, oldWTimes);
        if (currentToken != character.ConcurrencyToken)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new AdminEditRejectedException("This character's items changed since you loaded them (reload the page). No changes were saved.");
        }

        // Backup + audit BEFORE the update, in the same transaction.
        await using (var backup = connection.CreateCommand())
        {
            backup.Transaction = transaction;
            backup.CommandText =
                "INSERT INTO `_ko_admin_backups` (strCharID, strItem, strSerial, strItemTime, WarehouseData, WarehouseSerial, WarehouseTime, created_utc, admin_user) " +
                "VALUES (@name, @i, @s, @t, @wi, @ws, @wt, UTC_TIMESTAMP(), @admin)";
            backup.Parameters.AddWithValue("@name", character.Name);
            backup.Parameters.AddWithValue("@i", oldItems);
            backup.Parameters.AddWithValue("@s", oldSerials);
            backup.Parameters.AddWithValue("@t", oldTimes);
            backup.Parameters.AddWithValue("@wi", oldWItems);
            backup.Parameters.AddWithValue("@ws", oldWSerials);
            backup.Parameters.AddWithValue("@wt", oldWTimes);
            backup.Parameters.AddWithValue("@admin", adminUser);
            await backup.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            var sets = new List<string> { "`strItem` = @items", "`strSerial` = @serials" };
            update.Parameters.AddWithValue("@items", items);
            update.Parameters.AddWithValue("@serials", serials);
            if (HasItemTime)
            {
                sets.Add("`strItemTime` = @itemTimes");
                update.Parameters.AddWithValue("@itemTimes", itemTimes);
            }

            void AddScalar(string column, object value)
            {
                if (!Has(column)) return;
                sets.Add($"`{column}` = @{column}");
                update.Parameters.AddWithValue($"@{column}", value);
            }

            AddScalar("Level", character.Level);
            AddScalar("Exp", character.Exp);
            AddScalar("Loyalty", character.Loyalty);
            AddScalar("Gold", character.Gold);
            AddScalar("Class", character.ClassId);
            AddScalar("Authority", character.Authority);
            AddScalar("Points", character.StatPoints);
            if (character.Stats.Length == 5)
            {
                AddScalar("Strong", character.Stats[0]);
                AddScalar("Sta", character.Stats[1]);
                AddScalar("Dex", character.Stats[2]);
                AddScalar("Intel", character.Stats[3]);
                AddScalar("Cha", character.Stats[4]);
            }

            update.CommandText = $"UPDATE `USERDATA` SET {string.Join(", ", sets)} WHERE `strUserID` = @name";
            update.Parameters.AddWithValue("@name", character.Name);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (character.AccountId is not null && _warehouseColumns is { Count: > 0 } && _warehouseColumns.Contains("WarehouseData"))
        {
            await using var updateWarehouse = connection.CreateCommand();
            updateWarehouse.Transaction = transaction;
            updateWarehouse.CommandText =
                "UPDATE `WAREHOUSE` SET `nMoney` = @money, `WarehouseData` = @items, `strSerial` = @serials" +
                (HasWarehouseTime ? ", `WarehouseDataTime` = @times" : "") +
                " WHERE `strAccountID` = @account";
            updateWarehouse.Parameters.AddWithValue("@money", character.BankCoins);
            updateWarehouse.Parameters.AddWithValue("@items", wItems);
            updateWarehouse.Parameters.AddWithValue("@serials", wSerials);
            if (HasWarehouseTime)
                updateWarehouse.Parameters.AddWithValue("@times", wTimes);
            updateWarehouse.Parameters.AddWithValue("@account", character.AccountId);
            await updateWarehouse.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText =
                "INSERT INTO `_ko_admin_audit` (admin_user, action, target, detail, created_utc) VALUES (@admin, 'Character.Save', @target, @detail, UTC_TIMESTAMP())";
            audit.Parameters.AddWithValue("@admin", adminUser);
            audit.Parameters.AddWithValue("@target", character.Name);
            audit.Parameters.AddWithValue("@detail",
                $"level={character.Level} gold={character.Gold} bank={character.BankCoins} token={currentToken[..12]}");
            await audit.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // The returned token must describe what is actually stored: on schemas
        // without expiration columns the stored "blobs" are empty, matching
        // what the next load/save will read back.
        var storedItemTimes = HasItemTime ? itemTimes : Array.Empty<byte>();
        var storedWTimes = HasWarehouseTime ? wTimes : Array.Empty<byte>();
        var newToken = BlobToken(items, serials, storedItemTimes, wItems, wSerials, storedWTimes);
        KoLog.Info($"[admin] {adminUser} saved character {character.Name}");
        return newToken;
    }

    public async Task<IReadOnlyList<ItemSearchResult>> SearchItemsAsync(string term, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        await ProbeAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<ItemSearchResult>();
        if (_hasItemTable != true)
            return results;

        await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        var byNumber = uint.TryParse(term.Trim(), out var num);
        command.CommandText =
            "SELECT `Num`, `strName`, `Kind`, `Slot`, `Class`, `Duration`, `Countable`, `ItemType`, `ReqLevel` FROM `ITEM` " +
            (byNumber ? "WHERE `Num` = @num " : "WHERE `strName` LIKE @term ") +
            "ORDER BY `Num` LIMIT @limit OFFSET @offset";
        if (byNumber)
            command.Parameters.AddWithValue("@num", num);
        else
            command.Parameters.AddWithValue("@term", $"%{term.Trim()}%");
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));
        command.Parameters.AddWithValue("@offset", Math.Max(0, offset));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ItemSearchResult(
                Convert.ToUInt32(reader.GetValue(0)),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Convert.ToInt32(reader.GetValue(2)),
                Convert.ToInt32(reader.GetValue(3)),
                Convert.ToInt32(reader.GetValue(4)),
                Convert.ToInt32(reader.GetValue(5)),
                Convert.ToInt32(reader.GetValue(6)) != 0,
                Convert.ToInt32(reader.GetValue(7)),
                Convert.ToInt32(reader.GetValue(8))));
        }
        return results;
    }

    public async Task<ItemSearchResult?> GetItemAsync(uint num, CancellationToken cancellationToken = default)
    {
        var results = await SearchItemsAsync(num.ToString(), 1, 0, cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyDictionary<uint, string>> GetItemNamesAsync(IEnumerable<uint> nums, CancellationToken cancellationToken = default)
    {
        var wanted = nums.Where(n => n != 0).Distinct().ToList();
        var names = new Dictionary<uint, string>();
        if (wanted.Count == 0)
            return names;

        await ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (_hasItemTable != true)
            return names;

        await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var parameters = wanted.Select((_, i) => $"@n{i}").ToList();
        command.CommandText = $"SELECT `Num`, `strName` FROM `ITEM` WHERE `Num` IN ({string.Join(",", parameters)})";
        for (var i = 0; i < wanted.Count; i++)
            command.Parameters.AddWithValue($"@n{i}", wanted[i]);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names[Convert.ToUInt32(reader.GetValue(0))] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);

        return names;
    }

    public async Task<string> GiveItemByLetterAsync(string sender, string recipient, string subject, string message,
        uint itemId, short count, short durability, ulong serial, int coins, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenGameAsync(cancellationToken).ConfigureAwait(false);

            // Original: {? = CALL MAIL_BOX_SEND(sender, recipient, subject, message,
            //             type, itemId, count, durability, serial, coins)} with type 2 = item.
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "CALL MAIL_BOX_SEND(@sender, @recipient, @subject, @message, 2, @item, @count, @durability, @serial, @coins, @result)";
                command.Parameters.AddWithValue("@sender", sender);
                command.Parameters.AddWithValue("@recipient", recipient);
                command.Parameters.AddWithValue("@subject", subject);
                command.Parameters.AddWithValue("@message", message);
                command.Parameters.AddWithValue("@item", itemId);
                command.Parameters.AddWithValue("@count", (int)count);
                command.Parameters.AddWithValue("@durability", (int)durability);
                command.Parameters.AddWithValue("@serial", serial);
                command.Parameters.AddWithValue("@coins", coins);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var resultCommand = connection.CreateCommand();
            resultCommand.CommandText = "SELECT @result";
            var result = await resultCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var code = result is null or DBNull ? 1 : Convert.ToInt32(result);

            return code != 0 ? string.Empty : "MAIL_BOX_SEND reported failure (returned 0).";
        }
        catch (MySqlException ex) when (ex.Number == 1305) // PROCEDURE does not exist
        {
            return "MAIL_BOX_SEND hasn't been ported to MariaDB yet - use direct inventory edit (character must be offline) for now.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
