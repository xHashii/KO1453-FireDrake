namespace KnightOnline.Shared.Protocol;

public enum WorldEntryAction { Ignore, Bootstrap, AcknowledgeOnly, Enter }
/// <summary>Native start handshake: one MYINFO snapshot, stage-1 retries only
/// acknowledge, stage 2 cannot activate a character before the snapshot.</summary>
public sealed class WorldEntryFlow
{
    private bool _snapshot, _entered;
    public WorldEntryAction Accept(byte stage)
    {
        if (stage == 1)
        {
            if (_snapshot) return WorldEntryAction.AcknowledgeOnly;
            _snapshot = true;
            return WorldEntryAction.Bootstrap;
        }
        if (stage == 2 && _snapshot && !_entered)
        {
            _entered = true;
            return WorldEntryAction.Enter;
        }
        return WorldEntryAction.Ignore;
    }
}
