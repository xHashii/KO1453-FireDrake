-- Durable solo kill receipts and owner-only, expiring loot. No USERDATA rewrites.
CREATE TABLE IF NOT EXISTS KO_KILL_REWARD (
 KillToken char(32) NOT NULL PRIMARY KEY,
 Owner varchar(21) NOT NULL,
 CreatedAt datetime(6) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS KO_LOOT_BUNDLE (
 Id int unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
 KillToken char(32) NOT NULL UNIQUE,
 Owner varchar(21) NOT NULL,
 Zone smallint unsigned NOT NULL,
 NpcId smallint unsigned NOT NULL,
 X float NOT NULL,
 Z float NOT NULL,
 ExpiresAt datetime(6) NOT NULL,
 KEY ix_loot_owner(Owner,ExpiresAt)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS KO_LOOT_ENTRY (
 BundleId int unsigned NOT NULL,
 Slot tinyint unsigned NOT NULL,
 ItemId int unsigned NOT NULL,
 Count smallint unsigned NOT NULL,
 Claimed tinyint unsigned NOT NULL DEFAULT 0,
 PRIMARY KEY(BundleId,Slot)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS KO_SKILL_COOLDOWN (
 Owner varchar(21) NOT NULL,
 Skill int unsigned NOT NULL,
 AvailableAt datetime(6) NOT NULL,
 PRIMARY KEY(Owner,Skill)
) ENGINE=InnoDB;
