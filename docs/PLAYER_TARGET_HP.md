# Target HP for visible peer players

Native acceptance: owner confirms a515a46 restores mutual player rendering,
party invitation and kicking. Remaining party lifecycle/UI tests are not implied.

The supplied 13:29:41 GameServer log repeatedly shows Hashii requesting
TARGET_HP payload `01 00 00` / `01 00 01` (player ID 1, echo 0/1) without replies.
The handler supported self and NPC targets only; other player IDs fell through
to the NPC lookup. This is not another player-envelope or NPC_HP-width defect.

PlayerPresence now carries immutable current/max HP. TargetHealth reads the
snapshot under the hub lock and returns the existing source-shaped 13-byte
TARGET_HP reply. No session callback, other-session lock or database query is
needed. Same-zone/range/directional visibility and hidden-state checks apply.
Dead, missing, disconnected or uninitialized targets get no reply, matching the
source's dead-player refusal. NPC and self handlers remain unchanged; no party
membership is needed to see a visible player's HP. HP values update with player
presence publication (world tick <=250ms and ordinary packet processing).

Tests cover literal peer HP bytes, both echo flags, changed HP and negative
hidden/distant/other-zone/dead/disconnected requests. The existing million-HP
monster vector remains unchanged. Target HP replies are included in bounded
PLAYER WIRE diagnostics. Native HP-fill acceptance still requires retesting.

Test: select the other visible player, check a filled bar; damage/heal that player
and reselect/check polling updates. Repeat before/after joining and leaving a party.
Check an NPC/monster target still works. Do not enable PvP just to test HP: ordinary
monster damage/potions can provide the HP change.

Next milestone after remaining party UI/lifecycle and shared-AI acceptance:
combat/skill/effect foundations, then source-backed party XP/loot sharing.
Party-wanted (PartyBbs in this log) is still unsupported, not a failed party invite.
Stair collision remains a separate map-geometry issue, not fixed here.
