# Retail 1453 fidelity audit — initial findings

Goal: reproduce a specific retail 1453 behavior, not modern Knight Online or a
private-server ruleset. This is an initial gap review, **not certification that
all systems have been audited or are retail-complete**.

## Verified correction: InnHostess entry flow

`GameServer/NPCHandler.cpp`, `NPC_WAREHOUSE`, sends WIZ_WAREHOUSE / WAREHOUSE_REQ
(0x45 / 0x10). The **client** displays the Inn service menu. It is not a generic
server-built SelectMsg dialog, and its labels come from the installed client.
Only the subsequent WAREHOUSE_OPEN action 1 opens storage. The C# path previously
skipped the menu and directly opened storage; that is now corrected. NPC/range/
friendly checks still apply. Talking to the Inn does not charge an opening fee
or load storage. Storage action 1 revalidates access and applies the source PK fee.

## Sealing excluded by scope decision (2026-10-25)

The owner withdrew the newer-version item/character sealing extension. Its C#
service, password provisioning, decoder, tests and active migration have been
removed. The original C++ reference is retained as historical source, not live
C# functionality. Existing database records are not dropped or rewritten.
Native client Inn labels are client-owned; an unavailable entry may still appear.
Unsupported requests never charge coins, consume scrolls or transfer ownership.
Raw potentially sensitive request payloads remain redacted as a logging safeguard.

## Other known retail gaps (not fixed by the Inn change)

- Combat uses bounded prototype hit/armor rules, not a complete retail physical/
  elemental/resistance/success/critical engine. Only selected single-target skills
  are supported; buffs/DoT/AoE and many effects remain missing.
- Monster movement, automatic aggro, chasing, threat/pathfinding and NPC magic
  are not fully implemented. Current retaliation is stationary.
- Player visibility uses a radial hub, not the original spatial-region grid.
- Quest/Lua services, clan creation, clan/social systems, power-up store and
  character transfer are not implemented end-to-end. Showing native menu options
  does not make those services functional.
- Solo XP/drop and owner-only loot omit party/premium/event scaling and sharing.
- Trade/player merchants, repairs/upgrades, rentals/expiration and the full item
  flag lifecycle are incomplete. Warehouse protection is deliberately conservative.
- Prototype travel commands, boosted-starter tutorial handling and GM tools are
  development policies, not evidence of retail parity. Do not silently reset
  existing characters to change those policies.

See CSHARP_REWRITE_PLAN.md and the subsystem docs for the existing supported
scope. Subsequent increments need a reference behavior, packet vectors,
transactional tests and real-client acceptance before being marked retail-verified.

## Additional reference review

[RE-KO comparison](REKO_REFERENCE_REVIEW.md) reviews the Rust implementation at
pinned commit `217183d1771ac54a0e09de0e00a859f8af7c5538`. It confirms the Inn
menu opcode but explicitly lacks sealing. It also identifies protocol differences
requiring native-client verification rather than an unreviewed wholesale port.
