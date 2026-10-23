namespace KnightOnline.Database.Items;

/// <summary>
/// The ITEM-table slice the game-server session needs to make equipped items
/// matter (port of the _ITEM_TABLE fields SetSlotItemValue / SetUserAbility /
/// ItemEquipAvailable / IsValidSlotPos consume).
/// </summary>
public sealed record ItemProto(
    uint Num,
    byte Kind,        // m_bKind; Kind / 10 is the weapon group (WEAPON_*)
    byte Slot,        // m_bSlot; ItemSlotType enum value
    short Damage,     // m_sDamage
    short Weight,     // m_sWeight
    short Duration,   // m_sDuration (magic bags: added max-weight bonus)
    short Ac,         // m_sAc
    short Hitrate,    // m_sHitrate
    short Evasionrate,// m_sEvarate
    short StrB,
    short StaB,
    short DexB,
    short IntelB,
    short ChaB,
    short MaxHpB,
    short MaxMpB,
    short FireR,
    short ColdR,
    short LightningR,
    short MagicR,
    short PoisonR,    // m_bPoisonR
    short CurseR,     // disease resistance
    byte ReqLevel,
    byte ReqLevelMax,
    byte ReqRank,
    byte ReqTitle,
    byte ReqStr,
    byte ReqSta,
    byte ReqDex,
    byte ReqIntel,
    byte ReqCha,
    byte Countable)
{
    /// <summary>C++ GetItemGroup(): the weapon family (WEAPON_* in Define.h).</summary>
    public int Group => Kind / 10;

    /// <summary>C++ isBow(): bows and longbows (the launcher counts for the hit coefficient).</summary>
    public bool IsBow => Group is 7 or 8;

    /// <summary>C++ isShield(): shields live in the left-hand weapon group.</summary>
    public bool IsShield => Group == 6;
}
