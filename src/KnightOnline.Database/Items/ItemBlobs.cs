using System.Binary;

namespace KnightOnline.Database.Items;

/// <summary>
/// One item-stack slot as stored in USERDATA / WAREHOUSE blobs.
/// Mirrors the persisted fields of _ITEM_DATA (shared/globals.h).
/// </summary>
public sealed record ItemSlotData(
    uint ItemId,            // nNum; 0 = empty slot
    short Durability,       // sDuration
    short Count,            // sCount (stored as int16 by the original)
    ulong Serial,           // nSerialNum (parallel serial blob)
    uint ExpirationUnixTime // nExpirationTime (parallel time blob)
)
{
    public static readonly ItemSlotData Empty = new(0, 0, 0, 0, 0);
    public bool IsEmpty => ItemId == 0 || Count <= 0;
}

/// <summary>
/// Slot layout of the character inventory blob (shared/globals.h).
/// 0-13 equipped, 14-41 inventory, 42-46 cospre, 47-48 magic bags,
/// 49-60 magic bag 1, 61-72 magic bag 2  => 73 slots total.
/// </summary>
public static class InventorySlots
{
    public const int EquipmentCount = 14;      // SLOT_MAX
    public const int InventoryCount = 28;      // HAVE_MAX
    public const int CospreCount = 5;          // COSP_MAX
    public const int MagicBagCount = 2;        // MBAG_COUNT
    public const int MagicBagSize = 12;        // MBAG_MAX

    public const int InventoryStart = EquipmentCount;                                   // 14
    public const int CospreStart = InventoryStart + InventoryCount;                     // 42
    public const int MagicBagStart = CospreStart + CospreCount + MagicBagCount;         // 49
    public const int Total = MagicBagStart + MagicBagCount * MagicBagSize;              // 73

    public const int WarehouseTotal = 192;     // WAREHOUSE_MAX

    private static readonly string[] EquipmentNames =
    {
        "Right Earring", "Helmet", "Left Earring", "Necklace", "Pauldron",
        "Shoulder", "Right Hand", "Belt", "Left Hand", "Right Ring",
        "Legs", "Left Ring", "Gauntlets", "Boots",
    };

    private static readonly string[] CospreNames =
    {
        "Wings (cospre)", "Helmet (cospre)", "Glove R (cospre)", "Glove L (cospre)", "Armor (cospre)",
    };

    public static string Describe(int slot)
    {
        if (slot < EquipmentCount) return EquipmentNames[slot];
        if (slot < CospreStart) return $"Inventory {slot - InventoryStart}";
        if (slot < CospreStart + CospreCount) return CospreNames[slot - CospreStart];
        if (slot == 47) return "Magic Bag 1 (the bag itself)";
        if (slot == 48) return "Magic Bag 2 (the bag itself)";
        if (slot < MagicBagStart + MagicBagSize) return $"Magic Bag 1 slot {slot - MagicBagStart}";
        if (slot < Total) return $"Magic Bag 2 slot {slot - MagicBagStart - MagicBagSize}";
        return $"Slot {slot}";
    }
}

/// <summary>
/// Reader/writer for KO's parallel item blobs (DBAgent.cpp LoadUserData /
/// LoadWarehouseData / SaveUserData):
///
///   strItem / WarehouseData     : per slot  uint32 itemId + int16 durability + int16 count (8 bytes)
///   strSerial                   : per slot  uint64 serial                                    (8 bytes)
///   strItemTime / WarehouseDataTime : per slot uint32 expiration unix time                   (4 bytes)
///
/// PORTING NOTES / original quirks, preserved:
///  * The three blobs are parallel arrays indexed by slot; each is parsed with
///    its own length, so a short/absent blob yields empty trailing slots
///    (the C++ reads zeroes past the end via ByteBuffer overrun semantics).
///  * SaveUserData in this codebase only writes 47 slots (0-41 + cospre),
///    while LoadUserData reads up to 73. <see cref="Serialize"/> therefore
///    writes exactly slotCount slots - callers that round-trip a character
///    must keep the count they parsed (see <see cref="ParseResult.SlotCount"/>)
///    until the SAVE path is revisited during the GameServer port.
///  * The original clamps on load: unknown item ids and count &lt;= 0 are dropped,
///    non-countable items collapse to count 1, countable counts cap at 9999,
///    serial 0 gets a fresh serial. Parsing here is lossless; clamping is the
///    caller's (game logic's) job.
/// </summary>
public static class ItemBlobs
{
    public const int ItemEntrySize = 8;
    public const int SerialEntrySize = 8;
    public const int ExpirationEntrySize = 4;
    public const int MaxCountableCount = 9999; // ITEMCOUNT_MAX

    public sealed record ParseResult(ItemSlotData[] Slots, int SlotCount);

    /// <summary>Parses the three parallel blobs into <paramref name="slotCount"/> slots.</summary>
    public static ItemSlotData[] Parse(
        ReadOnlySpan<byte> itemBlob,
        ReadOnlySpan<byte> serialBlob,
        ReadOnlySpan<byte> expirationBlob,
        int slotCount)
    {
        var slots = new ItemSlotData[slotCount];

        for (var i = 0; i < slotCount; i++)
        {
            uint itemId = 0;
            short durability = 0, count = 0;
            ulong serial = 0;
            uint expiration = 0;

            var itemOffset = i * ItemEntrySize;
            if (itemOffset + ItemEntrySize <= itemBlob.Length)
            {
                itemId = BinaryPrimitives.ReadUInt32LittleEndian(itemBlob[itemOffset..]);
                durability = BinaryPrimitives.ReadInt16LittleEndian(itemBlob[(itemOffset + 4)..]);
                count = BinaryPrimitives.ReadInt16LittleEndian(itemBlob[(itemOffset + 6)..]);
            }

            var serialOffset = i * SerialEntrySize;
            if (serialOffset + SerialEntrySize <= serialBlob.Length)
                serial = BinaryPrimitives.ReadUInt64LittleEndian(serialBlob[serialOffset..]);

            var expirationOffset = i * ExpirationEntrySize;
            if (expirationOffset + ExpirationEntrySize <= expirationBlob.Length)
                expiration = BinaryPrimitives.ReadUInt32LittleEndian(expirationBlob[expirationOffset..]);

            slots[i] = new ItemSlotData(itemId, durability, count, serial, expiration);
        }

        return slots;
    }

    /// <summary>
    /// Convenience parse that derives the slot count from the item blob length
    /// (like the original's fixed-size fetch buffers) and remembers it for
    /// lossless round-tripping.
    /// </summary>
    public static ParseResult ParseExact(
        ReadOnlySpan<byte> itemBlob,
        ReadOnlySpan<byte> serialBlob,
        ReadOnlySpan<byte> expirationBlob)
    {
        var slotCount = itemBlob.Length / ItemEntrySize;
        return new ParseResult(Parse(itemBlob, serialBlob, expirationBlob, slotCount), slotCount);
    }

    /// <summary>Serializes back into the three parallel blobs.</summary>
    public static (byte[] Items, byte[] Serials, byte[] Expirations) Serialize(
        IReadOnlyList<ItemSlotData> slots)
    {
        var items = new byte[slots.Count * ItemEntrySize];
        var serials = new byte[slots.Count * SerialEntrySize];
        var expirations = new byte[slots.Count * ExpirationEntrySize];

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i] ?? ItemSlotData.Empty;

            BinaryPrimitives.WriteUInt32LittleEndian(items.AsSpan(i * ItemEntrySize), slot.ItemId);
            BinaryPrimitives.WriteInt16LittleEndian(items.AsSpan(i * ItemEntrySize + 4), slot.Durability);
            BinaryPrimitives.WriteInt16LittleEndian(items.AsSpan(i * ItemEntrySize + 6), slot.Count);
            BinaryPrimitives.WriteUInt64LittleEndian(serials.AsSpan(i * SerialEntrySize), slot.Serial);
            BinaryPrimitives.WriteUInt32LittleEndian(expirations.AsSpan(i * ExpirationEntrySize), slot.ExpirationUnixTime);
        }

        return (items, serials, expirations);
    }

    /// <summary>
    /// Applies the original's load-time clamping (DBAgent.cpp):
    /// non-countable -> 1, countable -> at most 9999. Item-table lookup
    /// (countable?) is supplied by the caller.
    /// </summary>
    public static ItemSlotData ClampForLoad(ItemSlotData slot, bool isCountable)
    {
        if (slot.IsEmpty)
            return ItemSlotData.Empty;

        var count = slot.Count;
        if (!isCountable && count > 1)
            count = 1;
        else if (count > MaxCountableCount)
            count = MaxCountableCount;

        return slot with { Count = count };
    }
}
