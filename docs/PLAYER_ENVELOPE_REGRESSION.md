# Restore source player discovery envelopes

## Evidence from the 13:24:03 owner capture

The diagnostic build b69461a was running. Hashi is authority 1 (ordinary), not a
hidden GM. Hashii is authority 0; after the accepted `+visible` at 13:25:03 both
sessions report `hidden=False`, `visible=1` and both directional known sets true.
Distance is about six units. Both sides queue their actor records successfully.

This rules out the previous suggestion that the issue is simply two hidden GMs
or a missed hub announcement. Chat is a separate opcode and does not verify that
the client can consume actor discovery records. The earlier refresh did not fix it.

The actual captured old IN prefix for Hashi was `01 01 00 05 ...` (byte action 1,
ushort actor ID 1, byte-length name 5). Requested details were `01 00 01 00 05 ...`
(ushort count 1, ushort actor ID 1, name length). These match the substitutions in
9427470, not the supplied original server. No protocol version was negotiated to
justify switching those envelopes.

## Source/history comparison and correction

- `GameServer/CharacterMovementHandler.cpp`, `CUser::GetInOut`: explicitly writes
  `uint16(bType)` then actor ID. Restored **two-byte action** for both IN and OUT.
- `GameServer/User.cpp`, `CUser::RequestUserIn`: explicitly writes `uint8(0)` before
  each socket ID and GetUserInfo. Restored **per-record zero marker**.
- `GameServer/GameServerDlg.cpp`, region user-detail builder: same marker.
- 0f93025 through the pre-9427470 player hub had these source envelopes. 9427470
  changed them based on a different reference. This predates party membership;
  the Moradon actor import did not introduce these packet-format substitutions.

Expected restored Hashi IN prefix: `01 00 01 00 05 ...`.
Expected requested-details prefix: `01 00 00 01 00 05 ...`.
Expected OUT for ID 1: `02 00 01 00`.

The player-info BODY, region-ID list, positions, equipment, IDs, visibility policy,
chat and party code are unchanged. The diagnostic bytes and eligibility logs remain.
This is a targeted restoration of an identifiable protocol regression, not an
asset/map fix or a claim that changing arbitrary payload fields is safe.

## Verification and remaining uncertainty

Independent source-shaped readers and high/nontrivial actor IDs verify action
width, marker placement and byte-length names. Negative tests show how the removed
bytes shift those fields. Tests cover IN, OUT, requested details, membership,
refresh and hidden-state enforcement.

The capture does not expose the installed client's parser. Restoring the original
source envelopes is the specific compatibility correction to test, NOT proof of
successful native rendering. Native retest is required before closing this issue.
Do not call automated packet tests confirmation that the owner can see players.

After updating/restarting GameServer, reconnect both clients. Hashi needs no GM
command; Hashii needs `+visible`. Check both actors, movement and then party invite.
If still missing, retain the new GameServer log: restored prefixes can then be
checked and the unchanged actor body investigated rather than repeating toggles.
Do not change the map/TBLs, reset characters or change account authority.
