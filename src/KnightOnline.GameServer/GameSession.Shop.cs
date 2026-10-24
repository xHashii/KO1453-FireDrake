using System.Security.Cryptography;
using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Database.World;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private ushort _shopNpcId;
    private bool _inventoryFaulted;

    private bool ShopNpcAvailable(WorldNpc? npc, int group) => npc is not null &&
        ShopRules.CanUseNpc(npc.Prototype.Type, npc.Prototype.SellingGroup, group,
            PlayerX - npc.X, PlayerZ - npc.Z,
            npc.InteractionNation(_user!.Zone, _user.Nation) is 0 ||
            npc.InteractionNation(_user.Zone, _user.Nation) == _user.Nation);

    private void ItemTrade(Packet packet)
    {
        if (!CanTravel() || !ShopRules.TryRead(packet, out var request))
        {
            Send(ShopRules.Failure(1));
            return;
        }
        var npcId = request.Type == 3 ? _shopNpcId : request.NpcId;
        var npc = CurrentWorld?.Npcs.FirstOrDefault(n => n.Id == npcId);
        var group = request.Type == 3 ? npc?.Prototype.SellingGroup ?? 0 : request.Group;
        if (!ShopNpcAvailable(npc, group))
        {
            KoLog.Info($"[ SHOP REJECT ] {_charId} NPC={npcId} group={group}: unavailable, hostile or out of range");
            Send(ShopRules.Failure(1));
            return;
        }
        try
        {
            var item = request.Type == 3 ? null : _characters.LoadShopItemAsync(request.ItemId).GetAwaiter().GetResult();
            // Do not trust the signed legacy weight accumulator. Sum in 64 bits,
            // and refuse to transact an inventory containing unknown prototypes.
            var protos = _characters.LoadItemPrototypesAsync(_slots.Select(s => s.ItemId).Append(request.ItemId)).GetAwaiter().GetResult();
            var (weight, issues) = ShopRules.MeasureInventory(_slots, protos);
            if (issues.Count != 0)
            {
                LogInventoryIssues(issues, $"SHOP REJECT type={request.Type} requestedItem={request.ItemId} NPC={npcId}");
                Send(ShopRules.Failure(2));
                var first = issues[0];
                TravelMessage($"Shop rejected: {InventorySlots.Describe(first.Slot)}, item {first.ItemId}: {first.Reason}. No items or coins changed; see run log.");
                return;
            }
            var serial = (ulong)RandomNumberGenerator.GetInt32(1, int.MaxValue) << 32 |
                         (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
            var change = ShopRules.Propose(request, item, _slots, _user!.Gold, weight, _maxWeight, serial, out var error);
            if (change is null)
            {
                Send(ShopRules.Failure(error));
                KoLog.Info($"[ SHOP REJECT ] {_charId} type={request.Type} item={request.ItemId} slot={request.Position} count={request.Count} error={error} gold={_user.Gold} weight={weight}/{_maxWeight} itemDefinition={(item is null ? "missing" : "present")}");
                return;
            }
            var existing = _slots[InventorySlots.InventoryStart + request.Position];
            // Also forbid adding purchases to a sealed/rented stack.
            var protectedItem = request.Type == 2 || (request.Type == 1 && existing.ItemId != 0) ? request.ItemId : 0;
            if (!CommitInventory(change.Slots, change.Gold, protectedItem, existing.Serial)) return;
            _shopNpcId = npcId;
            _protos = protos;
            _itemWeight = (int)Math.Clamp(weight + (request.Type == 3 ? 0 : (long)item!.Weight * request.Count * (request.Type == 1 ? 1 : -1)), 0, int.MaxValue);
            SendItemWeight();
            Send(ShopRules.Success(change, request.Type));
            KoLog.Info($"[ SHOP ] {_charId} type={request.Type} NPC={npcId} item={request.ItemId} slot={request.Position} count={request.Count} price={change.Price} gold={change.Gold}");
        }
        catch (Exception ex)
        {
            InventoryUnavailable(ex);
        }
    }

    private void LogInventoryIssues(IReadOnlyList<InventoryIssue> issues, string context)
    {
        foreach (var issue in issues)
            KoLog.Warn($"[ INVENTORY INVALID ] {_charId} context={context} slot={issue.Slot} ({InventorySlots.Describe(issue.Slot)}) item={issue.ItemId} count={issue.Count} reason={issue.Reason}; saved inventory unchanged");
    }

    private bool CommitInventory(ItemSlotData[] slots, int gold, uint protectedItem = 0, ulong protectedSerial = 0, bool shopReply = true)
    {
        var (items, serials, _) = ItemBlobs.Serialize(slots);
        var expected = new InventorySnapshot(_user!.Items, _user.Serials ?? Array.Empty<byte>(), _user.Gold);
        var proposed = new InventorySnapshot(items, serials, gold);
        if (!_characters.TrySaveInventoryAsync(_charId, expected, proposed, protectedItem, protectedSerial).GetAwaiter().GetResult())
        {
            KoLog.Warn($"[ INVENTORY REJECT ] {_charId}: stale state or protected item; reconnect required, no proposal applied");
            if (shopReply) Send(ShopRules.Failure(1)); else SendItemMove(0);
            TravelMessage("Inventory changed or the item is protected. Reconnect to reload saved inventory.");
            _inventoryFaulted = true;
            Disconnect();
            return false;
        }
        _slots = slots;
        _user = _user with { Items = items, Serials = serials, Gold = gold };
        return true;
    }

    private void InventoryUnavailable(Exception ex)
    {
        // The database may have committed before the connection failed. Never
        // retry or keep using the old snapshot; reconnect is the reconciliation.
        KoLog.Error($"[ INVENTORY ] {_charId}: database outcome unavailable; disconnecting to reload saved state", ex);
        _inventoryFaulted = true;
        Disconnect();
    }
}
