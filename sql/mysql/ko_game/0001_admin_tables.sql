-- ============================================================================
-- KO_GAME migration 0001 - AdminPanel support tables
-- (Applied automatically by DatabaseMigrator; see docs/DATABASE.md. The game
-- server itself never reads these tables; they belong to the GM tooling.)
-- ============================================================================

-- Restorable snapshot of a character's item blobs, taken before every save.
CREATE TABLE IF NOT EXISTS `_ko_admin_backups` (
  `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `strCharID`      VARCHAR(20) NOT NULL,
  `strItem`        BLOB NULL,
  `strSerial`      BLOB NULL,
  `strItemTime`    BLOB NULL,
  `WarehouseData`  BLOB NULL,
  `WarehouseSerial` BLOB NULL,
  `WarehouseTime`  BLOB NULL,
  `created_utc`    DATETIME NOT NULL,
  `admin_user`     VARCHAR(64) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_admin_backups_char` (`strCharID`, `created_utc`)
) ENGINE=InnoDB;

-- Append-only audit trail of admin actions.
CREATE TABLE IF NOT EXISTS `_ko_admin_audit` (
  `id`          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `admin_user`  VARCHAR(64) NOT NULL,
  `action`      VARCHAR(80) NOT NULL,
  `target`      VARCHAR(64) NOT NULL DEFAULT '',
  `detail`      TEXT NULL,
  `created_utc` DATETIME NOT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_admin_audit_time` (`created_utc`)
) ENGINE=InnoDB;
