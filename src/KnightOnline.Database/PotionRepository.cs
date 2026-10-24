using KnightOnline.Database.Items;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed record PotionVitals(int ExpectedHp, int ExpectedMp, int Hp, int Mp, byte Resource, int CooldownMs);
public enum InventorySaveResult { Saved, StaleOrProtected, Cooldown }

public sealed partial class MariaDbCharacterRepository
{
    public async Task EnsurePotionDataAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS KO_POTION_COOLDOWN (strUserID varchar(21) NOT NULL, Resource tinyint unsigned NOT NULL, AvailableAt datetime(6) NOT NULL, PRIMARY KEY(strUserID,Resource)) ENGINE=InnoDB";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await using var gate = connection.CreateCommand();
        gate.CommandText = "SELECT GET_LOCK('KO1453_POTION_MAGIC_REPAIR',60)";
        gate.CommandTimeout = 65;
        if (Convert.ToInt32(await gate.ExecuteScalarAsync().ConfigureAwait(false)) != 1)
            throw new InvalidOperationException("Could not acquire potion data repair lock.");
        try
        {
            await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
            command.Transaction = transaction;
            using var source = typeof(MariaDbCharacterRepository).Assembly.GetManifestResourceStream("KnightOnline.Database.Items.PotionMagicRepair.sql")!;
            using var reader = new StreamReader(source);
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (!line.StartsWith("INSERT INTO", StringComparison.Ordinal)) continue;
                command.CommandText = line;
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        finally
        {
            gate.CommandText = "SELECT RELEASE_LOCK('KO1453_POTION_MAGIC_REPAIR')";
            await gate.ExecuteScalarAsync().ConfigureAwait(false);
        }
    }

    public async Task<PotionDefinition?> LoadPotionAsync(uint skill)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT m.MagicNum,i.Num,t.DirectType,t.FirstDamage,m.ReCastTime,i.ReqLevel,i.ReqLevelMax,i.Weight " +
            "FROM MAGIC m JOIN MAGIC_TYPE3 t ON t.iNum=m.MagicNum JOIN ITEM i ON i.Num=m.UseItem " +
            "WHERE m.MagicNum=@skill AND m.Moral=1 AND m.Type1=3 AND m.Type2=0 AND m.Skill=0 AND m.Msp=0 AND m.HP=0 " +
            "AND m.SuccessRate=100 AND t.DirectType IN (1,2) AND t.FirstDamage>0 AND t.TimeDamage=0 AND t.EndDamage=0 AND t.Duration=0 " +
            "AND i.Kind=97 AND i.Slot=17 AND i.Class=0 AND i.Race=0 AND i.Countable=1 AND i.Weight>=0 AND m.ReCastTime>=0 AND (i.Effect1=m.MagicNum OR i.Effect2=m.MagicNum)";
        command.Parameters.AddWithValue("@skill", skill);
        await using var row = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await row.ReadAsync().ConfigureAwait(false)) return null;
        var result = new PotionDefinition(Convert.ToUInt32(row.GetValue(0)), Convert.ToUInt32(row.GetValue(1)),
            Convert.ToByte(row.GetValue(2)), Convert.ToInt32(row.GetValue(3)), Math.Max(500, Convert.ToInt32(row.GetValue(4)) * 100),
            Convert.ToByte(row.GetValue(5)), Convert.ToByte(row.GetValue(6)), Convert.ToInt32(row.GetValue(7)));
        return await row.ReadAsync().ConfigureAwait(false) ? null : result; // no ambiguous definitions
    }
}
