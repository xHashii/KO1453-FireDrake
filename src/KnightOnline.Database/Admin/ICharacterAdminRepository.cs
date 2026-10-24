namespace KnightOnline.Database.Admin;

/// <summary>
/// GM-panel data access over the single configured game database
/// (accounts, characters, items, warehouse, and who is online).
/// Implementations must enforce the panel's safety rules:
///  - never save item blobs for an online character,
///  - optimistic concurrency (data must not have changed since load),
///  - every save writes an audit row + a restorable backup first.
/// </summary>
public interface ICharacterAdminRepository
{
    Task<IReadOnlyList<DropSummary>> SearchDropsAsync(string term, int offset = 0, CancellationToken cancellationToken = default);
    Task<DropDefinition?> GetDropAsync(short id, CancellationToken cancellationToken = default);
    Task<string> SaveDropAsync(DropDefinition drop, string admin, CancellationToken cancellationToken = default);

    Task<AdminStats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterSummary>> SearchCharactersAsync(string term, int limit = 100, CancellationToken cancellationToken = default, int offset = 0);

    Task<CharacterDetails?> GetCharacterAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Saves inventory + warehouse (+ gold/level/etc.) for an offline character.</summary>
    Task<string> SaveCharacterAsync(CharacterDetails character, string adminUser, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemSearchResult>> SearchItemsAsync(string term, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);

    Task<ItemSearchResult?> GetItemAsync(uint num, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<uint, string>> GetItemNamesAsync(IEnumerable<uint> nums, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives an item by GM letter via the MAIL_BOX_SEND procedure.
    /// Returns an error message (empty = success). Fails gracefully until the
    /// procedure has been ported from T-SQL.
    /// </summary>
    Task<string> GiveItemByLetterAsync(string sender, string recipient, string subject, string message,
        uint itemId, short count, short durability, ulong serial, int coins, CancellationToken cancellationToken = default);
}
