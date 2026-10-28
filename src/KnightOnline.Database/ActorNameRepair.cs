using System.Text.Json;
using KnightOnline.Shared.Diagnostics;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed partial class MariaDbCharacterRepository
{
    private sealed record ActorNameRule(string Table,int Id,int Model,string Old,string Name);
    private sealed record ActorNameCatalog(ActorNameRule[] Rules);

    /// <summary>Guarded name-only fixes also work after the one-time world import.</summary>
    public async Task<int> RepairActorNamesAsync()
    {
        var catalog=JsonSerializer.Deserialize<ActorNameCatalog>(
            await ReadMoradonResource("KnightOnline.Database.ActorNameRepairs.json"),
            new JsonSerializerOptions{PropertyNameCaseInsensitive=true})
            ?? throw new InvalidDataException("Missing actor name repair catalog.");
        var allowed=new HashSet<string>{"K_NPC","K_MONSTER","KO_MORADON_IMPORTED_NPC","KO_MORADON_IMPORTED_MONSTER"};
        foreach(var rule in catalog.Rules)
            if(!allowed.Contains(rule.Table)||rule.Name.Length is <1 or >30||rule.Name.Contains('`'))
                throw new InvalidDataException("Invalid actor name repair rule.");
        await using var connection=new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command=connection.CreateCommand();
        command.CommandText=await ReadMoradonResource("KnightOnline.Database.ActorNameSchema.sql");
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        await using var transaction=await connection.BeginTransactionAsync().ConfigureAwait(false);
        command.Transaction=transaction;
        var repaired=0;
        foreach(var rule in catalog.Rules)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@table",rule.Table);
            command.Parameters.AddWithValue("@id",rule.Id);
            command.Parameters.AddWithValue("@model",rule.Model);
            command.Parameters.AddWithValue("@old",rule.Old);
            command.Parameters.AddWithValue("@name",rule.Name);
            var predicate="sSid=@id AND sPid=@model AND BINARY RTRIM(COALESCE(strName,''))=BINARY @old";
            // Row locks serialize concurrent repairs; callbacks/other state are absent.
            command.CommandText=$"SELECT sSid FROM {rule.Table} WHERE {predicate} FOR UPDATE";
            var match=await command.ExecuteScalarAsync().ConfigureAwait(false);
            if(match is null or DBNull) continue;
            command.CommandText=$"INSERT INTO KO_ACTOR_NAME_BACKUP (TableName,Sid,Model,OldName,NewName,AppliedUtc) SELECT @table,sSid,sPid,strName,@name,UTC_TIMESTAMP(6) FROM {rule.Table} WHERE {predicate} AND NOT EXISTS (SELECT 1 FROM KO_ACTOR_NAME_BACKUP WHERE TableName=@table AND Sid=@id AND Model=@model)";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            command.CommandText=$"UPDATE {rule.Table} SET strName=@name WHERE {predicate}";
            repaired+=await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await transaction.CommitAsync().ConfigureAwait(false);
        KoLog.Info($"[ ACTOR NAMES ] Repaired {repaired} known damaged NPC/monster name(s); original names backed up, placements/stats unchanged.");
        return repaired;
    }
}
