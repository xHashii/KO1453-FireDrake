using KnightOnline.Database;
using KnightOnline.Database.World;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private readonly WorldCatalog _world;
    private readonly HashSet<ushort> _visibleNpcs = new();
    private readonly Dictionary<ushort, MapWarp> _gateWarps = new();
    private WorldObject? _activeGate;
    private WorldZone? CurrentWorld => _user is not null && _world.Zones.TryGetValue(_user.Zone, out var zone) ? zone : null;
    private float PlayerX => _user!.Px / 100f;
    private float PlayerZ => _user!.Pz / 100f;
    private bool Near(float x, float z, float radius) => (PlayerX - x) * (PlayerX - x) + (PlayerZ - z) * (PlayerZ - z) <= radius * radius;

    private IEnumerable<WorldNpc> NearbyNpcs() => CurrentWorld?.Npcs.Where(n => Near(n.X, n.Z, 120)) ?? Enumerable.Empty<WorldNpc>();

    private void SendWorldNpcs()
    {
        foreach (var id in _visibleNpcs)
            Send(new Packet(WizOpcode.NpcInout, (byte)2).WriteUInt16(id));
        _visibleNpcs.Clear();
        var nearby = NearbyNpcs().ToArray();
        // Full NPC records are batched well below the 16-bit frame limit.
        foreach (var batch in nearby.Chunk(30))
        {
            var reply = new Packet(WizOpcode.ReqNpcin).WriteUInt16((ushort)batch.Length);
            foreach (var npc in batch)
            {
                reply.WriteUInt16(npc.Id);
                npc.WriteInfo(reply, _user!.Zone, _user.Nation);
                _visibleNpcs.Add(npc.Id);
            }
            Send(reply);
        }
        if (nearby.Length == 0) Send(new Packet(WizOpcode.ReqNpcin).WriteUInt16(0));
        KoLog.Info($"[ WORLD ] {_charId} zone={_user!.Zone} visibleNPCs={nearby.Length}");
    }

    private void WorldMove(Packet packet)
    {
        if (!_inGame || packet.Length != 9 || CurrentWorld is not { } zone) return;
        var wireX = packet.ReadUInt16();
        var wireZ = packet.ReadUInt16();
        var wireY = packet.ReadInt16();
        var x = wireX / 10f;
        var z = wireZ / 10f;
        var speed = packet.ReadInt16();
        packet.ReadByte();
        if (!zone.Map.Contains(x, z)) return;
        if (speed != 0) _sitting = false;
        _user = _user! with { Px = wireX * 10, Pz = wireZ * 10, Py = wireY * 10 };
        var nearby = NearbyNpcs().ToDictionary(n => n.Id);
        foreach (var id in _visibleNpcs.Where(id => !nearby.ContainsKey(id)).ToArray())
        {
            Send(new Packet(WizOpcode.NpcInout, (byte)2).WriteUInt16(id));
            _visibleNpcs.Remove(id);
        }
        foreach (var npc in nearby.Values.Where(n => !_visibleNpcs.Contains(n.Id)))
        {
            var reply = new Packet(WizOpcode.NpcInout, (byte)1).WriteUInt16(npc.Id);
            npc.WriteInfo(reply, _user!.Zone, _user.Nation);
            Send(reply);
            _visibleNpcs.Add(npc.Id);
        }
        if (_activeGate is not null && !Near(_activeGate.X, _activeGate.Z, 10)) ClearGate();
    }

    private void WorldNpcRequest(Packet packet)
    {
        if (_user is null || packet.RemainingToRead < 2) return;
        var count = packet.ReadUInt16();
        if (count > 1000 || packet.RemainingToRead != count * 2) return;
        var available = NearbyNpcs().ToDictionary(n => n.Id);
        var requested = new HashSet<ushort>();
        for (var i = 0; i < count; i++) requested.Add(packet.ReadUInt16());
        foreach (var batch in requested.Where(available.ContainsKey).Select(id => available[id]).Chunk(30))
        {
            var reply = new Packet(WizOpcode.ReqNpcin).WriteUInt16((ushort)batch.Length);
            foreach (var npc in batch) { reply.WriteUInt16(npc.Id); npc.WriteInfo(reply, _user!.Zone, _user.Nation); }
            Send(reply);
        }
    }

    private void WorldNpcEvent(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead < 2) return;
        var id = packet.ReadUInt16();
        var npc = CurrentWorld?.Npcs.FirstOrDefault(n => n.Id == id);
        if (npc is null)
        {
            KoLog.Warn($"[ NPC REJECT ] {_charId}: id={id} not found in zone={_user!.Zone}");
            TravelMessage("That NPC is not in your current zone. Reconnect to refresh NPC IDs.");
            return;
        }
        if (!Near(npc.X, npc.Z, 11))
        {
            KoLog.Warn($"[ NPC REJECT ] {_charId}: id={id} outside interaction range; player={PlayerX:F1},{PlayerZ:F1} npc={npc.X:F1},{npc.Z:F1}");
            TravelMessage("Move closer to the NPC and right-click again.");
            return;
        }
        if (npc.Prototype.IsMonster) { TravelMessage("Monster combat is the next milestone; this spawn is currently stationary."); return; }
        KoLog.Info($"[ NPC ] {_charId} opened {npc.Prototype.Name} id={id} type={npc.Prototype.Type} shop={npc.Prototype.SellingGroup}");
        switch (npc.Prototype.Type)
        {
            case 21:
                _shopNpcId = id;
                Send(new Packet(WizOpcode.TradeNpc).WriteInt32(npc.Prototype.SellingGroup));
                break;
            case 22:
                _shopNpcId = id;
                Send(new Packet(WizOpcode.RepairNpc).WriteInt32(npc.Prototype.SellingGroup));
                break;
            case 24:
                Send(new Packet(WizOpcode.ItemUpgrade, (byte)1).WriteUInt16(id));
                break;
            default:
                TravelMessage($"{npc.Prototype.Name}: this NPC's quest/service is not implemented yet.");
                break;
        }
    }

    private void WorldObjectEvent(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead != 4 || CurrentWorld is not { } zone) return;
        var index = packet.ReadUInt16();
        var id = packet.ReadUInt16();
        var obj = zone.Objects.FirstOrDefault(o => o.Index == index);
        if (obj is null || !Near(obj.X, obj.Z, 10) || (obj.Nation != 0 && obj.Nation != _user!.Nation && obj.Type != 8))
        {
            Send(new Packet(WizOpcode.ObjectEvent).WriteByte(obj?.Type ?? 0).WriteByte(0));
            return;
        }
        if (obj.Type == 5)
        {
            ClearGate();
            _offeredTravel.Clear();
            _activeGate = obj;
            foreach (var warp in zone.Map.Warps.Where(w => w.Id / 10 == obj.Control
                && (w.Nation == 0 || w.Nation == _user!.Nation)
                && TravelRules.CanEnter(w.Zone, _user!.Nation, _user.Level)
                && _world.Zones.TryGetValue(w.Zone, out var destination) && destination.Map.Contains(w.X, w.Z)))
                _gateWarps[warp.Id] = warp;
            var reply = new Packet(WizOpcode.WarpList, (byte)1).WriteUInt16((ushort)_gateWarps.Count);
            foreach (var warp in _gateWarps.Values.OrderBy(w => w.Id))
                reply.WriteUInt16(warp.Id).WriteString(warp.Name).WriteString(warp.Announcement)
                    .WriteUInt16(warp.Zone).WriteUInt16(0).WriteUInt32(warp.Fee);
            Send(reply);
            KoLog.Info($"[ GATE ] {_charId} zone={zone.Id} object={index} group={obj.Control} routes={_gateWarps.Count}");
        }
        else if (obj.Type == 8)
        {
            // The client-supplied NPC ID must actually identify this zone's anvil proxy.
            var anvil = zone.Npcs.FirstOrDefault(n => n.Prototype.Id == obj.Index && n.ObjectType == 1);
            if (anvil is not null && (id == anvil.Id || id == obj.Index))
                Send(new Packet(WizOpcode.ItemUpgrade, (byte)1).WriteUInt16(anvil.Id));
            else Send(new Packet(WizOpcode.ObjectEvent, (byte)8).WriteByte(0));
        }
        else
        {
            Send(new Packet(WizOpcode.ObjectEvent).WriteByte(obj.Type).WriteByte(0));
            TravelMessage("This map object's service is not implemented yet.");
        }
    }

    private bool SelectGateWarp(ushort id)
    {
        if (_activeGate is null) return false;
        var gate = _activeGate;
        if (!_gateWarps.TryGetValue(id, out var warp) || gate.Zone != _user!.Zone || !Near(gate.X, gate.Z, 10)
            || !TravelRules.CanEnter(warp.Zone, _user.Nation, _user.Level)) return true;
        if (warp.Fee > int.MaxValue || _user.Gold < warp.Fee)
        {
            TravelMessage("You do not have enough coins for this gate.");
            return true;
        }
        var map = _world.Zones[warp.Zone].Map;
        var target = new TravelDestination(warp.Zone, warp.Name, (int)(warp.X * 100), (int)(warp.Z * 100), (int)(map.Height(warp.X, warp.Z) * 100));
        TravelSafely(() => BeginTravel(target, (int)warp.Fee));
        return true;
    }

    private void ClearGate() { _activeGate = null; _gateWarps.Clear(); }

    private void RejectWorldUpgrade(Packet packet)
    {
        if (!_inGame || packet.RemainingToRead < 1) return;
        var sub = packet.ReadByte();
        if ((sub is 2 or 3 or 7) && packet.RemainingToRead == 53)
        {
            packet.ReadByte(); packet.ReadUInt16();
            var reply = new Packet(WizOpcode.ItemUpgrade, sub).WriteByte(4); // UpgradeNoMatch, never burn items
            for (var i = 0; i < 10; i++) reply.WriteUInt32(packet.ReadUInt32()).WriteByte(packet.ReadByte());
            Send(reply);
        }
        TravelMessage("Anvil browsing is available. Upgrade/exchange processing is not implemented; no items changed.");
    }
}
