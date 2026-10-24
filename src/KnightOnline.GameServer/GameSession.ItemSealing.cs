using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private void ProcessItemSeal(Packet packet)
    {
        if (!ItemSealPackets.TryRead(packet, out var request)) return;
        if (!CanTravel() || !WarehouseNpcAvailable(_warehouseNpc))
        { Send(ItemSealPackets.Refused(request!.Action)); return; }
        try
        {
            var expected = new InventorySnapshot(_user!.Items, _user.Serials ?? [], _user.Gold);
            var result = _characters.SealItemAsync(_accountId, _charId, expected, request!).GetAwaiter().GetResult();
            if (result.Code == 1)
            {
                _user = _user with { Gold = result.Gold };
                _slots[14 + request!.Position] = _slots[14 + request.Position] with { Flag = request.Action == 1 ? (byte)4 : (byte)0 };
                _appearanceVersion++;
                if (request.Action == 1) Send(new Packet(WizOpcode.GoldChange).WriteByte(2).WriteUInt32(MariaDbCharacterRepository.ItemSealFee).WriteUInt32((uint)result.Gold));
            }
            Send(ItemSealPackets.Result(request!, result.Code));
            if (result.Code == 4) TravelMessage("Item security code is incorrect or not provisioned. No items or coins changed. Ask the server operator to provision your own code locally.");
            if (result.Code == 7) TravelMessage("Item security is temporarily locked after failed code attempts. Try again in five minutes.");
        }
        // Includes failures after COMMIT: no retry or success from the old snapshot.
        catch (Exception ex) { InventoryUnavailable(ex); }
    }
}
