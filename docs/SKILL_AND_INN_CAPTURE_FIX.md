# Sprint routing and Inn storage: captured-client regression

Evidence: owner-supplied GameServer log dated 2026-10-25, session beginning
03:15:44. Raw logs remain outside the repository; only necessary packet vectors
are included here/tests. Intro suppression is unchanged and was confirmed working
by the owner before this change.

## Inn

The log records `45` body `01 19 35`: open storage at NPC 13593. The old parser
accepted ONLY body `01`, rejected the captured explicit-NPC open and sent a
2-byte failure instead of a storage snapshot. The earlier "opened" message
actually referred to choosing the NPC/service menu, not loading storage.

Open now accepts either exact form; explicit NPC IDs must match the selected Inn
and may not be zero. Both forms still require the current friendly, live,
in-range type-31 NPC in the same zone. Malformed/spoofed requests cannot authorize
opening or fees. After successful loading/fee handling, a separate WAREHOUSE OPEN
log records the sent 3270-byte body (192 slots), NPC and balance. That log means
"snapshot sent", not proof that the client's window rendered. Existing CAS,
protection, account ownership, transfer and uncertain-commit handling remain.

## Skills

The capture is magic body `03 B1240300 0100 0100` plus six zero shorts: skill
**206001**, caster 1, target 1. The shipped MAGIC row identifies Sprint (self,
MAGIC_TYPE4 category 6); its TYPE4 row specifies **5 MP, ten seconds, 150% speed**.
It is not an HP/MP consumable. Previously the self-target check unconditionally
fell through to UsePotion, producing the reported POTION REJECT.

Magic is now parsed once by common MagicPackets framing, then resolved through
an authoritative consumable definition or the skill handler. Unknown/unsupported
self spells fail as SKILL REJECT, never an inferred potion. Actual HP/MP potions
retain their inventory/serial/protection and transactional consumption checks.

Implemented next skill slice: pure beneficial self-speed TYPE4/category-6 effects
with source-defined class/level/tree, cost, cast/recast, duration and speed. A
strict loader refuses mixed/secondary effects, other morals, item costs and
unsupported stat changes rather than silently dropping them. Sprint requires no
consumable. MP and persistent cooldown commit together; repeat/active requests
cannot stack the speed or spend a second cost. The server tracks the timed effect
and sends source-shaped duration/speed fields, then category-6 removal on expiry,
death or travel. Disconnect clears the transient effect; it is not saved/restored
on reconnect, but already spent MP/cooldown persists. Hidden-GM visibility filtering
is used for effect broadcasts. Existing global movement-speed enforcement is
still a separate missing system; this patch does not claim anti-speedhack parity.

Other skill families (party buffs, heals, attack combos, DoT/AoE, stealth, etc.)
are still incomplete. This is the captured Sprint fix and routing correction,
not a claim that every class skill now works.

## Tests and acceptance

Literal tests cover the exact captured open and Sprint packets, full bank reply
length, explicit/implicit authorization, spoof/truncation/trailing-byte refusal,
TYPE4 duration/speed/removal layout and deterministic effect expiry/clear.
MariaDB tests load actual source MAGIC/TYPE4 rows, prove Sprint is not a potion,
check MP/cooldown/reconnect behavior and preserve item/serial/coin snapshots.
Existing attack/potion/warehouse regression suites remain enabled.

Client check after rebuild/restart:
1. Inn -> Access Storage: see all eight pages and a WAREHOUSE OPEN snapshot log.
2. Deposit/withdraw an item, reconnect and verify it and coins.
3. Cast Sprint on the matching class: expect SKILL (not POTION REJECT), 5 MP spent,
   speed effect for ten seconds, then normal speed; repeat before expiry must not
   stack/charge again. Verify cooldown after reconnect.
4. Use a real HP/MP potion while injured/depleted: consumption still works.
5. Check death/travel cancellation. No native Windows UI test ran in the sandbox.
