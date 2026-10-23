namespace KnightOnline.Shared.Protocol;

/// <summary>Port of enum LogonOpcodes (LogInServer/LoginSession.h).</summary>
public static class LsOpcode
{
    public const byte VersionReq = 0x01;      // LS_VERSION_REQ
    public const byte DownloadInfoReq = 0x02; // LS_DOWNLOADINFO_REQ
    public const byte Cryption = 0xF2;        // LS_CRYPTION
    public const byte LoginReq = 0xF3;        // LS_LOGIN_REQ
    public const byte MGameLogin = 0xF4;      // LS_MGAME_LOGIN (declared, never handled in the original)
    public const byte ServerList = 0xF5;      // LS_SERVERLIST
    public const byte News = 0xF6;            // LS_NEWS
    public const byte UnkF7 = 0xF7;           // LS_UNKF7
}
