using MySqlConnector;

namespace KnightOnline.Database;

/// <summary>Cooperative session/admin exclusion, including the account-wide warehouse.
/// Names are folded by MariaDB, not the process culture. Connection-scoped locks must
/// never be returned to a pool while held. All clients must run this server version.</summary>
public sealed class CharacterEditLease : IDisposable
{
    private readonly MySqlConnection _connection;
    private readonly bool _ownsConnection;
    private readonly List<string> _names = [];
    public const string CharacterLockExpression = "SHA2(CONCAT(DATABASE(), ':ko-character:', UPPER(strUserID)),256)";

    private CharacterEditLease(MySqlConnection connection, bool ownsConnection)
    { _connection = connection; _ownsConnection = ownsConnection; }

    public static CharacterEditLease Acquire(MySqlConnection connection, string? account, string character, bool ownsConnection = false)
    {
        var lease = new CharacterEditLease(connection, ownsConnection);
        try
        {
            foreach (var (kind, value) in new[] { ("account", account), ("character", character) })
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT SHA2(CONCAT(DATABASE(), ':ko-' ,@kind, ':', UPPER(@value)),256)";
                command.Parameters.AddWithValue("@kind", kind);
                command.Parameters.AddWithValue("@value", value);
                var name = Convert.ToString(command.ExecuteScalar())!;
                command.CommandText = "SELECT GET_LOCK(@lock,0)";
                command.Parameters.AddWithValue("@lock", name);
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("Character/account is in use by the game server or another editor. Retry after logout.");
                lease._names.Add(name);
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    public void Ping()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.ExecuteScalar(); // A failed lease connection requires disconnect, not unprotected play.
    }

    public void Dispose()
    {
        try
        {
            foreach (var name in _names.AsEnumerable().Reverse())
            {
                using var command = _connection.CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@name)";
                command.Parameters.AddWithValue("@name", name);
                command.ExecuteScalar();
            }
        }
        finally
        {
            _names.Clear();
            if (_ownsConnection) _connection.Dispose();
        }
    }
}
