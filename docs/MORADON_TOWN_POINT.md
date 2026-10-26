# Moradon Town point

Owner-confirmed minimap position: **X=816, Z=543**, flat plaza between the lower
stairs and the nation teleport gates. `/town`, `+town` and native Home/Town
requests share GameSession.Town and now select this point in expanded Moradon
(zone 21) for both nations, regardless of old START_POSITION values.

Database/internal destination units: PX=81600, PZ=54300. Wire movement units:
X=8160, Z=5430. BeginTravel calculates Y from the active server terrain, validates
bounds and persists the position through the existing transaction. Half-HP,
living/travel-state restrictions remain. No inventory, coins, NPC placement,
respawn/start rows or map filename is migrated by this change. Other travel
commands and zones retain their configured destinations.

The default repository Moradon is only 512 units wide and cannot contain this
point. For that legacy map, Town retains its database-authored destination.
For the owner's selected expanded map, the server must already load the matching
larger SMD. The `[ TOWN ]` log prints the actual destination and loaded map extent;
if it reports the old coordinates/512, check the live ZONE_INFO reference and
actual deployed data/map directory rather than forcing an out-of-bounds warp.

Deploy current GameServer and test `/town` from elsewhere in zone 21 with >=half
HP. Confirm HUD coordinates 816,543 and flat-ground landing; reconnect to verify
position persistence. This change does not certify stair collision or client/server
height alignment. Native-client acceptance remains pending.

GM turbo is a client input feature: the inspected Open-KO GameProcMain reference
uses hold-Q for AUTHORITY_MANAGER with chat unfocused. Existing MYINFO sends real
owner authority. No invented server-side held-key detector or universal speed
bypass is added here; exact behavior depends on the user's executable.

## Follow-up fix: native Town still selected old coordinates

The prior bounds fallback left Town on the old point when the server still loaded
512-unit Moradon, even though the owner's client displayed the expanded map.
Now the selected larger SMD is packaged as `data/map/moradon_expanded.smd` and
copied into build/publish output. Its source is the owner's previously supplied
map (SHA256 ceedfdcf0a77519f498c8db903903e775cf8053bab5fc53e7732e32999da42a4).
No alternate client/protocol support is reintroduced.

Migration 0011 changes ONLY server-1 zone-21's exact legacy filename
`moradon_20060124.smd` to `moradon_expanded.smd`. GameServer startup also applies
the same guarded update when LoginServer migration is disabled. Custom map names
are preserved. Restart is required because WorldCatalog caches maps. This fixes
the prerequisite for the already implemented 816,543 Town rule. Native H-menu
Home and chat Town both dispatch to the same Town method.

START_POSITION, NPC spawn coordinates, ZONE_INFO init coordinates, existing
character positions/items and other zones are not rewritten. However, loading
the new map changes terrain-derived NPC heights and the available warp data;
those are inherent map differences, not stair-collision repair. The old SMD is
retained for rollback, not deleted or overwritten.

Update/rebuild/restart GameServer. Expect the `[ WORLD ]` zone-21 warp count to
be 14 and `[ TOWN ]` to show X=816 Z=543 mapExtent=1024. No manual SQL is required
for the exact legacy reference. If the destination remains old, provide the Town
log: a preserved custom map or a stale binary/runtime asset may be involved.

Rollback: stop server, copy original SMD to a distinct custom rollback filename
(e.g. moradon_legacy_restore.smd), point only server-1 zone-21 at that filename,
then restart. Using a custom name prevents the exact-legacy migration from
immediately selecting the expanded map again. This changes no saved positions;
use a disposable test character and return to safe ground before rollback.
