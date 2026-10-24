using KnightOnline.Database;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private int _travelStage;
    private readonly Dictionary<ushort, TravelDestination> _offeredTravel = new();

    private void BasicStateChange(Packet packet)
    {
        if (!_inGame || _currentHp <= 0 || packet.RemainingToRead != 3) return;
        var type = packet.ReadByte();
        var value = packet.ReadUInt16();
        // Only public standing/sitting and emotion states; never GM/invisibility flags.
        if ((type == 1 && value is 1 or 2)
            || (type == 4 && value is 1 or 2 or 3 or 11 or 12 or 13 or 14 or 15))
        {
            if (type == 1) _sitting = value == 2;
            Send(new Packet(WizOpcode.StateChange).WriteUInt16(_socketId).WriteByte(type).WriteUInt32(value));
        }
    }

    private void TravelMessage(string message)
    {
        Send(ChatPackets.Message(1, _user!.Nation, ushort.MaxValue, "Server", message));
    }

    private void TravelChat(Packet packet)
    {
        if (!_inGame || _travelStage != 0 || !ChatPackets.TryRead(packet, out var type, out var message))
            return;
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (type is not (1 or 2) || words.Length == 0 || (words[0][0] != '/' && words[0][0] != '+'))
        {
            PlayerChat(type, message);
            return;
        }
        var gmCommand = words[0][0] == '+';
        if (gmCommand && _user!.Authority != 0)
        {
            TravelMessage("GM commands require Authority 0. Reconnect after promotion.");
            return;
        }
        KoLog.Info($"[ COMMAND ] {_charId}: {message}");
        switch ("/" + words[0][1..].ToLowerInvariant())
        {
            case "/testvitals":
                if (_user!.Authority != 0) { TravelMessage("Test vitals requires GM authority."); break; }
                if (words.Length != 3 || !int.TryParse(words[1], out var testHp) || !int.TryParse(words[2], out var testMp) ||
                    testHp < 1 || testHp > _maxHp || testMp < 0 || testMp > _maxMp)
                { TravelMessage("Usage: +testvitals <HP 1..max> <MP 0..max>"); break; }
                _sitting = false;
                RestoreVitals(testHp - _currentHp, testMp - _currentMp);
                TravelMessage("Test vitals set. Use your HP/MP potions now; normal regeneration still applies.");
                break;
            case "/heal":
                if (_user!.Authority == 0) RestoreVitals(_maxHp, _maxMp);
                else TravelMessage("Healing command requires GM authority.");
                break;
            case "/town":
                Town();
                break;
            case "/help":
                TravelMessage("Travel: /town, /teleport, /zone <id/name>, /moradon, /home, /eslant. GMs: +zonechange <id>, +moradon, +heal.");
                break;
            case "/moradon":
                TravelTo("moradon");
                break;
            case "/home":
                TravelTo("home");
                break;
            case "/eslant":
                TravelTo("eslant");
                break;
            case "/teleport":
            case "/warp":
            case "/zonechange":
            case "/zone":
                if (words.Length == 2) TravelTo(words[1]);
                else ShowTravelList();
                break;
            default:
                TravelMessage("That command is not implemented. Use /help for available travel commands.");
                break;
        }
    }

    private bool CanTravel() => _inGame && _travelStage == 0 && _currentHp > 0;

    private void Town()
    {
        if (!CanTravel()) return;
        if (_currentHp < _maxHp / 2)
        {
            TravelMessage("You need at least half your HP to use /town.");
            return;
        }
        TravelSafely(() =>
        {
            var target = _characters.LoadTravelDestinationsAsync(_user!.Nation, _user.Level, _user.Zone)
                .GetAwaiter().GetResult().FirstOrDefault();
            if (target is null) TravelMessage("No town position is configured for this zone.");
            else BeginTravel(target);
        });
    }

    private void TravelTo(string destination)
    {
        if (!CanTravel()) return;
        var id = TravelRules.Resolve(destination, _user!.Nation);
        if (id is null || !TravelRules.CanEnter(id.Value, _user.Nation, _user.Level))
        {
            TravelMessage("Destination unavailable for your nation/level. Use /teleport for routes.");
            return;
        }
        TravelSafely(() =>
        {
            var target = _characters.LoadTravelDestinationsAsync(_user.Nation, _user.Level)
                .GetAwaiter().GetResult().FirstOrDefault(zone => zone.Zone == id);
            if (target is null) TravelMessage("Destination missing from this server's ZONE_INFO.");
            else BeginTravel(target);
        });
    }

    private void ShowTravelList()
    {
        if (!CanTravel()) return;
        TravelSafely(() =>
        {
            ClearGate();
            _offeredTravel.Clear();
            var destinations = _characters.LoadTravelDestinationsAsync(_user!.Nation, _user.Level).GetAwaiter().GetResult();
            // Prototype travel menu: synthetic warp IDs are zone IDs, never arbitrary client coordinates.
            var reply = new Packet(WizOpcode.WarpList, (byte)1);
            reply.WriteUInt16((ushort)destinations.Count);
            foreach (var target in destinations)
            {
                _offeredTravel[target.Zone] = target;
                reply.WriteUInt16(target.Zone).WriteString(target.Name).WriteString("Prototype travel")
                    .WriteUInt16(target.Zone).WriteUInt16(0).WriteUInt32(0);
                TravelMessage($"/zone {target.Zone} - {target.Name}");
            }
            Send(reply);
        });
    }

    private void SelectTravel(Packet packet)
    {
        if (!CanTravel() || packet.RemainingToRead != 2) return;
        var id = packet.ReadUInt16();
        if (SelectGateWarp(id)) return;
        // Do not mistake native map warp IDs for our synthetic menu IDs.
        if (!_offeredTravel.TryGetValue(id, out var target)
            || !TravelRules.CanEnter(target.Zone, _user!.Nation, _user.Level)) return;
        TravelSafely(() => BeginTravel(target));
    }

    private void BeginTravel(TravelDestination target, int fee = 0)
    {
        // Persist before changing session state or notifying the client. A DB failure
        // must not leave client/session and the saved zone disagreeing.
        if (!_world.Zones.TryGetValue(target.Zone, out var destination) || !destination.Map.Contains(target.Px / 100f, target.Pz / 100f))
            throw new InvalidOperationException("Destination is outside the loaded world.");
        target = target with { Py = (int)(destination.Map.Height(target.Px / 100f, target.Pz / 100f) * 100) };
        var gold = _characters.SaveTravelPositionAsync(_charId, target, fee).GetAwaiter().GetResult();
        _savedPosition = new(target.Zone,target.Px,target.Pz,target.Py);
        _pendingMelee = null; _casting = null;
        var sameZone = target.Zone == _user!.Zone;
        _user = _user with { Zone = target.Zone, Px = target.Px, Pz = target.Pz, Py = target.Py, Gold = gold };
        _offeredTravel.Clear();
        ClearGate();
        _sitting = false;
        if (fee > 0) Send(new Packet(WizOpcode.GoldChange, (byte)2).WriteUInt32((uint)fee).WriteUInt32((uint)gold));
        if (sameZone)
        {
            Send(TravelPackets.Warp(target.Px, target.Pz));
            SendWorldNpcs();
        }
        else
        {
            _travelStage = 1;
            Send(TravelPackets.ChangeZone(target.Zone, target.Px, target.Pz, target.Py));
        }
        KoLog.Info($"[ TRAVEL ] {_charId} zone={target.Zone} position={target.Px},{target.Pz},{target.Py}");
    }

    private void FinishTravel(Packet packet)
    {
        if (!_inGame || packet.RemainingToRead != 1) return;
        var phase = packet.ReadByte();
        if (phase == 1 && _travelStage == 1)
        {
            // Player/merchant regions remain empty; NPC records come from the static world.
            Send(new Packet(WizOpcode.Regionchange).WriteUInt16(0));
            SendWorldNpcs();
            Send(new Packet(WizOpcode.MerchantInout, (byte)1).WriteUInt16(0));
            Send(new Packet(WizOpcode.ZoneChange, (byte)2));
            _travelStage = 2;
        }
        else if (phase == 2 && _travelStage == 2)
        {
            _travelStage = 0;
            SendTime();
            SendWeather();
            KoLog.Info($"[ TRAVEL ] {_charId} loaded zone {_user!.Zone}");
        }
    }

    private void TravelSafely(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            KoLog.Error($"Travel failed for {_charId}.", ex);
            TravelMessage("Travel failed at the database. Your previous position has been kept if the save failed.");
        }
    }
}
