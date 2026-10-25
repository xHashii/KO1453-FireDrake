# Experimental 1534 handshake test

This is the first compatibility increment, NOT certified 1534 gameplay/world
entry. 1453 remains the default; no schemas, data, inventories or map files change.
Reference: ForcePower/KODevelopers-1534 cd3c07dd310baee59702c78ca165c177464ec161,
LoginSession.cpp HandleVersion/HandleServerlist and GameServer/LoginHandler.cpp.

Implemented: explicit validated 1453/1534 per-instance profiles; 1534 advertisement
and patch cap; 1534 uint16 server-list echo; configured GameServer handshake version
and existing key exchange. Authentication and encryption checks remain intact.
The rest of character-entry/gameplay packets have NOT been certified for 1534.

## Owner steps

1. Back up both active INI files. Prefer a private copy of the deployed server and
   a copy of the game database; no other players during this test. Keep an untouched
   1453 installation and use a trusted, unmodified 1534 executable/resources.
2. Rebuild/publish latest code; do not overwrite configured INIs with samples.
3. In active LogInServer.ini [SETTINGS], set:

   PROTOCOL_VERSION=1534
   ADVERTISE_VERSION=1534

4. In active GameServer.ini [SETTINGS], set:

   PROTOCOL_VERSION=1534

5. Restart both servers. Logs must show LoginServer profile=1534 advertised=1534
   and GameServer profile=1534. If not, wrong binary/config is being launched.
6. Connect once. Supply LoginServer log from connection through close. The
   [ VERSION WIRE ] line includes ONLY version-request bytes (up to 2 bytes) and
   version-reply bytes; 1534 reply must be FE05. No account/password payloads are
   logged by this diagnostic. Never enable raw login credential captures.
7. If server list appears, continue with a disposable existing test character.
   Supply GameServer log if loading fails; do not repeatedly create/modify real
   characters or use shops/storage to diagnose unsupported world packets.

## Rollback

Stop both servers. Restore backed-up INIs, or set PROTOCOL_VERSION=1453 on both
and ADVERTISE_VERSION=1453 on LoginServer. Restart and use original 1453 client.
No SQL rollback is necessary because the feature changes no database schema/data.
Normal login/gameplay can still write account/session/character state; this is
why a copied database/test character is preferred.

## Interpretation

Immediate reset with FE05: known server-list echo fix has not been reached.
Investigate actual client build/version behavior and framed version exchange;
do not claim the early reset was fixed by this change. No bypass of checks or
client patching is part of the implementation. Character selection/world entry
failures require additional independently verified compatibility work.
