using MySqlConnector;

namespace KnightOnline.Database;

/// <summary>
/// Connection settings for one MariaDB database. KO uses two databases:
/// the account DB (ODBC DSN "KO_MAIN") and the game DB (DSN "KO_GAME",
/// ) - in the originals. The C# port takes explicit settings instead of DSNs.
/// </summary>
public sealed record DatabaseSettings(
    string Server,
    uint Port,
    string Database,
    string User,
    string Password)
{
    public string BuildConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = Server,
            Port = Port,
            Database = Database,
            UserID = User,
            Password = Password,
            // KO's character names / messages predate utf8mb4 everywhere; the
            // converted schema decides. Connection charset follows the server.
            SslMode = MySqlSslMode.None,
            Pooling = true,
        };
        return builder.ConnectionString;
    }
}
