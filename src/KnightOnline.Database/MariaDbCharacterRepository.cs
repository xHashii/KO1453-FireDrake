using MySqlConnector;

namespace KnightOnline.Database;

/// <summary>
/// MariaDB port of the database calls behind the game-server login and
/// character-select state machine:
///
/// * <c>GAME_LOGIN</c> and <c>NATION_SELECT</c> - the two stored procedures the
///   original reaches through ODBC (T-SQL bodies kept verbatim in
///   sql/mysql/kn_online/procedures_tsql/), implemented here as plain SQL with
///   the same return-value contract;
/// * <c>CDBAgent::GetAllCharID</c> + <c>CDBAgent::LoadCharInfo</c> - the
///   ACCOUNT_CHAR / USERDATA reads that fill WIZ_ALLCHAR_INFO_REQ.
///
/// Like <see cref="MariaDbAccountRepository"/> this opens a fresh pooled
/// connection per call instead of sharing one ODBC handle, and implements the
/// routines in C# rather than as MariaDB procedures.
/// </summary>
public sealed class MariaDbCharacterRepository
{
    /// <summary>
    /// Equipped-inventory slots the character-select screen renders, as slot
    /// indices into the strItem blob, in the order LoadCharInfo writes them:
    /// HEAD, BREAST, SHOULDER, LEG, GLOVE, FOOT, RIGHTHAND, LEFTHAND
    /// (shared/globals.h).
    /// </summary>
    private static readonly int[] VisibleSlots = { 1, 4, 5, 10, 12, 13, 6, 8 };

    /// <summary>Bytes per inventory slot in strItem: uint32 id + uint16 durability + uint16 count.</summary>
    private const int InventorySlotSize = 8;

    private readonly string _connectionString;

    public MariaDbCharacterRepository(DatabaseSettings settings)
    {
        _connectionString = settings.BuildConnectionString();
    }

    /// <summary>
    /// Port of the GAME_LOGIN stored procedure: -1 when the account is unknown
    /// or the password does not match, otherwise the account's nation - 0 when
    /// it has not chosen one yet (no ACCOUNT_CHAR row), which is what sends a
    /// fresh account to the nation-select screen.
    /// </summary>
    public async Task<sbyte> GameLoginAsync(string account, string password, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT strPasswd FROM TB_USER WHERE strAccountID = @account";
        command.Parameters.AddWithValue("@account", account);

        var stored = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (stored is null or DBNull)
            return -1;

        // The T-SQL comparison runs under a case-insensitive, pad-blank
        // collation against a char(28) column; TrimEnd + OrdinalIgnoreCase is
        // the same rule the LoginServer's ACCOUNT_LOGIN port uses.
        if (!string.Equals(Convert.ToString(stored)!.TrimEnd(), password.TrimEnd(), StringComparison.OrdinalIgnoreCase))
            return -1;

        command.Parameters.Clear();
        command.CommandText = "SELECT bNation FROM ACCOUNT_CHAR WHERE strAccountID = @account";
        command.Parameters.AddWithValue("@account", account);

        var nation = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return nation is null or DBNull ? (sbyte)0 : Convert.ToSByte(nation);
    }

    /// <summary>
    /// Port of the NATION_SELECT stored procedure: records the chosen nation
    /// (ACCOUNT_CHAR) and makes sure the account has a warehouse row, in one
    /// transaction. Returns the nation on success, 0 on failure - the value
    /// CUser::ReqSelectNation echoes back to the client.
    /// </summary>
    public async Task<byte> NationSelectAsync(string account, byte nation, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(strAccountID) FROM ACCOUNT_CHAR WHERE strAccountID = @account";
                command.Parameters.AddWithValue("@account", account);

                var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                if (count == 0)
                {
                    // The T-SQL procedure inserts only (strAccountID, bNation);
                    // the converted schema declares bCharNum NOT NULL without a
                    // default, so the port fills it in explicitly.
                    command.Parameters.Clear();
                    command.CommandText =
                        "INSERT INTO ACCOUNT_CHAR (strAccountID, bNation, bCharNum) " +
                        "VALUES (@account, @nation, 0)";
                    command.Parameters.AddWithValue("@account", account);
                    command.Parameters.AddWithValue("@nation", nation);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(strAccountID) FROM WAREHOUSE WHERE strAccountID = @account";
                command.Parameters.AddWithValue("@account", account);

                var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                if (count == 0)
                {
                    command.Parameters.Clear();
                    command.CommandText =
                        "INSERT INTO WAREHOUSE (strAccountID, nMoney, dwTime, WarehouseData, strSerial) " +
                        "VALUES (@account, 0, 0, @data, @serial)";
                    command.Parameters.AddWithValue("@account", account);
                    command.Parameters.AddWithValue("@data", new byte[] { 0x00 });
                    command.Parameters.AddWithValue("@serial", new byte[] { 0x00 });
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return nation; // the procedure ends with RETURN @bNation
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return 0;
        }
    }

    /// <summary>
    /// Port of CDBAgent::GetAllCharID followed by three CDBAgent::LoadCharInfo
    /// calls: the account's character slots exactly as WIZ_ALLCHAR_INFO_REQ
    /// needs them. Accounts without an ACCOUNT_CHAR row - or with NULL character
    /// ids in it - get zero-filled slots, never fewer of them.
    /// </summary>
    public async Task<IReadOnlyList<CharacterSelectInfo>> LoadCharacterSelectInfoAsync(
        string account, CancellationToken cancellationToken = default)
    {
        var characterIds = new string[CharacterSelectInfo.SlotCount];
        Array.Fill(characterIds, string.Empty);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT strCharID1, strCharID2, strCharID3 FROM ACCOUNT_CHAR WHERE strAccountID = @account";
            command.Parameters.AddWithValue("@account", account);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                for (var i = 0; i < characterIds.Length; i++)
                    characterIds[i] = reader.IsDBNull(i) ? string.Empty : reader.GetString(i).TrimEnd();
            }
        }

        var slots = new List<CharacterSelectInfo>(characterIds.Length);
        foreach (var characterId in characterIds)
            slots.Add(await LoadCharInfoAsync(connection, characterId, cancellationToken).ConfigureAwait(false));

        return slots;
    }

    /// <summary>
    /// Port of CDBAgent::LoadCharInfo: USERDATA row plus the eight visible
    /// equipped slots parsed out of the strItem inventory blob. An empty
    /// character id - or a missing USERDATA row - yields an all-zero slot,
    /// exactly like the original's memset'd buffer.
    /// </summary>
    private static async Task<CharacterSelectInfo> LoadCharInfoAsync(
        MySqlConnection connection, string characterId, CancellationToken cancellationToken)
    {
        byte race = 0, level = 0, face = 0, hair = 0, zone = 0;
        ushort characterClass = 0;
        byte[]? itemData = null;

        if (characterId.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Race, Class, HairRGB, `Level`, Face, Zone, strItem FROM USERDATA WHERE strUserID = @charId";
            command.Parameters.AddWithValue("@charId", characterId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                race = reader.IsDBNull(0) ? (byte)0 : Convert.ToByte(reader.GetValue(0));
                characterClass = reader.IsDBNull(1) ? (ushort)0 : Convert.ToUInt16(reader.GetValue(1));
                hair = reader.IsDBNull(2) ? (byte)0 : Convert.ToByte(reader.GetValue(2));
                level = reader.IsDBNull(3) ? (byte)0 : Convert.ToByte(reader.GetValue(3));
                face = reader.IsDBNull(4) ? (byte)0 : Convert.ToByte(reader.GetValue(4));
                zone = reader.IsDBNull(5) ? (byte)0 : Convert.ToByte(reader.GetValue(5));
                itemData = reader.IsDBNull(6) ? null : (byte[])reader.GetValue(6);
            }
        }

        var equipment = new EquippedItem[CharacterSelectInfo.VisibleEquipmentCount];
        for (var i = 0; i < equipment.Length; i++)
        {
            var offset = VisibleSlots[i] * InventorySlotSize;
            uint itemId = 0;
            ushort durability = 0;
            if (itemData is not null && offset + 6 <= itemData.Length)
            {
                itemId = BitConverter.ToUInt32(itemData, offset);
                durability = BitConverter.ToUInt16(itemData, offset + 4);
            }

            equipment[i] = new EquippedItem(itemId, durability);
        }

        return new CharacterSelectInfo(characterId, race, characterClass, level, face, hair, zone, equipment);
    }
}
