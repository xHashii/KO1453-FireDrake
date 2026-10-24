namespace KnightOnline.Database.Items;

/// <summary>The shipped 600-byte BEGINNER_ITEM.strItem blobs contain non-item
/// residue after the 42 ordinary equipment/inventory slots. The original
/// DBAgent::LoadUserData skips unknown prototypes. Recognize ONLY these exact
/// source tuples, with no serial/expiration, without deleting or rewriting them.
/// A real ITEM definition always takes precedence in the caller.</summary>
public static class LegacyStarterTail
{
    private static readonly HashSet<(int Slot, uint Id, short Durability, short Count)> Known = new()
    {
        (44, 65537u, -8687, 5044),
        (44, 79536u, -30463, 2763),
        (44, 84536u, 225, 2231),
        (44, 89161u, -3645, 3295),
        (45, 65537u, 19781, 5197),
        (45, 78536u, 13263, 2597),
        (45, 83536u, -11277, 2597),
        (45, 89161u, -14653, 5195),
        (46, 65537u, -26223, 4890),
        (46, 65537u, 2776, 5784),
        (46, 79536u, -12272, 2597),
        (46, 79536u, 14289, 2597),
        (47, 65537u, 0, 0),
        (47, 79536u, 0, 0),
        (55, 79536u, -30463, 2763),
        (55, 84536u, 225, 2231),
        (55, 89161u, -3645, 3295),
        (56, 78536u, 13263, 2597),
        (56, 83536u, -11277, 2597),
        (56, 89161u, 0, 0),
        (57, 79536u, -12272, 2597),
        (57, 79536u, 14289, 2597),
        (58, 65537u, 0, 0),
        (58, 79536u, 0, 0),
        (66, 79536u, -30463, 2763),
        (66, 89161u, -3645, 3295),
        (67, 78536u, 13263, 2597),
        (67, 89161u, 0, 0),
        (68, 79536u, -12272, 2597),
        (69, 65537u, 0, 0),
        (69, 79536u, 0, 0),
    };

    public static bool IsKnownUnusedSlot(int index, ItemSlotData slot) =>
        slot.Serial == 0 && slot.ExpirationUnixTime == 0 &&
        Known.Contains((index, slot.ItemId, slot.Durability, slot.Count));
}
