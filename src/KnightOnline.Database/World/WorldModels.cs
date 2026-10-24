using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.World;

public sealed record NpcPrototype(ushort Id, string Name, ushort Model, ushort Size, uint Weapon1,
    uint Weapon2, byte Nation, byte Type, int SellingGroup, byte Level, int MaxHp = 1, bool IsMonster = false, int DropGroup = 0, int Armor = 0, int Experience = 0, int Money = 0, int AttackDamage = 1, int AttackDelayMs = 2000, int AttackRange = 3);
public sealed record NpcSpawn(ushort Zone, ushort Prototype, int Left, int Top, int Right, int Bottom, int Count, byte Direction, bool IsMonster = false, int RespawnSeconds = 30);
public sealed record WorldObject(ushort Zone, ushort Index, byte Type, ushort Control, byte Nation,
    byte Status, float X, float Y, float Z);
public sealed record WorldNpc(ushort Id, NpcPrototype Prototype, float X, float Y, float Z,
    byte Direction, byte ObjectType = 0, byte GateOpen = 0, int RespawnSeconds = 30)
{
    public MonsterCombat Combat { get; } = new(Prototype.MaxHp, RespawnSeconds);
    public int Health => Prototype.IsMonster ? Combat.Health(Environment.TickCount64) : Prototype.MaxHp;
    /// <summary>Legacy clients can refuse right-clicks on an opposing-faction NPC
    /// before emitting WIZ_NPC_EVENT. Moradon has shared services, but its source
    /// prototypes mix Karus and El Morad groups. Only presentation of shared
    /// services is viewer-relative; guards, other zones and gate permissions
    /// retain their authored faction.</summary>
    public byte InteractionNation(ushort zone, byte viewerNation) =>
        !Prototype.IsMonster && zone == 21 && viewerNation is 1 or 2 && IsSharedService(Prototype.Type)
            ? viewerNation : Prototype.Nation;

    public static bool IsSharedService(byte type) => type is
        21 or 22 or 23 or 24 or 29 or 31 or 33 or 34 or 36 or 37 or 40 or 46 or 47 or 49 or 77 or 126 or 132 or 136 or 162;

    // CNpc::GetNpcInfo, with NPC ID prefixed by the caller. Names use byte lengths.
    public void WriteInfo(Packet packet, ushort zone = 0, byte viewerNation = 0)
    {
        packet.SByte();
        packet.WriteUInt16(Prototype.Id).WriteUInt16(Prototype.Model).WriteByte(Prototype.Type)
            .WriteInt32(Prototype.SellingGroup).WriteUInt16(Prototype.Size)
            .WriteUInt32(Prototype.Weapon1).WriteUInt32(Prototype.Weapon2).WriteString(Prototype.Name)
            .WriteByte(InteractionNation(zone, viewerNation)).WriteByte(Prototype.Level)
            .WriteUInt16((ushort)(X * 10)).WriteUInt16((ushort)(Z * 10)).WriteUInt16(unchecked((ushort)(short)(Y * 10)))
            .WriteUInt32(GateOpen).WriteByte(ObjectType).WriteUInt16(0).WriteUInt16(0).WriteByte(Direction);
    }
}
public sealed record ZoneDefinition(ushort Id, string MapName);
public sealed record WorldData(IReadOnlyList<ZoneDefinition> Zones, IReadOnlyDictionary<ushort, NpcPrototype> Prototypes,
    IReadOnlyList<NpcSpawn> Spawns, IReadOnlyList<WorldObject> Objects, IReadOnlyDictionary<ushort, NpcPrototype>? Monsters = null, DropCatalog? Drops = null);
public sealed record WorldZone(ushort Id, SmdMap Map, IReadOnlyList<WorldNpc> Npcs, IReadOnlyList<WorldObject> Objects);
