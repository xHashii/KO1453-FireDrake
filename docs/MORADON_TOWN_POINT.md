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
