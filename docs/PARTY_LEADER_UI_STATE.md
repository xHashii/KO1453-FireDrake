# Party leader state and native context menus

Owner reports no promote-leader option in either actor or party-frame context menu.
The server already supports native PARTY action 28 with leader/member checks, but
that does not prove the installed client exposes an action-28 menu item.

## Confirmed server defect corrected

`PlayerPackets.Info` always wrote zero in the source `m_bPartyLeader` field, even
for the authoritative leader. Party state-change messages said leader while IN/
requested-user records said nonleader. Actor refreshes, equipment changes, Town
warps and late-viewer discovery could therefore introduce contradictory state.

- PartyHub.IsLeader reads the authoritative leader-first group under its lock.
- PlayerAppearance now carries that bool and writes the existing one-byte field.
- GameSession tracks leadership changes, reasserts self STATE_CHANGE type 6,
  increments appearance revision and republishes the correct actor info. Existing
  viewers get the normal OUT/IN refresh and new/requested viewers get the same flag.
- Promotion/disband clears the old leader flag. No client-controlled role is trusted.
- Packet lengths/envelopes and other fields are unchanged. Ordinary/nonleader
  golden vectors remain byte-identical. No map/TBL/database changes are needed.
- `[PARTY LEADER SYNC]` identifies each actual transition and appearance revision.

Tests cover authoritative role after join/promotion/disband, exact field offset,
existing/late/requested viewers and clearing the old leader snapshot. Existing TCP
promotion/authorization tests remain in place.

## Native acceptance still required

Form a party, then as its CURRENT leader right-click the OTHER member's actor and
party-frame entry. Do not look for promotion on yourself or as a nonleader. Check
again after Town and after promoting another member: controls must follow the new
leader, not the original inviter. Capture the GameServer log if the option remains
absent, including PARTY LEADER SYNC/STATE_CHANGE/player records.

This change corrects inconsistent server state. It does NOT edit client menus or
prove that the missing option was caused by this field. A server packet cannot add
a menu entry that the installed 1453 executable/UI does not implement. If it still
is not offered with correct leader state, inspect the exact deployed executable
and its relevant UI/context-menu resources; newer-client menu resources must not
be substituted blindly. No chat-command workaround is presented as a menu fix.
