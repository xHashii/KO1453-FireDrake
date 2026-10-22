namespace KnightOnline.Shared;

/// <summary>
/// Port of shared/version.h (<c>__VERSION</c>).
/// Knight Online v1453 - "Reign of the Fire Drake".
/// All version-conditional (<c>#if __VERSION</c>) code paths in the C# port
/// implement the 1453 branch only.
/// </summary>
public static class GameVersion
{
    public const int Version = 1453;
}
