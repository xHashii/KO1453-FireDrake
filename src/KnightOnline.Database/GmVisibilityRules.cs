namespace KnightOnline.Database;

/// <summary>Session-only public visibility; never changes stored GM authority.</summary>
public static class GmVisibilityRules
{
    public static bool IsGm(byte authority) => authority is 0 or 250;
    public static bool Hidden(byte authority, bool visibleOptIn) => IsGm(authority) && !visibleOptIn;
    public static bool TrySet(byte authority, string command, int argumentCount, out bool visible)
    {
        visible = false;
        if (!IsGm(authority) || argumentCount != 1) return false;
        if (command == "visible") { visible = true; return true; }
        return command == "invisible";
    }
}
