using KnightOnline.Shared.Network;
using KnightOnline.Database.Items;
using KnightOnline.Database;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private readonly PlayerHub _players;
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
        KoLog.Info($"[ GM VISIBILITY ] {_charId}: {(visible ? "visible" : "hidden")}");
        TravelMessage(visible ? "GM visibility: visible to nearby players." : "GM visibility: hidden from other players.");
    }
    private short _direction;
    private int _appearanceVersion;
    private void UpdatePlayerPresence()
    {
        if (!IsConnected || !_inGame || _travelStage != 0 || _inventoryFaulted || _user is null)
        { _players.Remove(_socketId); _parties.Remove(_socketId); return; }
        UpdatePartyPresence();
        var u = _user;
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
        if (!_inGame || packet.RemainingToRead < 2) return;
        var count = packet.ReadUInt16();
        if (count > 1000 || packet.RemainingToRead != count*2) return;
        var ids = new ushort[count]; for (var i=0;i<count;i++) ids[i]=packet.ReadUInt16();
        UpdatePlayerPresence(); foreach(var reply in _players.Requested(_socketId,ids)) Send(reply);
    }
}
