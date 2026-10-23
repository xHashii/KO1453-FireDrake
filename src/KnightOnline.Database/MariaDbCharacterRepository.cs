using KnightOnline.Shared.Diagnostics;
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

    /// <summary>
    /// Whether the class exists in the COEFFICIENT table - the port of the
    /// C++-side gate <c>m_CoefficientArray.GetData(sClass) == nullptr</c> that
    /// rejects WIZ_NEW_CHAR with NEWCHAR_INVALID_DETAILS before any database
    /// write is attempted.
    /// </summary>
    public async Task<bool> HasCoefficientAsync(ushort characterClass, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM COEFFICIENT WHERE sClass = @class LIMIT 1";
        command.Parameters.AddWithValue("@class", characterClass);

        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    /// <summary>
    /// Port of the CREATE_NEW_CHAR stored procedure (T-SQL body in
    /// sql/mysql/kn_online/procedures_tsql/): nation/race coherence, name
    /// uniqueness, the ACCOUNT_CHAR slot update with bCharNum += 1, the USERDATA
    /// insert, then the BEGINNER_ITEM template (level, experience, skill blob,
    /// gold, starting items, loyalty, points) and the novice class bump
    /// (+5/+6/+7/+8 by class branch). Return values are the NEWCHAR_* codes:
    /// 0 success, 2 invalid details, 3 name exists, 4 database error.
    ///
    /// Two documented contractions of the T-SQL:
    /// * the USERDATA insert fills the NOT NULL columns the T-SQL INSERT omits
    ///   (Rank, Title, Level, ..., sQuestCount, MannerPoint, LoyaltyMonthly,
    ///   dtCreateTime) with zeroes / NOW() - on SQL Server those columns had
    ///   defaults the converted schema does not carry, and the beginner
    ///   template overwrites the meaningful ones immediately after;
    /// * the eighteen EDIT_QUEST calls that seed tutorial quest flags into
    ///   USERDATA.strQuest are skipped: the quest tables ship empty, so the
    ///   flags have no consumer yet. They belong with the quest import.
    ///   A missing BEGINNER_ITEM row collapses to 4 (the T-SQL would either
    ///   roll back on a NOT NULL violation or commit a NULL-level character).
    /// </summary>
    public async Task<byte> CreateCharacterAsync(
        string account, byte index, string charId, byte race, ushort characterClass, byte hair, byte face,
        byte strong, byte sta, byte dex, byte intel, byte cha, CancellationToken cancellationToken = default)
    {
        const byte invalidDetails = 2, exists = 3, dbError = 4;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        byte nation;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT bNation FROM ACCOUNT_CHAR WHERE strAccountID = @account";
            command.Parameters.AddWithValue("@account", account);

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (value is null or DBNull)
                return invalidDetails;
            nation = Convert.ToByte(value);
        }

        // Karus races sit below 10, El Morad's above; anything else is rejected.
        if ((nation == 1 && race > 10) || (nation == 2 && race < 10) || (nation != 1 && nation != 2))
            return invalidDetails;

        // char(21): SQL Server truncated silently on the way in; do the same.
        if (charId.Length > 21)
            charId = charId[..21];

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(strUserID) FROM USERDATA WHERE strUserID = @charId";
            command.Parameters.AddWithValue("@charId", charId);

            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0)
                return exists;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var slot = index switch
            {
                0 => "strCharID1",
                1 => "strCharID2",
                2 => "strCharID3",
                _ => null,
            };

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    $"UPDATE ACCOUNT_CHAR SET {slot} = @charId, bCharNum = bCharNum + 1 " +
                    "WHERE strAccountID = @account";
                command.Parameters.AddWithValue("@charId", charId);
                command.Parameters.AddWithValue("@account", account);

                if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0)
                    return dbError;
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO USERDATA (strUserID, Nation, Race, Class, HairRGB, Face, " +
                    "Strong, Sta, Dex, Intel, Cha, " +
                    "Rank, Title, Level, Exp, Loyalty, City, Knights, Fame, Hp, Mp, Sp, Authority, " +
                    "Points, Gold, Zone, PX, PZ, PY, dwTime, sQuestCount, MannerPoint, LoyaltyMonthly, dtCreateTime) " +
                    "VALUES (@charId, @nation, @race, @class, @hair, @face, " +
                    "@strong, @sta, @dex, @intel, @cha, " +
                    "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, " +
                    "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, NOW())";
                command.Parameters.AddWithValue("@charId", charId);
                command.Parameters.AddWithValue("@nation", nation);
                command.Parameters.AddWithValue("@race", race);
                command.Parameters.AddWithValue("@class", characterClass);
                command.Parameters.AddWithValue("@hair", hair);
                command.Parameters.AddWithValue("@face", face);
                command.Parameters.AddWithValue("@strong", strong);
                command.Parameters.AddWithValue("@sta", sta);
                command.Parameters.AddWithValue("@dex", dex);
                command.Parameters.AddWithValue("@intel", intel);
                command.Parameters.AddWithValue("@cha", cha);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // SUBSTRING(RTRIM(LTRIM(STR(@sClass))), 3, 1): the class branch is
            // the third digit of the three-digit class code (201 -> 1).
            var classText = characterClass.ToString();
            var branch = classText.Length >= 3 ? classText[2] - '0' : (int?)null;

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "SELECT Level, Exp, Skill, Gold, strItem, Loyalty, Points " +
                    "FROM BEGINNER_ITEM WHERE Class = @branch";
                command.Parameters.AddWithValue("@branch", branch is null ? DBNull.Value : branch);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    return dbError;

                var level = Convert.ToByte(reader.GetValue(0));
                var exp = Convert.ToInt32(reader.GetValue(1));
                var skill = reader.IsDBNull(2) ? DBNull.Value : reader.GetValue(2);
                var gold = Convert.ToInt32(reader.GetValue(3));
                var items = reader.IsDBNull(4) ? DBNull.Value : reader.GetValue(4);
                var loyalty = Convert.ToInt32(reader.GetValue(5));
                var points = Convert.ToInt16(reader.GetValue(6));
                await reader.CloseAsync().ConfigureAwait(false);

                command.Parameters.Clear();
                command.CommandText =
                    "UPDATE USERDATA SET Level = @level, Exp = @exp, strSkill = @skill, Gold = @gold, " +
                    "strItem = @items, Loyalty = @loyalty, Points = @points WHERE strUserID = @charId";
                command.Parameters.AddWithValue("@level", level);
                command.Parameters.AddWithValue("@exp", exp);
                command.Parameters.AddWithValue("@skill", skill);
                command.Parameters.AddWithValue("@gold", gold);
                command.Parameters.AddWithValue("@items", items);
                command.Parameters.AddWithValue("@loyalty", loyalty);
                command.Parameters.AddWithValue("@points", points);
                command.Parameters.AddWithValue("@charId", charId);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // The T-SQL INSERT left Zone/PX/PZ/PY to SQL Server defaults the
            // converted schema lost; a character at zone 0 has no map and can
            // never enter the world, so the port spawns it at its nation's
            // home zone init point. ZONE_INFO and USERDATA both store
            // world * 100 (ZoneInfoSet and DBAgent::LoadUserData divide by
            // 100 for the float world position); the WIZ_SEL_CHAR/WIZ_MYINFO
            // answers send GetSPosX() = uint16(world * 10), i.e. stored / 10.
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    "UPDATE USERDATA u JOIN ZONE_INFO z ON z.ZoneNo = @nation AND z.ServerNo = 1 " +
                    "SET u.Zone = z.ZoneNo, u.PX = z.InitX, u.PZ = z.InitZ, u.PY = z.InitY " +
                    "WHERE u.strUserID = @charId";
                command.Parameters.AddWithValue("@nation", nation);
                command.Parameters.AddWithValue("@charId", charId);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Novice bump: branch 1..4 promotes the class by 5..8.
            var bump = branch switch { 1 => 5, 2 => 6, 3 => 7, 4 => 8, _ => 0 };
            if (bump != 0)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "UPDATE USERDATA SET Class = Class + @bump WHERE strUserID = @charId";
                command.Parameters.AddWithValue("@bump", bump);
                command.Parameters.AddWithValue("@charId", charId);

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            // The client only ever sees NEWCHAR_DB_ERROR; the log must say why.
            KoLog.Error($"CREATE_NEW_CHAR for '{charId}' (account {account}) failed at the database.", ex);
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return dbError;
        }
    }

    /// <summary>
    /// Port of the LOAD_USER_DATA stored procedure: the character must be one
    /// of the account's three ACCOUNT_CHAR slots, then USERDATA is read for
    /// the session. The procedure's live fix-up (one class step for level 60+
    /// novices of the listed classes) runs first, exactly as in the T-SQL.
    /// Returns null when the character is not this account's, does not exist,
    /// or the account has no warehouse row - the three loads that gate the
    /// original's success byte (the warehouse row always exists here because
    /// NATION_SELECT creates it; premium and saved-magic load nothing until
    /// those systems are ported).
    /// </summary>
    public async Task<UserData?> LoadUserDataAsync(string account, string charId, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT strCharID1, strCharID2, strCharID3 FROM ACCOUNT_CHAR WHERE strAccountID = @account";
            command.Parameters.AddWithValue("@account", account);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var owned = false;
            for (var i = 0; i < 3 && !owned; i++)
                owned = !reader.IsDBNull(i) && string.Equals(reader.GetString(i).TrimEnd(), charId, StringComparison.Ordinal);
            if (!owned)
                return null;
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT 1 FROM WAREHOUSE WHERE strAccountID = @account LIMIT 1";
            command.Parameters.AddWithValue("@account", account);
            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                return null;
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "UPDATE USERDATA SET Class = Class + 1 WHERE strUserID = @charId AND Level > 59 " +
                "AND Class IN (105, 107, 109, 111, 205, 207, 209, 211)";
            command.Parameters.AddWithValue("@charId", charId);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Distinct names on purpose: C# forbids reusing 'command'/'reader' at
        // method scope when enclosed blocks already used them (CS0136).
        await using var select = connection.CreateCommand();
        select.CommandText =
            "SELECT Nation, Race, Class, HairRGB, Rank, Title, Level, Exp, Loyalty, Face, City, " +
            "Knights, Fame, Hp, Mp, Sp, Strong, Sta, Dex, Intel, Cha, Authority, Points, Gold, " +
            "Zone, PX, PZ, PY, strSkill, strItem, MannerPoint, LoyaltyMonthly, sQuestCount, strQuest " +
            "FROM USERDATA WHERE strUserID = @charId";
        select.Parameters.AddWithValue("@charId", charId);

        await using var row = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await row.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new UserData(
            Convert.ToByte(row.GetValue(0)),
            Convert.ToByte(row.GetValue(1)),
            Convert.ToUInt16(row.GetValue(2)),
            Convert.ToByte(row.GetValue(3)),
            Convert.ToByte(row.GetValue(4)),
            Convert.ToByte(row.GetValue(5)),
            Convert.ToByte(row.GetValue(6)),
            Convert.ToInt32(row.GetValue(7)),
            Convert.ToInt32(row.GetValue(8)),
            Convert.ToByte(row.GetValue(9)),
            Convert.ToByte(row.GetValue(10)),
            Convert.ToInt16(row.GetValue(11)),
            Convert.ToByte(row.GetValue(12)),
            Convert.ToInt16(row.GetValue(13)),
            Convert.ToInt16(row.GetValue(14)),
            Convert.ToInt16(row.GetValue(15)),
            Convert.ToByte(row.GetValue(16)),
            Convert.ToByte(row.GetValue(17)),
            Convert.ToByte(row.GetValue(18)),
            Convert.ToByte(row.GetValue(19)),
            Convert.ToByte(row.GetValue(20)),
            Convert.ToByte(row.GetValue(21)),
            Convert.ToInt16(row.GetValue(22)),
            Convert.ToInt32(row.GetValue(23)),
            Convert.ToUInt16(row.GetValue(24)),
            Convert.ToInt32(row.GetValue(25)),
            Convert.ToInt32(row.GetValue(26)),
            Convert.ToInt32(row.GetValue(27)),
            GetBlob(row, 28),
            GetBlob(row, 29),
            Convert.ToInt32(row.GetValue(30)),
            Convert.ToInt32(row.GetValue(31)),
            Convert.ToInt16(row.GetValue(32)),
            GetBlob(row, 33));
    }

    private static byte[] GetBlob(MySqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(ordinal);

    /// <summary>
    /// The COEFFICIENT row for a class: the Hp/Mp/Sp factors SetMaxHp and
    /// SetMaxMp build the derived maxima from (null when the class is missing).
    /// </summary>
    public async Task<ClassCoefficient?> LoadCoefficientAsync(ushort characterClass, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Hp, Mp, Sp FROM COEFFICIENT WHERE sClass = @class";
        command.Parameters.AddWithValue("@class", characterClass);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new ClassCoefficient(
            Convert.ToSingle(reader.GetValue(0)),
            Convert.ToSingle(reader.GetValue(1)),
            Convert.ToSingle(reader.GetValue(2)));
    }

    /// <summary>
    /// The LEVEL_UP row for a level - CGameServerDlg::GetExpByLevel, which
    /// returns 0 when the level is missing from the table.
    /// </summary>
    public async Task<long> LoadLevelExpAsync(int level, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Exp FROM LEVEL_UP WHERE level = @level";
        command.Parameters.AddWithValue("@level", level);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0L : Convert.ToInt64(value);
    }
}
