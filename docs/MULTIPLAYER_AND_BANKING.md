# Multiplayer and banking foundations

## Nearby players

A shared hub tracks immutable player snapshots and symmetric visibility within
120 world units in the same zone. Entering/leaving range, entering the world,
travel and disconnect generate native USER_INOUT records. Movement, rotation
and allowed sitting/emotion changes reach visible peers. Equipment/stat changes
refresh peer appearance with OUT/IN; this is deliberately not a complete clan,
party, merchant, stealth or buff state implementation.

The remote-player wire layout follows `CUser::GetUserInfo` in
`GameServer/CharacterMovementHandler.cpp`: byte-prefixed name, clanless fields,
pose/direction and ten item records in BREAST/LEG/HEAD/GLOVE/FOOT/SHOULDER/
RIGHTHAND/LEFTHAND/CTOP/CHELMET order, followed by a **byte** zone ID. It is not
the character-selection layout. Unknown model references are suppressed on this
wire path, without changing saved inventory. Unsupported clan and king metadata
is not invented. REQ_USERIN only returns currently visible peers, deduplicated
and batched in groups of thirty to stay below packet/frame limits.

Player IDs use the TCP server's live allocation, bounded to 1–9999, separate from
NPC/monster IDs starting at 10000. IDs recycle only after disconnect cleanup.
Excess connections are refused without stopping the server. Hub routing never
acquires another session's game-state lock; callbacks only enqueue output.
Visibility updates inspect the publishing player against peers, not every
possible pair on each move. This is radial prototype visibility, not a full port
of the original spatial region grid, movement speed enforcement or collision.

## Skill bars

SKILLDATA action 1 stores up to 64 opaque uint32 shortcut values in the existing
USERDATA_SKILLSHORTCUT table; action 2 replies with action/count/values. Empty
bars can be cleared and missing bars load with zero entries. Saves use the
original 260-byte padded storage, a character-row lock and a transaction. Invalid
lengths/counts/trailing bytes are rejected. Malformed or duplicate saved rows
are not silently overwritten by a load. The table is created if absent on startup.

These are UI shortcuts, not skill-point allocations or permission to execute a
skill. Existing combat skill validation remains authoritative. Shortcut saves
commit immediately; no logout-time inventory/profile rewrite is introduced.

## Warehouse

Friendly live type-31 warehouse NPCs open the native warehouse UI within eleven
world units. All operations revalidate the selected NPC, zone/range, living
player and travel state. A dialog cannot authorize remote storage after walking
away. Travel clears its selection and cached snapshot.

Supported operations:
- Open 192 slots (eight pages of twenty-four) and account-wide bank balance.
- Deposit/withdraw ordinary inventory items, including partial stack transfers.
- Move complete stacks within a bank page or within the ordinary inventory.
- Deposit/withdraw coins, with signed-safe arithmetic and the 2.1-billion cap.
- Charge the original 10000-coin opening fee in zones 71/72/73, atomically.

Bounds are strict: bag positions 0–27, page positions 0–23 and pages 0–7.
Overwrites, incompatible stacks, counts above 9999, insufficient counts/funds,
non-stackable splits, no-storage item IDs and overweight withdrawals are refused.
Sealed/rental protection records are checked for both source and occupied target.
Timed item transfers are conservatively refused: existing expiration metadata is
preserved, not silently discarded. This is not a rental/expiration engine.

The existing USERDATA and WAREHOUSE rows are locked and compare-and-swapped in
one transaction. Ownership and exactly-one legacy row identity are checked.
Inventory blobs/serials/coins and warehouse blobs/serials/coins commit together.
HP/MP/XP/position are not rewritten. Optional WarehouseDataTime is supported;
opaque warehouse trailers and inventory tails are retained. Optional inventory
strItemTime prevents moving a timed item through a runtime that cannot yet model
its expiration. The existing account/character edit lease also excludes a second
cooperating session/editor. As before, that connection-scoped lease is not a
complete fencing solution against database connection loss.

Success is published only after commit. A stale/protected or uncertain database
outcome disconnects the session for reload, rather than retrying a transfer or
saving old inventory on logout. The legacy protocol has no request nonce: two
valid repeated requests represent two separately validated transfers.

## Verification and manual acceptance

Automated verification covers the literal original remote-player wire vector,
visibility transitions, rotation routing, bounded user requests, ID-band reuse,
shortcut packet/storage round trips, warehouse arithmetic/bounds/stack rules,
concurrent bank writers, protection/ownership/duplicate-row refusal, optional
expiration handling and SQL rollback across both inventory and bank updates.

After backing up the database, rebuilding and restarting GameServer:
1. Use **two different accounts** in the same zone. Enter/leave range and verify
   each player appears/disappears, moves and turns. Test sitting and equipment.
2. Log out one client, reconnect and change zones: no old-player ghost should
   remain. Verify the character-select preview still displays saved equipment.
3. Assign shortcuts, reconnect, and verify the same bar. Test clearing the bar.
4. At a warehouse NPC, deposit/withdraw an item and part of a stack, move bank
   items, and deposit/withdraw coins. Reconnect and verify both balances and item
   counts. Use a second character on the same account to verify shared storage.
5. Walk away and attempt another bank action; it must fail without changing data.
6. Recheck shop purchases, combat loot and logout position for regressions.

CI does not render the Windows client. These visual/native-UI checks still need
real-client confirmation. Trade, player merchants, repairs/upgrades, full magic,
clans/parties, and chasing/pathfinding AI remain separate milestones.

## Compatibility corrections after the two-client test

- GM authority is retained in the database, private MYINFO and server permissions.
  Remote appearance packets advertise authority 1 for full/limited GMs so clients
  that hide remote GM models display them to ordinary players. This is a public
  rendering compatibility field, not a promotion/demotion or an authorization
  source. Explicit GM invisibility is not implemented; GMs are visible by default.
- Earlier account creation used a single zero byte for both empty warehouse
  blobs. The loader accepts this precise empty sentinel (also for optional time
  data), keeps the raw bytes as the CAS snapshot, and does not rewrite on open.
  The first successful transaction writes the canonical slot layout. New account
  creation now writes 192 zeroed slots from the start. Nonzero partial records
  and duplicate identities are still refused rather than normalized/destructively
  repaired.
- A warehouse read/validation failure now reports a failed open and keeps the
  game connection alive, with no fee or transfer written. Unknown outcomes during
  actual writes still disconnect for reconciliation; that safety rule is not
  relaxed.

Retest with a GM and an ordinary account: the ordinary account should see the GM,
and opening the Inn should keep the session alive and show existing bank data.
Verify a deposit/withdrawal and reconnect; no SQL reset or inventory wipe is needed.
