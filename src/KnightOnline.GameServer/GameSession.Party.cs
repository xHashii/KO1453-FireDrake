using KnightOnline.Database;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private readonly PartyHub _parties;
    private long _nextPartyAt;
    private (short MaxHp,int Hp,short MaxMp,int Mp)? _lastPartyVitals;

    private void UpdatePartyPresence()
    {
        if (!IsConnected || !_inGame || _travelStage != 0 || _inventoryFaulted || _user is null)
        { _parties.Remove(_socketId); return; }
        _parties.Publish(new PartyMember(_socketId, _charId, _user.Nation, _user.Zone,
            _user.Level, _user.Class, _maxHp, (short)_currentHp, _maxMp, (short)_currentMp,
            GmVisibilityRules.Hidden(_user.Authority, _gmVisible), p => { Send(p); }));
        var vitals=(_maxHp,_currentHp,_maxMp,_currentMp);
        if (_lastPartyVitals != vitals && _parties.Members(_socketId).Length > 0)
            KoLog.Info($"[ PARTY VITALS ] sid={_socketId} char={_charId} hp={_currentHp}/{_maxHp} mp={_currentMp}/{_maxMp}");
        _lastPartyVitals=vitals;
    }

    private void ProcessParty(Packet packet)
    {
        UpdatePartyPresence();
        if (!_inGame || _travelStage != 0 || _inventoryFaulted || _user is null) return;
        var now = Environment.TickCount64;
        if (now < _nextPartyAt) return;
        _nextPartyAt = now + 300;
        var accepted=_parties.Process(_socketId, packet);
        KoLog.Info($"[ PARTY ACTION ] sid={_socketId} char={_charId} accepted={accepted} leaderFirstRoster={string.Join(",",_parties.Members(_socketId))}");
        if (!accepted)
            TravelMessage("Party request refused: check invitation, leader, online member, level, nation and zone.");
    }
}
