using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.World;

public readonly record struct MonsterLife(int Hp, string Token, bool Evading);

public sealed record MonsterHealth(int Hp, int Damage, bool Killed, string? KillToken = null);

/// <summary>One shared runtime life per spawn, guarded independently of session locks.</summary>
public sealed class MonsterCombat(int maxHp, int respawnSeconds)
{
    private readonly object _gate = new();
    private readonly int _maxHp = Math.Max(1, maxHp);
    private int _hp = Math.Max(1, maxHp);
    private long _respawnAt = long.MaxValue, _diedAt;
    private ushort _target;
    private bool _evading;
    private long _nextRetaliation, _engagedAt;
    private string _life = Guid.NewGuid().ToString("N");
    public string LifeToken { get { lock (_gate) return _life; } }
    public string? DeathToken { get { lock (_gate) return _hp == 0 ? _life : null; } }
    public bool CorpseVisible(long now)
    { lock (_gate) { Respawn(now); return _hp == 0 && now < Math.Min(_respawnAt, _diedAt + 30000); } }
    public bool Retaliate(ushort target, long now, int delayMs)
    {
        lock (_gate)
        {
            Respawn(now);
            if (_evading || _hp <= 0 || _target != target || now < _nextRetaliation || now - _engagedAt > 30000) return false;
            _nextRetaliation = now + Math.Clamp(delayMs, 500, 10000); return true;
        }
    }

    private void Respawn(long now)
    {
        if (_hp == 0 && now >= _respawnAt) { _hp = _maxHp; _evading = false; _respawnAt = long.MaxValue; _target = 0; _nextRetaliation = 0; _engagedAt = 0; _life = Guid.NewGuid().ToString("N"); }
    }
    public MonsterLife Read(long now)
    { lock(_gate) { Respawn(now); return new(_hp,_life,_evading); } }
    internal bool WhileLiving(string life,long now,Action change)
    { lock(_gate) { Respawn(now); if(_hp<=0 || _life!=life) return false; change(); return true; } }
    internal void ChooseTarget(ushort target,long now)
    {
        lock(_gate)
        {
            Respawn(now); if(_hp<=0 || _evading) return;
            if(_nextRetaliation==0) _nextRetaliation=now+1000;
            _target=target; _engagedAt=now;
        }
    }
    internal void BeginReturn(long now)
    { lock(_gate) { Respawn(now); if(_hp>0) { _evading=true; _target=0; } } }
    internal void FinishReturn(long now)
    {
        lock(_gate)
        {
            Respawn(now); if(_hp<=0 || !_evading) return;
            _hp=_maxHp; _evading=false; _target=0; _nextRetaliation=0; _engagedAt=0;
            _life=Guid.NewGuid().ToString("N");
        }
    }

    public int Health(long now)
    {
        lock (_gate) { Respawn(now); return _hp; }
    }
    public MonsterHealth? Hit(int damage, long now, ushort attacker = 0, string? expectedLife = null)
    {
        lock (_gate)
        {
            Respawn(now);
            if (_evading || _hp <= 0 || damage <= 0 || expectedLife is not null && expectedLife != _life) return null;
            if (attacker != 0)
            {
                // Hits renew aggro, not the attack timer. Repeated R hits must
                // never indefinitely postpone the monster's pending retaliation.
                if (_target == 0 || now-_engagedAt > 30000 || _nextRetaliation == 0) _nextRetaliation = now+1000;
                _target = attacker; _engagedAt = now;
            }
            var applied = Math.Min(_hp, damage);
            _hp -= applied;
            if (_hp == 0) { _diedAt = now; _respawnAt = now + Math.Max(6, respawnSeconds) * 1000L; }
            return new(_hp, applied, _hp == 0, _hp == 0 ? _life : null);
        }
    }
}

public readonly record struct MeleeRequest(byte Type, ushort Target);
public static class MeleeRules
{
    // CUser::Attack: type, untrusted result, target, untrusted delay and distance.
    public static bool TryRead(Packet packet, out MeleeRequest request)
    {
        request = default;
        if (packet.RemainingToRead != 8) return false;
        var type = packet.ReadByte(); packet.ReadByte();
        var target = packet.ReadUInt16(); packet.ReadInt16(); packet.ReadInt16();
        if (type is not (1 or 2)) return false;
        request = new(type, target); return true;
    }
    public static int ImpactDelayMs(int cadenceMs) => Math.Clamp(cadenceMs/3,250,600);
    public static int DelayMs(int weaponDelay) => Math.Clamp(weaponDelay + 10, 100, 500) * 10;
    public static float Range(int weaponRange) => 15f + Math.Clamp(weaponRange, 0, 100);
    // Initial deterministic PvE formula, not the full C++ damage/critical/buff port.
    public static int Damage(int hit, int armor) => (int)Math.Clamp((long)Math.Max(1, hit) * 200 / (200L + Math.Max(0, armor)), 1, ushort.MaxValue);
    public static Packet Reply(MeleeRequest request, ushort attacker, byte result) =>
        new Packet(WizOpcode.Attack).WriteByte(request.Type).WriteByte(result).WriteUInt16(attacker).WriteUInt16(request.Target);
}
