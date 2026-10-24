using System.Globalization;
using System.IO.Compression;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed record ItemCatalogRepairResult(int Restored, int SourceCount);

public sealed partial class MariaDbCharacterRepository
{
    /// <summary>Restore ALL missing ITEM definitions from the shipped authoritative
    /// catalog. Preserve every existing definition, inventory, serial and coin value.
    /// No INSERT IGNORE: invalid data must fail and roll back, not be silently coerced.</summary>
    public async Task<ItemCatalogRepairResult> RepairItemCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        // ITEM has no unique index in the shipped DDL. Serialize cooperating
        // startups, and lock the existing table range before reading missing IDs.
        await using var gate = connection.CreateCommand();
        gate.CommandText = "SELECT GET_LOCK('KO1453_ITEM_CATALOG_REPAIR',60)";
        gate.CommandTimeout = 65;
        if (Convert.ToInt32(await gate.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 1)
            throw new InvalidOperationException("Could not acquire ITEM catalog repair lock. Retry startup.");
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT Num FROM ITEM FOR UPDATE";
            var present = new HashSet<uint>();
            await using (var rows = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                while (await rows.ReadAsync(cancellationToken).ConfigureAwait(false))
                    if (!rows.IsDBNull(0))
                    {
                        var number = Convert.ToInt64(rows.GetValue(0));
                        if (number >= 0 && number <= uint.MaxValue) present.Add((uint)number);
                    }

            using var resource = typeof(MariaDbCharacterRepository).Assembly.GetManifestResourceStream(
                "KnightOnline.Database.Items.ItemCatalog.sql.gz")
                ?? throw new InvalidOperationException("Embedded ITEM catalog missing; rebuild/publish the Database project.");
            using var gzip = new GZipStream(resource, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var columns = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Empty ITEM catalog.");
            var batch = new List<string>(200);
            var sourceCount = 0;
            var restored = 0;
            async Task FlushAsync()
            {
                if (batch.Count == 0) return;
                command.CommandText = $"INSERT INTO ITEM {columns} VALUES {string.Join(",", batch)}";
                restored += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } row)
            {
                var comma = row.IndexOf(',');
                var id = uint.Parse(row.AsSpan(1, comma - 1), CultureInfo.InvariantCulture);
                sourceCount++;
                if (!present.Add(id)) continue;
                batch.Add(row);
                if (batch.Count == 200) await FlushAsync().ConfigureAwait(false);
            }
            await FlushAsync().ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(restored, sourceCount);
        }
        finally
        {
            // Release even after rollback/cancellation; never return a pooled
            // connection holding the server-wide advisory lock.
            gate.CommandText = "SELECT RELEASE_LOCK('KO1453_ITEM_CATALOG_REPAIR')";
            await gate.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
