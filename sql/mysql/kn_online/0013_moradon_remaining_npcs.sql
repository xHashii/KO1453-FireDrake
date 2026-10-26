-- Separate snapshot for layout v2; do not overwrite the original v1 backup.
CREATE TABLE IF NOT EXISTS KO_MORADON_V2_NPCPOS_BACKUP LIKE K_NPCPOS;
