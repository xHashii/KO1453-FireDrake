# Party membership acceptance closure

## Evidence, not a blanket retail-completeness claim

| Area | Automated server evidence | Actual Windows client evidence |
|---|---|---|
| Player visibility | Source-envelope vectors, directional visibility checks | Owner confirmed after a515a46 |
| Invite / accept / kick | Hub rules + TCP scenario | Owner confirmed |
| Roster consistency / outsider isolation | Three TCP members plus fourth outsider | Final UI check pending |
| Party chat | Exact trusted sender/name/channel/message, member-only recipients | Final UI check pending |
| HP/MP | Exact 16-bit vitals fields, updates to all members, no unchanged spam | Final party-panel check pending |
| Death / recovery | Zero HP retains party, recovery updates without rejoin | Final UI check pending |
| Promote | Old/new leader flags, status-100 roster insertion, old leader denied kick | Final UI/control check pending |
| Self leave / rejoin | Removal to all affected members, stale chat denied | Final UI check pending |
| Network/client forced exit | Real TCP EOF, nonleader removal, leader disband | Final UI check pending |
| Zone transition | Session cleanup policy + defensive publication cleanup | Final UI check pending |
| Decline / expiry / max eight / malformed/replay/concurrent permits | Deterministic hub tests | Optional negative UI checks |

The test harness runs the production PartyHub with KoTcpServer/KoSession and real
loopback TCP clients using AA55/55AA packet frames. Fixture-only opcodes publish
trusted vitals/zone snapshots. It does NOT execute the full authenticated GameSession,
client graphics, native UI parser, actual skills, database state or Windows process
termination. Those fixture opcodes exist only in the verification project.

This finishes the automated membership acceptance scenario; native checks are
still explicitly pending, not marked passed based on self-authored byte vectors.

## Gap fixed during review

GameSession removes membership on map-changing travel (not same-map H/Town), but PartyHub.Publish previously
only canceled invitations on a changed zone. A direct zone republish could retain
an established roster/chat route. Publication now also removes membership on zone
change, using the existing leave/disband rules. Tests cover two-member disband and
three-member removal. Cross-zone party retention remains deliberately unsupported.

## Diagnostics

- `[PARTY ACTION]` records accepted/refused requests and sender's leader-first roster.
- `[PARTY VITALS]` records changed current/max HP and MP for established members,
  not unchanged quarter-second publications.
- Bounded `[PLAYER WIRE]` dumps now also include outgoing PARTY packets, allowing
  comparison of actual native UI failures with the automated scenario.
- Existing packet logs distinguish session/character. Party chat content is not
  additionally logged by this change.

## Short final owner check

Prefer three clients (A, B, C). Visible GM opt-in is needed only for GM characters.

1. A invites B. Send party chat in both directions. A third nonmember must not see it.
2. Use MP on B and take nonfatal monster damage. Check B's HP/MP in A's party panel;
   sit/use a potion and check recovery updates. Also recheck the target HP bar
   (the peer-target fix is separate and still needs native confirmation).
3. Add C, promote B. Check the leader indicator/roster; B can kick, A cannot.
4. C leaves, then rejoins. Close C via Alt+F4: the others must remove C once.
5. Close leader B: A must no longer have an active party. Reconnect B: no ghost
   membership. Form again and change zone: cleanup must follow the documented policy.

Send the GameServer log only if a step fails, with the step number. No DB reset,
new map/TBL patch or test-only commands are needed. With just two clients, chat,
vitals, promotion and disconnect can be checked; leaving immediately disbands the
remaining solo member, so use three to check removal while retaining a party.

## Scope boundary / next development

No party XP, loot sharing, party heal/buff targeting, party-wanted board, persistent
parties or cross-zone retention is added. `PartyBbs` remains unsupported. Next is
combat/skill/effect foundations followed by source-backed party reward distribution.
Stair collision is a separate map geometry task, not a party feature.


### Same-map Town correction

The owner's 13:42:42 log shows Home at 13:48:36 producing PARTY 05 for both
members before a same-zone Moradon warp. BeginTravel incorrectly removed the
party unconditionally. Cleanup now compares source and destination zone AFTER
the successful DB save: same-map H/Town and warps retain membership, leadership,
chat and pending invitations; another map still removes membership. Regression
tests cover two-member parties, pending invites and three-member TCP scenarios.
Disconnect, kick, explicit leave/disband and hidden-GM policies are unchanged.
Native H/Town confirmation remains pending after this correction.
