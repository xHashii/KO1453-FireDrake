-- One-time expanded Moradon monster placement, applied transactionally at startup.
-- Preserve original monster placements separately from every NPC layout snapshot.
CREATE TABLE IF NOT EXISTS KO_MORADON_MONSTERPOS_BACKUP LIKE K_NPCPOS;
