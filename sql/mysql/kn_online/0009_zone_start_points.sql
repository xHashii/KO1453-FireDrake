-- The dump contains placeholder 10,10 init points for many maps, and
-- homeland coordinates outside the 1024x1024 Eslant maps. Replace only
-- those known shipped values using START_POSITION (world units -> stored *100).
-- Shared maps have nation-specific travel starts; InitX/Z is just a fallback.
-- Custom/non-placeholder init points and all zone IDs/map names are preserved.
UPDATE `ZONE_INFO` z
JOIN `START_POSITION` s ON s.ZoneID = z.ZoneNo
SET z.InitX = IF(z.ZoneNo IN (2, 12), s.sElmoradX, s.sKarusX) * 100,
    z.InitZ = IF(z.ZoneNo IN (2, 12), s.sElmoradZ, s.sKarusZ) * 100
WHERE z.ServerNo = 1 AND (
    (z.InitX = 1000 AND z.InitZ = 1000)
    OR (z.ZoneNo = 11 AND z.InitX = 35400 AND z.InitZ = 161000)
    OR (z.ZoneNo = 12 AND z.InitX = 167000 AND z.InitZ = 37000)
    OR (z.ZoneNo = 48 AND z.InitX = 128 AND z.InitZ = 123)
);
