using KnightOnline.Database;
using KnightOnline.Shared.Protocol;
internal sealed partial class GameSession
{
    private void SkillBar(Packet packet)
    {
        if (_user is null || _inventoryFaulted || !SkillBarPackets.TryRead(packet,out var action,out var values)) return;
        try
        {
            if(action==2) Send(SkillBarPackets.Loaded(_characters.LoadSkillBarAsync(_charId).GetAwaiter().GetResult()));
            else _characters.SaveSkillBarAsync(_charId,values).GetAwaiter().GetResult();
        }
        catch(Exception ex) { InventoryUnavailable(ex); }
    }
}
