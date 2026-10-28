using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Network;

public sealed record PlayerPresence(ushort Id, ushort Zone, float X, float Z, byte[] Info,
    int AppearanceVersion, Action<Packet> Deliver, bool Hidden = false, int MaxHp = 0, int Hp = 0);

/// <summary>Serializes visibility transitions and packet enqueueing, never session locks.
/// Info arrays are owned immutable snapshots. Callbacks must only enqueue packets.</summary>
public sealed class PlayerHub
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort,PlayerPresence> _players = new();
    private readonly Dictionary<ushort,HashSet<ushort>> _visible = new();
    private static bool Near(PlayerPresence a, PlayerPresence b) => a.Id != b.Id && a.Zone == b.Zone &&
        (a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z) <= 120*120;
    // Owner's C++ reference: CUser::GetInOut writes uint16 action; both
    // RequestUserIn and region details write uint8(0) before each socket ID.
    // Do not substitute another client's envelopes without native evidence.
    private static Packet In(PlayerPresence p) => new Packet(WizOpcode.UserInout).WriteUInt16(1).WriteUInt16(p.Id).WriteBytes(p.Info);
    private static Packet Out(ushort id) => new Packet(WizOpcode.UserInout).WriteUInt16(2).WriteUInt16(id);
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

    /// <summary>Bounded event-driven diagnostic snapshot. No callbacks or hidden-state changes.</summary>
    public string DescribeVisibility(ushort id)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(id, out var viewer)) return $"viewer={id} published=false";
            var peers = _players.Values.Where(p => p.Id != id).OrderBy(p => p.Id).ToArray();
            var details = peers.Take(16).Select(peer =>
            {
                var reason = peer.Zone != viewer.Zone ? "different-zone" :
                    !Near(viewer, peer) ? "out-of-range" : peer.Hidden ? "hidden-peer" : "eligible";
                return FormattableString.Invariant($"peer={peer.Id},zone={peer.Zone},pos={peer.X:F2}/{peer.Z:F2},hidden={peer.Hidden},distance={MathF.Sqrt((viewer.X-peer.X)*(viewer.X-peer.X)+(viewer.Z-peer.Z)*(viewer.Z-peer.Z)):F2},reason={reason},viewerKnowsPeer={_visible[id].Contains(peer.Id)},peerKnowsViewer={_visible[peer.Id].Contains(id)}");
            });
            return FormattableString.Invariant($"viewer={id} published=true zone={viewer.Zone} pos={viewer.X:F2}/{viewer.Z:F2} hidden={viewer.Hidden} infoBytes={viewer.Info.Length} visible={_visible[id].Count} candidates={peers.Length} [{string.Join("; ", details)}] truncated={peers.Length > 16}");
        }
    }

    /// <summary>Read peer HP without acquiring another session's game-state lock.
    /// Uses the same directional visibility and current range policy as actors.</summary>
    public Packet? TargetHealth(ushort viewerId, ushort targetId, byte echo)
    {
        lock (_gate)
        {
            if (echo > 1 || !_players.TryGetValue(viewerId, out var viewer) ||
                !_players.TryGetValue(targetId, out var target) || target.Hidden ||
                !Near(viewer, target) || !_visible[viewerId].Contains(targetId) ||
                target.MaxHp <= 0 || target.Hp <= 0) return null;
            return TargetHealthPackets.Reply(target.Id, echo, target.MaxHp, target.Hp);
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
                foreach(var member in batch) result.WriteByte(0).WriteUInt16(member.Id).WriteBytes(member.Info);
                replies.Add(result);
            }
            if(replies.Count==0) replies.Add(new Packet(WizOpcode.ReqUserin).WriteUInt16(0));
            return replies;
        }
    }
}
