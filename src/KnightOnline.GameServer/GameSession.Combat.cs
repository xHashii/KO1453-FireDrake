using KnightOnline.Database.World;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private long _nextMeleeAt;

    private void MeleeAttack(Packet packet)
    {
        if (!_inGame || _user is null || _currentHp <= 0 || _travelStage != 0 ||
            _sitting || !MeleeRules.TryRead(packet, out var request)) return;
        var now = Environment.TickCount64;
        if (now < _nextMeleeAt) return;
        var npc = CurrentWorld?.Npcs.FirstOrDefault(n => n.Id == request.Target);
        // PvE only. Services, guards, gates, users and cross-zone/unseen IDs are never damageable.
        if (npc is null || !npc.Prototype.IsMonster || !_visibleNpcs.Contains(npc.Id))
        { Send(MeleeRules.Reply(request, _socketId, 0)); return; }
        _protos.TryGetValue(Slot(RightHand).ItemId, out var weapon);
        if (!Near(npc.X, npc.Z, MeleeRules.Range(weapon?.Range ?? 10)))
        { Send(MeleeRules.Reply(request, _socketId, 0)); return; }
        _nextMeleeAt = now + MeleeRules.DelayMs(weapon?.Delay ?? 100);
        var hit = npc.Combat.Hit(MeleeRules.Damage(_totalHit, npc.Prototype.Armor), now);
        if (hit is null) { Send(MeleeRules.Reply(request, _socketId, 0)); SyncWorldNpcs(); return; }
        UpdateChatPresence();
        _chat.Region(_user.Zone, npc.X, npc.Z, 120,
            MeleeRules.Reply(request, _socketId, hit.Killed ? (byte)2 : (byte)1));
        Send(TargetHealthPackets.Reply(npc.Id, 0, npc.Prototype.MaxHp, hit.Hp, (ushort)hit.Damage));
        if (hit.Killed) SyncWorldNpcs();
        KoLog.Info($"[ MELEE ] {_charId} target={npc.Id} proto={npc.Prototype.Id} damage={hit.Damage} HP={hit.Hp}/{npc.Prototype.MaxHp} killed={hit.Killed}");
    }
}
