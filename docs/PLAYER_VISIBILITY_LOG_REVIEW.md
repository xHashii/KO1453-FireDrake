# 2026-10-28 native visibility log review

Reviewed the supplied GameServer 13:16:21 and LoginServer 13:16:17 logs. Private
uploads are not committed. This is evidence review, NOT a claim of a solved
native rendering defect. The preceding refresh change did not resolve the issue.

## Confirmed facts

- GameServer identifies build d2a43922, so the refresh changes were running.
- Hashi entered zone 21 at 821.50,541.10; Hashii at 822.30,540.50: distance 1.00
  world unit, well within the 120-unit peer radius. Both finished GAMESTART 2.
- At 13:17:12, server logged player IN (121 bytes), OUT and IN plus a region list.
  Client requested ID 1 (`01000100`) and server replied REQ_USERIN (122 bytes).
- At 13:17:23 Hashii's `+visible` was accepted. Both 121/122-byte actor record
  lengths appear in outgoing IN logs; another request for ID 1 got a reply.
  This is not adequately explained by simply forgetting the visibility command.
- No world-refresh exception or idle disconnect appears before the test ends.
  The 13:18:04 / 13:18:14 transport-reset messages occur at client closure;
  they do not establish an earlier visibility failure.
- LoginServer authenticated both accounts. Its login-socket closures do not imply
  that the separate game sessions were missing. This is a game-world issue.
- Object-proxy warnings 1019–1022 concern map objects, not player discovery.

## What cannot be inferred from those logs

Outgoing discovery records were logged with only packet lengths and the shared
127.0.0.1 address. They did not identify the destination session, exact record
bytes or public appearance fields. A log saying IN was sent does not prove the
client accepted/rendered it. The logs therefore cannot identify the defective
field or attribute the issue to party logic versus another protocol change.

History review: the original player foundation was 0f93025; native envelopes
changed in 9427470 (byte action / no requested-record zero prefix). These predate
the party slice 083d15c. Party added its own presence publication but did not
rewrite PlayerPackets.Info or PlayerHub's actor record format. The exact Moradon
import also did not change that wire layout. Temporal association alone is not
proof of causation; do not change envelopes again without useful capture data.

## Added diagnostics

- All game RX/TX lines carry `sid` and character, distinguishing both localhost
  clients. Existing payload redaction on incoming login data remains in place.
- Discovery TX (USER_INOUT, REQ_USERIN, REGIONCHANGE) has `[PLAYER WIRE]`, exact
  hex up to 1024 bytes, truncation flag and enqueue success. No passwords, full
  MYINFO or character/account blobs are added to these dumps. Successful queueing
  is explicitly NOT logged as successful client parsing.
- `[PLAYER PRESENCE GATE]` logs changes in connected/in-game/travel/fault/loaded/
  zone/authority/GM-opt-in eligibility, not every timer tick or movement packet.
- `[PLAYER VISIBILITY]` at world-entry, visibility commands, requested details and
  disconnect includes authority/public visibility state, race/class, actor IDs,
  positions, distance, hidden/range/zone reasons and both directional known sets.
  Candidate detail is bounded to 16; total count/truncation is explicit.
- `[PLAYER REQUEST]` records bounded requested IDs or exact validation rejection.

No packet format, GM policy, party rules, map selection or character data is
changed by this diagnostic patch.

## Next capture

Restart the updated GameServer, connect both characters near each other, use
`+visible` where applicable, move/rotate each once, and capture about 10 seconds.
Send the new GameServer log. LoginServer log is not needed again for this issue.
Do not reset data, reinstall the map, or change client TBLs for this test. The
capture will distinguish filtering/eligibility, failed queueing and an apparently
valid discovery exchange requiring closer native payload analysis.
