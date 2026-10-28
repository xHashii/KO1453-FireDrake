# Fire Drake 1453: implementation audit and delivery roadmap

**Audit date: 25 October 2026.**

Follow-up: [intro startup correction](INTRO_LOGIN_FIX.md) now implements native
MYINFO/ACK/quest ordering, one-shot bootstrap and explicit temporary intro
suppression at all levels. Actual-client repeated-login acceptance is pending. Scope: the live C# rewrite in `src/`, its tests,
SQL and runtime assets, compared with the legacy C++ reference. This is a code
and dependency audit, **not a claim of native-client retail certification**.

## 1. Executive assessment

We have a persistent **solo-PvE vertical slice with multiplayer visibility**,
not a complete MMORPG server. Login, character entry, basic equipment, a subset
of combat, solo XP/loot, NPC shopping and banking have working implementations.
The social, competitive and event loops are largely still absent.

Two important corrections to the premise:

- **Skills have been started**, but only selected single-target enemy attacks,
  HP/MP potions, skill-point allocation and shortcut persistence. That is a small
  fraction of a complete four-class skill system.
- **NPC shops have been started**, with transactional buying/selling and shop
  inventory moves. **Player-to-player trade and player merchants/markets have
  not**. An NPC shop is not a player market.

Do not estimate a completion percentage from the number of handlers, SQL tables,
menu entries or tests. Their cost and completeness differ enormously. The most
important missing dependencies are effect/targeting rules, moving monster AI,
party membership/rewards, two-owner item transactions and PvP permission rules.

### Scope reversal: sealing removed

At the owner's instruction, the newer-version item/character sealing extension
is excluded. Removed from the C# product: service, decoder, password hashing,
operator provisioning command, runtime protection overlay introduced by that
extension, dedicated tests and active `0011_item_sealing.sql` migration. The
original C++ files and original schema remain reference material, not live code.

The Inn storage menu is retained. Its labels belong to the installed client;
server removal cannot promise that a later-client seal label disappears. An
unsupported request gets an unavailable notice and cannot mutate inventory,
charge a fee or transfer ownership. Credential-payload redaction remains a
logging defense, not a sealing service.

**No destructive database rollback is performed.** If the extension was actually
deployed, its tables/migration history may remain unused. Existing `SEALED_ITEMS`
records still receive the older conservative sale/storage checks. Previously
sealed items may consequently remain restricted: inventory those records and
identify their provenance before any targeted operator cleanup. Do not delete
all protection records, undo unrelated bound/rental records, or automatically
refund coins without a reliable transaction history. No further code-setting or
sealing commands should be used.

## 2. Evidence and how to read status

- **Implemented slice:** executable C# handler/repository/rules exist for the
  listed behavior; not an assertion that every edge case or animation is retail.
- **Partial:** meaningful implementation exists, but important cases are missing.
- **Not ported:** no end-to-end C# gameplay service; original C++ or SQL alone
  does not count.
- **Excluded / verify era:** intentionally not enabled, or applicability to the
  target 1453 build must be established before scheduling implementation.

Primary evidence:

| Area | Live implementation / verification | Legacy reference |
|---|---|---|
| Dispatch and character entry | `src/KnightOnline.GameServer/Program.cs`, database character/account repositories | `GameServer/User.cpp`, `LoginHandler.cpp`, `CharacterSelectionHandler.cpp`, `DBAgent.cpp` |
| Players and chat | `GameSession.Players.cs`, `GameSession.Chat.cs`, Shared `PlayerHub.cs`, `ChatHub.cs`; foundations/chat/session tests | `CharacterMovementHandler.cpp`, `ChatHandler.cpp`, `Region.cpp` |
| Combat and skills | `GameSession.Combat.cs`, `GameSession.Potions.cs`, Database `CombatSkills.cs`, `PotionRepository.cs`, `SkillPointRules.cs` | `AttackHandler.cpp`, `Unit.cpp`, `MagicProcess.cpp`, `MagicInstance.cpp` |
| Rewards and world | `GameSession.Loot.cs`, `GameSession.World.cs`, Database `CombatRewards.cs`, `World/` | `Npc.cpp`, `NPCHandler.cpp`, `AIServer/`, `Map.cpp` |
| Items and economy | `GameSession.Shop.cs`, `GameSession.Warehouse.cs`, `ShopRepository.cs`, `WarehousePersistence.cs`, `Items/` | `ItemHandler.cpp`, `TradeHandler.cpp`, `MerchantHandler.cpp`, `UpgradeHandler.cpp` |
| Operations and safety | `CharacterEditLease.cs`, Database `Admin/`, AdminPanel, `tests/KnightOnline.Verification/`, CI workflow | Legacy DB and account behavior; explicit C# safety improvements |
| Missing social/event systems | No matching live C# gameplay service in the dispatcher | `PartyHandler.cpp`, `FriendHandler.cpp`, `KnightsManager.cpp`, `KingSystem.cpp`, `GameEvent.cpp`, `ArenaHandler.cpp`, `AIServer/MAP.cpp` |

The original tree contains later-version material. For example, Chaos, Juraid
and Border Defense identifiers are **not proof** of 1453 availability. RE-KO is
a useful pinned secondary implementation, not an unconditional retail spec;
see `REKO_REFERENCE_REVIEW.md`.

## 3. What has actually been delivered

### Transport, database and account foundation — implemented slice

- .NET solution with Shared, Database, LoginServer, GameServer, AdminPanel and
  console verification projects; legacy source retained for comparison.
- Packet framing, byte primitives, JvCryption, LZF and CRC32 with golden vectors.
- Login/version/server-list flow and game authentication, nation selection,
  character creation/list/selection and world-entry handshake.
- SQL Server-to-MariaDB conversion tooling, committed schema/static data,
  startup migrations/repairs, item catalog recovery and asset checks.
- Lossless item/serial blob handling and preservation of opaque legacy tails;
  narrowly scoped writes rather than blind inventory saves on logout.
- Row locks, expected-snapshot CAS, duplicate-row rejection and cooperating
  account/character edit leases. Unknown inventory commit outcomes disconnect
  for reload rather than retry from stale state.

**Missing/hardening:** full character deletion workflow, account lifecycle and
recovery, multi-process/server transitions, connection-loss lease fencing,
broader abuse controls, performance/load profiling, operational recovery drills
and security review. The login/password scheme inherited from the legacy
protocol is not modern web authentication. Porting a packet is not sufficient
security validation.

### Character/world presentation — partial

- Corrected physical equipment order in character selection; visible gear uses
  current saved inventory instead of hiding all models as a workaround.
- MYINFO/stat calculation, ordinary equip/unequip and bag/cospre shuffles.
- Native player IN/OUT byte-action envelopes, requested-user records without the
  extra prefix, region-ID lists at entry/travel completion and uint32 XP updates.
- Same-zone radial visibility, remote move/rotation, selected poses, appearance
  refreshes and cleanup. Player IDs are bounded separately from NPC IDs.
- GMs hidden by default; authorized `+visible` / `+invisible` and slash aliases;
  visibility is session-only and reconnect resets it to hidden.
- Map height/warp loading, static NPC/object/spawn placement, native gate and
  zone-change flow, town/travel conveniences and saved movement checkpoints.

**Missing:** native spatial grid, movement-speed/teleport validation, collision
and line of sight, complete zone flags/capacities, terrain/event-tile behavior,
stealth/invisibility detection and synchronization of buffs/clan/party/merchant
state. The SMD loader skips collision/event structures rather than implementing
all collision and room triggers. A map being loadable does not make its event
playable. Public prototype travel routes must not bypass future event admission.

### Basic combat, progression and recovery — partial

- Server-selected melee damage/timing, deferred melee impacts with revalidation,
  shared monster HP/life tokens, corpse/death/respawn handling.
- Stationary retaliation against the latest attacker; no full chase AI.
- Learned class/tree/level/weapon/MP checks for selected non-combo MAGIC_TYPE1
  attacks and immediate single-target MAGIC_TYPE3 HP damage.
- Cast timing/cancellation, persisted per-skill/global cooldowns and MP spending.
- HP/MP potions, sitting/recovery ticks, saved vitals and free town regeneration.
- Skill-tree point allocation and saved shortcut bars (opaque shortcuts do not
  grant permission to cast).
- Solo kill XP, level thresholds, level-up points and solo loot/pickup; atomic
  reward and pickup guards, source-backed drop-generation rules and serials.

**Missing:** full damage/hit/resistance rules; class advancement/master quests;
party scaling; PvP; most skills; death XP/NP rules; resurrection options;
durability wear; complete timed-state persistence. Do not call basic R attacks,
one attack spell and potions a finished combat system.

### NPC commerce and bank — implemented slices

- NPC shop buy/sell/move, authored item/group validation, weight/stack/coin caps,
  source serial preservation and transactional persistence.
- Inn service menu distinct from opening storage; account-wide 192-slot bank,
  eight pages, item/coin deposit/withdrawal and moves.
- Explicit-NPC and implicit-selected-NPC request formats with exact framing;
  selected live friendly Inn and distance checks remain mandatory.
- Source opening fee policy, conservative existing protection checks, optional
  warehouse time metadata and refusal to discard unsupported timed-item data.

**Missing:** NPC repairs and wear, working anvil upgrades/exchanges, every special
NPC shop variant, player trade, selling/buying merchants, offline markets,
expiration/rental lifecycle, complete item deletion/splitting/storage variants.
Anvil windows can open, but processing currently refuses without consuming items.
Offline markets are not approved merely because contemporary KO has them.

### Chat, quests and administration — mixed

- Local chat and whisper target/block behavior are implemented, with basic
  throttling. Party/clan/alliance/shout channels are not complete services.
- Quest-state bytes are loaded/sent. A targeted quest-500 compatibility repair
  exists for boosted characters; this is **not** a Lua quest engine.
- The owner reported a repeated intro. Keep that as an **open native-client
  regression until a successful repeated-login test confirms resolution**.
- Admin character/inventory/bank tools, guarded offline editing, item serial
  allocation, audit/backups and drop/data browsing have implementations. They
  are separate from native gameplay. The owner has not run the panel; do not
  attribute observed inventory problems to panel edits.

## 4. Detailed backlog: gameplay and social systems

| System | Current state | Required work / acceptance boundary |
|---|---|---|
| Complete skills | Partial subset only | Shared target permissions and effect scheduler; skill-by-skill class coverage, costs, interrupt/failure rules and packet stages; see next section. |
| Monster AI | Stationary retaliation | Spawn/patrol/idle, proactive aggro, threat, chase/return/leash, pathfinding, ranged/caster attacks, heals/support, linked groups and bosses; shared simulation rather than duplicated per-viewer AI. |
| Party | Not ported | Invite/accept/reject/expiry, authoritative membership/leader, kick/leave/disband, disconnect/zone/death handling, HP/MP display, party chat, buff/heal targeting, XP/range/level sharing and loot distribution; party-wanted board separately. |
| Player trade | Not ported | Request/cancel/busy state, distance/nation/rules, item/coin offers, offer revisions, both confirmations, one atomic two-character commit; see economy contract below. |
| Player merchants | Not ported | Selling listings, listing reservations, buyer quantity/weight/coin checks, atomic purchase, appearance/signs/browse packets, close/disconnect/travel behavior. Verify buying merchants against the actual era before adding. |
| Clans / Knights | Not ported | Create/name/fee/nation rules, invite/accept/withdraw/remove, leader/assistant roles, member list/presence, clan chat, persistence, grades/rank/fame/capes/emblems and notices as supported by the client. Clanless zero metadata is not membership support. |
| Alliances | Not ported | Persisted clan-to-clan membership, leader permissions, invitations/removal/disband, native identity/cape/roster metadata and alliance chat; requires working clans first. |
| Friends | Not ported | Persistent list, add/delete/limits, same-name/duplicate rules, online/zone status and login/logout updates, client list actions; reuse presence safely without exposing hidden GM location. |
| Other chat | Partial | Nation/zone/shout eligibility and cost/rate rules; party/clan/alliance routing only to current members; mute/block/admin moderation and cross-nation policy. |
| Quests / NPC dialogue | Not ported end-to-end | Lua runner/bindings or equivalent verified interpreter, dialogue choices, requirements, kills/collect objectives, transactional rewards, class changes, resets, exchanges and replay refusal; ship/test actual scripts. |
| Mail / post office | Not ported native workflow | List/read/delete/claim/send, attachments/coins/COD if era-supported, offline recipient ownership, claim-once ledger, expiration/returns. Admin give-by-letter writes do not implement the player's mailbox. |
| NPC repair / durability | Repair explicitly refuses | Source cost formula, slot identity/maximum durability, coin update and repair together, persisted weapon/armor wear and stat refresh; safe small next economy slice. |
| Anvil / exchanges | UI/refusal only | Recipe/scroll/item validation, success tables, item transformation/burn/fees and serial policy committed together; scripted/Chaotic Generator exchanges separated from upgrades. Do not invent rates. |
| Inventory completeness | Partial | Source-supported destroy/split/stack variants, magic-bag restrictions, timed/rented/bound/duplicate-item policy, cross-service preservation, item mall receipts as applicable. Existing protection records must not be bypassed. |
| Progression | Solo XP/stat/tree allocation | Retail XP/death/scaling rules, class advancement/mastery, quest requirements, stat/skill resets and caps; do not overwrite existing boosted profiles to change defaults. |
| PvP / nation points | Not a complete path | Legal target/zone/nation checks, safe zones, damage/effects against players, assists, deaths/resurrection, NP/loyalty/ranks/rewards and exploit prevention. Entering CZ is not implemented PvP. |
| Arena / duels | Not ported | Era-correct entry, teams/eligibility, death/return behavior and rules distinct from war-zone PvP; do not infer duel UI from newer source. |
| Rankings / king / tax | Not ported | Authoritative NP/clan rankings, election/king permissions, treasury/taxes/notices and persistence; constrain to verified period behavior. |
| Premium / PUS | Not end-to-end | Eligibility/time/rates and purchase receipt/delivery/claim-once behavior, only after target-era scope is agreed. No web payment processing or modern store features implied. |
| Rental / expiration | Mostly unsupported/refused | Timers, expiry cleanup, runtime flags, reconnect behavior and item use/transfer restrictions across every consuming service; source contains partly disabled paths. |
| GM / operations | Small command subset + panel | Audited authorized commands, announcements, bans/mutes, event controls, live observability and safe reload boundaries; no ordinary-player privilege escalation. |

### Skill implementation should be a matrix, not one giant handler

For each actual target-client skill, record class/tree, requirement, weapon/item
cost, HP/MP cost, target moral, effect types, timing/range/radius, cooldown,
stack group, dispel/expiry behavior and persistence. Classify it as supported,
unsupported or blocked by missing shared infrastructure.

Required families include:

1. Physical single-target/combo attacks; ranged/projectile weapons and ammo.
2. Direct heals, group heals and restoration-over-time.
3. Direct elemental damage, DoT and AoE; correct resistance/accuracy/miss rules.
4. Stat/HP/AC/attack/speed buffs; debuffs, cures and dispels; consistent stacking.
5. Stealth/detection, transformations and other class-specific states.
6. Teleports, summons, resurrection and resource/item-consuming special skills.
7. Correct primary/secondary effect combinations and NPC casts.

Map these to the actual `MAGIC`/`MAGIC_TYPE*` definitions and native messages.
Do not assume every higher-numbered type/skill in the mixed-version database is
1453 content. A timed effect must expire/remove correctly on death, zone change,
logout/reconnect, cancellation and dispel before it is considered delivered.

### Two-owner economy contract

Trade and markets need more than UI packets. Use deterministic lock ordering for
both characters/accounts; validate current ownership, leases, snapshots, item
serials/protections, weights, stack limits and coin bounds in the committing
transaction. Any offer change invalidates **both** prior confirmations. Cancel
on disconnect, travel, death or loss of eligibility; never remove items while
only one party has committed. The buyer and seller either both receive their
new state or neither does. An unknown commit disconnects/reloads both sides;
no automatic replay from old snapshots. Listings need durable single-claim
state so simultaneous purchases cannot sell the same reserved quantity twice.

## 5. Events, wars, bosses and zone systems

**No full C# event controller/scheduler exists today.** Spawning ordinary monsters,
loading `.aievt` files into the repository, or allowing travel to a map does not
implement an event. Current public travel allows some dungeon/Bifrost-numbered
zones without a complete event access policy; audit and gate these before
claiming scheduled access is enforced.

| Event / system | Implementation status | Work and era gate |
|---|---|---|
| Lunar/nation wars and invasion | Not ported | Schedule/announcements, nation admission/capacities, guards/keepers/monuments, authoritative score/victory, invasion unlock/close, return/cleanup and rewards. Confirm map rotation and exact 1453 conditions. |
| Dark/low-level war variants | Not ported | Verify target-era availability, level bounds, map, scoring and reward differences; do not reuse ordinary war rules without evidence. |
| Castle siege / Delos | Not ported | Clan/alliance eligibility, registration, siege objects/gates, ownership capture, victor, castle benefits/taxes and persisted ownership; depends on clans, PvP, object combat and scheduler. |
| Forgotten Temple | Not ported | Verified NPC registration/entry, level brackets/capacity, room/wave timers, boss completion, party/nation policy, survivor reward-once and failure/exit handling. Exact timings/rewards need target-build evidence. |
| Bifrost | Not ported as an event | Monument competition, winning-nation entry window, delayed other-nation access, closing/return logic, sin-monster content, fragments and Chaotic Generator recipes/rewards; do not use modern schedules as 1453 defaults. |
| Bosses / boss rooms | Basic ordinary spawn lifecycle only | Authored boss spawn windows/randomness, unique spawn ownership, skills/aggro/pathing, room triggers, death/reward and restart policy. No custom six-hour timer should be called retail. |
| Dragon/dungeon/monster-suppression content | Not ported end-to-end | Verify which quests/maps/room scripts belong to the build, entry tickets/party rules, ordered objectives, progression, rewards and exit. |
| Seasonal snow war / holiday events | Not ported | Confirm era, period activation, special attacks/equipment/score/rewards and cleanup; disabled by default until specified. |
| GM EXP/drop events | No full controller | Announce/start/end, explicit configuration, non-overlapping rate rules, audit and restart recovery; not a substitute for retail scheduled events. |
| Border Defense / Juraid / Chaos / later arenas | Excluded pending era verification | Mixed legacy source has identifiers/schedules, but that is insufficient evidence. Do not silently expand the 1453 scope as happened with sealing. |
| Modern private-server events/features | Not approved | Collection races, battle-royale-style events, autoloot, offline merchants, custom bosses and other modern additions require a separate explicit decision. |

### Shared event infrastructure required first

Use a persisted event state machine: announced -> registration -> running ->
completed/failed -> cleanup, with a unique event instance ID, server-authoritative
clock and explicit configured timezone. Determine late-entry/capacity/eligibility,
zone access, participant snapshots, objective transitions and return positions.
Rewards must have a unique claim receipt per eligible recipient/event instance.
Define behavior on crash, restart, missed schedule, administrator cancellation,
disconnect and overlapping events. Test time with a controllable clock rather
than waiting hours in CI. No hard-coded Sofia or contemporary official timetable
should silently become the game's retail schedule.

### Historical scope evidence (not a timetable specification)

The contemporaneous July 2006 expansion announcement describes new zones,
monsters/bosses and PvP additions, but not their complete packet/rate rules:
[3](https://www.mmorpg.com/news/reign-of-the-fire-drake-2000060684).
A March 2008 industry report associates Fire Drake with the post office and
level-cap change:
[7](https://www.gamesindustry.biz/knight-online).
A secondary history associates Bifrost with Fire Drake, but its detailed event
rules cannot establish a specific client's schedule or all 1453 limits:
[1](https://gamia-archive.fandom.com/wiki/Knight_Online).
Use these to identify candidates for verification, not to copy later event
rewards, premium policies or private-server parameters wholesale.

## 6. Recommended implementation order

| Milestone | Deliverable | Exit gate |
|---|---|---|
| **0 — Stabilize the actual client loop** | Seal extension removed; repeat login/intro/selection tests; two-client appearance, XP, shortcuts, NPC shop and bank regression; native protocol evidence tracked. | No reproduced intro loop/ghosts/data loss; results explicitly recorded, not assumed from CI. |
| **1 — Combat/skill and world foundations** | Skill inventory/matrix, shared target/effect lifecycle and deterministic timers; basic chase/leash AI plus movement validation; next verified attack/heal/buff/DoT families. | Costs/cooldowns/effects and their cancel/expiry rules tested through death/travel/reconnect; one representative skill per family demonstrated in the real client. |
| **2 — Party + first cooperative quests** | Party membership/chat/HP, target rules, party XP/loot policy and reward persistence; starter tutorial/dialogue/first kill-collect-reward quest. | Two or more accounts can invite, fight, share rewards, leave/reconnect without duplicate grants or stale membership. |
| **3 — Item/economy completeness** | Repair/wear first; atomic player trade; then sell merchants, verified upgrades/exchanges and native mail. | Two-owner SQL/concurrency/failure tests pass; no item/coin creation or loss; real-client browse/confirm/cancel demonstrated. |
| **4 — Persistent social systems** | Friends, clans/roles/chat/appearance, then alliances, grades/capes/rankings as verified. Friends can proceed in parallel with earlier milestones. | Permissions/persistence/native UI and disconnect cleanup tested with multiple accounts; no forged membership or hidden-GM information leaks. |
| **5 — PvP and zone policy** | Nation/zone/target permissions, damage/effects/death/NP and zone admission; supported arenas. | Friendly/safe-zone/invalid-target attacks refused; valid opposing-nation combat and accounting survive reconnect without farming/replay exploits. |
| **6 — Scheduled retail content** | Shared scheduler/reward receipts, then Forgotten Temple/Bifrost, nation wars/invasion and clan-dependent siege in dependency order. | Each event can run start-to-cleanup and restart/cancel safely with no duplicate rewards or stranded players. |
| **7 — Release hardening** | Load/soak, malformed traffic, shutdown/restart and DB/network-failure drills; backup/restore, telemetry, growth/retention and deployment procedures. | A documented release acceptance suite succeeds on the actual client and a disposable production-like database. |

This is dependency ordering, not a promise of calendar dates. A full skills or
war system is not a one-handler task. **The next gameplay work should be
milestone 0 client acceptance, followed by the skill/effect matrix and shared
combat primitives—not enabling event menus over missing combat/party services.**
Small bounded fixes (e.g. repair) can be delivered alongside this, with their own
acceptance tests. Do not remove unknown/legacy player data to make tests pass.

## 7. Test and operational gates for every new subsystem

1. **Reference:** native client packet evidence and era-specific rules, with
   unknown fields labeled unknown. Literal independent byte vectors.
2. **Rules:** success and refusal, boundaries, malformed/trailing data, server
   permissions and invalid source/target state.
3. **Persistence:** ownership, duplicate identities, stale CAS, concurrent writers,
   replay, rollback at each write stage and unknown commit behavior.
4. **Lifecycle:** death, travel, logout/reconnect, restart and disconnected peer;
   no lingering timer, invitation, offer, effect, listing or event participation.
5. **Native client:** at least two accounts for social/economy/PvP, visible UI
   result plus saved state after reconnect; record actual observations.
6. **Operations:** audit non-secret metadata, preserve credentials/redaction,
   bounded queues/rate limits, shutdown behavior, backups and migration safety.

Current CI has **Build + golden vectors**, **Migration SQL gate**, and **Shop
transactions (MariaDB)**. The last job also exercises other database foundations;
its name is not evidence that trade, party, clans or events are tested. Existing
verification includes transport/crypto, chat, character/admin/blob/catalog,
melee/potions/selected skills, solo XP/loot, travel/recovery, player visibility,
shortcuts, warehouse transactions and graceful shutdown. Most new subsystem
acceptance tests listed above do not exist yet because those systems are absent.

## 8. Open decisions and risks to keep visible

- Pin the exact client executable/build and content provenance; the C++ source,
  database, Lua assets and online guides are not uniformly 1453.
- Resolve the reported repeating intro with observed repeated-login behavior.
  Existing quest-state repair is a workaround, not proof of completion.
- Agree which development policies remain during implementation: boosted starts,
  travel commands and any simplified rates. Keep them distinguishable from retail.
- Do not retroactively reset existing characters, items, points or quest progress
  when correcting starter/default policies.
- Plan schema/index evolution for index-free legacy tables without silently
  selecting a duplicate account/character row. Keep account/character leases,
  CAS and uncertain-outcome protections while adding multi-owner operations.
- Current per-session timers/synchronous DB calls and radial visibility need
  scalability review; do not advertise large-population capacity without a load test.
- Kill receipts, cooldowns and claimed loot/event data need retention/observability;
  safely archive only after choosing replay/reward retention rules.
- Admin-panel implementation is not evidence of native gameplay implementation,
  and the owner has not used it. Maintain that distinction in bug reports.
- There is no full retail certification, production load benchmark or completed
  Windows two-client acceptance record in this audit.

## Follow-up: first party membership slice

See [PARTY_MEMBERSHIP.md](PARTY_MEMBERSHIP.md). Native invitation/acceptance,
leader controls, eight-member roster, HP/MP updates and party chat now have an
initial implementation with authority/lifecycle tests. This supersedes the
"not ported" membership status above, not the broader party backlog. Native
client acceptance is pending; shared XP/loot, party-target skills, cross-zone
retention and party-wanted remain unimplemented. No database reset/migration.

Expanded-Moradon NPC placement follow-up: see [MORADON_NPC_LAYOUT.md](MORADON_NPC_LAYOUT.md).

## Current follow-up: expanded Moradon monsters and immediate next work

See [MORADON_MONSTERS.md](MORADON_MONSTERS.md) for the new-map placement upgrade:
161 exact supplied spawn rows plus 34 explicitly adapted legacy rows, 646 monsters
in total. Other zones, NPCs, definitions and character data are preserved. This
supersedes the earlier "monsters unchanged" statement for the monster follow-up,
not for the historical NPC-only migrations. Placement is not moving monster AI.

The current short roadmap and next implementation priority are in
[NEXT_GAMEPLAY_STEPS.md](NEXT_GAMEPLAY_STEPS.md): native world acceptance, then
shared basic monster aggro/chase/leash/movement, followed by combat/party rewards,
progression/economy, social/PvP and finally dependent events/release hardening.

## Follow-up: first shared monster AI slice

[BASIC_MONSTER_AI.md](BASIC_MONSTER_AI.md) supersedes the stationary-only AI status:
shared authoritative positions, source passive/aggressive acquisition, direct-step
chase, shared retaliation cadence, leash/return/heal, corpse/respawn position and
native movement packets are implemented. Conservative SMD shape/terrain checks
are monster-only, not full pathfinding or a player stair-collision fix. Native
client acceptance is pending; idle roaming, packs, navigation and advanced monster
skills remain missing. No database reset or new spawn migration.
