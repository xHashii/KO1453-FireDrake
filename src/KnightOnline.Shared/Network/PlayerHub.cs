using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Network;

public sealed record PlayerPresence(ushort Id, ushort Zone, float X, float Z, byte[] Info,
    int AppearanceVersion, Action<Packet> Deliver, bool Hidden = false);

/// <summary>Serializes visibility transitions and packet enqueueing, never session locks.
/// Info arrays are owned immutable snapshots. Callbacks must only enqueue packets.</summary>
public sealed class PlayerHub
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort,PlayerPresence> _players = new();
    private readonly Dictionary<ushort,HashSet<ushort>> _visible = new();
    private static bool Near(PlayerPresence a, PlayerPresence b) => a.Id != b.Id && a.Zone == b.Zone &&
        (a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z) <= 120*120;
    private static Packet In(PlayerPresence p) => new Packet(WizOpcode.UserInout).WriteByte(1).WriteUInt16(p.Id).WriteBytes(p.Info);
    private static Packet Out(ushort id) => new Packet(WizOpcode.UserInout).WriteByte(2).WriteUInt16(id);
    public void Publish(PlayerPresence player)
    {
        lock (_gate)
        {
            var changed = _players.TryGetValue(player.Id,out var old) && old.AppearanceVersion != player.AppearanceVersion;
            _players[player.Id] = player;
            if (!_visible.ContainsKey(player.Id)) _visible[player.Id] = [];
            var own = _visible[player.Id];
            foreach (var other in _players.Values)
            {
                if (other.Id == player.Id) continue;
                var theirs = _visible[other.Id];
                // Visibility is directional: a hidden GM still sees other
                // players, but no viewer receives that GM's actor or updates.
                if (Near(player,other) && !other.Hidden)
                {
                    if (own.Add(other.Id)) player.Deliver(In(other));
                }
                else if (own.Remove(other.Id)) player.Deliver(Out(other.Id));
                if (Near(player,other) && !player.Hidden)
                {
                    if (theirs.Add(player.Id)) other.Deliver(In(player));
                    else if (changed) { other.Deliver(Out(player.Id)); other.Deliver(In(player)); }
                }
                else if (theirs.Remove(player.Id)) other.Deliver(Out(player.Id));
            }
        }
    }
    /// <summary>Reannounce current actors after client loading or an explicit show command.
    /// A prior queued IN is not proof that the native client retained the actor.
    /// Never change hidden state or send hidden actors to another viewer.</summary>
    public void Refresh(ushort id)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(id, out var player)) return;
            foreach (var other in _players.Values)
            {
                if (other.Id == id) continue;
                if (_visible[id].Contains(other.Id) && !other.Hidden)
                {
                    player.Deliver(Out(other.Id));
                    player.Deliver(In(other));
                }
                if (_visible[other.Id].Contains(id) && !player.Hidden)
                {
                    other.Deliver(Out(id));
                    other.Deliver(In(player));
                }
            }
            // Native discovery is also retriggered; do not depend solely on unsolicited IN.
            player.Deliver(RegionList(id));
        }
    }

    public void Remove(ushort id)
    {
        lock (_gate)
        {
            _players.Remove(id); _visible.Remove(id);
            foreach (var viewer in _players.Values)
                if (_visible[viewer.Id].Remove(id)) viewer.Deliver(Out(id));
        }
    }
    public void Broadcast(ushort sender, Packet packet, bool includeSelf = false)
    {
        lock (_gate)
        {
            if (!_players.ContainsKey(sender)) return;
            foreach (var viewer in _players.Values)
                if ((includeSelf && viewer.Id == sender) || _visible[viewer.Id].Contains(sender)) viewer.Deliver(packet);
        }
    }
    public Packet RegionList(ushort viewer)
    {
        lock (_gate)
        {
            var ids=_visible.TryGetValue(viewer,out var seen)
                ? seen.Append(viewer).Order().ToArray() : [];
            var result=new Packet(WizOpcode.Regionchange).WriteUInt16((ushort)ids.Length);
            foreach(var id in ids) result.WriteUInt16(id);
            return result;
        }
    }
    public IReadOnlyList<Packet> Requested(ushort viewer, IEnumerable<ushort> ids)
    {
        lock (_gate)
        {
            var members = _visible.TryGetValue(viewer,out var seen)
                ? ids.Distinct().Take(1000).Where(id=>seen.Contains(id)&&_players.ContainsKey(id)).Select(id=>_players[id]).ToArray()
                : [];
            var replies=new List<Packet>();
            foreach(var batch in members.Chunk(30))
            {
                var result=new Packet(WizOpcode.ReqUserin).WriteUInt16((ushort)batch.Length);
                foreach(var member in batch) result.WriteUInt16(member.Id).WriteBytes(member.Info);
                replies.Add(result);
            }
            if(replies.Count==0) replies.Add(new Packet(WizOpcode.ReqUserin).WriteUInt16(0));
            return replies;
        }
    }
}
