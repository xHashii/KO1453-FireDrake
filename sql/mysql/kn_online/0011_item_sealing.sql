-- Separate from TB_USER.strSealPasswd: imported accounts have a public shared default.
CREATE TABLE IF NOT EXISTS KO_ITEM_SEAL_SECURITY (
 strAccountID varchar(21) NOT NULL PRIMARY KEY,
 Salt varbinary(16) NOT NULL,
 CodeHash varbinary(32) NOT NULL,
 FailedAttempts tinyint unsigned NOT NULL DEFAULT 0,
 LockedUntil datetime(6) NULL
) ENGINE=InnoDB;
-- Stable transactional mutex for each serial, including after an unseal.
CREATE TABLE IF NOT EXISTS KO_ITEM_SEAL_LOCK (
 nItemSerial bigint NOT NULL PRIMARY KEY
) ENGINE=InnoDB;
