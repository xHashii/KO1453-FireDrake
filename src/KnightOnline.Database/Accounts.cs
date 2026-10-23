namespace KnightOnline.Database;

/// <summary>One row of the VERSION table (LogInServer _VERSION_INFO).</summary>
public sealed record VersionInfo(ushort Version, ushort HistoryVersion, string FileName);

/// <summary>Result codes of the ACCOUNT_LOGIN routine, as the client expects them.</summary>
public enum AccountLoginResult : byte
{
    Success = 0x01,
    NotFound = 0x02,
    InvalidPassword = 0x03,
    Banned = 0x04,
    InGame = 0x05,
    Error = 0x06,
    Agreement = 0x0F,
    Failed = 0xFF,
}

/// <summary>
/// Everything the LoginServer needs from the account database.
/// Port of LogInServer/CDBProcess.
/// </summary>
public interface IAccountRepository
{
    /// <summary>SELECT sVersion, sHistoryVersion, strFilename FROM VERSION</summary>
    Task<IReadOnlyList<VersionInfo>> LoadVersionListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// SELECT serverid, zone1_count, zone2_count, zone3_count FROM CONCURRENT
    /// Returned as serverId -> total users (sum of the three zone counts).
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> LoadUserCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls ACCOUNT_LOGIN (T-SQL: {? = CALL ACCOUNT_LOGIN(?, ?)}).
    /// The MariaDB port of the procedure takes an OUT parameter - see
    /// sql/mysql/ko_main/. To be replaced by the converted original
    /// procedure once the real database export is available.
    /// </summary>
    Task<AccountLoginResult> AccountLoginAsync(string accountId, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls ACCOUNT_PREMIUM: remaining premium hours, or -1 if none.
    /// </summary>
    Task<short> AccountPremiumAsync(string accountId, CancellationToken cancellationToken = default);
}
