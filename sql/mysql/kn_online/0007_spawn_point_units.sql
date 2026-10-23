-- 0006 (and CREATE_NEW_CHAR until now) wrote ZONE_INFO init points divided by
-- 10, but DBAgent::LoadUserData reads USERDATA.PX/PZ/PY as world * 100 - the
-- same unit ZONE_INFO stores - and divides by 100 for the float world
-- position; the WIZ_SEL_CHAR/WIZ_MYINFO answers then send stored / 10
-- (world * 10). Re-spawn every character at its nation's home zone init point
-- in the right unit. Nobody can move yet, so no real position is lost.
UPDATE `USERDATA` u
JOIN `ZONE_INFO` z ON z.ZoneNo = u.Nation AND z.ServerNo = 1
SET u.PX = z.InitX, u.PZ = z.InitZ, u.PY = z.InitY
WHERE u.PX <> z.InitX OR u.PZ <> z.InitZ OR u.PY <> z.InitY;
