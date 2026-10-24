using System.Security.Cryptography;

namespace KnightOnline.Database.World;

public sealed record LootEntry(uint Item, ushort Count, byte Slot = 0);
public sealed record DropRow(int[] Items, int[] Chances);

/// <summary>GiveNpcHaveItem/ItemProdution from AIServer/Npc.cpp. Integer ranges
/// intentionally retain the source's inclusive random / exclusive interval boundaries.</summary>
public sealed class DropCatalog
{
    public Dictionary<int, DropRow> Drops { get; } = new();
    public Dictionary<int, int[]> Groups { get; } = new();
    public Dictionary<int, int[]> Grades { get; } = new();
    public Dictionary<int, int[]> Weapons { get; } = new();
    public Dictionary<int, int[]> Armor { get; } = new();
    public Dictionary<int, int[]> Rarity { get; } = new();
    private static int Random(int min, int max) => RandomNumberGenerator.GetInt32(min, max + 1);
    private static int Pick(int[] weights, int roll)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
        { var end = sum + Math.Max(0, weights[i]); if (roll >= sum && roll < end) return i + 1; sum = end; }
        return 0;
    }
    public IReadOnlyList<LootEntry> Roll(NpcPrototype npc, byte nation, Func<int,int,int>? random = null)
    {
        var next = random ?? Random;
        var result = new List<LootEntry>();
        var money = Math.Min(32000L, (long)npc.Money * next(70,100) / 100);
        if (money > 0) result.Add(new(900000000, (ushort)money));
        if (!Drops.TryGetValue(npc.DropGroup, out var row)) return result;
        for (var i = 0; i < 5; i++)
        {
            var code = row.Items[i];
            if (code <= 0 || row.Chances[i] <= 0 || next(1,10000) > row.Chances[i]) continue;
            if (i < 2)
                code = code < 100 ? Produce(code,npc.Level,nation,next)
                    : Groups.TryGetValue(code,out var items) && items.Length == 30 ? items[next(1,30)-1] : 0;
            if (code > 0) result.Add(new((uint)code, (ushort)(code is >= 391010000 and <= 391060000 ? 20 : 1)));
        }
        return result;
    }
    private int Produce(int index, int level, byte nation, Func<int,int,int> next)
    {
        var branch = next(1,10000);
        if (!Grades.TryGetValue(index,out var grades)) return 0;
        var grade = Pick(grades,next(1,1000));
        if (grade == 0) return 0;
        int Quality(int type)
        {
            if (!Rarity.TryGetValue(level/5,out var weights)) return -1;
            return Pick(weights,next(0,1000)) switch
            {
                1 => type == 1 ? next(16,24) : type == 2 ? next(12,24) : next(0,10),
                2 => type == 1 ? next(6,15) : type == 2 ? next(6,11) : next(0,10),
                3 => type == 3 ? next(0,10) : 5,
                _ => 0
            };
        }
        if (branch < 4001)
        {
            var roll = next(1,10000);
            var family = roll < 701 ? 1 : roll < 1401 ? 2 : roll < 2101 ? 3 : roll < 2801 ? 4 : roll < 3501 ? 5 : roll < 5501 ? 6 : roll < 6501 ? 7 : roll < 8501 ? 8 : 9;
            if (!Weapons.TryGetValue(level/10,out var weights)) return 0;
            var itemClass = Pick(weights,next(0,1000)); if (itemClass == 0) return 0;
            var race = next(1,10000) < 5000 ? 10000 : 50000;
            var handedness = next(1,10000) < 5000 ? 0 : 5000000;
            var quality = Quality(1); if (quality < 0) return 0;
            return 100000000 + itemClass*100000 + family*10000000 + race + handedness + quality*10 + grade;
        }
        if (branch < 8001)
        {
            if (!Armor.TryGetValue(level/10,out var weights)) return 0;
            var itemClass = Pick(weights,next(0,1000)); if (itemClass == 0) return 0;
            var roll = next(0,10000); int family, race;
            if (nation == 1)
            {
                family = roll < 2000 ? 0 : roll < 4000 ? 40000000 : roll < 6000 ? 60000000 : 80000000;
                race = roll < 2000 ? 10000 : roll < 4000 ? 20000 : roll < 6000 ? 30000 : next(0,10000) < 5000 ? 20000 : 40000;
            }
            else
            {
                family = roll < 3300 ? 0 : roll < 5600 ? 40000000 : roll < 7800 ? 60000000 : 80000000;
                var sex = next(0,10000);
                race = family == 0 ? sex < 3333 ? 110000 : sex < 6666 ? 120000 : 130000 : sex < 5000 ? 120000 : 130000;
            }
            var piece = Math.Min(4,next(0,10000)/2000) + 1;
            var quality = Quality(2); if (quality < 0) return 0;
            return 200000000 + family + itemClass*1000000 + race + piece*1000 + quality*10 + grade;
        }
        var accessory = Math.Min(3,next(0,10000)/2500) + 1;
        var accessoryRace = next(1,10000) < 5000 ? 110000 : 150000;
        var accessoryQuality = Quality(3); if (accessoryQuality < 0) return 0;
        return 300000000 + accessory*10000000 + accessoryRace + accessoryQuality*10 + grade;
    }
}
