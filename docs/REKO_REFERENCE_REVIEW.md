# RE-KO reference review

Reviewed 2026-10-24, pinned to upstream commit
`217183d1771ac54a0e09de0e00a859f8af7c5538`:
https://github.com/KBurakTokmak/RE-KO/tree/217183d1771ac54a0e09de0e00a859f8af7c5538

This is a code/documentation comparison, not an execution of RE-KO, independent
verification of its disassembly addresses, or proof that its client matches our
user's client. No Rust code or client assets were imported into this repository.
The project is MIT-licensed; retain the license/attribution if adapting its code.

## Does it implement the missing sealing systems?

**No.** Its feature backlog explicitly classifies ITEM-06 (sealing/unsealing,
binding, expiration/rental metadata) as Missing / Unverified. Its exclusions
identify the same empty C++ CharacterSealProcess and call for a separate
original-client/source investigation. It therefore supplies neither a finished
character-ring transfer implementation nor verified 1453 seal-code/premium rules.

References:
- [Backlog, ITEM-06 and reference exclusions](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/docs/design/1453-feature-backlog.md)
- [Project scope and limitations](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/README.md)

## Useful confirmation

`shops.rs` sends `[0x45,0x10]` for a type-31 Inn NPC. `storage.rs` handles action 1
separately and emits bank balance plus 192 seventeen-byte item records. This
agrees with our restored native services-menu entry and storage reply layout.
It does not mean Create Clan, Power Up Store or sealing are implemented merely
because the client's native menu displays those entries.

References:
- [Inn NPC dispatch](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/crates/reko-server/src/shops.rs)
- [Storage handler](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/crates/reko-server/src/storage.rs)

## Concrete differences to resolve before claiming retail compatibility

| Priority | Finding | Current C# behavior / required verification |
| --- | --- | --- |
| P0 | Native player record envelopes differ | RE-KO `social.rs` writes a byte USER_INOUT subtype; `lib.rs` builds REQ_USERIN records with no additional zero prefix. Our hub writes a ushort subtype and an additional zero marker in requested records, following the supplied C++. Verify against the actual installed client before changing either layout: the user has already observed visible named peers, so do not blindly substitute another client build's format. |
| P0 | XP packet width differs | RE-KO's protocol notes say subcode 0 + uint32 XP, citing native 006AB660. Our CompleteMonsterHit writes int64 XP following the supplied C++. Resolve with a native-client parser/capture, then add an independently derived byte vector rather than testing an assumption against itself. |
| P1 | Warehouse requests may omit NPC ID | RE-KO `storage.rs` accepts both explicit-NPC and implicit-selected-NPC transfer shapes. Our parser only accepts explicit-NPC requests. Verify the actual client sender; if adding the implicit form, it must still require the server-selected, live, friendly, in-range Inn. |
| P1 | World-entry region handshake | RE-KO documents/sends 0x15 player-ID lists including self and a 0x16 details exchange after load completion. Our hub primarily pushes full IN records. Audit loading/reentry/duplicate stage-1 behavior, especially two-client visibility and equipment refreshes. |
| P1 | Missing versus explicitly empty skill bar | RE-KO distinguishes no saved row (no load reply, preserving a local bar) from an explicitly saved empty bar (zero-count reply). Our code returns an empty reply in both cases. Native client behavior needs checking before introducing a migration or changing first-login behavior. |
| P1 | Item-icon ownership | RE-KO documents native shop/warehouse/anvil acknowledgements as owning their UI icons; extra item-count updates can produce ghosts/null icons. Our warehouse currently avoids duplicate item-count deltas; preserve this invariant when adding seals, quests, repairs and upgrades. |
| P2 | Visibility grid | RE-KO uses 48-unit regions and a 3x3 region view. Our visibility uses a 120-unit circle, so actor entry/exit boundaries are not retail-equivalent. |

References:
- [Native protocol findings](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/docs/re/protocol.md)
- [Player serialization](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/crates/reko-server/src/social.rs)
- [Requested-user handler](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/crates/reko-server/src/lib.rs)
- [Shortcut layout/persistence](https://github.com/KBurakTokmak/RE-KO/blob/217183d1771ac54a0e09de0e00a859f8af7c5538/docs/re/skillbar.md)

## Additional implementation context we can use

- `npc_ai.rs`, `movement.rs`, population and world-service notes: source-backed
  aggression/chasing/return, region handling and client interpolation. These can
  guide the missing AI increment, but its bounded interpolation adaptations are
  not proof of retail AIServer scheduling.
- `skills.rs`, class-specific magic handlers and combat notes: broader skills,
  buffs, DoT, AoE, target morals and party effects. Their docs still identify
  incomplete PvP and effects, and some formulas use supplementary 2086 sources.
- `npc_scripts.rs`, quest rewards and quest progression notes: offered-menu
  validation, native quest packet packing and atomic reward transactions.
- Clan/social handlers: Inn-host clan creation, membership and party lifecycle.
- `upgrading.rs` and rates notes: repair/anvil recipes and acknowledgement flow.
  They explicitly label imported upgrade probabilities as not verified retail rates.

These are cross-check/reference candidates, not automatic replacements for our
MariaDB snapshots, inventory blobs, serial/protection metadata and edit leases.
RE-KO uses a different SQLite persistence model. Adapting logic must retain our
transaction/CAS/duplicate-identity and uncertain-commit safety guarantees.

## Do not copy its project-specific policies as retail rules

The README targets singleplayer/small LAN, not a certified retail server. Examples
of explicit owner choices include `/autoloot`, administrative fixed-damage tests,
first-login account registration, and boss grouping/six-hour/restart-reset policy.
Its world notes distinguish these from imported source timers and spawn rows.
Its protocol document is useful evidence, but every claim still needs validation
against the client/data we actually deploy.

## Recommended sequence

1. Establish actual client-build identity and resolve the P0 envelope/XP conflicts
   with independent byte vectors and two-client regression captures.
2. Validate native Inn menu/storage selection and both warehouse sender shapes.
3. Implement end-to-end item flags/security-code/premium enforcement before
   advertising item sealing. Never log seal passwords.
4. Recover character-sealing request/reply phases and period-correct ownership,
   eligibility, CSS/ring and serial rules. Do not guess or consume items on an
   incomplete path.
5. Use the AI/combat/quest/social references incrementally, with source and policy
   evidence recorded for each subsystem.
