using KnightOnline.Shared.Protocol;

namespace KnightOnline.Shared.Network;

public sealed record PartyMember(ushort Id, string Name, byte Nation, ushort Zone,
    byte Level, ushort Class, short MaxHp, short Hp, short MaxMp, short Mp,
    bool Hidden, Action<Packet> Deliver);

/// <summary>Session-only membership. All transitions serialized; callbacks ONLY enqueue
/// packets, never acquire session locks or perform database work. No XP/loot sharing.</summary>
public sealed class PartyHub
{
    private sealed record Invite(ushort Leader, long Expires);
    private readonly object _gate = new();
    private readonly Dictionary<ushort, PartyMember> _players = new();
    private readonly Dictionary<ushort, List<ushort>> _groups = new();
    private readonly Dictionary<ushort, Invite> _invites = new();
    private readonly Func<long> _clock;
    public PartyHub(Func<long>? clock = null) => _clock = clock ?? (() => Environment.TickCount64);

    public ushort[] Members(ushort id)
    { lock (_gate) return _groups.TryGetValue(id, out var group) ? group.ToArray() : []; }

    private void Send(ushort id, Packet packet)
    { if (_players.TryGetValue(id, out var player)) player.Deliver(packet); }
    private static Packet Failure(short reason = -1) => new Packet(WizOpcode.Party).WriteByte(3).WriteInt16(reason);
    public static Packet Insert(PartyMember p, byte status = 1) => new Packet(WizOpcode.Party)
        .WriteByte(3).WriteUInt16(p.Id).WriteByte(status).WriteString(p.Name)
        .WriteInt16(p.MaxHp).WriteInt16(p.Hp).WriteByte(p.Level).WriteUInt16(p.Class)
        .WriteInt16(p.MaxMp).WriteInt16(p.Mp).WriteByte(p.Nation);
    private static Packet Vitals(PartyMember p) => new Packet(WizOpcode.Party).WriteByte(6)
        .WriteUInt16(p.Id).WriteInt16(p.MaxHp).WriteInt16(p.Hp).WriteInt16(p.MaxMp).WriteInt16(p.Mp);
    private static Packet LeaderFlag(ushort id, byte value) => new Packet(WizOpcode.StateChange)
        .WriteUInt16(id).WriteByte(6).WriteUInt32(value);
    private void Broadcast(List<ushort> group, Packet packet)
    { foreach (var id in group) Send(id, packet); }

    private void CancelInvite(ushort target)
    {
        if (!_invites.Remove(target, out var invite)) return;
        Send(invite.Leader, Failure());
        Send(target, new Packet(WizOpcode.Party).WriteByte(5));
    }
    private void CancelInvites(ushort id)
    {
        foreach (var target in _invites.Where(p => p.Key == id || p.Value.Leader == id).Select(p => p.Key).ToArray())
            CancelInvite(target);
    }
    private void Expire()
    {
        var now = _clock();
        foreach (var target in _invites.Where(p => now >= p.Value.Expires).Select(p => p.Key).ToArray()) CancelInvite(target);
    }
    private static bool Eligible(PartyMember a, PartyMember b) => a.Id != b.Id && !a.Hidden && !b.Hidden
        && a.Hp > 0 && b.Hp > 0 && a.Zone == b.Zone && a.Zone != 85
        && (a.Nation == b.Nation || a.Zone is 21 or 55)
        && ((b.Level <= a.Level * 3 / 2 && b.Level >= a.Level * 2 / 3) || Math.Abs(a.Level - b.Level) <= 8);

    public void Publish(PartyMember member)
    {
        lock (_gate)
        {
            Expire();
            _players.TryGetValue(member.Id, out var old);
            _players[member.Id] = member;
            if (old is not null && (old.Zone != member.Zone || member.Hp <= 0 || member.Hidden)) CancelInvites(member.Id);
            if (member.Hidden && _groups.TryGetValue(member.Id, out var hiddenGroup)) Leave(member.Id, hiddenGroup);
            if (old is null || !_groups.TryGetValue(member.Id, out var group)) return;
            if ((old.MaxHp, old.Hp, old.MaxMp, old.Mp) != (member.MaxHp, member.Hp, member.MaxMp, member.Mp)) Broadcast(group, Vitals(member));
            if (old.Level != member.Level) Broadcast(group, new Packet(WizOpcode.Party).WriteByte(7).WriteUInt16(member.Id).WriteByte(member.Level));
            if (old.Class != member.Class) Broadcast(group, new Packet(WizOpcode.Party).WriteByte(8).WriteUInt16(member.Id).WriteUInt16(member.Class));
        }
    }
    public void Remove(ushort id)
    {
        lock (_gate)
        {
            CancelInvites(id);
            if (_groups.TryGetValue(id, out var group)) Leave(id, group);
            _players.Remove(id);
        }
    }
    private void Disband(List<ushort> group)
    {
        foreach (var id in group.ToArray()) CancelInvites(id);
        Broadcast(group, LeaderFlag(group[0], 0));
        Broadcast(group, new Packet(WizOpcode.Party).WriteByte(5));
        foreach (var id in group) _groups.Remove(id);
    }
    private void Leave(ushort id, List<ushort> group)
    {
        CancelInvites(id);
        if (group[0] == id || group.Count <= 2) { Disband(group); return; }
        Broadcast(group, new Packet(WizOpcode.Party).WriteByte(4).WriteUInt16(id));
        group.Remove(id); _groups.Remove(id);
    }

    // Strict parsing before state changes: Packet's legacy soft reads cannot
    // turn a truncated PERMIT/REMOVE into authorization.
    public bool Process(ushort sender, Packet packet)
    {
        if (packet.RemainingToRead < 1) return false;
        var action = packet.ReadByte(); string name = ""; ushort target = 0; byte permit = 0;
        if (action is 1 or 3)
        {
            if (packet.RemainingToRead < 2) return false;
            var length = packet.ReadUInt16();
            if (length is 0 or > 20 || packet.RemainingToRead != length) return false;
            packet.ReadPosition -= 2; name = packet.ReadString();
            if (name.Any(char.IsControl)) return false;
        }
        else if (action == 2)
        { if (packet.RemainingToRead != 1) return false; permit = packet.ReadByte(); if (permit > 1) return false; }
        else if (action is 4 or 28)
        { if (packet.RemainingToRead != 2) return false; target = packet.ReadUInt16(); }
        else if (action != 5 || packet.RemainingToRead != 0) return false;
        lock (_gate)
        {
            Expire();
            if (!_players.TryGetValue(sender, out var player)) return false;
            if (action is 1 or 3)
            {
                var matches = _players.Values.Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                _groups.TryGetValue(sender, out var existing);
                if (matches.Length != 1 || !Eligible(player, matches[0]) || _groups.ContainsKey(matches[0].Id)
                    || _invites.ContainsKey(sender) || _invites.ContainsKey(matches[0].Id)
                    || _invites.Values.Any(i => i.Leader == sender || i.Leader == matches[0].Id)
                    || (action == 1 ? existing is not null : existing is null || existing[0] != sender || existing.Count >= 8))
                { Send(sender, Failure()); return false; }
                _invites[matches[0].Id] = new(sender, _clock() + 30000);
                Send(matches[0].Id, new Packet(WizOpcode.Party).WriteByte(2).WriteUInt16(sender).WriteString(player.Name));
                return true;
            }
            if (action == 2)
            {
                if (!_invites.Remove(sender, out var invite)) return false;
                if (permit == 0) { Send(invite.Leader, Failure()); return true; }
                _groups.TryGetValue(invite.Leader, out var group);
                if (_groups.ContainsKey(sender) || !_players.TryGetValue(invite.Leader, out var leader)
                    || !Eligible(leader, player) || (group is not null && (group[0] != leader.Id || group.Count >= 8)))
                { Send(invite.Leader, Failure()); Send(sender, new Packet(WizOpcode.Party).WriteByte(5)); return false; }
                if (group is null) { group = [leader.Id]; _groups[leader.Id] = group; }
                foreach (var id in group) Send(sender, Insert(_players[id]));
                group.Add(sender); _groups[sender] = group;
                Broadcast(group, Insert(player));
                Broadcast(group, LeaderFlag(group[0], 1));
                return true;
            }
            if (!_groups.TryGetValue(sender, out var party)) return false;
            if (action == 4)
            {
                if (!party.Contains(target) || (sender != target && party[0] != sender)) return false;
                Leave(target, party); return true;
            }
            if (party[0] != sender) return false;
            if (action == 5) { Disband(party); return true; }
            var index = party.IndexOf(target);
            if (index <= 0) return false;
            CancelInvites(sender);
            (party[0], party[index]) = (party[index], party[0]);
            Broadcast(party, LeaderFlag(sender, 0)); Broadcast(party, LeaderFlag(target, 1));
            Broadcast(party, Insert(_players[target], 100)); return true;
        }
    }
    public bool Chat(ushort sender, string message)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(sender, out var player) || !_groups.TryGetValue(sender, out var group)) return false;
            if (message.Length is 0 or > 128 || message.Any(char.IsControl)) return false;
            Broadcast(group, ChatPackets.Message(3, player.Nation, sender, player.Name, message)); return true;
        }
    }
}
