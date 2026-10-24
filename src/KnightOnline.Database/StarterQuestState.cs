using System.Buffers.Binary;

namespace KnightOnline.Database;

/// <summary>Compatibility policy while the starter tutorial is not ported.
/// Level-boosted characters must not be offered the level-one intro on each login.
/// Only quest 500 in its initial state is advanced; other quest progress is preserved.
/// </summary>
public static class StarterQuestState
{
    public const ushort QuestId = 500;

    public static (byte[] Blob, short Count, bool Changed) SkipForBoostedCharacter(
        byte level, byte[] blob, short count)
    {
        if (level <= 1)
            return (blob, count, false);

        var entries = Math.Clamp((int)count, 0, blob.Length / 3);
        for (var i = 0; i < entries; i++)
        {
            var offset = i * 3;
            if (BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(offset, 2)) != QuestId)
                continue;
            if (blob[offset + 2] != 1)
                return (blob, count, false);
            var updated = (byte[])blob.Clone();
            updated[offset + 2] = 2;
            return (updated, count, true);
        }

        if (entries == short.MaxValue)
            throw new InvalidOperationException("No room for the starter quest flag.");
        var result = new byte[Math.Max(blob.Length, (entries + 1) * 3)];
        blob.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(entries * 3, 2), QuestId);
        result[entries * 3 + 2] = 2;
        return (result, (short)(entries + 1), true);
    }
}
