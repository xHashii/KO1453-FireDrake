-- One-time placement upgrade is applied transactionally by GameServer startup.
-- Keep pre-upgrade placement rows for operator rollback. No NPC definitions or
-- character/item data is stored here. LIKE preserves the live placement schema.
CREATE TABLE IF NOT EXISTS KO_MORADON_LAYOUT (
 LayoutId int NOT NULL PRIMARY KEY,
 AppliedUtc datetime(6) NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS KO_MORADON_NPCPOS_BACKUP LIKE K_NPCPOS;
CREATE TABLE IF NOT EXISTS KO_MORADON_OBJECTPOS_BACKUP LIKE K_OBJECTPOS;
