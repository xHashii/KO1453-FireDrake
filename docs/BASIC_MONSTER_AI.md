# Basic shared monster AI — first slice

## Implemented

One GameServer-owned `MonsterSimulation` advances every 250 ms, independently
of connected viewers. Sessions publish immutable, short-lived player snapshots
and receive position revisions through their existing NPC visibility refresh.
AI never acquires a session lock, invokes session callbacks or performs SQL.

- Idle, chase, attack, return and dead states; idle currently means stationary.
- Source K_MONSTER byActType 1/2 are passive; 6 stays special/non-proactive.
  Other types proactively select the nearest valid visible player, ID as tie-break.
  Friend assistance for source types 3/4 is not implemented.
- Passive monsters engage after a valid hit. Latest eligible attacker can take
  aggro; this is not a full threat table. Hidden GMs may damage but are not targeted.
- Dead, travelling, faulted or disconnected players are removed. Snapshot TTL is
  two seconds. A per-connection GUID prevents a reused player ID inheriting aggro
  or being removed by an old disconnect callback.
- Source search range, trace range and speed2 feed movement (speed2 * 1.5 from
  AIServer's default MONSTER_SPEED=1500). Prototype safety caps: search 40 units,
  trace 8..80 units, speed 0..12 units/second. These are conservative first-slice
  limits, not a claim that every special/boss movement rule is retail-complete.
- Chase stops at attack range. Existing persisted player-damage handling consumes
  a shared target/cadence permit, so extra viewers cannot duplicate attacks.
- Losing the target, exceeding leash, 30 seconds without reaching attack range or
  landing a hit, or a two-second blocked chase starts return. Return is invulnerable
  and retraces bounded breadcrumbs rather than teleporting through geometry.
  At home: heal, clear aggro, change encounter token (reject stale queued swings).
- Zero-speed prototypes stay stationary. Stalled scheduler time is capped at 0.5s
  per update, so a hitch never creates a long catch-up teleport.
- Corpse position freezes at death, preserving loot location. Existing authored
  respawn duration restores the spawn position and a fresh life token.
- All NPC range checks, spawn/info packets and kill/loot positions now use shared
  runtime position snapshots rather than immutable spawn coordinates.

## Protocol and geometry evidence

`GameServer/Npc.cpp::MoveResult`: WIZ_NPC_MOVE 0x0B, ushort ID, ushort X,
ushort Z, signed-Y bit pattern, ushort speed; coordinates/speed are multiplied
by ten. Existing actors receive changed motion/stop packets; late viewers get
current actor info before movement. No coordinate or NPC-placement SQL migration.

SMD global triangle data follows `N3BASE/N3ShapeMgr.cpp::LoadCollisionData`.
The loader now retains a read-only spatial triangle index. Monster direct steps
check terrain slope, bounds, body-width rays and double-sided shape collision.
Line-of-sight also tests terrain and shape occlusion before aggro or player hits.
Both methods ignore untrusted client Y and use terrain instead.

**This is not pathfinding.** A monster blocked by a wall cannot route around it;
it returns instead. There is no navmesh, swept capsule, authored patrol, idle
roaming, linked packs, caster/boss AI or door-state-aware collision. Conservative
rays can reject valid narrow passages. Terrain-derived Y does not place monsters
on bridges/platforms above terrain. Player movement still trusts client Y; the
previous player stair-sinking issue is NOT fixed by these monster-only checks.

## Concurrency and lifetime

Lock order: simulation -> individual MonsterCombat. Hit paths release the combat
lock before engaging AI. Position writes are atomic immutable snapshots guarded
by the current living encounter token; a kill/respawn race cannot move another
life. The single simulation owns all paths/targets and grants a shared attack
cooldown. No SQL occurs under its lock. Shutdown cancels and awaits the loop;
unexpected loop failure stops the server instead of silently disabling AI.

## Verification / acceptance

Automated tests: native literal movement vectors, passive/proactive behavior,
step bounds, shared attack permits, multiple observers, leash/evade/heal, stale
life hits, reconnect/ID reuse, hidden GM, stale presence/zone change, corpse and
respawn, scheduler hitch, synthetic double-sided walls, blocked return, steep
terrain/edges, zero-speed prototypes, concurrent hits/presence and cancellation.
The MariaDB world tests still load real maps/placements and preserve snapshots.

Native-client acceptance is pending. With a disposable normal character (or GM
`+visible`), test in open monster fields:

1. Hit a passive monster and move away: it should chase. Approach an aggressive
   type: it should acquire you without a hit. Confirm attacks obey range/cadence.
2. With two clients, watch the same monster: motion/HP/target must agree and it
   must not speed up or deal duplicate attacks when another viewer arrives.
3. Leave its home area, Town, die or disconnect: it must drop you and return,
   heal at home and reject hits during return. Reconnect must not inherit aggro.
4. Kill while moving: corpse/loot stay at death position; authored respawn occurs
   at its original field point. Check existing XP/loot and reconnect persistence.
5. Try a wall and steep slope: no direct tunnelling or attacks through the wall;
   blocked monsters may stop/return, not intelligently route around obstacles.
6. Restart once; ensure no new duplicate spawns or character/item changes.

Next: native acceptance and fixes, then expand shared combat/skill/effect rules
and party rewards. Richer navigation and monster behavior remain visible backlog.
