using System.Text;
using KnightOnline.Database;
using KnightOnline.Shared.Configuration;

namespace KnightOnline.LoginServer;

/// <summary>Port of _SERVER_INFO (LogInServer/Define.h).</summary>
public sealed class ServerInfoEntry
{
    public string ServerIp { get; init; } = "127.0.0.1";
    public string LanIp { get; init; } = "127.0.0.1";
    public string ServerName { get; init; } = string.Empty;
    public short UserCount { get; set; }
    public short ServerId { get; init; }
    public short GroupId { get; init; }
    public short PlayerCap { get; init; }
    public short FreePlayerCap { get; init; }
    public string KarusKingName { get; init; } = string.Empty;
    public string KarusNotice { get; init; } = string.Empty;
    public string ElMoradKingName { get; init; } = string.Empty;
    public string ElMoradNotice { get; init; } = string.Empty;
}

/// <summary>
/// Everything LoginServer::GetInfoFromIni() read from LogInServer.ini,
/// plus the MariaDB connection settings that replace the [ODBC] section.
/// Falls back to the exact same defaults as the C++ when keys are missing.
/// </summary>
public sealed class LoginServerSettings
{
    public const string ConfigFileName = "LogInServer.ini";

    private const int MaxUser = 3000; // shared/globals.h MAX_USER

    public int Port { get; private init; }
    public bool AllowOfflineStart { get; private init; }
    public bool AutoMigrate { get; private init; } = true;
    public string MigrationsPath { get; private init; } = "sql/kn_online";

    /// <summary>
    /// When true (the default) <see cref="KnightOnline.Database.Migrations.DatabaseMigrator"/>
    /// cleans up the
    /// leftovers of a script that failed halfway before retrying it, so a
    /// broken migration is recoverable by simply starting the server again.
    /// Set MIGRATION_REPAIR=0 only if you manage the schema by hand.
    /// </summary>
    public bool MigrationRepair { get; private init; } = true;

    /// <summary>
    /// When true, a successful first login with an unknown account creates
    /// that account. This was unconditional in the original ACCOUNT_LOGIN
    /// procedure; the rewrite keeps it off unless explicitly enabled.
    /// </summary>
    public bool AutoRegister { get; private init; }

    public DatabaseSettings Database { get; private init; } =
        new("127.0.0.1", 3306, "KN_Online", "username", "password");
    public string FtpUrl { get; private init; } = "ftp.yoursite.net";
    public string FtpPath { get; private init; } = "/";
    public List<ServerInfoEntry> Servers { get; private init; } = new();

    /// <summary>Pre-rendered news blob (LogInServer News struct), Latin1 bytes.</summary>
    public byte[] NewsContent { get; private init; } = Array.Empty<byte>();

    public static LoginServerSettings Load(string path = ConfigFileName)
    {
        // Working directory first (dotnet run from the repo root), then next
        // to the executable (running the built exe from its output folder).
        if (!Path.IsPathRooted(path) && !File.Exists(path))
        {
            var nextToExe = Path.Combine(AppContext.BaseDirectory, path);
            if (File.Exists(nextToExe))
                path = nextToExe;
        }

        var ini = new IniFile(path);

        var settings = new LoginServerSettings
        {
            Port = ini.GetInt("SETTINGS", "PORT", 15100),
            AllowOfflineStart = ini.GetBool("SETTINGS", "ALLOW_OFFLINE_START", false),
            AutoMigrate = ini.GetBool("DATABASE", "AUTO_MIGRATE", true),
            MigrationsPath = ini.GetString("DATABASE", "MIGRATIONS", "sql/kn_online"),
            MigrationRepair = ini.GetBool("DATABASE", "MIGRATION_REPAIR", true),
            AutoRegister = ini.GetBool("DATABASE", "AUTO_REGISTER", false),
            FtpUrl = ini.GetString("DOWNLOAD", "URL", "ftp.yoursite.net"),
            FtpPath = ini.GetString("DOWNLOAD", "PATH", "/"),
            Database = new DatabaseSettings(
                ini.GetString("DATABASE", "SERVER", "127.0.0.1"),
                (uint)ini.GetInt("DATABASE", "PORT", 3306),
                ini.GetString("DATABASE", "DATABASE", "KN_Online"),
                ini.GetString("DATABASE", "UID", "username"),
                ini.GetString("DATABASE", "PWD", "password")),
            NewsContent = BuildNews(ini),
        };

        var serverCount = ini.GetInt("SERVER_LIST", "COUNT", 1);
        if (serverCount <= 0)
            serverCount = 1;

        for (var i = 0; i < serverCount; i++)
        {
            string Key(string name) => $"{name}_{i:00}";

            settings.Servers.Add(new ServerInfoEntry
            {
                ServerIp = ini.GetString("SERVER_LIST", Key("SERVER"), "127.0.0.1"),
                LanIp = ini.GetString("SERVER_LIST", Key("LANIP"), "127.0.0.1"),
                ServerName = ini.GetString("SERVER_LIST", Key("NAME"), "TEST|Server 1"),
                ServerId = (short)ini.GetInt("SERVER_LIST", Key("ID"), 1),
                GroupId = (short)ini.GetInt("SERVER_LIST", Key("GROUPID"), 1),
                PlayerCap = (short)ini.GetInt("SERVER_LIST", Key("PREMLIMIT"), MaxUser),
                FreePlayerCap = (short)ini.GetInt("SERVER_LIST", Key("FREELIMIT"), MaxUser),
                KarusKingName = ini.GetString("SERVER_LIST", Key("KING1"), string.Empty),
                ElMoradKingName = ini.GetString("SERVER_LIST", Key("KING2"), string.Empty),
                KarusNotice = ini.GetString("SERVER_LIST", Key("KINGMSG1"), string.Empty),
                ElMoradNotice = ini.GetString("SERVER_LIST", Key("KINGMSG2"), string.Empty),
            });
        }

        return settings;
    }

    /// <summary>
    /// Port of the NEWS rendering in GetInfoFromIni(): up to 3 blocks of
    ///   title '#' 0x00 '\n' message 0x00 '\n' '#' 0x00 '\n' 0x00 '\n'
    /// </summary>
    private static byte[] BuildNews(IniFile ini)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < 3; i++)
        {
            var title = ini.GetString("NEWS", $"TITLE_{i:00}", string.Empty);
            if (title.Length == 0)
                continue;

            var message = ini.GetString("NEWS", $"MESSAGE_{i:00}", string.Empty);
            if (message.Length == 0)
                continue;

            builder.Append(title);
            builder.Append('#').Append('\0').Append('\n');   // BOX_START
            builder.Append(message);
            builder.Append('\0').Append('\n');               // LINE_ENDING
            builder.Append('#').Append('\0').Append('\n');   // BOX_END = BOX_START
            builder.Append('\0').Append('\n');               //         + LINE_ENDING
        }

        return Encoding.Latin1.GetBytes(builder.ToString());
    }
}
