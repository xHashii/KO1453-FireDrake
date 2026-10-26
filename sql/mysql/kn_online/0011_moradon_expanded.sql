-- Owner-selected expanded Moradon. Keep the old runtime SMD for rollback.
-- Only replace the shipped legacy map reference; preserve custom zone/map rows.
-- No USERDATA, inventory, NPC placement or START_POSITION rewrite.
UPDATE `ZONE_INFO`
SET strZoneName = 'moradon_expanded.smd'
WHERE ServerNo = 1 AND ZoneNo = 21
  AND strZoneName = 'moradon_20060124.smd';
