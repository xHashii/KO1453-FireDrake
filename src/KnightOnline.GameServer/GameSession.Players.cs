using KnightOnline.Shared.Network;
using KnightOnline.Database.Items;
using KnightOnline.Database.World;
using KnightOnline.Database;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private readonly PlayerHub _players;
    private readonly MonsterSimulation _monsters;
    private readonly Guid _monsterLease=Guid.NewGuid();
    private MonsterActorKey MonsterIdentity => new(_socketId,_monsterLease);
    protected override void OnConnected()
    {
        _socketId=SessionId;
        base.OnConnected();
    }
    private bool _gmVisible; // Explicit opt-in resets on every new connection.
    private void SetGmVisibility(string command, int argumentCount)
    {
        if (!GmVisibilityRules.TrySet(_user!.Authority,command,argumentCount,out var visible))
        {
            TravelMessage(GmVisibilityRules.IsGm(_user.Authority)
                ? "Usage: +visible or +invisible (no arguments)."
                : "Visibility commands require GM authority.");
            return;
        }
        _gmVisible=visible;
        UpdatePlayerPresence();
        _players.Refresh(_socketId);
        TracePlayerVisibility("gm-"+command);
        KoLog.Info($"[ GM VISIBILITY ] {_charId}: {(visible ? "visible" : "hidden")}");
        TravelMessage(visible ? "GM visibility: visible to nearby players." : "GM visibility: hidden from other players.");
    }
    private string? _lastPresenceGate;
    private void TracePlayerVisibility(string reason) =>
        KoLog.Info($"[ PLAYER VISIBILITY ] reason={reason} sid={_socketId} char={_charId} authority={_user?.Authority} gmOptIn={_gmVisible} race={_user?.Race} class={_user?.Class} appearanceVersion={_appearanceVersion} {_players.DescribeVisibility(_socketId)}");

    private short _direction;
    private int _appearanceVersion;
    private void UpdatePlayerPresence()
    {
        // Log eligibility transitions only, never every 250ms/movement publish.
        var gate=$"connected={IsConnected} inGame={_inGame} travel={_travelStage} inventoryFaulted={_inventoryFaulted} loaded={_user is not null} zone={_user?.Zone} authority={_user?.Authority} gmOptIn={_gmVisible}";
        if (_lastPresenceGate != gate)
        {
            _lastPresenceGate=gate;
            KoLog.Info($"[ PLAYER PRESENCE GATE ] sid={_socketId} char={_charId} {gate}");
        }
        if (!IsConnected || !_inGame || _travelStage != 0 || _inventoryFaulted || _user is null)
        { _players.Remove(_socketId); _parties.Remove(_socketId); _monsters.Remove(MonsterIdentity); return; }
        UpdatePartyPresence();
        var u = _user;
        if(_currentHp<=0) _monsters.Remove(MonsterIdentity);
        else _monsters.Publish(new(MonsterIdentity,u.Zone,PlayerX,PlayerZ,!GmVisibilityRules.Hidden(u.Authority,_gmVisible)));
        var info=PlayerPackets.Info(new(_charId,u.Nation,u.Level,u.Race,u.Class,
            (ushort)(u.Px/10),(ushort)(u.Pz/10),(short)(u.Py/10),u.Face,u.Hair,
            _currentHp<=0?(byte)3:_sitting?(byte)2:(byte)1,u.Authority,_direction,0,(byte)u.Zone,_slots),id=>_protos.ContainsKey(id));
        _players.Publish(new(_socketId,u.Zone,PlayerX,PlayerZ,info,_appearanceVersion,p => { Send(p); },GmVisibilityRules.Hidden(u.Authority,_gmVisible)));
    }
    private void PlayerRotate(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead != 2) return;
        _direction = packet.ReadInt16(); UpdatePlayerPresence();
        _players.Broadcast(_socketId,new Packet(WizOpcode.Rotate).WriteUInt16(_socketId).WriteInt16(_direction));
    }
    private void RequestedPlayers(Packet packet)
    {
        if (!_inGame || packet.RemainingToRead < 2)
        { KoLog.Warn($"[ PLAYER REQUEST ] sid={_socketId} char={_charId} rejected: inGame={_inGame} bytes={packet.RemainingToRead}"); return; }
        var count = packet.ReadUInt16();
        if (count > 1000 || packet.RemainingToRead != count*2)
        { KoLog.Warn($"[ PLAYER REQUEST ] sid={_socketId} char={_charId} rejected: count={count} remaining={packet.RemainingToRead}"); return; }
        var ids = new ushort[count]; for (var i=0;i<count;i++) ids[i]=packet.ReadUInt16();
        UpdatePlayerPresence();
        KoLog.Info($"[ PLAYER REQUEST ] sid={_socketId} char={_charId} ids={string.Join(",",ids.Take(32))} count={count}");
        TracePlayerVisibility("requested-users");
        foreach(var reply in _players.Requested(_socketId,ids)) Send(reply);
    }
}
