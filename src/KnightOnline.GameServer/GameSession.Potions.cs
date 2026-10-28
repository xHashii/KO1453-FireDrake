using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private void UsePotion(MagicRequest request, PotionDefinition definition)
    {
        void Reject(string reason)
        {
            Send(PotionRules.Magic(request, _socketId, false));
            KoLog.Info($"[ POTION REJECT ] {_charId} skill={request.Skill}: {reason}");
        }
        if (!CanTravel() || request.Caster != _socketId || (request.Target != _socketId && request.Target != ushort.MaxValue) || request.Stage is not (1 or 3))
        {
            Reject("invalid state, caster, target or stage");
            return;
        }
        try
        {
            var change = PotionRules.Propose(definition, _slots, _currentHp, _currentMp, _maxHp, _maxMp, _user!.Level);
            if (change is null) { Reject("no usable inventory stack, level restriction, dead, or resource already full"); return; }
            if (request.Stage == 1)
            {
                Send(PotionRules.Magic(request, _socketId, true, casting: true));
                return;
            }
            var source = _slots[change.Index];
            var blobs = ItemBlobs.Serialize(change.Slots);
            var expected = new InventorySnapshot(_user.Items, _user.Serials ?? Array.Empty<byte>(), _user.Gold);
            var proposed = new InventorySnapshot(blobs.Items, blobs.Serials, _user.Gold);
            var result = _characters.TryUsePotionAsync(_charId, expected, proposed, definition.Item, source.Serial,
                new PotionVitals(_user.Hp, _user.Mp, change.Hp, change.Mp, definition.Resource, definition.CooldownMs)).GetAwaiter().GetResult();
            if (result == InventorySaveResult.Cooldown) { Reject("potion cooldown"); return; }
            if (result != InventorySaveResult.Saved)
            {
                Reject("stale inventory/vitals or protected item; reconnect required");
                _inventoryFaulted = true;
                Disconnect();
                return;
            }
            _slots = change.Slots;
            _user = _user with { Items = blobs.Items, Serials = blobs.Serials, Hp = (short)change.Hp, Mp = (short)change.Mp };
            RestoreVitals(change.Hp - _currentHp, change.Mp - _currentMp);
            _itemWeight = Math.Max(0, _itemWeight - definition.Weight);
            Send(PotionRules.Stack(definition.Item, (byte)(change.Index - InventorySlots.InventoryStart), _slots[change.Index]));
            SendItemWeight();
            Send(PotionRules.Magic(request, _socketId, true));
            KoLog.Info($"[ POTION ] {_charId} item={definition.Item} skill={request.Skill} remaining={_slots[change.Index].Count} HP={_currentHp}/{_maxHp} MP={_currentMp}/{_maxMp}");
        }
        catch (Exception ex) { InventoryUnavailable(ex); }
    }

    private void TargetHp(Packet packet)
    {
        if (!_inGame || packet.RemainingToRead != 3) return;
        var id = packet.ReadUInt16();
        var echo = packet.ReadByte();
        if (echo > 1) return;
        if (id == _socketId)
        {
            Send(TargetHealthPackets.Reply(id, echo, _maxHp, _currentHp));
            return;
        }
        if (id < 10000)
        {
            var reply = _players.TargetHealth(_socketId, id, echo);
            if (reply is not null) Send(reply);
            return;
        }
        var npc = CurrentWorld?.Npcs.FirstOrDefault(n => n.Id == id);
        if (npc is null || !_visibleNpcs.Contains(id) || !Near(npc.X, npc.Z, 120)) return;
        Send(TargetHealthPackets.Reply(id, echo, npc.Prototype.MaxHp, npc.Health));
    }
}
