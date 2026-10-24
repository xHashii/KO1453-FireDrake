using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace KnightOnline.Database.Admin;

public sealed class DropDefinition
{
    public short Id { get; init; }
    public int?[] Items { get; init; } = new int?[5];
    public int?[] Chances { get; init; } = new int?[5];
    public string Token { get; set; } = "";
}
public sealed record DropSummary(short Id, long Monsters);

public sealed partial class CharacterAdminRepository
{
    public async Task<IReadOnlyList<DropSummary>> SearchDropsAsync(string term, int offset = 0, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenGameAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT d.sIndex, (SELECT COUNT(*) FROM K_MONSTER m WHERE m.sItem=d.sIndex) FROM K_MONSTER_ITEM d " +
            "WHERE CAST(d.sIndex AS CHAR) LIKE @term OR EXISTS (SELECT 1 FROM K_MONSTER m WHERE m.sItem=d.sIndex AND m.strName LIKE @term) " +
            "ORDER BY d.sIndex LIMIT 51 OFFSET @offset";
        command.Parameters.AddWithValue("@term", $"%{term.Trim()}%");
        command.Parameters.AddWithValue("@offset", Math.Max(0, offset));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<DropSummary>();
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(reader.GetInt16(0), reader.GetInt64(1)));
        return rows;
    }

    private static string DropJson(DropDefinition drop) => JsonSerializer.Serialize(new { drop.Id, drop.Items, drop.Chances });
    private static string DropToken(DropDefinition drop) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DropJson(drop))));

    private static async Task<DropDefinition?> ReadDropAsync(MySqlConnection connection, MySqlTransaction? transaction,
        short id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT * FROM K_MONSTER_ITEM WHERE sIndex=@id" + (transaction is null ? "" : " FOR UPDATE");
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var drop = new DropDefinition { Id = id };
        for (var i = 0; i < 5; i++)
        {
            var item = reader.GetOrdinal($"iItem{i + 1:00}");
            var chance = reader.GetOrdinal($"sPersent{i + 1:00}");
            drop.Items[i] = reader.IsDBNull(item) ? null : Convert.ToInt32(reader.GetValue(item));
            drop.Chances[i] = reader.IsDBNull(chance) ? null : Convert.ToInt32(reader.GetValue(chance));
        }
        if (await reader.ReadAsync(cancellationToken))
            throw new AdminEditRejectedException("Duplicate drop group IDs. Repair the ambiguous database rows before editing.");
        drop.Token = DropToken(drop);
        return drop;
    }

    public async Task<DropDefinition?> GetDropAsync(short id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenGameAsync(cancellationToken);
        return await ReadDropAsync(connection, null, id, cancellationToken);
    }

    public static void ValidateDrop(DropDefinition drop)
    {
        if (drop.Items.Length != 5 || drop.Chances.Length != 5 || drop.Id <= 0)
            throw new AdminEditRejectedException("A drop group needs a positive ID and exactly five slots.");
        for (var i = 0; i < 5; i++)
        {
            if (drop.Items[i] < 0 || drop.Chances[i] < 0 || drop.Chances[i] > 10000)
                throw new AdminEditRejectedException("Item/group codes must be nonnegative; chances must be 0–10000 (10000 = 100%).");
            if (drop.Items[i].GetValueOrDefault() == 0 && drop.Chances[i].GetValueOrDefault() > 0)
                throw new AdminEditRejectedException($"Slot {i + 1}: a nonzero chance requires an item/group code.");
        }
    }

    public async Task<string> SaveDropAsync(DropDefinition drop, string admin, CancellationToken cancellationToken = default)
    {
        ValidateDrop(drop);
        await using var connection = await OpenGameAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var before = await ReadDropAsync(connection, transaction, drop.Id, cancellationToken)
            ?? throw new AdminEditRejectedException("Drop group no longer exists.");
        if (before.Token != drop.Token) throw new AdminEditRejectedException("Drop group changed in another editor. Reload before saving.");
        for (var i = 0; i < 5; i++)
        {
            var code = drop.Items[i].GetValueOrDefault();
            // Existing source data may contain broken/unused references. Validate newly
            // introduced or activated references, without making unrelated fixes compulsory.
            if (code == 0 || (drop.Items[i] == before.Items[i] && drop.Chances[i].GetValueOrDefault() <= before.Chances[i].GetValueOrDefault())) continue;
            await using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = i < 2
                ? code < 100 ? "SELECT COUNT(*) FROM MAKE_ITEM_GRADECODE WHERE byItemIndex=@code"
                    : "SELECT COUNT(*) FROM MAKE_ITEM_GROUP WHERE iItemGroupNum=@code"
                : "SELECT COUNT(*) FROM ITEM WHERE Num=@code";
            check.Parameters.AddWithValue("@code", code);
            if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new AdminEditRejectedException($"Slot {i + 1}: missing or duplicate {(i < 2 ? "production/group" : "ITEM")} definition {code}.");
        }
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE K_MONSTER_ITEM SET " + string.Join(",", Enumerable.Range(1, 5).Select(i => $"iItem{i:00}=@item{i},sPersent{i:00}=@chance{i}")) + " WHERE sIndex=@id";
            update.Parameters.AddWithValue("@id", drop.Id);
            for (var i = 0; i < 5; i++)
            {
                update.Parameters.AddWithValue($"@item{i + 1}", (object?)drop.Items[i] ?? DBNull.Value);
                update.Parameters.AddWithValue($"@chance{i + 1}", (object?)drop.Chances[i] ?? DBNull.Value);
            }
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var audit = connection.CreateCommand())
        {
            audit.Transaction = transaction;
            audit.CommandText = "INSERT INTO _ko_admin_audit(admin_user,action,target,detail,created_utc) VALUES(@admin,'Drop.Save',@id,@detail,UTC_TIMESTAMP())";
            audit.Parameters.AddWithValue("@admin", admin);
            audit.Parameters.AddWithValue("@id", drop.Id.ToString());
            audit.Parameters.AddWithValue("@detail", JsonSerializer.Serialize(new { before = DropJson(before), after = DropJson(drop), activation = "GameServer restart required" }));
            await audit.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return DropToken(drop);
    }
}
