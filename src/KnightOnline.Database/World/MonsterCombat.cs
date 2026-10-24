using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.World;

public sealed record MonsterHealth(int Hp, int Damage, bool Killed);

/// <summary>One shared runtime life per spawn, guarded independently of session locks.</summary>
public sealed class MonsterCombat(int maxHp, int respawnSeconds)
{
    private readonly object _gate = new();
    private readonly int _maxHp = Math.Max(1, maxHp);
    private int _hp = Math.Max(1, maxHp);
    private long _respawnAt = long.MaxValue;

    private void Respawn(long now)
    {
        if (_hp == 0 && now >= _respawnAt) { _hp = _maxHp; _respawnAt = long.MaxValue; }
    }
    public int Health(long now)
    {
        lock (_gate) { Respawn(now); return _hp; }
    }
    public MonsterHealth? Hit(int damage, long now)
    {
        lock (_gate)
        {
            Respawn(now);
            if (_hp <= 0 || damage <= 0) return null;
            var applied = Math.Min(_hp, damage);
            _hp -= applied;
            if (_hp == 0) _respawnAt = now + Math.Max(1, respawnSeconds) * 1000L;
            return new(_hp, applied, _hp == 0);
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
        if (type != 1) return false;
        request = new(type, target); return true;
    }
    public static int DelayMs(int weaponDelay) => Math.Clamp(weaponDelay + 10, 100, 500) * 10;
    public static float Range(int weaponRange) => Math.Clamp(weaponRange / 10f + 1f, 2f, 5f);
    // Initial deterministic PvE formula, not the full C++ damage/critical/buff port.
    public static int Damage(int hit, int armor) => (int)Math.Clamp((long)Math.Max(1, hit) * 200 / (200L + Math.Max(0, armor)), 1, ushort.MaxValue);
    public static Packet Reply(MeleeRequest request, ushort attacker, byte result) =>
        new Packet(WizOpcode.Attack).WriteByte(request.Type).WriteByte(result).WriteUInt16(attacker).WriteUInt16(request.Target);
}
