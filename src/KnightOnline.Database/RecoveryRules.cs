namespace KnightOnline.Database;

/// <summary>Normal recovery from CUser::HPTimeChange. The original checks elapsed seconds > 5.</summary>
public static class RecoveryRules
{
    public const int IntervalSeconds = 6;

    public static (int Hp, int Mp) Amounts(byte level, ushort characterClass, bool sitting,
        bool gm, int maxHp, int maxMp, int currentMp)
    {
        if (sitting && gm) return (maxHp, maxMp);
        var percent = characterClass is 110 or 210 && currentMp < 30 * maxMp / 100 ? 120 : 100;
        var hp = sitting ? (int)(level * (1 + level / 30.0)) + 3 : 0;
        var mp = sitting
            ? (maxMp * 5 / (level + 29) + 3) * percent / 100
            : (int)((level * (1 + level / 60.0) + 1) * 0.2 + 3) * percent / 100;
        return (hp, mp);
    }
}
