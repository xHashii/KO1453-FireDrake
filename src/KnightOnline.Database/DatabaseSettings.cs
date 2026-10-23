using MySqlConnector;

namespace KnightOnline.Database;

/// <summary>
/// Connection settings for the single MariaDB database used by the C# servers.
/// The received MSSQL database was named KN_Online because the original
/// C++ servers reached it through ODBC, but that name is not baked into the
/// rewrite: <see cref="Database"/> comes from server config and can be any
/// valid MariaDB database name. Migration SQL deliberately contains no
/// CREATE DATABASE / USE statement.
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
            // The converted game data is UTF-8 and contains Turkish text, and
            // DatabaseMigrator creates the database as utf8mb4. Pinning the
            // connection charset keeps literals in the migration scripts - and
            // character names on the wire - away from a server whose default
            // is latin1, which would turn every non-ASCII character into '?'.
            CharacterSet = "utf8mb4",
            SslMode = MySqlSslMode.None,
            Pooling = true,
        };
        return builder.ConnectionString;
    }
}
