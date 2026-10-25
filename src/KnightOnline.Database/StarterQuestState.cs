using System.Buffers.Binary;
using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database;

/// <summary>Temporary explicit compatibility policy: the introductory tutorial
/// cannot complete until the quest engine is ported. Suppress its reserved flag
/// at every level; never reset or complete any other quest.</summary>
public static class StarterQuestState
{
    public const ushort QuestId = 500;
    public const int QuestLimit = 200;
    public static SortedDictionary<ushort, byte> Read(byte[] blob, short count)
    {
        if (count < 0 || count > QuestLimit || count > blob.Length / 3)
            throw new InvalidDataException("Invalid quest record count; saved progress was not rewritten.");
        var records = new SortedDictionary<ushort, byte>();
        for (var i = 0; i < count; i++)
            if (!records.TryAdd(BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(i * 3)), blob[i * 3 + 2]))
                throw new InvalidDataException("Duplicate quest identity; saved progress was not rewritten.");
        return records;
    }
    public static (byte[] Blob, short Count, bool Changed) SuppressUnsupportedIntro(byte[] blob, short count)
    {
        var records = Read(blob, count);
        if (records.TryGetValue(QuestId, out var state) && state == 2) return (blob, count, false);
        var index = -1;
        for (var i = 0; i < count; i++)
            if (BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(i * 3)) == QuestId) { index = i; break; }
        if (index < 0 && count == QuestLimit) throw new InvalidDataException("No free reserved intro record; saved progress was not rewritten.");
        var nextCount = index < 0 ? (short)(count + 1) : count;
        if (index < 0) index = count;
        var result = new byte[Math.Max(blob.Length, nextCount * 3)];
        blob.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(index * 3), QuestId);
        result[index * 3 + 2] = 2;
        return (result, nextCount, true);
    }
    public static Packet Snapshot(byte[] blob, short count)
    {
        var records = Read(blob, count);
        // Never silently reintroduce state 1 from an absent DB flag.
        if (!records.TryGetValue(QuestId, out var state) || state != 2)
            throw new InvalidDataException("Intro suppression must be persisted before world entry.");
        var p = new Packet(WizOpcode.Quest, 1).WriteUInt16((ushort)records.Count);
        foreach (var (id, value) in records) p.WriteUInt16(id).WriteByte(value);
        return p;
    }
}
