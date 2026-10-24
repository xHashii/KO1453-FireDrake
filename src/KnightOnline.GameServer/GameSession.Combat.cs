using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Database.World;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private long _nextMeleeAt;
    private sealed record PendingMelee(ushort Target, ushort Zone, string Life, long Impact, int Damage, float Range, ItemSlotData Right, ItemSlotData Left);
    private PendingMelee? _pendingMelee;
    private Timer? _meleeTimer;
    private (uint Skill, ushort Target, long Ready)? _casting;

    private void MeleeAttack(Packet packet)
    {
        if (!CanTravel() || _user is null || _sitting || !MeleeRules.TryRead(packet, out var request)) return;
        var now = Environment.TickCount64;
        if (now < _nextMeleeAt) return;
        var npc = CombatTarget(request.Target);
        if (npc is null) { Send(MeleeRules.Reply(request,_socketId,0)); return; }
        var weapon = EquippedWeapon();
        var range = MeleeRules.Range(weapon?.Range ?? 0);
        if (!Near(npc.X,npc.Z,range))
        {
            KoLog.Info($"[ MELEE REJECT ] {_charId} target={npc.Id}: range, player={PlayerX:F1},{PlayerZ:F1} monster={npc.X:F1},{npc.Z:F1} allowed={range:F1}");
            Send(MeleeRules.Reply(request,_socketId,0)); return;
        }
        var cadence = MeleeRules.DelayMs(weapon?.Delay ?? 90);
        _nextMeleeAt = now + cadence;
        _pendingMelee = new(npc.Id,_user.Zone,npc.Combat.LifeToken,now+MeleeRules.ImpactDelayMs(cadence),
            MeleeRules.Damage(_totalHit,npc.Prototype.Armor),range,Slot(RightHand),Slot(LeftHand));
        UpdateChatPresence();
        // Start the swing now. Do not replace it with a kill result at frame zero.
        _chat.Region(_user.Zone,npc.X,npc.Z,120,MeleeRules.Reply(request,_socketId,1));
        _meleeTimer ??= new Timer(_ =>
        {
            lock (_gameGate)
            {
                try { ResolveMeleeImpact(); }
                catch (Exception ex) { InventoryUnavailable(ex); }
            }
        },null,TimeSpan.FromMilliseconds(50),TimeSpan.FromMilliseconds(50));
    }
    private void ResolveMeleeImpact()
    {
        var now = Environment.TickCount64;
        if (_pendingMelee is not { } swing || now < swing.Impact) return;
        _pendingMelee = null;
        if (!IsConnected || _inventoryFaulted || !CanTravel() || _sitting || _user!.Zone != swing.Zone || now > swing.Impact+1000 ||
            Slot(RightHand) != swing.Right || Slot(LeftHand) != swing.Left) return;
        var npc = CombatTarget(swing.Target);
        if (npc is null || !Near(npc.X,npc.Z,swing.Range)) return;
        var hit = npc.Combat.Hit(swing.Damage,now,_socketId,swing.Life);
        if (hit is not null) CompleteMonsterHit(npc,hit);
    }
    private ItemProto? EquippedWeapon()
    {
        if (_protos.TryGetValue(Slot(RightHand).ItemId,out var weapon) && !weapon.IsShield) return weapon;
        return _protos.TryGetValue(Slot(LeftHand).ItemId,out weapon) && !weapon.IsShield ? weapon : null;
    }
    private WorldNpc? CombatTarget(ushort id) => CurrentWorld?.Npcs.FirstOrDefault(n => n.Id == id && n.Prototype.IsMonster && n.Health > 0 && _visibleNpcs.Contains(id));

    private void CompleteMonsterHit(WorldNpc npc, MonsterHealth hit)
    {
        Send(TargetHealthPackets.Reply(npc.Id,0,npc.Prototype.MaxHp,hit.Hp,(ushort)hit.Damage));
        KoLog.Info($"[ COMBAT ] {_charId} target={npc.Id} proto={npc.Prototype.Id} damage={hit.Damage} HP={hit.Hp}/{npc.Prototype.MaxHp} killed={hit.Killed}");
        if (!hit.Killed) return;
        SyncWorldNpcs(); // DEAD only; do not immediately delete the actor/death animation.
        try
        {
            var previousLevel = _user!.Level;
            var reward = _characters.AwardKillAsync(_charId,hit.KillToken!,npc,_user.Zone,
                new(_user.Level,_user.Exp,_points,_user.Skill),_world.Drops.Roll(npc.Prototype,_user.Nation)).GetAwaiter().GetResult();
            if (reward is null) return;
            _user = _user with { Level=reward.Progress.Level, Exp=reward.Progress.Exp, Points=reward.Progress.Points, Skill=reward.Progress.Skills };
            _points = reward.Progress.Points;
            if (_user.Level != previousLevel)
            {
                RecomputeStats(refillWhenEmpty:false);
                if (!PersistCombatVitals(_maxHp,_maxMp)) return;
                var maxExp = _characters.LoadLevelExpAsync(_user.Level).GetAwaiter().GetResult();
                Send(new Packet(WizOpcode.LevelChange).WriteUInt16(_socketId).WriteByte(_user.Level).WriteInt16(_points)
                    .WriteByte(_user.Skill.Length == 0 ? (byte)0 : _user.Skill[0]).WriteUInt32((uint)maxExp).WriteUInt32((uint)_user.Exp)
                    .WriteInt16(_maxHp).WriteInt16((short)_currentHp).WriteInt16(_maxMp).WriteInt16((short)_currentMp)
                    .WriteUInt16(_maxWeight).WriteUInt16((ushort)Math.Clamp(_itemWeight,0,ushort.MaxValue)));
            }
            Send(new Packet(WizOpcode.ExpChange).WriteByte(0).WriteInt64(_user.Exp));
            RefreshLoot();
            KoLog.Info($"[ REWARD ] {_charId} kill={hit.KillToken} exp={_user.Exp} level={_user.Level} bundle={reward.Bundle}");
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }

    private bool UseCombatSkill(Packet packet)
    {
        var position = packet.ReadPosition;
        if (!PotionRules.TryRead(packet,out var request)) return false;
        packet.ReadPosition = position;
        if (request.Target == _socketId || request.Target == ushort.MaxValue) return false;
        void Reject(string reason)
        {
            Send(CombatSkillRules.Reply(request.Skill,_socketId,request.Target,4,false));
            KoLog.Info($"[ SKILL REJECT ] {_charId} skill={request.Skill}: {reason}");
        }
        if (!CanTravel() || _user is null || _sitting || request.Caster != _socketId || request.Stage is not (1 or 2 or 3))
        { Reject("state/caster/stage"); return true; }
        try
        {
            var skill = _characters.LoadCombatSkillAsync(request.Skill).GetAwaiter().GetResult();
            var target = CombatTarget(request.Target);
            if (skill is null || !CombatSkillRules.Learned(skill,_user.Class,_user.Level,_user.Skill) || target is null)
            { Reject("unsupported/unlearned skill or target"); return true; }
            var weapon = EquippedWeapon();
            if (skill.WeaponGroup != 0 && skill.WeaponGroup != weapon?.Group)
            { Reject("required weapon group"); return true; }
            var range = skill.Type == 1 ? Math.Max(MeleeRules.Range(weapon?.Range ?? 0),skill.Range) : Math.Max(15,skill.Range);
            if (!Near(target.X,target.Z,range) || _currentMp < skill.Mp) { Reject("range or MP"); return true; }
            var now = Environment.TickCount64;
            if (request.Stage == 1)
            {
                _pendingMelee = null;
                if (_casting is { } pending && now < pending.Ready) { Reject("already casting"); return true; }
                _casting = (skill.Id,target.Id,now+skill.CastMs);
                Send(CombatSkillRules.Reply(skill.Id,_socketId,target.Id,1,true)); return true;
            }
            if (request.Stage == 2)
            {
                if (skill.CastMs > 0 && (_casting is not { } flying || flying.Skill != skill.Id || flying.Target != target.Id || now < flying.Ready))
                { Reject("flying before cast completion"); return true; }
                Send(CombatSkillRules.Reply(skill.Id,_socketId,target.Id,2,true)); return true;
            }
            if (skill.CastMs > 0 && (_casting is not { } cast || cast.Skill != skill.Id || cast.Target != target.Id || now < cast.Ready || now > cast.Ready+5000))
            { Reject("cast timing"); return true; }
            _casting = null;
            var result = _characters.SpendCombatSkillAsync(_charId,skill,_user.Hp,_user.Mp,_currentHp,_currentMp).GetAwaiter().GetResult();
            if (result == InventorySaveResult.Cooldown) { Reject("cooldown"); return true; }
            if (result != InventorySaveResult.Saved) throw new InvalidOperationException("Stale skill vitals snapshot.");
            var mp = _currentMp-skill.Mp;
            _user = _user with { Hp=(short)_currentHp, Mp=(short)mp }; RestoreVitals(0,mp-_currentMp);
            var damage = skill.Type == 1 ? (int)Math.Clamp((long)MeleeRules.Damage(_totalHit,target.Prototype.Armor)*skill.HitPercent/100+skill.AddDamage,1,ushort.MaxValue) : Math.Clamp(skill.DirectDamage,1,ushort.MaxValue);
            var hit = target.Combat.Hit(damage,now,_socketId);
            if (hit is null) { Reject("target already dead"); return true; }
            UpdateChatPresence();
            _chat.Region(_user.Zone,target.X,target.Z,120,CombatSkillRules.Reply(skill.Id,_socketId,target.Id,3,true));
            CompleteMonsterHit(target,hit);
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
        return true;
    }

    private void AllocateSkillPoint(Packet packet)
    {
        if (!CanTravel() || _user is null || packet.RemainingToRead != 1) return;
        var type = packet.ReadByte();
        var skills = SkillPointRules.Allocate(_user.Skill,type,_user.Class,_user.Level);
        if (skills is null)
        { Send(new Packet(WizOpcode.SkillptChange).WriteByte(type).WriteByte(type < _user.Skill.Length ? _user.Skill[type] : (byte)0)); return; }
        try
        {
            if (!_characters.SaveAllocatedSkillAsync(_charId,_user.Skill,skills,_user.Class,_user.Level).GetAwaiter().GetResult())
                throw new InvalidOperationException("Stale skill point allocation.");
            _user = _user with { Skill=skills };
            // Like CUser::SkillPointChange, success is client-predicted; only failures reply.
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }

    private bool PersistCombatVitals(int hp, int mp)
    {
        if (!_characters.TrySaveCombatVitalsAsync(_charId,_user!.Hp,_user.Mp,hp,mp).GetAwaiter().GetResult())
            throw new InvalidOperationException("Stale combat vitals; reconnect required.");
        var deltaHp = hp-_currentHp; var deltaMp = mp-_currentMp;
        _user = _user with { Hp=(short)hp, Mp=(short)mp };
        RestoreVitals(deltaHp,deltaMp); return true;
    }
    private void MonsterRetaliation()
    {
        if (!CanTravel() || _inventoryFaulted || CurrentWorld is null) return;
        try
        {
            foreach (var npc in CurrentWorld.Npcs)
            {
                if (!npc.Prototype.IsMonster || !_visibleNpcs.Contains(npc.Id) || !Near(npc.X,npc.Z,npc.Prototype.AttackRange) ||
                    !npc.Combat.Retaliate(_socketId,Environment.TickCount64,npc.Prototype.AttackDelayMs)) continue;
                var damage = Math.Min(_currentHp,MeleeRules.Damage(npc.Prototype.AttackDamage,_totalAc));
                PersistCombatVitals(_currentHp-damage,_currentMp);
                _sitting = false;
                UpdateChatPresence();
                _chat.Region(_user!.Zone,npc.X,npc.Z,120,MeleeRules.Reply(new(1,_socketId),npc.Id,_currentHp == 0 ? (byte)2 : (byte)1));
                if (_currentHp == 0)
                {
                    _chat.Region(_user.Zone,PlayerX,PlayerZ,120,new Packet(WizOpcode.Dead).WriteUInt16(_socketId));
                    _casting = null; _pendingMelee = null; break;
                }
            }
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }

    private void RevivePlayer(Packet packet)
    {
        if (!_inGame || _user is null || _currentHp != 0 || _travelStage != 0 || packet.RemainingToRead != 1 || packet.ReadByte() != 1) return;
        try
        {
            var destination = _characters.LoadTravelDestinationsAsync(_user.Nation,_user.Level,_user.Zone).GetAwaiter().GetResult().FirstOrDefault();
            if (destination is null || CurrentWorld is not { } zone || !zone.Map.Contains(destination.Px/100f,destination.Pz/100f)) return;
            destination = destination with { Py=(int)(zone.Map.Height(destination.Px/100f,destination.Pz/100f)*100) };
            if (!_characters.TrySaveCombatVitalsAsync(_charId,_user.Hp,_user.Mp,_maxHp,_maxMp,destination).GetAwaiter().GetResult())
                throw new InvalidOperationException("Stale revival snapshot.");
            _user = _user with { Hp=_maxHp, Mp=_maxMp, Zone=destination.Zone, Px=destination.Px, Pz=destination.Pz, Py=destination.Py };
            _savedPosition = new(destination.Zone,destination.Px,destination.Pz,destination.Py);
            _pendingMelee = null;
            _sitting=false;
            Send(new Packet(WizOpcode.Regene).WriteUInt16((ushort)(destination.Px/10)).WriteUInt16((ushort)(destination.Pz/10)).WriteUInt16(unchecked((ushort)(short)(destination.Py/10))));
            RestoreVitals(_maxHp-_currentHp,_maxMp-_currentMp);
            SendWorldNpcs();
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }
}
