using MySqlConnector;

namespace KnightOnline.Database;

public static class SkillPointRules
{
    public static byte[]? Allocate(byte[] original, byte type, ushort characterClass, byte level)
    {
        if (type is < 5 or > 8 || original.Length <= 8 || original[0] < 1 || original[type] >= level || characterClass%100 <= 4 ||
            type == 8 && (characterClass%2 != 0 || characterClass%100 < 6 || original[8] >= 20 || original[8] >= level-60)) return null;
        var skills = (byte[])original.Clone(); skills[0]--; skills[type]++; return skills;
    }
}
public sealed partial class MariaDbCharacterRepository
{
    public async Task<bool> SaveAllocatedSkillAsync(string owner, byte[] original, byte[] proposed, ushort characterClass, byte level)
    {
        await using var connection = new MySqlConnection(_connectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.Parameters.AddWithValue("@id",owner);
        command.CommandText="SELECT strUserID FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using (var reader = await command.ExecuteReaderAsync()) if (!await reader.ReadAsync() || await reader.ReadAsync()) return false;
        command.Parameters.AddWithValue("@old",original); command.Parameters.AddWithValue("@new",proposed);
        command.Parameters.AddWithValue("@class",characterClass); command.Parameters.AddWithValue("@level",level);
        command.CommandText="UPDATE USERDATA SET strSkill=@new WHERE strUserID=@id AND COALESCE(strSkill,X'')=@old AND Class=@class AND Level=@level";
        if (await command.ExecuteNonQueryAsync() != 1) return false;
        await transaction.CommitAsync(); return true;
    }
}
