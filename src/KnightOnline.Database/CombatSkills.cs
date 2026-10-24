using KnightOnline.Shared.Protocol;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed record CombatSkill(uint Id, int SkillClass, int Level, int Mp, int CastMs, int RecastMs, int Range,
    int Type, int HitPercent, int AddDamage, int DirectDamage, int WeaponGroup);
public static class CombatSkillRules
{
    public static bool Learned(CombatSkill skill, ushort characterClass, byte level, byte[] allocated)
    {
        var tree = skill.SkillClass % 10;
        return skill.SkillClass > 0 && skill.SkillClass / 10 == characterClass &&
            (tree == 0 ? level >= skill.Level : tree < allocated.Length && allocated[tree] >= skill.Level);
    }
    public static Packet Reply(uint skill, ushort caster, ushort target, byte stage, bool success)
    {
        var packet = new Packet(WizOpcode.MagicProcess).WriteByte(success ? stage : (byte)4).WriteUInt32(skill).WriteUInt16(caster).WriteUInt16(target);
        for (var i = 0; i < 7; i++) packet.WriteInt16(i == 1 && success ? (short)1 : i == 3 && !success ? (short)-103 : (short)0);
        return packet;
    }
}
public sealed partial class MariaDbCharacterRepository
{
    public async Task<CombatSkill?> LoadCombatSkillAsync(uint skill)
    {
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT m.Skill,m.SkillLevel,m.Msp,m.CastTime,m.ReCastTime,m.Range,m.Type1,m.ItemGroup," +
            "t.Hit,t.AddDamage,p.FirstDamage FROM MAGIC m LEFT JOIN MAGIC_TYPE1 t ON t.iNum=m.MagicNum LEFT JOIN MAGIC_TYPE3 p ON p.iNum=m.MagicNum " +
            "WHERE m.MagicNum=@skill AND m.Moral=7 AND m.Skill>0 AND m.HP=0 AND m.UseItem=0 AND m.Msp>=0 AND m.Type2=0 AND m.SuccessRate=100 " +
            "AND ((m.Type1=1 AND t.ComboCount<=1 AND t.Hit>0 AND t.AddDamage>=0) OR (m.Type1=3 AND p.DirectType=1 AND p.FirstDamage<0 AND p.TimeDamage=0 AND p.EndDamage=0 AND p.Duration=0 AND p.Radius=0))";
        command.Parameters.AddWithValue("@skill",skill);
        await using var row = await command.ExecuteReaderAsync();
        if (!await row.ReadAsync()) return null;
        int Value(int i) => row.IsDBNull(i) ? 0 : Convert.ToInt32(row.GetValue(i));
        var result = new CombatSkill(skill,Value(0),Value(1),Value(2),Math.Max(0,Value(3))*100,Math.Max(500,Value(4)*100),
            Math.Clamp(Value(5),0,100),Value(6),Value(8),Value(9),Math.Abs(Value(10)),Value(7));
        return await row.ReadAsync() ? null : result;
    }

    public async Task<InventorySaveResult> SpendCombatSkillAsync(string owner, CombatSkill skill, int oldHp, int oldMp, int hp, int mp)
    {
        if (hp <= 0 || mp < skill.Mp || skill.Mp < 0) return InventorySaveResult.StaleOrProtected;
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.Parameters.AddWithValue("@id",owner);
        command.CommandText = "SELECT strUserID FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using (var reader = await command.ExecuteReaderAsync()) if (!await reader.ReadAsync() || await reader.ReadAsync()) return InventorySaveResult.StaleOrProtected;
        foreach (var id in new uint[] { 0, skill.Id })
        {
            if (command.Parameters.Contains("@skill")) command.Parameters.RemoveAt("@skill");
            command.Parameters.AddWithValue("@skill",id);
            command.CommandText = "INSERT INTO KO_SKILL_COOLDOWN VALUES(@id,@skill,UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE Skill=VALUES(Skill)";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "SELECT AvailableAt>UTC_TIMESTAMP(6) FROM KO_SKILL_COOLDOWN WHERE Owner=@id AND Skill=@skill FOR UPDATE";
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) return InventorySaveResult.Cooldown;
        }
        command.Parameters.AddWithValue("@hp",hp); command.Parameters.AddWithValue("@mp",mp-skill.Mp);
        command.Parameters.AddWithValue("@oldHp",oldHp); command.Parameters.AddWithValue("@oldMp",oldMp);
        command.CommandText = "UPDATE USERDATA SET Hp=@hp,Mp=@mp WHERE strUserID=@id AND Hp=@oldHp AND Mp=@oldMp";
        if (await command.ExecuteNonQueryAsync() != 1) return InventorySaveResult.StaleOrProtected;
        command.Parameters.AddWithValue("@delay",skill.RecastMs*1000L);
        command.CommandText = "UPDATE KO_SKILL_COOLDOWN SET AvailableAt=TIMESTAMPADD(MICROSECOND,IF(Skill=0,500000,@delay),UTC_TIMESTAMP(6)) WHERE Owner=@id AND Skill IN (0,@skill)";
        await command.ExecuteNonQueryAsync(); await transaction.CommitAsync(); return InventorySaveResult.Saved;
    }
}
