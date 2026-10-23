-- Characters created before the creation path learned to set a spawn point
-- sit at Zone 0 / 0,0,0: zone 0 has no ZONE_INFO row, so WIZ_SEL_CHAR can
-- never place them and the client cannot enter the world. Move every such
-- character to its nation's home zone init point, the same place CREATE_NEW_CHAR
-- now spawns new characters. ZONE_INFO stores world * 100 (ZoneInfoSet divides
-- by 100); USERDATA and the WIZ_SEL_CHAR answer carry world * 10, hence / 10.
UPDATE `USERDATA` u
JOIN `ZONE_INFO` z ON z.ZoneNo = u.Nation AND z.ServerNo = 1
SET u.Zone = z.ZoneNo, u.PX = z.InitX / 10, u.PZ = z.InitZ / 10, u.PY = z.InitY / 10
WHERE u.Zone = 0;
