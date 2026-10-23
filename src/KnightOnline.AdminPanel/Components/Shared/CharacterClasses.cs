namespace KnightOnline.AdminPanel.Components.Shared;

/// <summary>Class/nation display names. IDs from GameServer/GameDefine.h.</summary>
public static class CharacterClasses
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [101] = "Warrior", [102] = "Rogue", [103] = "Magician", [104] = "Priest",
        [105] = "Berserker", [106] = "Guardian (Master)",
        [107] = "Hunter", [108] = "Penetrator (Master)",
        [109] = "Sorcerer", [110] = "Necromancer (Master)",
        [111] = "Shaman", [112] = "Dark Priest (Master)",
        [201] = "Warrior", [202] = "Rogue", [203] = "Magician", [204] = "Priest",
        [205] = "Blade", [206] = "Protector (Master)",
        [207] = "Ranger", [208] = "Assassin (Master)",
        [209] = "Mage", [210] = "Enchanter (Master)",
        [211] = "Cleric", [212] = "Druid (Master)",
    };

    /// <summary>1 warrior, 2 rogue, 3 magician, 4 priest (GROUP_*).</summary>
    public static int BaseGroup(int classId)
    {
        var last = classId % 100;
        if (last is >= 1 and <= 4) return last;
        return (last - 5) / 2 + 1; // 105/106 -> 1, 107/108 -> 2, ...
    }

    public static string ClassName(int classId)
        => Names.TryGetValue(classId, out var name) ? name : $"Class {classId}";

    public static string NationName(int nation) => nation switch
    {
        1 => "Karus",
        2 => "El Morad",
        _ => $"Nation {nation}",
    };

    public static string AuthorityName(int authority) => authority switch
    {
        0 => "Game Master",
        1 => "Player",
        11 => "Muted",
        12 => "Attack disabled",
        250 => "Limited GM",
        255 => "BANNED",
        _ => $"Authority {authority}",
    };
}
