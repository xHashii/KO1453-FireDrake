using KnightOnline.Database.Items;

namespace KnightOnline.Database.Admin;

public sealed record CharacterSummary(
    string Name,
    string? AccountId,
    int Nation,          // 1 = Karus, 2 = El Morad
    int ClassId,
    int Level,
    int Zone,
    int KnightsId,
    long Gold,
    bool IsOnline);

/// <summary>Everything the character editor shows for one character.</summary>
public sealed class CharacterDetails
{
    public required string Name { get; init; }
    public string? AccountId { get; internal set; }
    public int Nation { get; init; }
    public int Race { get; init; }
    public int ClassId { get; set; }
    public int Level { get; set; }
    public long Exp { get; set; }
    public long Loyalty { get; set; }
    public long Gold { get; set; }
    public int Zone { get; init; }
    public int KnightsId { get; init; }
    public int Fame { get; init; }
    public int Authority { get; set; }
    public int Hp { get; init; }
    public int Mp { get; init; }
    public int[] Stats { get; set; } = new int[5]; // STR, STA, DEX, INT, CHA
    public int StatPoints { get; set; }

    public required ItemSlotData[] Inventory { get; init; }   // 73 slots
    public required ItemSlotData[] Warehouse { get; set; }    // 192 slots
    public long BankCoins { get; set; }

    public bool IsOnline { get; set; }

    /// <summary>Columns the connected USERDATA table does not have (shown as read-only in the UI).</summary>
    public required IReadOnlySet<string> MissingColumns { get; init; }

    /// <summary>Slot count the stored inventory blob actually has (47 in this codebase's save, 73 max).</summary>
    public int StoredInventorySlots { get; init; }

    /// <summary>Optimistic-concurrency token captured at load (checksums of all item blobs).</summary>
    public required string ConcurrencyToken { get; set; }
}

public sealed record ItemSearchResult(
    uint Num,
    string Name,
    int Kind,
    int Slot,
    int ClassId,
    int Duration,
    bool Countable,
    int ItemType,
    int ReqLevel);

public sealed record AdminStats(
    long Accounts,
    long Characters,
    long Online,
    long ItemDefinitions,
    bool AccountDbOk,
    bool GameDbOk,
    string? Error);

/// <summary>Thrown when a save is rejected because the character is online or data changed meanwhile.</summary>
public sealed class AdminEditRejectedException(string message) : Exception(message);
