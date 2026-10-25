# Warehouse deposit diagnosis and next skills increment

The 2026-10-25 03:29 client capture proves storage opened: a 3270-byte snapshot
was sent. Its sole shown deposit is body `0219352FB63F3605061601000000`:
action 2, Inn 13593, item **910145071**, page 5, bag slot 6, bank slot 22, count 1.
The source ITEM row names this `Two-handed Sword of <selfname>(+11)`.

`GameServer/GameDefine.h` defines ITEM_NO_TRADE=900000001 as cannot be traded,
sold or stored; `ItemHandler.cpp::WarehouseProcess` checks it on deposit.
Therefore this captured request is a **deliberate restricted-personal-item
refusal**, not evidence that ordinary item deposits fail. Preserve that retail
rule rather than globally allowing personal/quest items to move between accounts.

Changes:
- Explain malformed, unavailable-Inn, missing-session, metadata and proposal
  failures in private chat and logs instead of silently sending a 2-byte refusal.
- Keep the no-storage ID restriction on **deposit**, not ordinary bag rearrangement
  while using the warehouse. Allow recovery/rearrangement of already-stored legacy
  items subject to the existing transactional protection checks.
- Exact captured restricted-item vector and rule regressions; ordinary-item
  captured-shaped deposit -> metadata -> proposal -> atomic save -> fresh bank
  load -> withdrawal tests. Preserve serials/counts, opaque tails and balances.

No personal-item exception or custom storage permission was enabled. Test with
an ordinary shop weapon/potion, then reconnect and withdraw it. This capture alone
cannot certify the graphical transfer for every item; if an ordinary item fails,
the new reason logging will identify the next condition without another guess.

## Next step: first healing skill family

Following the agreed combat/skills milestone, add a bounded next path rather
than jump straight to party/events: source-defined immediate **self healing**.
Load MAGIC moral 1/2 plus pure TYPE3 positive HP effect, no item/HP cost,
secondary/periodic/AoE effects. Moral 2 permits self, but this increment explicitly
refuses other-player targets until friendly recipient/zone rules are implemented.

Actual source Tiny healing112001 supplies 15 HP, 5 MP and a 1900ms cast.
Validate class/tree/level, living state, caster/self target, MP, cast completion,
movement interruption and cooldown. Full-HP/dead targets fail without payment.
Clamp healing to max HP. HP gain, MP deduction and cooldown share the existing
row-locked transaction; never enter the potion inventory path. Tests use the
actual source MAGIC/TYPE3 rows and verify HP/MP persistence and unchanged items/
coins. Movement/travel already cancels the shared cast state. This does not add
resurrection, HoT, party healing, every buff, or a complete priest skill tree.

The owner confirmed the prior Sprint and Inn-open changes work; intro suppression
also remains confirmed and unchanged. The next client acceptance should cover
ordinary warehouse transfers and a learned matching-class heal, followed by
friendly-player targeting/effect infrastructure and then parties.
