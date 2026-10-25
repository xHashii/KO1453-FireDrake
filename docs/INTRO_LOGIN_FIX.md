# Repeated login intro: startup correction

## Defects addressed

1. The server sent WIZ_QUEST before WIZ_MYINFO. The supplied C++ does this,
   but the native-1453 reference sends MYINFO, start acknowledgement, then the
   saved quest snapshot. Client initialization must precede restored quest state.
2. Repeated GAMESTART stage 1 resent the entire bootstrap, including MYINFO and
   quest state. It now only resends the start acknowledgement. Stage 2 cannot
   activate the world before stage 1 and only activates once.
3. The previous flag workaround only applied above level 1, preserved reserved
   quest-500 states other than 1, and the sender could silently seed missing
   quest 500 as state 1. That continually offers an unsupported intro path.

Evidence: RE-KO pinned `217183d1771ac54a0e09de0e00a859f8af7c5538`,
`crates/reko-server/src/lib.rs` GAMESTART handler, `quests.rs::snapshot`, and
`docs/re/protocol.md` World entry. This is source evidence of a startup defect,
not a capture of the owner's installed executable or proof of its precise movie
callback. No new packet pretending to be a verified intro-completion callback
has been invented.

## Explicit temporary policy

The full tutorial/quest engine is not implemented. Until it is, **suppress the
unsupported introductory tutorial at every level**, rather than promise a
working once-only tutorial. Reserved quest 500 is persisted as state 2 before
world entry and sent after MYINFO/start ACK. No other quest's state is completed,
reset or deleted. Inventory, items/serials, coins, skills and vitals are untouched.
This is not proof that the character actually played/completed a tutorial.
When implementing the real tutorial, replace this policy with verified
first-entry/progress/completion handling; do not mass-reset existing characters.

Quest count/duplicate corruption is refused without rewriting the saved blob.
Updates lock a unique character row, preserve padding and use the existing
validated ownership/edit-lease login path. There is no new migration or manual
SQL step. Existing completed flags need no DB update: the packet ordering fix
still applies to them.

## Regression coverage

- Initial, repeated and late stage 1; premature/repeated stage 2; unknown stages.
- Exact native quest snapshot bytes, sorted records and completed flag.
- Reserved states 0/1/2/3/4, missing flag, idempotence, unrelated quests/padding.
- Refusal of malformed counts/duplicate IDs and missing unpersisted intro state.
- MariaDB tests through the real public LoadUserDataAsync path using the shipped
  USERDATA DDL: level-one login, reconnect in a new repository, boosted/missing
  flag, blob/vital/coin preservation and corrupt-count refusal.

## Native-client acceptance still required

Rebuild/restart GameServer, then fully log out and back in twice with the same
character. Repeat with another existing character and, if available, a fresh
level-one character. The intro should not reopen; movement, inventory and NPC
UI should remain usable. Check the log for:

- `[ INTRO POLICY ]`: only when the persisted reserved flag needed repair.
- `[ INTRO SNAPSHOT ]`: confirms flag 500=2 was sent after MYINFO/start ACK.

A subsequent normal login need not print POLICY again; SNAPSHOT is the relevant
send confirmation. Do not call the issue native-client verified until this test
succeeds. If the intro persists, retain that startup log and identify the actual
client build/movie UI before changing additional quest IDs or flags.
