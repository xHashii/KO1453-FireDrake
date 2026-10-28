using KnightOnline.Database;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    // Packet processing, timer callbacks and disconnect saves share one game-state lock.
    private readonly object _gameGate = new();
    private Timer? _recoveryTimer;
    private Timer? _worldTimer;
    private bool _sitting;

    private void StartRecovery()
    {
        _worldTimer ??= new Timer(_ =>
        {
            lock (_gameGate)
            {
                if (!_inGame || !IsConnected || _user is null || _travelStage != 0) return;
                try
                {
                    TickSpeedEffect();
                    SyncWorldNpcs();
                    MonsterRetaliation();
                    UpdatePlayerPresence();
                    if (Environment.TickCount64 >= _nextLootRefresh)
                    { _nextLootRefresh = Environment.TickCount64+5000; RefreshLoot(); }
                }
                catch (Exception ex) { KoLog.Error("World visibility refresh failed.", ex); }
            }
        }, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
        _recoveryTimer ??= new Timer(_ =>
        {
            lock (_gameGate)
            {
                if (!_inGame || !IsConnected || _user is null || _travelStage != 0 || _currentHp <= 0)
                    return;
                var amount = RecoveryRules.Amounts(_user.Level, _user.Class, _sitting,
                    _user.Authority == 0, _maxHp, _maxMp, _currentMp);
                RestoreVitals(amount.Hp, amount.Mp);
            }
        }, null, TimeSpan.FromSeconds(RecoveryRules.IntervalSeconds), TimeSpan.FromSeconds(RecoveryRules.IntervalSeconds));
    }

    private void RestoreVitals(int hp, int mp)
    {
        var oldHp = _currentHp;
        var oldMp = _currentMp;
        _currentHp = Math.Clamp(_currentHp + hp, 0, Math.Max(0, (int)_maxHp));
        _currentMp = Math.Clamp(_currentMp + mp, 0, Math.Max(0, (int)_maxMp));
        if (oldHp != _currentHp)
            Send(new Packet(WizOpcode.HpChange).WriteUInt16((ushort)_maxHp)
                .WriteUInt16((ushort)_currentHp).WriteUInt16(ushort.MaxValue));
        if (oldMp != _currentMp)
            Send(new Packet(WizOpcode.MspChange).WriteUInt16((ushort)_maxMp).WriteUInt16((ushort)_currentMp));
        UpdatePartyPresence();
    }

    protected override void OnDisconnected()
    {
        lock (_gameGate)
        {
            if (_inGame) TracePlayerVisibility("disconnect");
            _speedEffect.Clear();
            _chat.Remove(_socketId);
            _players.Remove(_socketId);
            _monsters.Remove(MonsterIdentity);
            _parties.Remove(_socketId);
            _pendingMelee = null; _meleeTimer?.Dispose(); _meleeTimer = null;
            _worldTimer?.Dispose(); _worldTimer = null;
            _recoveryTimer?.Dispose();
            _recoveryTimer = null;
            if (_inGame && _user is not null)
            {
                try { SaveCurrentPosition(); }
                catch (Exception ex) { KoLog.Error($"Could not save logout position for {_charId}.",ex); }
            }
            if (_inGame && _user is not null && !_inventoryFaulted)
            {
                try { _characters.SaveVitalsAsync(_charId, _currentHp, _currentMp, _user.Hp, _user.Mp).GetAwaiter().GetResult(); }
                catch (Exception ex) { KoLog.Error($"Could not save HP/MP for {_charId}.", ex); }
            }
            _inGame = false;
            _editLeaseTimer?.Dispose(); _editLeaseTimer = null;
            try { _editLease?.Dispose(); } catch (Exception ex) { KoLog.Error("Session lock cleanup failed.", ex); }
            _editLease = null;
        }
        base.OnDisconnected();
    }
}
