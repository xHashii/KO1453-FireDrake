# Native player refresh and exit-time position saving

## Player visibility

The hub previously treated a queued IN as proof the native client retained the
actor. Once it marked the peer visible, an unchanged publish (including repeating
`+visible`) sent nothing. A discarded loading-time record/stale client actor
could therefore remain missing while the server believed discovery was complete.

World-entry completion and explicit GM visibility commands now reannounce visible
peers in BOTH directions with native OUT/IN and resend the invoking client's region
list. This is event-driven, not continuous actor churn. Existing hidden/range/zone
filters still apply and disconnects cannot be resurrected by refresh.

The screenshot alone does not prove which client's actor was discarded. Both
characters being GMs is also important: hidden-by-default remains intentional.
**Use `+visible` in BOTH clients**, including after reconnect. One visible GM does
not reveal a second GM who is still hidden. No authority/database change is made.

Test with two nearby clients: both opt in, see each other, move/rotate/sit, repeat
`+visible` without duplicate/ghost actors, hide one and check directional removal,
then reconnect/opt in again. Ordinary players need no visibility command. Test
party membership only after confirming actors are visible. Native acceptance of
this refresh remains pending; packet and hub regressions do not certify rendering.

## Exit-time persistence

Removed the 10-second movement/position database checkpoint from the 250ms world
visibility timer. Position is saved by the existing game-state-locked final
`OnDisconnected` handler, followed by HP/MP saving, before releasing the character
edit lease. The server awaits session cleanup before graceful shutdown completes.

- Client normal exit, Alt+F4, client crash/kill: save when TCP closure is detected.
- Server kick/disconnect or session exception: same final handler.
- Network loss without TCP EOF: game connections now use the existing source-style
  30-second valid-packet idle timeout, swept every 5 seconds. Saving follows timeout
  detection, not the instant the cable is removed. Normal clients must keep sending
  their native heartbeat packets while stationary.
- Ctrl+C and service-manager SIGTERM: orderly server shutdown waits for final saves.
- A hard server kill, runtime abort, machine crash or power failure CANNOT reliably
  run exit handlers. With periodic position saving removed, latest movement may be
  lost in these cases. This is not a claim of crash-proof logout persistence.
- Failed/stale DB writes are logged rather than overwriting concurrent state.

Inventory, purchases, loot/rewards, skill changes and vitals-consuming transactions
remain immediate and atomic. Travel/respawn position changes remain transactional;
they are not postponed until logout. Timer regeneration remains in memory until
its existing save/transaction paths. No whole-character delayed snapshot is added.

Test: walk to a distinctive position, wait >10 seconds (no POSITION SAVE log), then
exit/reconnect. Repeat Alt+F4 and client process kill; reconnect at the last accepted
position. Test Ctrl+C with two connected characters and check both final save logs.
For cable-loss tests allow 30–35 seconds after the last valid packet before retrying.
Check a stationary client stays connected beyond this window with native heartbeats.
Check purchases/Inn transfers and XP remain durable regardless of exit path.

Automated regressions cover bilateral refresh, repeated refresh, hidden actors,
region discovery, disconnected-peer exclusion, TCP EOF, idle disconnect and server
stop awaiting callbacks. Hard server crash recovery is deliberately not promised.
