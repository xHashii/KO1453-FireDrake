namespace KnightOnline.Database.Admin;

/// <summary>
/// Port of CGameServerDlg::GenerateItemSerial (GameServerDlg.cpp):
///   serial = serverNo(56) | (year%100)(48) | tm_mon(40) | mday(32)
///          | hour(24) | minute(16) | counter(0)        [all fields 8 bits]
/// Note the original uses tm_mon raw (0-11) - preserved: uniqueness is what
/// matters, and staying bit-compatible keeps serials indistinguishable from
/// game-generated ones.
///
/// The game server keeps its own counter starting at 0 each boot, so two
/// generators could in theory collide within the same minute. The panel
/// seeds its counter randomly per process to make that vanishingly unlikely;
/// when the GameServer port grows an admin API, serials should be requested
/// from it instead.
/// </summary>
public sealed class ItemSerialGenerator
{
    private readonly object _gate = new();
    private ushort _counter;

    public ItemSerialGenerator(int serverNo = 1)
    {
        ServerNo = (byte)serverNo;
        _counter = (ushort)Random.Shared.Next(0, ushort.MaxValue);
    }

    public byte ServerNo { get; }

    public ulong Next()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var counter = _counter++;

            return ((ulong)ServerNo << 56)
                 | ((ulong)(now.Year % 100) << 48)
                 | ((ulong)(now.Month - 1) << 40)
                 | ((ulong)now.Day << 32)
                 | ((ulong)now.Hour << 24)
                 | ((ulong)now.Minute << 16)
                 | counter;
        }
    }
}
