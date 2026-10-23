using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.World;

public sealed record NpcPrototype(ushort Id, string Name, ushort Model, ushort Size, uint Weapon1,
    uint Weapon2, byte Nation, byte Type, int SellingGroup, byte Level);
public sealed record NpcSpawn(ushort Zone, ushort Prototype, int Left, int Top, int Right, int Bottom, int Count, byte Direction);
public sealed record WorldObject(ushort Zone, ushort Index, byte Type, ushort Control, byte Nation,
    byte Status, float X, float Y, float Z);
public sealed record WorldNpc(ushort Id, NpcPrototype Prototype, float X, float Y, float Z,
    byte Direction, byte ObjectType = 0, byte GateOpen = 0)
{
    // CNpc::GetNpcInfo, with NPC ID prefixed by the caller. Names use byte lengths.
    public void WriteInfo(Packet packet)
    {
        packet.SByte();
        packet.WriteUInt16(Prototype.Id).WriteUInt16(Prototype.Model).WriteByte(Prototype.Type)
            .WriteInt32(Prototype.SellingGroup).WriteUInt16(Prototype.Size)
            .WriteUInt32(Prototype.Weapon1).WriteUInt32(Prototype.Weapon2).WriteString(Prototype.Name)
            .WriteByte(Prototype.Nation).WriteByte(Prototype.Level)
            .WriteUInt16((ushort)(X * 10)).WriteUInt16((ushort)(Z * 10)).WriteUInt16(unchecked((ushort)(short)(Y * 10)))
            .WriteUInt32(GateOpen).WriteByte(ObjectType).WriteUInt16(0).WriteUInt16(0).WriteByte(Direction);
    }
}
public sealed record ZoneDefinition(ushort Id, string MapName);
public sealed record WorldData(IReadOnlyList<ZoneDefinition> Zones, IReadOnlyDictionary<ushort, NpcPrototype> Prototypes,
    IReadOnlyList<NpcSpawn> Spawns, IReadOnlyList<WorldObject> Objects);
public sealed record WorldZone(ushort Id, SmdMap Map, IReadOnlyList<WorldNpc> Npcs, IReadOnlyList<WorldObject> Objects);
