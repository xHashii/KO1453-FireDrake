# 1453 party membership — first native-client slice

Implemented native WIZ_PARTY (0x2F) operations from PartyHandler.cpp / packets.h:
create invitation (1), permit/refuse (2), invite existing group (3), remove/self
leave (4), disband (5), promote leader (28). Server sends source-shaped insertion,
HP/MP, level/class, removal and leader-state packets. HP/MP fields are 16-bit,
as declared by the local reference User.h. Party chat is native channel 3.

## Authority and lifecycle

- No membership until explicit acceptance of a live invitation; 30-second expiry.
- Exactly one outstanding outgoing/incoming invitation per participant. No
  unsolicited permits, duplicate joins, competing invitation theft or ambiguous
  character-name resolution. Maximum eight accepted members.
- Revalidate alive/visible state, level window, nation, same-zone eligibility,
  leader and capacity at acceptance, not just request time.
- Source basic level window: 2/3..1.5x or +/-8. Opposing nations permitted in
  Moradon (21) and Forgotten Temple (55), not elsewhere; Chaos (85) refused.
  Clan/chicken level exceptions are not implemented yet.
- Nonleaders cannot invite, kick others, promote or disband; any member may leave.
  Leader leaving/disconnecting disbands; a two-member party becoming solo disbands.
- Disconnect/inventory fault and **map-changing** travel remove membership and pending invitations.
  H/Town and other same-map warps preserve the party and invitations.
  This conservative travel policy is NOT full retail cross-zone party behavior.
- Death cancels invitations but retains existing membership and updates HP.
- Hidden GMs cannot be invited, invite or remain in a party; +visible opt-in is
  required, consistent with existing hidden-by-default policy.
- Server-only session identities; no DB membership rows or character blob writes.
  Session-ID reuse cannot inherit an invitation or party.
- Immutable player snapshots, one membership lock, delivery only queues packets;
  never acquire another session's game lock or write a DB transaction in the hub.
- Group members receive leader state. Actor snapshots now carry the authoritative
  leader bit too, with revision refresh on leadership changes for existing/late
  viewers; see PARTY_LEADER_UI_STATE.md. Native menu visibility remains unverified.

## What is NOT included

No shared XP/loot, party heals/buffs, party-wanted board, clan integration,
quest-party credit or persistent parties. Combat rewards still follow existing
solo rules. Do not call this full retail party gameplay or economy parity.

## Verification

Automated tests cover native byte vectors, malformed/trailing data, missing and
replayed permits, concurrent duplicate acceptance, capacity, leader authority,
refusal/timeout/death/zone/disconnect eligibility and session ID reuse, private
party-chat routing and HP/MP update layout. Owner confirms native visibility, invitation/acceptance and
kicking after a515a46. Other Windows UI acceptance remains pending.

A four-client TCP/KO-frame harness now checks three-member rosters plus a
nonmember observer, private party chat, HP/MP changes/death/recovery, leader
promotion and old-leader denial, self-leave/rejoin, zone changes, forced client
close and leader disconnect cleanup. See PARTY_ACCEPTANCE.md for exact scope.

## Owner acceptance (two or preferably three clients)

1. Same-zone characters within the level window; visible GMs only (+visible).
2. Invite through native UI; decline once, invite again, accept. Check the roster.
3. Send party chat; verify a third nonmember does not receive it.
4. Add third member, promote, verify old leader cannot kick/disband, then kick.
5. Take damage/use MP/sit to regenerate; check member HP/MP panels update.
6. Leave, logout, reconnect and travel; verify cleanup and no stale membership.
7. Wait >30 seconds before accepting an invitation; confirm no late join.

Do not expect group rewards yet. Record client UI behavior and server logs for
any remaining native layout mismatch before claiming acceptance.


### Same-map Town correction

The owner's 13:42:42 log shows Home at 13:48:36 producing PARTY 05 for both
members before a same-zone Moradon warp. BeginTravel incorrectly removed the
party unconditionally. Cleanup now compares source and destination zone AFTER
the successful DB save: same-map H/Town and warps retain membership, leadership,
chat and pending invitations; another map still removes membership. Regression
tests cover two-member parties, pending invites and three-member TCP scenarios.
Disconnect, kick, explicit leave/disband and hidden-GM policies are unchanged.
Native H/Town confirmation remains pending after this correction.
