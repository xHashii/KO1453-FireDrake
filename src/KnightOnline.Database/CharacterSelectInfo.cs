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

    /// <summary>HEAD, BREAST, SHOULDER, RIGHTHAND, LEFTHAND, LEG, GLOVE, FOOT.</summary>
    public const int VisibleEquipmentCount = 8;

    /// <summary>Every account has exactly three character slots in 1453.</summary>
    public const int SlotCount = 3;
}

/// <summary>
/// One equipped item as the character-select screen shows it: id and
/// durability only - the count field of the inventory blob is not sent.
/// </summary>
public sealed record EquippedItem(uint ItemId, ushort Durability);

/// <summary>
/// The slice of USERDATA the game-server session needs once a character is
/// selected (port of the LOAD_USER_DATA result set, trimmed to what the
/// rewrite consumes so far: identity, stats, spawn point, skill/item/quest
/// blobs and the manner/monthly-loyalty counters WIZ_MYINFO carries).
/// PX/PZ/PY are stored world * 100 (DBAgent divides by 100 for the float
/// world position); the wire sends world * 10, i.e. stored / 10.
/// </summary>
public sealed record UserData(
    byte Nation,
    byte Race,
    ushort Class,
    byte Hair,
    byte Rank,
    byte Title,
    byte Level,
    int Exp,
    int Loyalty,
    byte Face,
    byte City,
    short Knights,
    byte Fame,
    short Hp,
    short Mp,
    short Sp,
    byte Str,
    byte Sta,
    byte Dex,
    byte Int,
    byte Cha,
    byte Authority,
    short Points,
    int Gold,
    ushort Zone,
    int Px,
    int Pz,
    int Py,
    byte[] Skill,
    byte[] Items,
    int MannerPoint,
    int LoyaltyMonthly,
    short QuestCount,
    byte[] Quests,
    byte[]? Serials = null);

/// <summary>
/// The COEFFICIENT row slice SetMaxHp/SetMaxMp and SetUserAbility need for a
/// class: the HP/MP/SP factors plus the weapon hit coefficients and the
/// AC/hitrate/evasionrate factors.
/// </summary>
public sealed record ClassCoefficient(
    float Hp,
    float Mp,
    float Sp,
    float ShortSword,
    float Sword,
    float Axe,
    float Club,
    float Spear,
    float Bow,
    float Staff,
    float Ac,
    float Hitrate,
    float Evasionrate);
