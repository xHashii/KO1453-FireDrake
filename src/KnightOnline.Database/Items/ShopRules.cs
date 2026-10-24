using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database.Items;

public sealed record ShopItem(uint Id, int BuyPrice, int SellType, byte SellingGroup,
    byte Race, byte Countable, short Weight, short Duration);

public sealed record ShopRequest(byte Type, int Group, ushort NpcId, uint ItemId, byte Position, ushort Count, byte Destination = 0);
public sealed record ShopChange(ItemSlotData[] Slots, int Gold, uint Price, byte SellingGroup);

/// <summary>NPCHandler.cpp ItemTrade: ordinary NPC shops, with overflow/overwrite fixes.
/// No king/siege taxes, premium discounts, player merchants or repair processing.</summary>
public static class ShopRules
{
    public const int CoinMax = 2_100_000_000;

    public static bool TryRead(Packet packet, out ShopRequest request)
    {
        request = new(0, 0, 0, 0, 0, 0);
        if (packet.RemainingToRead == 0) return false;
        var type = packet.ReadByte();
        if (type is 1 or 2 && packet.RemainingToRead == 13)
        {
            request = new(type, packet.ReadInt32(), packet.ReadUInt16(), packet.ReadUInt32(), packet.ReadByte(), packet.ReadUInt16());
            return true;
        }
        if (type == 3 && packet.RemainingToRead == 6)
        {
            request = new(type, 0, 0, packet.ReadUInt32(), packet.ReadByte(), 0, packet.ReadByte());
            return true;
        }
        return false;
    }

    public static bool CanUseNpc(byte type, int actualGroup, int requestedGroup, float dx, float dz, bool friendly) =>
        type is 21 or 22 && actualGroup > 0 && actualGroup == requestedGroup && friendly && dx * dx + dz * dz <= 121;

    public static bool Sells(int npcGroup, byte itemGroup) => itemGroup != 0 &&
        npcGroup - itemGroup * 1000 is 0 or 1 or 2 or 72;

    /// <summary>Pure proposal: never changes the supplied slots. Caller must validate NPC,
    /// protection records, and atomically persist the proposal before publishing it.</summary>
    public static ShopChange? Propose(ShopRequest request, ShopItem? item, IReadOnlyList<ItemSlotData> slots,
        int gold, long currentWeight, int maxWeight, ulong newSerial, out byte error)
    {
        error = 2;
        if (slots.Count < InventorySlots.InventoryStart + InventorySlots.InventoryCount ||
            request.Position >= InventorySlots.InventoryCount || gold < 0 || gold > CoinMax) return null;
        var index = InventorySlots.InventoryStart + request.Position;
        var source = slots[index];
        if (request.Type == 3)
        {
            error = 4;
            if (request.Destination >= InventorySlots.InventoryCount || request.ItemId == 0 ||
                source.ItemId != request.ItemId || source.Count <= 0) return null;
            var moved = slots.ToArray();
            var destination = InventorySlots.InventoryStart + request.Destination;
            (moved[index], moved[destination]) = (moved[destination], moved[index]);
            return new(moved, gold, 0, 0);
        }
        if (request.Type is not (1 or 2) || item is null || item.Id != request.ItemId ||
            item.Id == 0 || item.BuyPrice < 0 || item.Weight < 0 || item.Duration < 0 ||
            request.Count is 0 or > ItemBlobs.MaxCountableCount ||
            (item.Countable == 0 && request.Count != 1)) return null;
        var count = request.Count;
        long price;
        ItemSlotData replacement;
        if (source.Flag != 0) return null;
        if (request.Type == 1)
        {
            error = 3;
            if (!Sells(request.Group, item.SellingGroup)) return null;
            price = (long)item.BuyPrice * count;
            if (price > gold) return null;
            error = 4;
            if (currentWeight < 0 || currentWeight + (long)item.Weight * count > maxWeight) return null;
            error = 2;
            if (source.ItemId != 0 && (source.ItemId != item.Id || item.Countable == 0 || source.Count <= 0 ||
                source.Count + count > ItemBlobs.MaxCountableCount || source.ExpirationUnixTime != 0 ||
                source.Durability != item.Duration)) return null;
            replacement = source.ItemId == 0
                ? new(item.Id, item.Duration, (short)count, newSerial, 0)
                : source with { Count = (short)(source.Count + count) };
            gold -= (int)price;
        }
        else
        {
            error = 2;
            if (item.Id >= 900000001 || item.Race == 20 || source.ItemId != item.Id ||
                source.Count < count || source.ExpirationUnixTime != 0) return null;
            price = (long)(item.SellType == 1 ? item.BuyPrice : item.BuyPrice / 6) * count;
            error = 3;
            if (gold + price > CoinMax) return null;
            replacement = source.Count == count ? ItemSlotData.Empty : source with { Count = (short)(source.Count - count) };
            gold += (int)price;
        }
        var proposed = slots.ToArray();
        proposed[index] = replacement;
        error = 0;
        return new(proposed, gold, (uint)price, item.SellingGroup);
    }

    /// <summary>Read-only audit; unknown weights must not silently become zero.
    /// Bag container slots add capacity rather than carried weight, matching SetUserAbility.</summary>
    public static (long Weight, IReadOnlyList<InventoryIssue> Issues) MeasureInventory(
        IReadOnlyList<ItemSlotData> slots, IReadOnlyDictionary<uint, ItemProto> protos)
    {
        long weight = 0;
        var issues = new List<InventoryIssue>();
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.ItemId == 0 || i is 47 or 48) continue;
            if (!protos.ContainsKey(slot.ItemId) && LegacyStarterTail.IsKnownUnusedSlot(i, slot)) continue;
            if (slot.Count <= 0) issues.Add(new(i, slot.ItemId, slot.Count, "non-positive saved count"));
            else if (!protos.TryGetValue(slot.ItemId, out var proto)) issues.Add(new(i, slot.ItemId, slot.Count, "missing ITEM definition"));
            else if (proto.Weight < 0) issues.Add(new(i, slot.ItemId, slot.Count, "negative ITEM.Weight"));
            else weight += (long)proto.Weight * slot.Count;
        }
        return (weight, issues);
    }

    public static Packet Failure(byte error) => new Packet(WizOpcode.ItemTrade).WriteByte(0).WriteByte(error);
    public static Packet Success(ShopChange change, byte type) => type == 3
        ? new Packet(WizOpcode.ItemTrade).WriteByte(3)
        : new Packet(WizOpcode.ItemTrade).WriteByte(1).WriteUInt32((uint)change.Gold).WriteUInt32(change.Price).WriteByte(change.SellingGroup);
}

public sealed record InventoryIssue(int Slot, uint ItemId, short Count, string Reason);
