namespace KnightOnline.Database;

/// <summary>
/// One character slot of the WIZ_ALLCHAR_INFO_REQ answer, in the exact field
/// order CDBAgent::LoadCharInfo writes it (GameServer/DBAgent.cpp):
/// name, race, class, level, face, hair, zone, then the eight equipped-item
/// slots the character-select screen renders. An empty <see cref="CharacterId"/>
/// is an unused slot - the original sends those zero-filled rather than
/// omitting them, and the client always reads three.
/// </summary>
public sealed record CharacterSelectInfo(
    string CharacterId,
    byte Race,
    ushort Class,
    byte Level,
    byte Face,
    byte Hair,
    byte Zone,
    IReadOnlyList<EquippedItem> VisibleEquipment)
{
    public static CharacterSelectInfo Empty { get; } =
        new(string.Empty, 0, 0, 0, 0, 0, 0, new EquippedItem[VisibleEquipmentCount]);

    /// <summary>HEAD, BREAST, SHOULDER, LEG, GLOVE, FOOT, RIGHTHAND, LEFTHAND.</summary>
    public const int VisibleEquipmentCount = 8;

    /// <summary>Every account has exactly three character slots in 1453.</summary>
    public const int SlotCount = 3;
}

/// <summary>
/// One equipped item as the character-select screen shows it: id and
/// durability only - the count field of the inventory blob is not sent.
/// </summary>
public sealed record EquippedItem(uint ItemId, ushort Durability);
