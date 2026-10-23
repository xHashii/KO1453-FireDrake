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
            // KO's character names / messages predate utf8mb4 everywhere; the
            // converted schema decides. Connection charset follows the server.
            SslMode = MySqlSslMode.None,
            Pooling = true,
        };
        return builder.ConnectionString;
    }
}
