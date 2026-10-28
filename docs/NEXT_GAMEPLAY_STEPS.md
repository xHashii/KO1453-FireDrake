# Current gameplay roadmap

Continue this C# source with the current client and expanded Moradon; no new
version rewrite. Detailed backlog: IMPLEMENTATION_STATUS_AND_ROADMAP.md. This
short ordering supersedes its older immediate-next-step recommendation.

## Now: make the world playable

1. **Accept the expanded world on the native client.** Town, NPCs/gates/anvil,
   newly placed monster fields, attack/kill/XP/loot/respawn, reconnect. Existing
   placements outside Moradon stay authored and receive bounds/terrain checks.
   Resolve observed visual/model/height issues before declaring the map finished.
2. **Next implementation: shared basic monster AI.** One authoritative simulation
   across sessions: idle/aggro target, attack cadence, chase, distance leash and
   return-to-spawn. Add native movement updates and two-client consistency tests.
   Plan movement validation/collision explicitly; height sampling is not navigation
   or stair collision. Avoid per-viewer AI or claiming full pathfinding prematurely.
3. **Combat and party rewards.** Build out targeting and damage/heal/effect rules,
   timed buffs/debuffs, resistances and class skills. Then party XP and loot rules
   on shared kill/reward primitives. Membership/rosters/chat already exist; shared
   rewards and party skills do not. Keep each skill family independently testable.
4. **Progression and economy.** Wire supplied quest/Lua flows into validated
   objectives/rewards and persistence. Finish repair/equipment/upgrade gaps and
   native player trading, with transactional ownership/replay safeguards.
5. **Social and PvP.** Clans, permissions/grades, friends, complete party features,
   nation/PvP rules and rewards. Test with at least two accounts.
6. **Events and release hardening.** Wars, temple/Bifrost/siege only after their
   combat/party/clan dependencies. Add scheduling/restart safety, load/soak tests,
   backup/restore drills and operational limits before a public launch.

## What already exists, without claiming retail completeness

Login/characters/world entry, movement/nearby actors, NPC interaction, shop and
warehouse foundations, equipment, chat/player visibility, basic party membership,
melee/potions/selected heal/speed skills, solo kill XP/loot, death/respawn, travel,
and saved positions. Several systems are intentionally partial. Monster placement
is now implemented; monsters still lack full moving AI. Native acceptance remains
separate from automated tests, and the stair collision issue is not solved here.

Every slice: inspect source/packet evidence, implement narrow rules and lifecycle,
run packet and DB/concurrency tests, then supply a short native acceptance test.
Never reset characters/items to make a new feature work. No calendar estimates
or claim of full retail behavior without evidence.
