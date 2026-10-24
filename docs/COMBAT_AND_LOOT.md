# Solo PvE, skills and loot — October 2026

## Corrected regressions

- The first R-handler incorrectly divided ITEM.Range by ten and capped it at
  five world units. This rejected armed attacks at the client's normal stopping
  distance. Range now follows `Unit::isInAttackRange`: 15 + authored weapon range,
  bounded to 115 for pathological custom definitions. Both normal and long attack
  animation types are accepted. Timing/damage still come from the server, never
  from the client's result/delay/distance fields. The left hand is considered if
  there is no usable right-hand weapon. Range failures log coordinates and limit.
- Death no longer sends an immediate NPC OUT. Each viewer gets DEAD once while
  the corpse remains registered, so its animation and ITEM_DROP chest can render.
  Corpses remain until respawn or 30 seconds, whichever comes first. Respawn has
  a six-second minimum to avoid erasing the death animation. Returning actors get
  a fresh OUT/IN cycle even though their runtime ID is reused.

- Basic melee now sends the successful attack animation first and applies damage
  after a server windup of one-third cadence, clamped to 250–600 ms. Weak monsters
  can still die in one hit, but no longer receive DEAD in the same frame as the
  attack starts. Deferred impacts revalidate range, equipment, player state and
  the monster's per-life token; movement/travel/death/disconnect cancel them.
  This is bounded server timing, not a measured client animation-frame schedule.

## Reconnect state

- Character selection displays the latest saved equipped IDs/durability by
  default. The retired `CHARACTER_PREVIEW_EQUIPMENT=0` no longer suppresses gear.
  Only explicit `CHARACTER_PREVIEW_RECOVERY=1` enables naked diagnostic previews.
- Validated movement saves position every ten seconds and on disconnect, without
  overwriting inventory, coins, XP or vitals. Position CAS protects newer travel
  state and records the legacy-spawn-repair exemption. Graceful shutdown waits
  for final logout saves; a forced kill can lose movement after the checkpoint.
- Client checks: equip/unequip, logout and reopen selection; move away from town,
  logout/relogin, and confirm the same location. Attack a weak mob and confirm
  the swing starts before death/chest effects, then test stronger mobs normally.

## Implemented scope

- **Retaliation:** an attacked monster retaliates against its most recent attacker
  while in authored attack range, using source damage and attack delay. Target
  ownership and cadence are shared between sessions. It disengages after 30
  seconds without a hit. Monsters do not yet chase, pathfind or proactively aggro.
- **Skills:** source-backed, single-target enemy MAGIC_TYPE1 attacks (non-combo)
  and immediate MAGIC_TYPE3 HP damage (no DoT/AoE/secondary effect). The skill must
  match character class and level/allocated tree, weapon requirement, available
  MP, target visibility and range. Casting time is enforced; movement interrupts
  a pending cast. MP plus per-skill and global cooldowns commit together and
  survive reconnect. Unsupported skills return failure without spending MP.
  Physical damage uses the current bounded hit/armor formula and authored skill
  multiplier/addition; direct spells use authored first damage. Full critical,
  resistance, success-chance, buff and elemental formulas are not claimed.
- **Skill points:** category 5–8 allocation persists with a blob CAS. Novice and
  mastery restrictions follow CUser::SkillPointChange; invalid types cannot index
  outside the skill blob. Class advancement/resets themselves remain unported.
- **XP:** solo killing character receives source K_MONSTER.iExp. Level thresholds
  come from LEVEL_UP, with remainder XP, level cap 80, stat points and free skill
  points persisted together. This is raw solo XP, not party/premium/event/level-gap
  scaling. Existing USERDATA's signed 32-bit XP bound is retained without wrap.
- **Loot:** source iMoney roll (70–100%, capped at 32000), independent five-slot
  K_MONSTER_ITEM rolls out of 10000, procedural MAKE_ITEM_* generation in the
  first two slots, MAKE_ITEM_GROUP selection among 30 entries, and direct items
  in slots 3–5. Missing/ambiguous final ITEM definitions do not create fake items.
- **Ownership:** solo last hitter only. No party sharing, round-robin or public
  loot theft. Bundles expire after 120 seconds and require same zone and <=10
  world units for opening/pickup. Rendering the chest requires the associated
  corpse; reconnect within the corpse lifetime reannounces owned loot. Unclaimed
  chest rendering is not reconstructed across a GameServer process restart.
- **Persistence:** a per-life kill token prevents repeated XP/loot creation. XP,
  levels, points and bundle creation share a transaction. Pickup locks USERDATA
  then the bundle and commits its claimed entry with inventory/serials/coins in
  one transaction. Stale snapshots, wrong owner/zone/range, replayed/expired claims,
  full bags, stack/coin caps and weight failures do not consume the drop. Unknown
  commit outcomes disconnect; rewards already picked up remain on reconnect.
- **Player death:** retaliation saves HP/MP before reporting death. Free town
  regeneration persists vitals/position together. No XP penalty, resurrection
  stones or premium resurrection is implemented. Equipment wear remains pending.

New tables live in `0010_combat_rewards.sql`, applied idempotently by GameServer
startup and the normal migrator. Kill receipts/claimed entries are durable audit
records; there is no automatic deletion/archival policy yet. Take a database backup
before deployment and monitor these tables for growth on long-running installs.

## End-to-end client checklist

1. Stop GameServer; back up the database, rebuild and restart the updated build.
   Admin drop changes require this restart; there is no hot reload.
2. Equip a weapon, approach/select a monster and press R. Expect attack animation,
   decreasing HP and `[ COMBAT ]` log lines. Distant requests must not damage it.
3. Stand within the monster's attack range after hitting it. Its retaliation must
   reduce your HP. Use a potion and verify resource persistence on reconnect.
4. Use an actually learned supported attack skill (e.g. novice warrior Slash at
   level 3). Expect damage, MP cost and cooldown. Wrong-class, unlearned,
   unsupported or early/replayed casts must not grant damage/free effects.
5. Kill it. Observe death animation without immediate removal, XP update (and
   level-up if applicable), and a chest if the authored roll produced loot.
   There is no guaranteed chest for a monster whose roll is empty.
6. Open the chest and pick up coins/items. Check count, serial, weight and coins.
   A second player must not open or take the owner's loot. Repeated pickup packets
   must not duplicate the same entry.
7. Reconnect: received XP, levels, points, items and coins must still be present.
   Wait for respawn and verify the same spawn returns at full HP.
8. Let retaliation kill you; use the normal free regeneration button. Verify town
   position, restored HP/MP and saved inventory survive another reconnect.

CI covers captured R packets, packet layouts, source skill definitions, class/tree
checks, corpse/respawn state, concurrent hits, production/group drop rules, XP,
concurrent/replayed/expired pickup, SQL rollback, cooldown persistence, skill-point
CAS, and death/regeneration persistence. The graphical checklist still needs a
real 1453 client; CI is not a claim that every animation/skill was visually tested.
