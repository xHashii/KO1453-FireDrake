-- ============================================================================
-- KO_MAIN migration 0001 - account schema + routines (DRAFT)
-- ============================================================================
-- Applied automatically by DatabaseMigrator on server startup.
-- Do NOT edit after it has been applied anywhere: add a 0002_*.sql instead.
--
-- Reconstruction status (until the real SQL Server export is converted):
--   exact   : VERSION, CONCURRENT (columns verbatim from LogInServer/DBProcess.cpp)
--   assumed : TB_USER, CURRENTUSER, ACCOUNT_LOGIN, ACCOUNT_PREMIUM
--             (only ever touched via stored procedures in the original;
--             community-standard layout, VERIFY against the real export)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- VERSION: patch list. sVersion = patch version the file brings you to,
-- strFilename = file served from the [DOWNLOAD] FTP path.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VERSION (
  sVersion        SMALLINT UNSIGNED NOT NULL,
  sHistoryVersion SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  strFilename     VARCHAR(50)  NOT NULL DEFAULT '',
  PRIMARY KEY (sVersion)
) ENGINE=InnoDB;

-- Latest version is MAX(sVersion); seed 1453 so a 1453 client needs no patch.
INSERT INTO VERSION (sVersion, sHistoryVersion, strFilename)
VALUES (1453, 0, '')
ON DUPLICATE KEY UPDATE sVersion = sVersion;

-- ----------------------------------------------------------------------------
-- CONCURRENT: per-server population, written by the GameServer
-- (DBAgent.cpp: UPDATE CONCURRENT ...), read by the LoginServer.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CONCURRENT (
  serverid    TINYINT UNSIGNED NOT NULL,
  zone1_count SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  zone2_count SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  zone3_count SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (serverid)
) ENGINE=InnoDB;

INSERT INTO CONCURRENT (serverid) VALUES (1)
ON DUPLICATE KEY UPDATE serverid = serverid;

-- ----------------------------------------------------------------------------
-- TB_USER: ASSUMED standard layout. The original only exposes accounts
-- through ACCOUNT_LOGIN / GAME_LOGIN procedures, so exact columns must be
-- confirmed against the real database export.
--   strAuthority: 0 = player, 1 = GM, 255 = banned (community convention;
--   the original ACCOUNT_LOGIN encodes ban handling internally - VERIFY).
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS TB_USER (
  strAccountID VARCHAR(20) NOT NULL,
  strPasswd    VARCHAR(28) NOT NULL,
  strSocNo     VARCHAR(20) NOT NULL DEFAULT '',
  idays        INT         NOT NULL DEFAULT 0,
  strAuthority TINYINT UNSIGNED NOT NULL DEFAULT 0,
  PremiumDate  DATETIME    NULL DEFAULT NULL,
  CashPoint    INT         NOT NULL DEFAULT 0,
  PRIMARY KEY (strAccountID)
) ENGINE=InnoDB;

-- Who is currently logged in (drives AUTH_IN_GAME = 0x05). Maintained by
-- GAME_LOGIN / ACCOUNT_LOGOUT on the game side.
CREATE TABLE IF NOT EXISTS CURRENTUSER (
  strAccountID VARCHAR(20) NOT NULL,
  strCharID    VARCHAR(20) NOT NULL DEFAULT '',
  nServerNo    INT         NOT NULL DEFAULT 0,
  strServerIP  VARCHAR(20) NOT NULL DEFAULT '',
  strClientIP  VARCHAR(20) NOT NULL DEFAULT '',
  PRIMARY KEY (strAccountID)
) ENGINE=InnoDB;

DELIMITER //

-- ----------------------------------------------------------------------------
-- ACCOUNT_LOGIN (original: {? = CALL ACCOUNT_LOGIN(?, ?)} with a T-SQL return
-- value; MariaDB port uses an OUT parameter instead).
--   1 success, 2 not found, 3 invalid password, 4 banned, 5 already in game
-- ----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS ACCOUNT_LOGIN //
CREATE PROCEDURE ACCOUNT_LOGIN(
  IN  p_account  VARCHAR(20),
  IN  p_password VARCHAR(28),
  OUT p_result   INT
)
BEGIN
  DECLARE v_password VARCHAR(28);
  DECLARE v_authority TINYINT UNSIGNED;

  SET p_result = 2; -- AUTH_NOT_FOUND

  SELECT strPasswd, strAuthority
    INTO v_password, v_authority
    FROM TB_USER
   WHERE strAccountID = p_account;

  IF v_password IS NOT NULL THEN
    IF v_authority = 255 THEN
      SET p_result = 4;                        -- AUTH_BANNED
    ELSEIF NOT (v_password <=> p_password) THEN
      SET p_result = 3;                        -- AUTH_INVALID
    ELSEIF EXISTS (SELECT 1 FROM CURRENTUSER WHERE strAccountID = p_account) THEN
      SET p_result = 5;                        -- AUTH_IN_GAME
    ELSE
      SET p_result = 1;                        -- AUTH_SUCCESS
    END IF;
  END IF;
END //

-- ----------------------------------------------------------------------------
-- ACCOUNT_PREMIUM: remaining premium hours, -1 when no premium.
-- (Draft semantics - confirm against the real procedure.)
-- ----------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS ACCOUNT_PREMIUM //
CREATE PROCEDURE ACCOUNT_PREMIUM(
  IN  p_account VARCHAR(20),
  OUT p_hours   INT
)
BEGIN
  SET p_hours = -1;

  SELECT TIMESTAMPDIFF(HOUR, NOW(), PremiumDate)
    INTO p_hours
    FROM TB_USER
   WHERE strAccountID = p_account
     AND PremiumDate IS NOT NULL
     AND PremiumDate > NOW();

  IF p_hours IS NULL THEN
    SET p_hours = -1;
  END IF;
END //

DELIMITER ;

-- ----------------------------------------------------------------------------
-- Example test account (delete before production!):
-- INSERT INTO TB_USER (strAccountID, strPasswd) VALUES ('test', 'test');
-- ----------------------------------------------------------------------------
