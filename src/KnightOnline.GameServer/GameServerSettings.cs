using KnightOnline.Database;
using KnightOnline.Shared.Protocol;
using KnightOnline.Shared.Configuration;

namespace KnightOnline.GameServer;

/// <summary>
/// Configuration for the GameServer, read from <c>GameServer.ini</c> in the
/// working directory - the same shape as the LoginServer's LogInServer.ini
/// (the original C++ game server kept its settings in server.ini). A missing
/// file is not an error: every key falls back to the default below, so the
/// server still starts and only database access needs real values.
/// </summary>
public sealed record GameServerSettings(int Port, DatabaseSettings Database, bool CharacterPreviewEquipment = true, ushort ProtocolVersion = 1453)
{
    public const string ConfigFileName = "GameServer.ini";

    public static GameServerSettings Load(string path = ConfigFileName)
    {
        var ini = new IniFile(path);
        return new GameServerSettings(
            ini.GetInt("SETTINGS", "PORT", 15001), // GameServerDlg.cpp [SETTINGS] PORT
            new DatabaseSettings(
                ini.GetString("DATABASE", "SERVER", "127.0.0.1"),
                (uint)ini.GetInt("DATABASE", "PORT", 3306),
                ini.GetString("DATABASE", "DATABASE", "KN_Online"),
                ini.GetString("DATABASE", "UID", "username"),
                ini.GetString("DATABASE", "PWD", "password")),
            ini.GetInt("SETTINGS", "CHARACTER_PREVIEW_RECOVERY", 0) == 0,
            ClientProtocol.Validate(ini.GetInt("SETTINGS", "PROTOCOL_VERSION", 1453)));
    }
}
