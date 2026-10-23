using KnightOnline.Database;
using KnightOnline.Database.Migrations;
using KnightOnline.Shared;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.LoginServer;

/// <summary>
/// Port of class LoginServer (LogInServer/LoginServer.cpp): startup sequence,
/// version/patch list, cached server-list packet rebuilt every minute from
/// the CONCURRENT table (Timer_UpdateUserCount), and the user log file.
/// </summary>
public sealed class LoginServerApp
{
    private readonly LoginServerSettings _settings;
    private readonly IAccountRepository? _accounts;
    private readonly KoTcpServer<LoginSession> _server;
    private readonly object _serverListGate = new();
    private readonly object _userLogGate = new();

    private List<VersionInfo> _versionList = new();
    private ushort _latestVersion = GameVersion.Version; // m_sLastVersion starts at __VERSION
    private byte[] _serverListPayload = Array.Empty<byte>();
    private int _userCountFailures;
    private StreamWriter? _userLog;

    public LoginServerApp(LoginServerSettings settings)
    {
        _settings = settings;
        _accounts = TryCreateRepository(settings);
        _server = new KoTcpServer<LoginSession>(client => new LoginSession(client, this));
    }

    public LoginServerSettings Settings => _settings;

    public IAccountRepository? Accounts => _accounts;

    public ushort LatestVersion => _latestVersion;

    public IReadOnlyList<VersionInfo> VersionList => _versionList;

    private static IAccountRepository? TryCreateRepository(LoginServerSettings settings)
    {
        // Repository creation itself doesn't connect; connectivity is proven
        // when the version list is loaded in RunAsync, mirroring Startup().
        return new MariaDbAccountRepository(settings.Database, settings.AutoRegister);
    }

    public async Task RunAsync(CancellationToken shutdownToken)
    {
        OpenUserLog();

        // No manual imports: create the configured database if needed and
        // apply any pending scripts from the configured migrations folder
        // (default sql/kn_online; see docs/DATABASE.md).
        if (_settings.AutoMigrate)
        {
            try
            {
                var migrator = new DatabaseMigrator(
                    _settings.Database, _settings.MigrationsPath, _settings.MigrationRepair);
                await migrator.MigrateAsync(shutdownToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                KoLog.Error("Auto-migration failed.", ex);
                if (!_settings.AllowOfflineStart)
                    throw;
            }
        }

        var databaseReady = await LoadVersionListAsync().ConfigureAwait(false);
        if (!databaseReady)
        {
            // The C++ server refused to start without the DB. Offline start is
            // a C#-port convenience for testing the client before the database
            // has been converted - logins then return AUTH_ERROR.
            if (!_settings.AllowOfflineStart)
                throw new InvalidOperationException(
                    "Could not load the version list from the configured database. " +
                    "Check [DATABASE] in LogInServer.ini (or set ALLOW_OFFLINE_START=1).");

            KoLog.Warn("Starting WITHOUT a database connection (ALLOW_OFFLINE_START=1). Login attempts will fail.");
        }

        // Saying "latest version in database" when the database was never read
        // sends you looking in exactly the wrong place.
        var tableVersion = _latestVersion;
        if (databaseReady)
            KoLog.Info($"Latest version in database: {tableVersion}");
        else
            KoLog.Warn($"No version list from the database; falling back to the built-in version {tableVersion}.");

        ApplyAdvertisedVersion();

        if (_settings.AdvertiseVersion != 0)
            KoLog.Info($"Advertising version {_latestVersion} ([SETTINGS] ADVERTISE_VERSION) and offering " +
                       $"{_versionList.Count} patch file(s); the VERSION table's highest is {tableVersion}.");
        else if (databaseReady && tableVersion > GameVersion.Version)
            KoLog.Warn($"The VERSION table advertises {tableVersion} but this port targets {GameVersion.Version}. " +
                       $"A client older than that asks for the patch files above its own version and then dials " +
                       $"'{_settings.FtpUrl}{_settings.FtpPath}'; when that host does not exist the client stops " +
                       $"before the login dialog (its patcher cannot reach the FTP server). Set ADVERTISE_VERSION " +
                       $"to your client's version in [SETTINGS], e.g. ADVERTISE_VERSION={GameVersion.Version}.");

        // Build the first server list synchronously so early clients see data,
        // then refresh every 60 seconds like Timer_UpdateUserCount.
        await UpdateServerListAsync().ConfigureAwait(false);

        using var refreshShutdown = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        var refresher = RefreshServerListLoopAsync(refreshShutdown.Token);

        try
        {
            await _server.RunAsync(_settings.Port).ConfigureAwait(false);
        }
        finally
        {
            // Also stops the refresher when the listener dies first, so RunAsync
            // returns instead of hanging on the timer loop.
            refreshShutdown.Cancel();
            await refresher.ConfigureAwait(false);
        }
    }

    public void Stop() => _server.Stop();

    private async Task<bool> LoadVersionListAsync()
    {
        if (_accounts is null)
            return false;

        try
        {
            var versions = await _accounts.LoadVersionListAsync().ConfigureAwait(false);
            _versionList = versions.ToList();

            if (_versionList.Count > 0)
            {
                // The original resets to 0 then takes the max from the table.
                _latestVersion = 0;
                foreach (var version in _versionList)
                {
                    if (version.Version > _latestVersion)
                        _latestVersion = version.Version;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            KoLog.Error("Failed to load the version list.", ex);
            return false;
        }
    }

    /// <summary>
    /// Caps both the advertised version and the patch list at
    /// <see cref="LoginServerSettings.AdvertiseVersion"/> when it is set, so a
    /// client is never offered patches above the version it was just told to be.
    /// </summary>
    private void ApplyAdvertisedVersion()
    {
        var cap = _settings.AdvertiseVersion;
        if (cap == 0)
            return;

        _versionList = _versionList.Where(info => info.Version <= cap).ToList();
        _latestVersion = cap;
    }

    private async Task RefreshServerListLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                await UpdateServerListAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    /// <summary>Port of LoginServer::UpdateServerList() (1453 branch).</summary>
    public async Task UpdateServerListAsync()
    {
        if (_accounts is not null)
        {
            try
            {
                var counts = await _accounts.LoadUserCountsAsync().ConfigureAwait(false);
                foreach (var (serverId, count) in counts)
                {
                    var index = serverId - 1;
                    if (index >= 0 && index < _settings.Servers.Count)
                        _settings.Servers[index].UserCount = (short)count;
                }

                if (_userCountFailures > 0)
                {
                    KoLog.Info($"User counts are loading again after {_userCountFailures} failed attempt(s).");
                    _userCountFailures = 0;
                }
            }
            catch (Exception ex)
            {
                // This runs every 60 seconds. A database that is simply down
                // must not bury the log in identical stack traces, but the
                // first failure and the fact that it is ongoing stay visible.
                _userCountFailures++;
                if (_userCountFailures == 1)
                    KoLog.Error("Failed to refresh user counts.", ex);
                else if (_userCountFailures % 30 == 0)
                    KoLog.Warn($"Still unable to refresh user counts after {_userCountFailures} attempts " +
                               $"({ex.GetType().Name}: {ex.Message}).");
            }
        }

        var packet = new Packet(0);
        packet.WriteByte((byte)_settings.Servers.Count);

        foreach (var server in _settings.Servers)
        {
            packet.WriteString(server.ServerIp);
            // 1888+: LanIp - not sent for 1453.
            packet.WriteString(server.ServerName);

            if (server.UserCount <= server.PlayerCap)
                packet.WriteInt16(server.UserCount);
            else
                packet.WriteInt16(-1);

            // #if __VERSION >= 1453
            packet.WriteInt16(server.ServerId);
            packet.WriteInt16(server.GroupId);
            packet.WriteInt16(server.PlayerCap);
            packet.WriteInt16(server.FreePlayerCap);
            packet.WriteByte(1); // < 1600: unknown flag, 1 in 15XX samples

            packet.WriteString(server.KarusKingName);
            packet.WriteString(server.KarusNotice);
            packet.WriteString(server.ElMoradKingName);
            packet.WriteString(server.ElMoradNotice);
        }

        lock (_serverListGate)
        {
            _serverListPayload = packet.ToArray();
        }
    }

    /// <summary>Port of LoginServer::GetServerList(): appends the cached payload.</summary>
    public void AppendServerListTo(Packet result)
    {
        lock (_serverListGate)
        {
            result.WriteBytes(_serverListPayload);
        }
    }

    private void OpenUserLog()
    {
        Directory.CreateDirectory("Logs");
        var now = DateTime.Now;
        var path = $"Logs/Login_{now.Day}_{now.Month}_{now.Year}.log";
        _userLog = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
    }

    /// <summary>Port of WriteUserLogFile(). Never pass passwords in here.</summary>
    public void WriteUserLog(string message)
    {
        lock (_userLogGate)
        {
            _userLog?.Write(message);
        }
    }
}
