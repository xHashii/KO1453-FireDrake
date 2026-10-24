using System.Security.Cryptography;
using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Database.World;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private readonly Dictionary<uint,ushort> _announcedLoot = new();
    private long _nextLootRefresh;
    private void RefreshLoot()
    {
        if (_user is null) return;
        var bundles = _characters.LoadLootAsync(_charId,_user.Zone).GetAwaiter().GetResult();
        var active = bundles.Select(b => b.Id).ToHashSet();
        foreach (var id in _announcedLoot.Keys.Where(id => !active.Contains(id)).ToArray()) _announcedLoot.Remove(id);
        foreach (var bundle in bundles)
        {
            var npc = CurrentWorld?.Npcs.FirstOrDefault(n => n.Id == bundle.Npc);
            if (!Near(bundle.X,bundle.Z,120) || !_visibleNpcs.Contains(bundle.Npc) || npc?.Combat.DeathToken != bundle.KillToken) continue;
            if (_announcedLoot.TryAdd(bundle.Id,bundle.Npc))
                Send(LootPackets.Spawn(bundle.Npc,bundle.Id));
        }
    }
    private void ForgetLootForNpc(ushort npc)
    {
        foreach (var id in _announcedLoot.Where(pair => pair.Value == npc).Select(pair => pair.Key).ToArray()) _announcedLoot.Remove(id);
    }
    private LootBundle? FindLoot(uint id) => _characters.LoadLootAsync(_charId,_user!.Zone).GetAwaiter().GetResult()
        .FirstOrDefault(b => b.Id == id && Near(b.X,b.Z,10));

    private void OpenLoot(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead != 4) return;
        var id = packet.ReadUInt32();
        try
        {
            var bundle = FindLoot(id);
            if (bundle is null) return;
            Send(LootPackets.Open(bundle.Entries.Select(e => (e.Item,e.Count)).ToArray()));
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }
    private void PickupLoot(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead != 8) return;
        var bundleId = packet.ReadUInt32(); var itemId = packet.ReadUInt32();
        void Reject(byte reason=0) => Send(new Packet(WizOpcode.ItemGet).WriteByte(reason));
        try
        {
            var bundle = FindLoot(bundleId);
            var entry = bundle?.Entries.FirstOrDefault(e => e.Item == itemId);
            if (bundle is null || entry is null) { Reject(); return; }
            var proposed = _slots.ToArray(); var gold = _user!.Gold; var position = -1;
            ItemSlotData previous = ItemSlotData.Empty;
            var protos = _protos;
            if (itemId == 900000000)
            {
                if ((long)gold+entry.Count > ShopRules.CoinMax) { Reject(6); return; }
                gold += entry.Count;
            }
            else
            {
                var item = _characters.LoadShopItemAsync(itemId).GetAwaiter().GetResult();
                if (item is null || item.Countable == 0 && entry.Count != 1 || entry.Count > ItemBlobs.MaxCountableCount) { Reject(); return; }
                protos = _characters.LoadItemPrototypesAsync(_slots.Select(s => s.ItemId).Append(itemId)).GetAwaiter().GetResult();
                var (weight,issues) = ShopRules.MeasureInventory(_slots,protos);
                if (issues.Count > 0 || weight + (long)item.Weight*entry.Count > _maxWeight || item.Weight < 0 || item.Duration < 0)
                { Reject(6); return; }
                if (item.Countable != 0)
                    position = Enumerable.Range(14,28).FirstOrDefault(i => proposed[i].ItemId == itemId && proposed[i].Count > 0 && proposed[i].ExpirationUnixTime == 0 && proposed[i].Durability == item.Duration && proposed[i].Count+entry.Count <= ItemBlobs.MaxCountableCount,-1);
                if (position < 0) position = Enumerable.Range(14,28).FirstOrDefault(i => proposed[i].ItemId == 0,-1);
                if (position < 0) { Reject(6); return; }
                previous = proposed[position];
                var serial = (ulong)RandomNumberGenerator.GetInt32(1,int.MaxValue)<<32 | (uint)RandomNumberGenerator.GetInt32(1,int.MaxValue);
                proposed[position] = previous.ItemId == 0 ? new(itemId,item.Duration,(short)entry.Count,serial,0) : previous with { Count=(short)(previous.Count+entry.Count) };
            }
            var blobs = ItemBlobs.Serialize(proposed);
            var claim = new LootClaim(bundleId,entry.Slot,itemId,entry.Count,_user.Zone,PlayerX,PlayerZ);
            var saved = _characters.TryClaimLootAsync(_charId,new(_user.Items,_user.Serials ?? [],_user.Gold),new(blobs.Items,blobs.Serials,gold),claim,
                previous.ItemId,previous.Serial).GetAwaiter().GetResult();
            if (saved != InventorySaveResult.Saved) { Reject(); RefreshLoot(); return; }
            _slots = proposed; _user = _user with { Items=blobs.Items, Serials=blobs.Serials, Gold=gold }; _protos=protos;
            if (position >= 0)
            {
                Send(PotionRules.Stack(itemId,(byte)(position-14),proposed[position]));
                _itemWeight=(int)Math.Clamp(ShopRules.MeasureInventory(_slots,_protos).Weight,0,int.MaxValue); SendItemWeight();
            }
            Send(LootPackets.Taken(itemId,position < 0 ? entry.Count : (ushort)proposed[position].Count,gold,position < 0 ? (byte)255 : (byte)(position-14)));
            KoLog.Info($"[ LOOT ] {_charId} bundle={bundleId} item={itemId} count={entry.Count} gold={gold}");
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }
}
