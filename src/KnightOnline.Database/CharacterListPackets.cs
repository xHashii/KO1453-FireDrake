using KnightOnline.Shared.Protocol;

namespace KnightOnline.Database;

/// <summary>1453's fixed three-slot character list. Preview suppression is wire-only;
/// no saved equipment is changed. Used to recover from client equipment-asset failures.</summary>
public static class CharacterListPackets
{
    public static Packet Build(IReadOnlyList<CharacterSelectInfo> characters, bool showEquipment)
    {
        if (characters.Count > CharacterSelectInfo.SlotCount)
            throw new ArgumentException("1453 accepts exactly three character slots.", nameof(characters));
        var result = new Packet(WizOpcode.AllcharInfoReq).WriteByte(1);
        for (var i = 0; i < CharacterSelectInfo.SlotCount; i++)
        {
            var slot = i < characters.Count ? characters[i] : CharacterSelectInfo.Empty;
            result.WriteString(slot.CharacterId).WriteByte(slot.Race).WriteUInt16(slot.Class)
                .WriteByte(slot.Level).WriteByte(slot.Face).WriteByte(slot.Hair).WriteByte(slot.Zone);
            for (var j = 0; j < CharacterSelectInfo.VisibleEquipmentCount; j++)
            {
                var item = showEquipment && slot.CharacterId.Length != 0 && j < slot.VisibleEquipment.Count
                    ? slot.VisibleEquipment[j] : null;
                result.WriteUInt32(item?.ItemId ?? 0).WriteUInt16(item?.Durability ?? 0);
            }
        }
        return result;
    }
}
