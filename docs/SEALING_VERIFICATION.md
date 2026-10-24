# Sealing verification and ownership contract

Status: **item lock/unlock backend and source-layout game handler implemented;
character transfer remains disabled**. This is an explicitly approved newer-version
security extension, not a claim of retail 1453 fidelity. Native Windows UI acceptance
still requires a client test.

## Enabled item-lock policy and operation

- Use Inn Hostess -> Seal / Cancel, ordinary bag slots only. Sealing costs
  **1,000,000 coins**, unsealing is free. Unlike later commercial KO, this server
  extension does **not** require premium. Voucher-based unbinding is not implemented.
- Use a separate **eight-digit** code, not the login password. Never send it to
  chat or as a command-line argument. The legacy `TB_USER.strSealPasswd` shared
  default is deliberately ignored. No code is auto-created.
- A trusted operator provisions/resets a player's code on the server machine:

  ```powershell
  dotnet run --project src/KnightOnline.GameServer -- --set-seal-code ACCOUNT_ID
  ```

  Run from the directory containing the server's configured GameServer.ini.
  Two hidden interactive prompts collect the code. Repeated digits and the
  legacy `12345678` default are refused. This command does not start the listener.
  Until a code is provisioned, requests fail without charging or changing items.
  Operator-mediated provisioning/reset is the current administrative workflow,
  not a new self-service account portal. Authenticate the account owner out of
  band before resetting; local DB access is privileged.
- Codes use random 16-byte salt + PBKDF2-SHA256 (210000 iterations), constant-time
  verification, and no plaintext storage. Five wrong-code attempts lock the
  account's seal service for five minutes; reconnect cannot reset that timer.
- Sealing locks the security/account/character/serial identities, rejects ambiguous
  ownership and stale inventory snapshots, and commits the fee and protection row
  in one transaction. Unseal removes exactly one matching owned type-1 record.
  Bound/type-3 and rented items cannot be converted through this operation.
- Inventory bytes/serials/expiration metadata and unknown tail bytes are not
  rewritten by sealing. MYINFO, peer appearance and warehouse-open flags use a
  runtime protection overlay (4 sealed, 8 bound), reloaded from SEALED_ITEMS.
  Ordinary equipped/bag shuffles retain protection. Existing transactional guards
  reject sale, consumption, stacking and warehouse transfers of protected items;
  loot stacking skips protected stacks. Trade, merchant, mail and upgrades remain
  unavailable and must receive equivalent guards when implemented.
- Expected refusals do not write inventory/coins. Unknown database outcomes
  disconnect without retry; reconnect loads the committed state.

## Newer-version behavior references

These establish player-visible behavior, not the native 1453 packet layout:

- NTTGame product descriptions: account Security (Seal) Code, CSS -> stored or
  transferred character, and separate vouchers/premium for bound-item unbinding:
  http://www.nttgameonline.com/knight/en/news/news_detail/99/1
- NTTGame staff FAQ (2016): item protection requires security key; character
  sealing uses CSS and clean inventory:
  https://steamcommunity.com/app/389430/discussions/0/350543738455307459/
- Current illustrated Inn workflow (seal/cancel and item-code confirmation):
  https://helpdesk.bynogame.com/en/knight-online/knight-online-item-unseal-make-tradable
- Character ring workflow (CSS, character selection, code confirmation):
  https://helpdesk.bynogame.com/en/knight-online/how-to-ring-my-character

## Validation

CI covers literal result framing, code policy/hash verification, flag/blob
separation, persisted retry limits, ownership/stale/forged-item rejection,
seal/unseal/replay, reconnect flag restoration, protected mutation refusal,
concurrent requests and SQL rollback after a protection insert. Test in Windows:
provision a code, seal a bag item with enough coins, reconnect, verify the lock,
attempt sale/storage, unseal with wrong then correct code, and confirm no second
fee. No Windows UI test has been performed in the sandbox.

## Established by the supplied source

`GameServer/SealHandler.cpp` routes item seals through ITEM_UPGRADE subopcode 8.
Actions 1 and 2 decode an int16 placeholder (commented as -1), item uint32,
ordinary bag position byte and a two-byte-length password string (1–8 bytes).
The source validates position <28, claimed item identity and nonzero serial.
It defines a 1000000-coin sealing fee and these errors: 0 success, 2 failed,
3 insufficient coins, 4 invalid code, 5 premium-only, 6 failed, 7 too soon.
The C# decoder follows that candidate source shape and checks exact lengths.
Replies follow `DatabaseThread.cpp::ReqSealItem`: `[8, action, result, item:u32, position:u8]`. Its successful database result is **1**, despite the misleading preflight enum constant named SealErrorNone=0.
Malformed packets do not authorize any writes. Request ToString and packet logs
must not expose the password.

`SEALED_ITEMS` has account, character, item ID, item serial and seal-type fields.
Source loading maps seal type 1 to sealed and 3 to bound. Runtime protection overlay and code validation are now implemented for the C#
item-lock path. Premium is deliberately not required by this server extension.
The source-derived shape still needs a native client confirmation; it is not
independent proof of retail rules.

Character sealing uses subopcode 9, but the supplied CharacterSealProcess is
empty. RE-KO explicitly classifies sealing as missing/unverified. No character
request phases, ring-success payloads, item IDs or period eligibility rules have
been invented. Unknown subopcode-9 requests produce a private unavailable notice
without interpreting their bytes as ownership instructions.

## Ownership invariants and remaining character-transfer work

### Item protection review checklist (implemented path)

- Revalidate authenticated account -> exactly one ACCOUNT_CHAR ownership mapping
  -> exactly one USERDATA row in the same committing transaction. Do not accept a
  character/account/serial merely because the client supplied it.
- Lock and CAS current inventory/serials/coins; verify the selected bag item and
  its serial. Reject duplicate/ambiguous seal identities and rented/bound cases
  according to the verified source rules, not a guessed flag conversion.
- Verify the configured seal password. Premium is not required by this extension.
  Do not use a universal default, GM bypass or client appearance authority as an
  authorization source. Do not log secrets. The five-attempt/five-minute retry limit is an explicit server security policy,
  not a claim of a native retail cooldown.
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

For character-to-ring transfer, an actual target-client executable/source or a
reference implementing the native character stages is still required. The
permission to use newer feature semantics does not define the missing wire fields. Do not
request account or seal passwords. Current internet guides describe later
versions and do not establish the missing 1453 character-transfer wire format.
The 2008/2016 rule differences and source references are recorded in
`RETAIL_1453_AUDIT.md`.
