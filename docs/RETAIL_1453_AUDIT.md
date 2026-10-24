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

## Sealing evidence and blockers

- `GameServer/SealHandler.cpp` contains an item-seal handler: upgrade subopcode 8,
  action 1 seal / action 2 unseal, inventory position and password, error codes,
  and a 1000000-coin sealing charge. It explicitly leaves premium eligibility
  unresolved and queues database work; it is not an end-to-end verified reference.
- Its `CharacterSealProcess` (upgrade subopcode 9) is **empty**. A menu option or
  a UI screenshot is not evidence of implemented character-transfer semantics.
- Current C# sealing is not implemented. Do not advertise it as functional or
  consume a scroll/coins/create a ring merely because the menu is now displayed.
- Seal packets may contain security passwords. Their raw payloads are now
  redacted from logs. Do not request users' seal passwords or log them to reverse
  engineer the protocol.

Web research performed October 24, 2026:

1. Historical **October 1, 2008** FAQ (secondary source):
   http://ploytk.blogspot.com/2008/10/knight-character-seal-faq.html
   Describes sealing another character on the same account with a PUS scroll,
   producing a tradable Cypher Ring; one item allowed in the last inventory
   slot; magic-bag items disallowed; unsealing needs a free character slot.
   This is historical evidence, but does **not prove availability/rules in 1453**.
2. Dated **August 18, 2016** change notice/discussion:
   https://steamcommunity.com/app/389430/discussions/0/361798516942634756/
   Says all items must be removed and magic bags prevent sealing. This differs
   from the 2008 one-item exception and must not be silently backported.
3. Later official PUS page:
   http://www.nttgameonline.com/knight/en/news/news_detail/99/1
   Describes a CSS, security code and one-last-slot-item exception. Its web
   publication date does not establish an exact 1453 protocol or ruleset.
4. Modern item-unseal guide:
   https://helpdesk.bynogame.com/en/knight-online/knight-online-item-unseal-make-tradable
   Describes Inn > Seal/Cancel, but modern voucher mechanics are not a verified
   1453 item-seal reference.

Before implementing character transfer, establish the actual target client build
and period-correct protocol/rules. Needed verification includes: seal-code
validation and premium eligibility; CSS/ring item IDs; which character is eligible;
allowed last-slot items/magic bags; clan/king/online-character restrictions;
source/target nation and account-slot constraints; exact request/reply records;
and transaction/replay protection for both character ownership and ring serial.
No invented zero-cost or password-bypass fallback is acceptable.

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
