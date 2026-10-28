using KnightOnline.Shared.Diagnostics;

namespace KnightOnline.Database.World;

/// <summary>Immutable static NPC world shared by sessions. No monster AI/combat is implied.</summary>
public sealed class WorldCatalog
{
    public IReadOnlyDictionary<ushort, WorldZone> Zones { get; }
    public DropCatalog Drops { get; }

    public WorldCatalog(WorldData data, string mapDirectory)
    {
        Drops = data.Drops ?? new DropCatalog();
        if (data.Prototypes.Count == 0 || data.Spawns.Count == 0 || data.Objects.Count == 0)
            throw new InvalidDataException("NPC/object tables are empty. Import K_NPC, K_NPCPOS and K_OBJECTPOS before starting the world.");
        var zones = new Dictionary<ushort, WorldZone>();
        var maps = new Dictionary<string, SmdMap>(StringComparer.OrdinalIgnoreCase);
        var nextId = 10000;
        foreach (var definition in data.Zones)
        {
            if (Path.GetFileName(definition.MapName) != definition.MapName)
                throw new InvalidDataException("Map names must be filenames, not paths.");
            if (!maps.TryGetValue(definition.MapName, out var map))
            {
                map = new SmdMap(Path.Combine(mapDirectory, definition.MapName));
                maps.Add(definition.MapName, map);
            }
            var scoped = definition.Id==21 && string.Equals(definition.MapName,"moradon_expanded.smd",StringComparison.OrdinalIgnoreCase);
            var npcPrototypes = scoped && data.MoradonPrototypes is not null ? data.MoradonPrototypes : data.Prototypes;
            var monsterPrototypes = scoped && data.MoradonMonsters is not null ? data.MoradonMonsters : data.Monsters;
            var npcs = new List<WorldNpc>();
            ushort Allocate() => nextId < 20000 ? (ushort)nextId++ : throw new InvalidDataException("Static NPC ID band exhausted.");
            foreach (var spawn in data.Spawns.Where(s => s.Zone == definition.Id))
            {
                var prototypes = spawn.IsMonster ? monsterPrototypes : npcPrototypes;
                if (prototypes is null || !prototypes.TryGetValue(spawn.Prototype, out var proto))
                    throw new InvalidDataException($"Zone {definition.Id}: missing NPC prototype {spawn.Prototype}.");
                for (var i = 0; i < spawn.Count; i++)
                {
                    // Deterministic placement within the authored spawn rectangle. Static NPCs
                    // normally have identical endpoints. Preserve NumNPC without stacking groups.
                    var u = spawn.Count == 1 ? 0.5f : (i + 0.5f) / spawn.Count;
                    var v = spawn.Count == 1 ? 0.5f : ((i * 0.61803398875f + 0.5f) % 1);
                    var x = Math.Min(spawn.Left, spawn.Right) + Math.Abs(spawn.Left - spawn.Right) * u;
                    var z = Math.Min(spawn.Top, spawn.Bottom) + Math.Abs(spawn.Top - spawn.Bottom) * v;
                    if (!map.Contains(x, z)) throw new InvalidDataException($"Zone {definition.Id}: NPC {proto.Id} outside map at {x},{z}.");
                    npcs.Add(new WorldNpc(Allocate(), proto, x, map.Height(x, z), z, spawn.Direction, RespawnSeconds: spawn.RespawnSeconds));
                }
            }
            var objects = data.Objects.Where(o => o.Zone == definition.Id).ToArray();
            foreach (var obj in objects)
            {
                if (!map.Contains(obj.X, obj.Z)) throw new InvalidDataException($"Zone {definition.Id}: object {obj.Index} outside map.");
                // Warp gate controls are group IDs, not K_NPC prototypes. Their
                // geometry already exists in the client map; ObjectEvent opens the menu.
                if (obj.Type == 5) continue;
                // AIServer::AddObjectEventNpc uses the object index for gates/anvils/artifacts.
                var sid = obj.Type is 1 or 2 or 3 or 8 or 9 ? obj.Index : obj.Control;
                if (sid == 0) continue;
                if (!npcPrototypes.TryGetValue(sid, out var proto))
                {
                    KoLog.Warn($"[ WORLD ] zone={definition.Id} object={obj.Index}: no proxy NPC prototype {sid}; map object remains interactive.");
                    continue;
                }
                npcs.Add(new WorldNpc(Allocate(), proto, obj.X, obj.Y, obj.Z, 0, 1, obj.Status));
            }
            zones.Add(definition.Id, new WorldZone(definition.Id, map, npcs, objects));
            KoLog.Info($"[ WORLD ] zone={definition.Id} NPCs={npcs.Count(n => !n.Prototype.IsMonster)} monsters={npcs.Count(n => n.Prototype.IsMonster)} objects={objects.Length} warps={map.Warps.Count}");
        }
        Zones = zones;
    }

    public static string FindMapDirectory()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "data", "map");
                if (Directory.Exists(candidate)) return candidate;
            }
        throw new DirectoryNotFoundException("World maps not found. Publish with data/map or launch from the repository.");
    }
}
