using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Protocol;
using KnightOnline.Shared.Diagnostics;
internal sealed partial class GameSession
{
    private ushort _warehouseNpc;
    private WarehouseSnapshot? _warehouse;
    private bool WarehouseNpcAvailable(ushort id) => CanTravel() && CurrentWorld?.Npcs.FirstOrDefault(n => n.Id==id) is { } npc &&
        !npc.Prototype.IsMonster && npc.Health>0 && npc.Prototype.Type==31 && Near(npc.X,npc.Z,11) &&
        (npc.InteractionNation(_user!.Zone,_user.Nation)==0 || npc.InteractionNation(_user.Zone,_user.Nation)==_user.Nation);
    private void OpenWarehouse()
    {
        if (!WarehouseNpcAvailable(_warehouseNpc)) { Send(WarehouseRules.Result(1,false)); return; }
        try
        {
            _warehouse=null;
            var loaded=_characters.LoadWarehouseAsync(_accountId).GetAwaiter().GetResult();
            var fee=WarehouseRules.OpenFee(_user!.Zone);
            if(_user.Gold<fee) {Send(WarehouseRules.Result(1,false));return;}
            if(fee>0)
            {
                var expected=new InventorySnapshot(_user.Items,_user.Serials??[],_user.Gold);
                if(_characters.SaveWarehouseAsync(_charId,_accountId,expected,expected with {Gold=expected.Gold-fee},loaded,loaded,[]).GetAwaiter().GetResult()!=InventorySaveResult.Saved)
                    throw new InvalidOperationException("Stale warehouse opening fee snapshot.");
                _user=_user with {Gold=_user.Gold-fee};
                Send(new Packet(WizOpcode.GoldChange).WriteByte(2).WriteUInt32((uint)fee).WriteUInt32((uint)_user.Gold));
            }
            _warehouse=loaded;
            Send(WarehouseRules.Open(_warehouse.Gold,ItemBlobs.Parse(_warehouse.Items,_warehouse.Serials,_warehouse.Times,192)));
        }
        catch(Exception ex) { InventoryUnavailable(ex); }
    }
    private void Warehouse(Packet packet)
    {
        if(!WarehouseRules.TryRead(packet,out var r)) { Send(WarehouseRules.Result(0,false)); return; }
        if(_inventoryFaulted || !WarehouseNpcAvailable(_warehouseNpc)) { Send(WarehouseRules.Result(r.Action,false)); return; }
        if(r.Action==1) { OpenWarehouse(); return; }
        if(_warehouse is null || r.Npc!=_warehouseNpc) { Send(WarehouseRules.Result(r.Action,false)); return; }
        try
        {
            var stored=ItemBlobs.Parse(_warehouse.Items,_warehouse.Serials,_warehouse.Times,192);
            var item=r.Item==900000000?null:_characters.LoadShopItemAsync(r.Item).GetAwaiter().GetResult();
            var protos=_characters.LoadItemPrototypesAsync(_slots.Select(s=>s.ItemId).Append(r.Item)).GetAwaiter().GetResult();
            var measured=ShopRules.MeasureInventory(_slots,protos);
            if(measured.Issues.Count!=0) { Send(WarehouseRules.Result(r.Action,false)); return; }
            var change=WarehouseRules.Propose(r,item,_slots,stored,_user!.Gold,_warehouse.Gold,measured.Weight,_maxWeight);
            if(change is null) {Send(WarehouseRules.Result(r.Action,false));return;}
            var protections=new List<(uint Item,ulong Serial)>();
            for(var i=0;i<_slots.Length;i++) if(_slots[i]!=change.Inventory[i] && _slots[i].ItemId!=0) protections.Add((_slots[i].ItemId,_slots[i].Serial));
            for(var i=0;i<192;i++) if(stored[i]!=change.Warehouse[i] && stored[i].ItemId!=0) protections.Add((stored[i].ItemId,stored[i].Serial));
            var blobs=ItemBlobs.Serialize(change.Inventory);
            var bank=MariaDbCharacterRepository.WarehouseProposal(_warehouse,change.Warehouse,change.Bank);
            var expected=new InventorySnapshot(_user.Items,_user.Serials??[],_user.Gold);
            if(_characters.SaveWarehouseAsync(_charId,_accountId,expected,new(blobs.Items,blobs.Serials,change.Gold),_warehouse,bank,protections.ToArray()).GetAwaiter().GetResult()!=InventorySaveResult.Saved)
                throw new InvalidOperationException("Stale or protected warehouse transfer; reconnect required.");
            _slots=change.Inventory;_user=_user with {Items=blobs.Items,Serials=blobs.Serials,Gold=change.Gold};_warehouse=bank;_protos=protos;
            RecomputeStats(refillWhenEmpty:false);SendItemWeight();Send(WarehouseRules.Result(r.Action,true));
            KoLog.Info($"[ WAREHOUSE ] {_charId} action={r.Action} item={r.Item} count={r.Count} gold={change.Gold} bank={change.Bank}");
        }
        catch(Exception ex) {InventoryUnavailable(ex);}
    }
}
