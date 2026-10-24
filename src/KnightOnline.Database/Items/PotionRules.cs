using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.Items;

public sealed record PotionDefinition(uint Skill, uint Item, byte Resource, int Amount, int CooldownMs, byte MinLevel, byte MaxLevel, int Weight);
public sealed record PotionUse(ItemSlotData[] Slots, int Index, int Hp, int Mp);
public sealed record MagicRequest(byte Stage, uint Skill, ushort Caster, ushort Target);

public static class PotionRules
{
    public static bool TryRead(Packet packet, out MagicRequest request)
    {
        request = new(0, 0, 0, 0);
        // Observed 1453 client sends six data shorts; original C++ also accepts
        // a seventh via its soft over-read. Do not accept arbitrary truncation.
        if (packet.RemainingToRead is not (21 or 23)) return false;
        request = new(packet.ReadByte(), packet.ReadUInt32(), packet.ReadUInt16(), packet.ReadUInt16());
        return true;
    }

    public static PotionUse? Propose(PotionDefinition definition, IReadOnlyList<ItemSlotData> slots,
        int hp, int mp, int maxHp, int maxMp, byte level)
    {
        if (hp <= 0 || maxHp <= 0 || definition.Amount <= 0 || definition.Resource is not (1 or 2) ||
            level < definition.MinLevel || (definition.MaxLevel != 0 && level > definition.MaxLevel) ||
            (definition.Resource == 1 ? hp >= maxHp : mp >= maxMp)) return null;
        for (var i = InventorySlots.InventoryStart; i < Math.Min(slots.Count, InventorySlots.CospreStart); i++)
        {
            var item = slots[i];
            if (item.ItemId != definition.Item || item.Count <= 0 || item.Flag != 0 || item.ExpirationUnixTime != 0) continue;
            var proposed = slots.ToArray();
            proposed[i] = item.Count == 1 ? ItemSlotData.Empty : item with { Count = (short)(item.Count - 1) };
            return new(proposed, i, definition.Resource == 1 ? (int)Math.Min(maxHp, (long)hp + definition.Amount) : hp,
                definition.Resource == 2 ? (int)Math.Min(maxMp, (long)mp + definition.Amount) : mp);
        }
        return null;
    }

    public static Packet Magic(MagicRequest request, ushort caster, bool success, bool casting = false)
    {
        var result = new Packet(WizOpcode.MagicProcess).WriteByte(casting ? (byte)1 : success ? (byte)3 : (byte)4)
            .WriteUInt32(request.Skill).WriteUInt16(caster).WriteUInt16(caster);
        for (var i = 0; i < 7; i++) result.WriteInt16(i == 1 && success ? (short)1 : i == 3 && !success ? (short)-103 : (short)0);
        return result;
    }

    public static Packet Stack(uint itemId, byte position, ItemSlotData slot) => new Packet(WizOpcode.ItemCountChange)
        .WriteUInt16(1).WriteByte(1).WriteByte(position).WriteUInt32(itemId).WriteUInt32((uint)Math.Max(0, (int)slot.Count))
        .WriteByte(slot.Flag).WriteUInt16((ushort)slot.Durability);
}
