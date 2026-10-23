-- Characters created before the creation path learned to set a spawn point
-- sit at Zone 0 / 0,0,0: zone 0 has no ZONE_INFO row, so WIZ_SEL_CHAR can
-- never place them and the client cannot enter the world. Move every such
-- character to its nation's home zone init point (ZONE_INFO), the same place
-- CREATE_NEW_CHAR now spawns new characters.
UPDATE `USERDATA` u
JOIN `ZONE_INFO` z ON z.ZoneNo = u.Nation AND z.ServerNo = 1
SET u.Zone = z.ZoneNo, u.PX = z.InitX, u.PZ = z.InitZ, u.PY = z.InitY
WHERE u.Zone = 0;
