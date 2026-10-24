# Sealing verification and ownership contract

Status: **not enabled**. This increment adds a source-layout decoder and explicit
safe refusal, not a completed seal/unseal service. Do not describe showing the
native Inn menu as completing its seal services.

## Established by the supplied source

`GameServer/SealHandler.cpp` routes item seals through ITEM_UPGRADE subopcode 8.
Actions 1 and 2 decode an int16 placeholder (commented as -1), item uint32,
ordinary bag position byte and a two-byte-length password string (1–8 bytes).
The source validates position <28, claimed item identity and nonzero serial.
It defines a 1000000-coin sealing fee and these errors: 0 success, 2 failed,
3 insufficient coins, 4 invalid code, 5 premium-only, 6 failed, 7 too soon.
The C# decoder follows that candidate source shape and checks exact lengths.
A valid request currently receives `[8, action, 2]`, never a success or fee.
Malformed packets do not authorize any writes. Request ToString and packet logs
must not expose the password.

`SEALED_ITEMS` has account, character, item ID, item serial and seal-type fields.
Source loading maps seal type 1 to sealed and 3 to bound. Full runtime flag
propagation, security-code validation and premium eligibility are not complete
in the C# server. Source comments themselves leave premium policy unresolved.
The source-derived shape still needs a native client confirmation; it is not
independent proof of retail rules.

Character sealing uses subopcode 9, but the supplied CharacterSealProcess is
empty. RE-KO explicitly classifies sealing as missing/unverified. No character
request phases, ring-success payloads, item IDs or period eligibility rules have
been invented. Unknown subopcode-9 requests produce a private unavailable notice
without interpreting their bytes as ownership instructions.

## Required before enabling writes

### Item protection

- Revalidate authenticated account -> exactly one ACCOUNT_CHAR ownership mapping
  -> exactly one USERDATA row in the same committing transaction. Do not accept a
  character/account/serial merely because the client supplied it.
- Lock and CAS current inventory/serials/coins; verify the selected bag item and
  its serial. Reject duplicate/ambiguous seal identities and rented/bound cases
  according to the verified source rules, not a guessed flag conversion.
- Verify the configured seal password and period-correct active premium service.
  Do not use a universal default, GM bypass or client appearance authority as an
  authorization source. Do not log secrets. Define retry/lockout behavior from
  evidence rather than inventing a retail cooldown.
- Commit protection record and applicable fee together. Unseal must remove only
  the authenticated account's matching protection record after code validation;
  it must not delete arbitrary matching item IDs or another owner's protection.
- Propagate authoritative flags through MYINFO, inventory changes, warehouse,
  peer appearances and all item-transfer paths. Persistence alone is insufficient
  if a reconnect or packet still exposes the item as ordinary.

### Character transfer

- Identify and verify native preview/selection/confirm request and response
  stages, CSS/ring IDs, code checks, nation/slot constraints, allowed inventory
  exception (if any), magic bags and clan/king restrictions for the target build.
- Lock and validate both carrier and target character ownership; the target must
  not be the active character or a character in use by another session/editor.
  Preserve existing edit-lease/CAS safety; never replace it with a read-only
  preflight check followed by an unguarded write.
- Link the ring's unique serial to a durable sealed-character record. Seal must
  detach exactly the eligible account slot and consume the correct CSS/create
  the ring atomically, or do none of these.
- Unseal must claim that serial exactly once, revalidate the receiving account's
  nation and free slot, consume exactly that ring and attach ownership atomically.
  Replayed requests, stale snapshots, duplicate rows, full accounts, wrong nations
  and forged serials must not transfer a character or charge a fee.
- Never delete/recreate profile data to simulate transfer. Preserve character
  identity/progression/quest state and explicitly handle existing ownership-linked
  metadata. A rolled-back transfer must not leave a ring without a character or
  a character owned by two accounts.
- Disconnect/reload on uncertain commit, as with inventory/warehouse operations.
  Test concurrent seals/unseals and SQL failure after each transaction stage.

## Remaining evidence needed

An actual target-client executable/source, or a reference with verified native
seal/unseal parsing and corresponding period rules, is still required. Do not
request account or seal passwords. Current internet guides describe later
versions and do not establish the missing 1453 character-transfer wire format.
The 2008/2016 rule differences and source references are recorded in
`RETAIL_1453_AUDIT.md`.
