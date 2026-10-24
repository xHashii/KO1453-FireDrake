using System.Collections.Concurrent;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Network;

/// <summary>Immutable presence snapshots: routing never acquires another session's
/// game-state lock. Delivery callbacks must only enqueue outgoing packets.</summary>
public sealed record ChatMember(ushort Id, string Name, byte Nation, ushort Zone, float X, float Z,
    bool BlockPrivate, Action<Packet> Deliver);

public sealed class ChatHub
{
    private readonly ConcurrentDictionary<ushort, ChatMember> _members = new();
    public void Publish(ChatMember member) => _members[member.Id] = member;
    public void Remove(ushort id) => _members.TryRemove(id, out _);
    public ChatMember? Find(string name)
    {
        var matches = _members.Values.Where(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public int Local(ushort senderId, string message)
    {
        if (!_members.TryGetValue(senderId, out var sender)) return 0;
        var sent = 0;
        foreach (var recipient in _members.Values)
        {
            var dx = recipient.X - sender.X;
            var dz = recipient.Z - sender.Z;
            // CGameServerDlg::Send_FilterUnitRegion uses distance < 32.
            if (recipient.Zone != sender.Zone || dx * dx + dz * dz >= 32 * 32) continue;
            recipient.Deliver(ChatPackets.Message(1, sender.Nation, sender.Id, sender.Name, message));
            sent++;
        }
        return sent;
    }

    public bool Whisper(ushort senderId, ushort targetId, out string targetName, string message)
    {
        targetName = "";
        if (senderId == targetId || !_members.TryGetValue(senderId, out var sender) ||
            !_members.TryGetValue(targetId, out var target) || target.BlockPrivate) return false;
        targetName = target.Name;
        target.Deliver(ChatPackets.Message(2, sender.Nation, sender.Id, sender.Name, message));
        return true;
    }
}
