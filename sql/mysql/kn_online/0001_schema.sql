-- Auto-converted from SQL Server dump (mssql_to_mariadb.py). Review before production use.
-- Target database: chosen by server config / the importing connection (not by this file).
SET FOREIGN_KEY_CHECKS=0;
SET SQL_MODE='NO_AUTO_VALUE_ON_ZERO';

CREATE TABLE `_Aktivite`(
 `id` int AUTO_INCREMENT NOT NULL,
 `konu` text NULL,
 `aktivite` varchar(5000) NULL,
 `tarih` DATETIME NOT NULL,
 PRIMARY KEY 
(
 `id`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Anket`(
 `Anket_ID` int AUTO_INCREMENT NOT NULL,
 `Konu` varchar(100) NULL,
 `Madde1` varchar(50) NULL,
 `Madde2` varchar(50) NULL,
 `Madde3` varchar(50) NULL,
 `Oy1` int NOT NULL,
 `Oy2` int NOT NULL,
 `Oy3` int NOT NULL,
 `Tarih` DATETIME NULL,
 `Haber_ID` int NULL,
 `Yer` varchar(50) NULL,
 KEY `ix_auto_Anket_ID` (`Anket_ID`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Anket_Oylayanlar`(
 `Oy_Kullanan` varchar(50) NULL,
 `Anket_ID` int NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_DOWNLOADS`(
 `id` int AUTO_INCREMENT NOT NULL,
 `titletr` varchar(250) NULL,
 `titleen` varchar(250) NULL,
 `url` varchar(250) NULL,
 `tarih` varchar(250) NULL,
 KEY `ix_auto_id` (`id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Haber`(
 `id` int AUTO_INCREMENT NOT NULL,
 `konu` text NULL,
 `haber` varchar(5000) NULL,
 `tarih` DATETIME NOT NULL,
 PRIMARY KEY 
(
 `id`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_IP_Engelliler`(
 `id` int AUTO_INCREMENT NOT NULL,
 `ip` varchar(12) NULL,
 `strAccountID` varchar(21) NULL,
 `Char_1` varchar(21) NULL,
 `Char_2` varchar(21) NULL,
 `Char_3` varchar(21) NULL,
 `Tarih_Default` DATETIME NOT NULL,
 `Tarih` varchar(50) NULL,
 `Silinme_Tarihi` varchar(50) NULL,
 KEY `ix_auto_id` (`id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_K2DLoyalty`(
 `Clan_Sid` int NULL,
 `Clan_Name` varchar(50) NULL,
 `strUserID` varchar(21) NULL,
 `Loyalty` int NULL,
 `Sayi` int NULL,
 `Tarih` DATETIME NOT NULL,
 `Son_Tarih` DATETIME NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Link`(
 `id` int AUTO_INCREMENT NOT NULL,
 `adi` varchar(150) NULL,
 `link` varchar(300) NULL,
 `indirilme` int NOT NULL,
 `tarih` DATETIME NOT NULL,
 KEY `ix_auto_id` (`id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Mail`(
 `id` int AUTO_INCREMENT NOT NULL,
 `gonderen` varchar(50) NULL,
 `gonderilen` varchar(50) NULL,
 `msj` varchar(500) NULL,
 `tarih` DATETIME NOT NULL,
 `okundu` int NULL,
 PRIMARY KEY 
(
 `id`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Sifre_Degistir`(
 `strAccountId` varchar(21) NULL,
 `strEmail` varchar(50) NULL,
 `strAccMD5` varchar(500) NULL,
 `strCode` varchar(20) NULL,
 `Tarih` DATETIME NULL,
 `Durum` varchar(5) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Sikayet`(
 `id` int AUTO_INCREMENT NOT NULL,
 `eden` varchar(50) NULL,
 `zanli` varchar(50) NULL,
 `aciklama` varchar(500) NULL,
 `resim` varchar(500) NULL,
 `dosya` varchar(500) NULL,
 `tarih` DATETIME NULL,
 PRIMARY KEY 
(
 `id`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Ticket`(
 `id` int AUTO_INCREMENT NOT NULL,
 `ticket_type` varchar(12) NULL,
 `aciliyet` varchar(12) NULL,
 `ticket` varchar(50) NULL,
 `kim` varchar(50) NULL,
 `tarih` DATETIME NULL,
 `durum` int NULL,
 KEY `ix_auto_id` (`id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `_Ticket_Cevap`(
 `Ticket_Id` int NULL,
 `Cevaplayan` varchar(21) NULL,
 `Cevap` varchar(500) NULL,
 `Tarih` DATETIME NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ACCOUNT_CHAR`(
 `strAccountID` char(21) NOT NULL,
 `bNation` TINYINT UNSIGNED NOT NULL,
 `bCharNum` TINYINT UNSIGNED NOT NULL,
 `strCharID1` char(21) NULL,
 `strCharID2` char(21) NULL,
 `strCharID3` char(21) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `baslangic`(
 `class` smallint NULL,
 `stritem` varchar(400) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `BATTLE`(
 `sIndex` smallint NOT NULL,
 `byNation` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `BEGINNER_ITEM`(
 `Name` varchar(50) NULL,
 `Class` TINYINT UNSIGNED NULL,
 `Level` TINYINT UNSIGNED NULL,
 `Exp` bigint NULL,
 `Strong` smallint NOT NULL,
 `Dex` smallint NOT NULL,
 `Intel` smallint NOT NULL,
 `Cha` smallint NOT NULL,
 `Sta` smallint NOT NULL,
 `Skill` varchar(10) NULL,
 `Gold` int NULL,
 `strItem` varbinary(600) NULL,
 `WarehouseData` varbinary(1536) NULL,
 `Loyalty` int NULL,
 `Points` smallint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `BEGINNER_ITEM2`(
 `iClass` smallint NOT NULL,
 `strClass` char(10) NULL,
 `Gold` bigint NOT NULL,
 `strItem` varchar(1200) NULL,
 `strItem1` varbinary(1200) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `COEFFICIENT`(
 `sClass` smallint NOT NULL,
 `ShortSword` float NOT NULL,
 `Sword` float NOT NULL,
 `Axe` float NOT NULL,
 `Club` float NOT NULL,
 `Spear` float NOT NULL,
 `Pole` float NOT NULL,
 `Staff` float NOT NULL,
 `Bow` float NULL,
 `Hp` float NOT NULL,
 `Mp` float NOT NULL,
 `Sp` float NOT NULL,
 `Ac` float NOT NULL,
 `Hitrate` float NOT NULL,
 `Evasionrate` float NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `CONCURRENT`(
 `serverid` TINYINT UNSIGNED NOT NULL,
 `zone1_count` smallint NOT NULL,
 `zone2_count` smallint NOT NULL,
 `zone3_count` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `CURRENTUSER`(
 `strAccountID` varchar(50) NOT NULL,
 `strCharID` varchar(50) NOT NULL,
 `nServerNo` smallint NOT NULL,
 `strServerIP` varchar(50) NOT NULL,
 `strClientIP` varchar(50) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `EVENT`(
 `ZoneNum` TINYINT UNSIGNED NOT NULL,
 `EventNum` smallint NOT NULL,
 `Type` TINYINT UNSIGNED NOT NULL,
 `Cond1` char(128) NULL,
 `Cond2` char(128) NULL,
 `Cond3` char(128) NULL,
 `Cond4` char(128) NULL,
 `Cond5` char(128) NULL,
 `Exec1` char(128) NULL,
 `Exec2` char(128) NULL,
 `Exec3` char(128) NULL,
 `Exec4` char(128) NULL,
 `Exec5` char(128) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `EVENT_TRIGGER`(
 `nIndex` int NULL,
 `bNpcType` smallint NULL,
 `sNpcID` int NULL,
 `nTriggerNum` int NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `FRIEND_LIST`(
 `strUserID` char(21) NOT NULL,
 `strFriend1` char(21) NULL,
 `strFriend2` char(21) NULL,
 `strFriend3` char(21) NULL,
 `strFriend4` char(21) NULL,
 `strFriend5` char(21) NULL,
 `strFriend6` char(21) NULL,
 `strFriend7` char(21) NULL,
 `strFriend8` char(21) NULL,
 `strFriend9` char(21) NULL,
 `strFriend10` char(21) NULL,
 `strFriend11` char(21) NULL,
 `strFriend12` char(21) NULL,
 `strFriend13` char(21) NULL,
 `strFriend14` char(21) NULL,
 `strFriend15` char(21) NULL,
 `strFriend16` char(21) NULL,
 `strFriend17` char(21) NULL,
 `strFriend18` char(21) NULL,
 `strFriend19` char(21) NULL,
 `strFriend20` char(21) NULL,
 `strFriend21` char(21) NULL,
 `strFriend22` char(21) NULL,
 `strFriend23` char(21) NULL,
 `strFriend24` char(21) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `HOME`(
 `Nation` TINYINT UNSIGNED NOT NULL,
 `ElmoZoneX` int NOT NULL,
 `ElmoZoneZ` int NOT NULL,
 `ElmoZoneLX` TINYINT UNSIGNED NOT NULL,
 `ElmoZoneLZ` TINYINT UNSIGNED NOT NULL,
 `KarusZoneX` int NOT NULL,
 `KarusZoneZ` int NOT NULL,
 `KarusZoneLX` TINYINT UNSIGNED NOT NULL,
 `KarusZoneLZ` TINYINT UNSIGNED NOT NULL,
 `FreeZoneX` int NOT NULL,
 `FreeZoneZ` int NOT NULL,
 `FreeZoneLX` TINYINT UNSIGNED NOT NULL,
 `FreeZoneLZ` TINYINT UNSIGNED NOT NULL,
 `BattleZoneX` int NOT NULL,
 `BattleZoneZ` int NOT NULL,
 `BattleZoneLX` TINYINT UNSIGNED NOT NULL,
 `BattleZoneLZ` TINYINT UNSIGNED NOT NULL,
 `BattleZone2X` int NOT NULL,
 `BattleZone2Z` int NOT NULL,
 `BattleZone2LX` TINYINT UNSIGNED NOT NULL,
 `BattleZone2LZ` TINYINT UNSIGNED NOT NULL,
 `BattleZone3X` int NOT NULL,
 `BattleZone3Z` int NOT NULL,
 `BattleZone3LX` TINYINT UNSIGNED NOT NULL,
 `BattleZone3LZ` TINYINT UNSIGNED NOT NULL,
 `BattleZone4X` int NOT NULL,
 `BattleZone4Z` int NOT NULL,
 `BattleZone4LX` TINYINT UNSIGNED NOT NULL,
 `BattleZone4LZ` TINYINT UNSIGNED NOT NULL,
 `BattleZone5X` int NOT NULL,
 `BattleZone5Z` int NOT NULL,
 `BattleZone5LX` TINYINT UNSIGNED NOT NULL,
 `BattleZone5LZ` TINYINT UNSIGNED NOT NULL,
 `BattleZone6X` int NOT NULL,
 `BattleZone6Z` int NOT NULL,
 `BattleZone6LX` TINYINT UNSIGNED NOT NULL,
 `BattleZone6LZ` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ITEM`(
 `Num` int NULL,
 `strName` varchar(100) NULL,
 `Kind` TINYINT UNSIGNED NULL,
 `Slot` TINYINT UNSIGNED NULL,
 `Race` TINYINT UNSIGNED NULL,
 `Class` TINYINT UNSIGNED NULL,
 `Damage` smallint NULL,
 `Delay` smallint NULL,
 `Range` smallint NULL,
 `Weight` smallint NULL,
 `Duration` smallint NULL,
 `BuyPrice` int NULL,
 `SellPrice` int NULL,
 `Ac` smallint NULL,
 `Countable` TINYINT UNSIGNED NULL,
 `Effect1` int NULL,
 `Effect2` int NULL,
 `ReqLevel` TINYINT UNSIGNED NULL,
 `ReqLevelMax` TINYINT UNSIGNED NULL,
 `ReqRank` TINYINT UNSIGNED NULL,
 `ReqTitle` TINYINT UNSIGNED NULL,
 `ReqStr` TINYINT UNSIGNED NULL,
 `ReqSta` TINYINT UNSIGNED NULL,
 `ReqDex` TINYINT UNSIGNED NULL,
 `ReqIntel` TINYINT UNSIGNED NULL,
 `ReqCha` TINYINT UNSIGNED NULL,
 `SellingGroup` TINYINT UNSIGNED NULL,
 `ItemType` TINYINT UNSIGNED NULL,
 `Hitrate` smallint NULL,
 `Evasionrate` smallint NULL,
 `DaggerAc` smallint NULL,
 `SwordAc` smallint NULL,
 `MaceAc` smallint NULL,
 `AxeAc` smallint NULL,
 `SpearAc` smallint NULL,
 `BowAc` smallint NULL,
 `FireDamage` TINYINT UNSIGNED NULL,
 `IceDamage` TINYINT UNSIGNED NULL,
 `LightningDamage` TINYINT UNSIGNED NULL,
 `PoisonDamage` TINYINT UNSIGNED NULL,
 `HPDrain` TINYINT UNSIGNED NULL,
 `MPDamage` TINYINT UNSIGNED NULL,
 `MPDrain` TINYINT UNSIGNED NULL,
 `MirrorDamage` TINYINT UNSIGNED NULL,
 `Droprate` TINYINT UNSIGNED NULL,
 `StrB` smallint NULL,
 `StaB` smallint NULL,
 `DexB` smallint NULL,
 `IntelB` smallint NULL,
 `ChaB` smallint NULL,
 `MaxHpB` smallint NULL,
 `MaxMpB` smallint NULL,
 `FireR` smallint NULL,
 `ColdR` smallint NULL,
 `LightningR` smallint NULL,
 `MagicR` smallint NULL,
 `PoisonR` smallint NULL,
 `CurseR` smallint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ITEM_EXCHANGE`(
 `nIndex` int NOT NULL,
 `nNpcNum` smallint NOT NULL,
 `strNpcName` varchar(50) NULL,
 `strNote` varchar(100) NULL,
 `bRandomFlag` TINYINT UNSIGNED NOT NULL,
 `nOriginItemNum1` int NOT NULL,
 `nOriginItemCount1` smallint NOT NULL,
 `nOriginItemNum2` int NOT NULL,
 `nOriginItemCount2` smallint NOT NULL,
 `nOriginItemNum3` int NOT NULL,
 `nOriginItemCount3` smallint NOT NULL,
 `nOriginItemNum4` int NOT NULL,
 `nOriginItemCount4` smallint NOT NULL,
 `nOriginItemNum5` int NOT NULL,
 `nOriginItemCount5` smallint NOT NULL,
 `nOriginItemNum6` int NOT NULL,
 `nOriginItemCount6` smallint NOT NULL,
 `nOriginItemNum7` int NOT NULL,
 `nOriginItemCount7` smallint NOT NULL,
 `nOriginItemNum8` int NOT NULL,
 `nOriginItemCount8` smallint NOT NULL,
 `nOriginItemNum9` int NOT NULL,
 `nOriginItemCount9` smallint NOT NULL,
 `nOriginItemNum10` int NOT NULL,
 `nOriginItemCount10` smallint NOT NULL,
 `nOriginItemNum11` int NOT NULL,
 `nOriginItemCount11` smallint NOT NULL,
 `nExchangeItemNum1` int NOT NULL,
 `nExchangeItemCount1` smallint NOT NULL,
 `nExchangeItemNum2` int NOT NULL,
 `nExchangeItemCount2` smallint NOT NULL,
 `nExchangeItemNum3` int NOT NULL,
 `nExchangeItemCount3` smallint NOT NULL,
 `nExchangeItemNum4` int NOT NULL,
 `nExchangeItemCount4` smallint NOT NULL,
 `nExchangeItemNum5` int NOT NULL,
 `nExchangeItemCount5` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ITEM_GROUP`(
 `group` smallint NOT NULL,
 `name` char(30) NULL,
 `item1` int NOT NULL,
 `item2` int NOT NULL,
 `item3` int NOT NULL,
 `item4` int NOT NULL,
 `item5` int NOT NULL,
 `item6` int NOT NULL,
 `item7` int NOT NULL,
 `item8` int NOT NULL,
 `item9` int NOT NULL,
 `item10` int NOT NULL,
 `item11` int NOT NULL,
 `item12` int NOT NULL,
 `item13` int NOT NULL,
 `item14` int NOT NULL,
 `item15` int NOT NULL,
 `item16` int NOT NULL,
 `item17` int NOT NULL,
 `item18` int NOT NULL,
 `item19` int NOT NULL,
 `item20` int NOT NULL,
 `item21` int NOT NULL,
 `item22` int NOT NULL,
 `item23` int NOT NULL,
 `item24` int NOT NULL,
 `item25` int NOT NULL,
 `item26` int NOT NULL,
 `item27` int NOT NULL,
 `item28` int NOT NULL,
 `item29` int NOT NULL,
 `item30` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ITEM_OP`(
 `nItemID` int NOT NULL,
 `bTriggerType` TINYINT UNSIGNED NOT NULL,
 `nSkillID` int NOT NULL,
 `bTriggerRate` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ITEM_UPGRADE`(
 `nIndex` int NOT NULL,
 `nNPCNum` smallint NOT NULL,
 `strName` char(210) NULL,
 `strNote` varchar(100) NULL,
 `nOriginType` smallint NOT NULL,
 `nOriginItem` smallint NOT NULL,
 `nReqItem1` int NOT NULL,
 `nReqItem2` int NOT NULL,
 `nReqItem3` int NOT NULL,
 `nReqItem4` int NOT NULL,
 `nReqItem5` int NOT NULL,
 `nReqItem6` int NOT NULL,
 `nReqItem7` int NOT NULL,
 `nReqItem8` int NOT NULL,
 `nReqNoah` int NOT NULL,
 `bRateType` TINYINT UNSIGNED NOT NULL,
 `nGenRate` smallint NOT NULL,
 `nGiveItem` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ITEMUP_PROBABILITY`(
 `bType` TINYINT UNSIGNED NOT NULL,
 `nMaxSuccess` smallint NOT NULL,
 `nMaxFail` smallint NOT NULL,
 `nCurSuccess` smallint NOT NULL,
 `nCurFail` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_MONSTER`(
 `sSid` smallint NOT NULL,
 `strName` varchar(30) NULL,
 `sPid` smallint NOT NULL,
 `sSize` smallint NOT NULL,
 `iWeapon1` int NOT NULL,
 `iWeapon2` int NOT NULL,
 `byGroup` TINYINT UNSIGNED NOT NULL,
 `byActType` TINYINT UNSIGNED NOT NULL,
 `byType` TINYINT UNSIGNED NOT NULL,
 `byFamily` TINYINT UNSIGNED NOT NULL,
 `byRank` TINYINT UNSIGNED NOT NULL,
 `byTitle` TINYINT UNSIGNED NOT NULL,
 `iSellingGroup` int NOT NULL,
 `sLevel` smallint NOT NULL,
 `iExp` int NOT NULL,
 `iLoyalty` int NOT NULL,
 `iHpPoint` int NOT NULL,
 `sMpPoint` smallint NOT NULL,
 `sAtk` smallint NOT NULL,
 `sAc` smallint NOT NULL,
 `sHitRate` smallint NOT NULL,
 `sEvadeRate` smallint NOT NULL,
 `sDamage` smallint NOT NULL,
 `sAttackDelay` smallint NOT NULL,
 `bySpeed1` TINYINT UNSIGNED NOT NULL,
 `bySpeed2` TINYINT UNSIGNED NOT NULL,
 `sStandtime` smallint NOT NULL,
 `iMagic1` int NOT NULL,
 `iMagic2` int NOT NULL,
 `iMagic3` int NOT NULL,
 `sFireR` smallint NOT NULL,
 `sColdR` smallint NOT NULL,
 `sLightningR` smallint NOT NULL,
 `sMagicR` smallint NOT NULL,
 `sDiseaseR` smallint NOT NULL,
 `sPoisonR` smallint NOT NULL,
 `sLightR` smallint NOT NULL,
 `sBulk` smallint NOT NULL,
 `byAttackRange` TINYINT UNSIGNED NOT NULL,
 `bySearchRange` TINYINT UNSIGNED NOT NULL,
 `byTracingRange` TINYINT UNSIGNED NOT NULL,
 `iMoney` int NOT NULL,
 `sItem` smallint NOT NULL,
 `byDirectAttack` TINYINT UNSIGNED NOT NULL,
 `byMagicAttack` TINYINT UNSIGNED NOT NULL,
 `byMoneyType` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_MONSTER_ITEM`(
 `sIndex` smallint NOT NULL,
 `iItem01` int NULL,
 `sPersent01` smallint NULL,
 `iItem02` int NULL,
 `sPersent02` smallint NULL,
 `iItem03` int NULL,
 `sPersent03` smallint NULL,
 `iItem04` int NULL,
 `sPersent04` smallint NULL,
 `iItem05` int NULL,
 `sPersent05` smallint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_NPC`(
 `sSid` smallint NOT NULL,
 `strName` varchar(30) NULL,
 `sPid` smallint NOT NULL,
 `sSize` smallint NOT NULL,
 `iWeapon1` int NOT NULL,
 `iWeapon2` int NOT NULL,
 `byGroup` TINYINT UNSIGNED NOT NULL,
 `byActType` TINYINT UNSIGNED NOT NULL,
 `byType` TINYINT UNSIGNED NOT NULL,
 `byFamily` TINYINT UNSIGNED NOT NULL,
 `byRank` TINYINT UNSIGNED NOT NULL,
 `byTitle` TINYINT UNSIGNED NOT NULL,
 `iSellingGroup` int NOT NULL,
 `sLevel` smallint NOT NULL,
 `iExp` int NOT NULL,
 `iLoyalty` int NOT NULL,
 `iHpPoint` int NOT NULL,
 `sMpPoint` smallint NOT NULL,
 `sAtk` smallint NOT NULL,
 `sAc` smallint NOT NULL,
 `sHitRate` smallint NOT NULL,
 `sEvadeRate` smallint NOT NULL,
 `sDamage` smallint NOT NULL,
 `sAttackDelay` smallint NOT NULL,
 `bySpeed1` TINYINT UNSIGNED NOT NULL,
 `bySpeed2` TINYINT UNSIGNED NOT NULL,
 `sStandtime` smallint NOT NULL,
 `iMagic1` int NOT NULL,
 `iMagic2` int NOT NULL,
 `iMagic3` int NOT NULL,
 `sFireR` smallint NOT NULL,
 `sColdR` smallint NOT NULL,
 `sLightningR` smallint NOT NULL,
 `sMagicR` smallint NOT NULL,
 `sDiseaseR` smallint NOT NULL,
 `sPoisonR` smallint NOT NULL,
 `sLightR` smallint NOT NULL,
 `sBulk` smallint NOT NULL,
 `byAttackRange` TINYINT UNSIGNED NOT NULL,
 `bySearchRange` TINYINT UNSIGNED NOT NULL,
 `byTracingRange` TINYINT UNSIGNED NOT NULL,
 `iMoney` int NOT NULL,
 `sItem` smallint NOT NULL,
 `byDirectAttack` TINYINT UNSIGNED NOT NULL,
 `byMagicAttack` TINYINT UNSIGNED NOT NULL,
 `byMoneyType` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_NPCPOS`(
 `ZoneID` smallint NOT NULL,
 `NpcID` int NOT NULL,
 `ActType` TINYINT UNSIGNED NOT NULL,
 `RegenType` TINYINT UNSIGNED NOT NULL,
 `DungeonFamily` TINYINT UNSIGNED NOT NULL,
 `SpecialType` TINYINT UNSIGNED NOT NULL,
 `TrapNumber` TINYINT UNSIGNED NOT NULL,
 `LeftX` int NOT NULL,
 `TopZ` int NOT NULL,
 `RightX` int NOT NULL,
 `BottomZ` int NOT NULL,
 `LimitMinX` int NOT NULL,
 `LimitMinZ` int NOT NULL,
 `LimitMaxX` int NOT NULL,
 `LimitMaxZ` int NOT NULL,
 `NumNPC` TINYINT UNSIGNED NOT NULL,
 `RegTime` smallint NOT NULL,
 `byDirection` TINYINT UNSIGNED NOT NULL,
 `DotCnt` TINYINT UNSIGNED NOT NULL,
 `path` text NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_OBJECTEVENT`(
 `sZoneNo` smallint NOT NULL,
 `sIndex` smallint NOT NULL,
 `byLife` TINYINT UNSIGNED NOT NULL,
 `sBelong` smallint NOT NULL,
 `sType` smallint NOT NULL,
 `sControlNpcId` smallint NOT NULL,
 `sStatus` smallint NOT NULL,
 `sPosX` smallint NOT NULL,
 `sPosY` smallint NOT NULL,
 `sPosZ` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_OBJECTPOS`(
 `ZoneID` smallint NOT NULL,
 `Belong` TINYINT UNSIGNED NOT NULL,
 `sIndex` smallint NOT NULL,
 `Type` TINYINT UNSIGNED NOT NULL,
 `ControlNpcID` smallint NOT NULL,
 `Status` TINYINT UNSIGNED NOT NULL,
 `PosX` float NOT NULL,
 `PosY` float NOT NULL,
 `PosZ` float NOT NULL,
 `byLife` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `K_WARPINFO`(
 `sWarpID` smallint NOT NULL,
 `strWarpName` varchar(32) NULL,
 `strAnnounce` varchar(256) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KING_BALLOT_BOX`(
 `strAccountID` char(21) NOT NULL,
 `strCharID` char(21) NOT NULL,
 `byNation` TINYINT UNSIGNED NOT NULL,
 `strCandidacyID` char(21) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KING_CANDIDACY_NOTICE_BOARD`(
 `strUserID` char(21) NOT NULL,
 `byNation` TINYINT UNSIGNED NOT NULL,
 `sNoticeLen` smallint NOT NULL,
 `strNotice` varbinary(1024) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KING_ELECTION_LIST`(
 `byType` TINYINT UNSIGNED NOT NULL,
 `byNation` TINYINT UNSIGNED NOT NULL,
 `nKnights` smallint NULL,
 `strName` char(21) NULL,
 `nMoney` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KING_SYSTEM`(
 `byNation` TINYINT UNSIGNED NOT NULL,
 `byType` TINYINT UNSIGNED NOT NULL,
 `sYear` smallint NOT NULL,
 `byMonth` TINYINT UNSIGNED NOT NULL,
 `byDay` TINYINT UNSIGNED NOT NULL,
 `byHour` TINYINT UNSIGNED NOT NULL,
 `byMinute` TINYINT UNSIGNED NOT NULL,
 `byImType` TINYINT UNSIGNED NOT NULL,
 `sImYear` smallint NOT NULL,
 `byImMonth` TINYINT UNSIGNED NOT NULL,
 `byImDay` TINYINT UNSIGNED NOT NULL,
 `byImHour` TINYINT UNSIGNED NOT NULL,
 `byImMinute` TINYINT UNSIGNED NOT NULL,
 `byNoahEvent` TINYINT UNSIGNED NOT NULL,
 `byNoahEvent_Day` TINYINT UNSIGNED NOT NULL,
 `byNoahEvent_Hour` TINYINT UNSIGNED NOT NULL,
 `byNoahEvent_Minute` TINYINT UNSIGNED NOT NULL,
 `sNoahEvent_Duration` smallint NOT NULL,
 `byExpEvent` TINYINT UNSIGNED NOT NULL,
 `byExpEvent_Day` TINYINT UNSIGNED NOT NULL,
 `byExpEvent_Hour` TINYINT UNSIGNED NOT NULL,
 `byExpEvent_Minute` TINYINT UNSIGNED NOT NULL,
 `sExpEvent_Duration` smallint NOT NULL,
 `nTribute` int NOT NULL,
 `byTerritoryTariff` TINYINT UNSIGNED NOT NULL,
 `nTerritoryTax` int NOT NULL,
 `nNationalTreasury` int NOT NULL,
 `strKingName` char(21) NULL,
 `strImRequestID` char(21) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KNIGHTS`(
 `IDNum` smallint NOT NULL,
 `Flag` TINYINT UNSIGNED NOT NULL,
 `Nation` TINYINT UNSIGNED NOT NULL,
 `Ranking` TINYINT UNSIGNED NOT NULL,
 `IDName` char(21) NOT NULL,
 `Members` smallint NOT NULL,
 `Chief` char(21) NOT NULL,
 `ViceChief_1` char(21) NULL,
 `ViceChief_2` char(21) NULL,
 `ViceChief_3` char(21) NULL,
 `Gold` bigint NOT NULL,
 `Domination` smallint NOT NULL,
 `Points` int NOT NULL,
 `Mark` MEDIUMBLOB NULL,
 `sMarkVersion` smallint NOT NULL,
 `sMarkLen` smallint NOT NULL,
 `sCape` smallint NOT NULL,
 `bCapeR` TINYINT UNSIGNED NOT NULL,
 `bCapeG` TINYINT UNSIGNED NOT NULL,
 `bCapeB` TINYINT UNSIGNED NOT NULL,
 `sAllianceKnights` smallint NOT NULL,
 `ClanPointFund` int NOT NULL,
 `strClanNotice` char(128) NULL,
 `ClanPointMethod` TINYINT UNSIGNED NOT NULL,
 `dtCreateTime` datetime NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KNIGHTS_ALLIANCE`(
 `sMainAllianceKnights` smallint NOT NULL,
 `sSubAllianceKnights` smallint NOT NULL,
 `sMercenaryClan_1` smallint NOT NULL,
 `sMercenaryClan_2` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KNIGHTS_CAPE`(
 `sCapeIndex` smallint NOT NULL,
 `strName` char(30) NOT NULL,
 `nBuyPrice` int NOT NULL,
 `nDuration` int NOT NULL,
 `byGrade` TINYINT UNSIGNED NOT NULL,
 `nBuyLoyalty` int NOT NULL,
 `byRanking` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KNIGHTS_RATING`(
 `nRank` int NOT NULL,
 `shIndex` smallint NOT NULL,
 `strName` char(20) NOT NULL,
 `nPoints` int NOT NULL,
 `nClanPointFund` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KNIGHTS_SIEGE_WARFARE`(
 `sCastleIndex` smallint NOT NULL,
 `sMasterKnights` smallint NOT NULL,
 `bySiegeType` TINYINT UNSIGNED NOT NULL,
 `byWarDay` TINYINT UNSIGNED NOT NULL,
 `byWarTime` TINYINT UNSIGNED NOT NULL,
 `byWarMinute` TINYINT UNSIGNED NOT NULL,
 `sChallengeList_1` smallint NOT NULL,
 `sChallengeList_2` smallint NOT NULL,
 `sChallengeList_3` smallint NOT NULL,
 `sChallengeList_4` smallint NOT NULL,
 `sChallengeList_5` smallint NOT NULL,
 `sChallengeList_6` smallint NOT NULL,
 `sChallengeList_7` smallint NOT NULL,
 `sChallengeList_8` smallint NOT NULL,
 `sChallengeList_9` smallint NOT NULL,
 `sChallengeList_10` smallint NOT NULL,
 `byWarRequestDay` TINYINT UNSIGNED NOT NULL,
 `byWarRequestTime` TINYINT UNSIGNED NOT NULL,
 `byWarRequestMinute` TINYINT UNSIGNED NOT NULL,
 `byGuerrillaWarDay` TINYINT UNSIGNED NOT NULL,
 `byGuerrillaWarTime` TINYINT UNSIGNED NOT NULL,
 `byGuerrillaWarMinute` TINYINT UNSIGNED NOT NULL,
 `strChallengeList` char(50) NULL,
 `sMoradonTariff` smallint NOT NULL,
 `sDellosTariff` smallint NOT NULL,
 `nDungeonCharge` int NOT NULL,
 `nMoradonTax` int NOT NULL,
 `nDellosTax` int NOT NULL,
 `sRequestList_1` smallint NOT NULL,
 `sRequestList_2` smallint NOT NULL,
 `sRequestList_3` smallint NOT NULL,
 `sRequestList_4` smallint NOT NULL,
 `sRequestList_5` smallint NOT NULL,
 `sRequestList_6` smallint NOT NULL,
 `sRequestList_7` smallint NOT NULL,
 `sRequestList_8` smallint NOT NULL,
 `sRequestList_9` smallint NOT NULL,
 `sRequestList_10` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `KNIGHTS_USER`(
 `sIDNum` smallint NOT NULL,
 `strUserID` char(21) NOT NULL,
 `nDonatedNP` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `LEVEL_UP`(
 `level` TINYINT UNSIGNED NOT NULL,
 `Exp` bigint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC`(
 `MagicNum` int NOT NULL,
 `EnName` char(250) NULL,
 `KrName` char(250) NULL,
 `Description` varchar(1000) NULL,
 `BeforeAction` int NOT NULL,
 `TargetAction` TINYINT UNSIGNED NOT NULL,
 `SelfEffect` TINYINT UNSIGNED NOT NULL,
 `FlyingEffect` smallint NOT NULL,
 `TargetEffect` smallint NOT NULL,
 `Moral` TINYINT UNSIGNED NOT NULL,
 `SkillLevel` smallint NOT NULL,
 `Skill` smallint NOT NULL,
 `Msp` smallint NOT NULL,
 `HP` smallint NOT NULL,
 `ItemGroup` TINYINT UNSIGNED NOT NULL,
 `UseItem` int NOT NULL,
 `CastTime` smallint NOT NULL,
 `ReCastTime` smallint NOT NULL,
 `SuccessRate` smallint NOT NULL,
 `Type1` TINYINT UNSIGNED NOT NULL,
 `Type2` TINYINT UNSIGNED NOT NULL,
 `Range` smallint NOT NULL,
 `Etc` smallint NOT NULL,
 `UseStanding` TINYINT UNSIGNED NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE1`(
 `iNum` int NOT NULL,
 `Name` char(250) NULL,
 `Description` varchar(1000) NULL,
 `Type` TINYINT UNSIGNED NOT NULL,
 `HitRate` smallint NOT NULL,
 `Hit` smallint NOT NULL,
 `AddDamage` smallint NOT NULL,
 `Delay` TINYINT UNSIGNED NOT NULL,
 `ComboType` TINYINT UNSIGNED NOT NULL,
 `ComboCount` TINYINT UNSIGNED NOT NULL,
 `ComboDamage` smallint NOT NULL,
 `Range` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE2`(
 `iNum` int NOT NULL,
 `Name` char(30) NOT NULL,
 `Description` char(100) NULL,
 `HitType` TINYINT UNSIGNED NOT NULL,
 `HitRate` smallint NOT NULL,
 `AddDamage` smallint NOT NULL,
 `AddRange` smallint NOT NULL,
 `NeedArrow` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE3`(
 `iNum` int NOT NULL,
 `DirectType` TINYINT UNSIGNED NOT NULL,
 `FirstDamage` smallint NOT NULL,
 `EndDamage` smallint NOT NULL,
 `TimeDamage` smallint NOT NULL,
 `Duration` TINYINT UNSIGNED NOT NULL,
 `Attribute` TINYINT UNSIGNED NOT NULL,
 `Radius` TINYINT UNSIGNED NOT NULL,
 `Angle` smallint NOT NULL,
 PRIMARY KEY 
(
 `iNum`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE4`(
 `iNum` int NOT NULL,
 `BuffType` TINYINT UNSIGNED NOT NULL,
 `Radius` TINYINT UNSIGNED NOT NULL,
 `Duration` smallint NOT NULL,
 `AttackSpeed` TINYINT UNSIGNED NOT NULL,
 `Speed` TINYINT UNSIGNED NOT NULL,
 `AC` smallint NOT NULL,
 `ACPct` smallint NOT NULL,
 `Attack` TINYINT UNSIGNED NOT NULL,
 `MagicAttack` TINYINT UNSIGNED NOT NULL,
 `MaxHP` smallint NOT NULL,
 `MaxHPPct` smallint NOT NULL,
 `MaxMP` smallint NOT NULL,
 `MaxMPPct` smallint NOT NULL,
 `HitRate` TINYINT UNSIGNED NOT NULL,
 `AvoidRate` smallint NOT NULL,
 `Str` smallint NOT NULL,
 `Sta` smallint NOT NULL,
 `Dex` smallint NOT NULL,
 `Intel` smallint NOT NULL,
 `Cha` smallint NOT NULL,
 `FireR` TINYINT UNSIGNED NOT NULL,
 `ColdR` TINYINT UNSIGNED NOT NULL,
 `LightningR` TINYINT UNSIGNED NOT NULL,
 `MagicR` TINYINT UNSIGNED NOT NULL,
 `DiseaseR` TINYINT UNSIGNED NOT NULL,
 `PoisonR` TINYINT UNSIGNED NOT NULL,
 `ExpPct` smallint NOT NULL,
 `SpecialAmount` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE5`(
 `iNum` int NOT NULL,
 `Name` char(250) NULL,
 `Description` varchar(1000) NULL,
 `Type` TINYINT UNSIGNED NOT NULL,
 `ExpRecover` TINYINT UNSIGNED NOT NULL,
 `NeedStone` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE6`(
 `iNum` int NOT NULL,
 `Name` char(250) NULL,
 `Description` varchar(1000) NULL,
 `Size` smallint NOT NULL,
 `TransformID` smallint NOT NULL,
 `Duration` smallint NOT NULL,
 `MaxHp` smallint NOT NULL,
 `MaxMp` smallint NOT NULL,
 `Speed` TINYINT UNSIGNED NOT NULL,
 `AttackSpeed` smallint NOT NULL,
 `TotalHit` smallint NOT NULL,
 `TotalAc` smallint NOT NULL,
 `TotalHitRate` smallint NOT NULL,
 `TotalEvasionRate` smallint NOT NULL,
 `TotalFireR` smallint NOT NULL,
 `TotalColdR` smallint NOT NULL,
 `TotalLightningR` smallint NOT NULL,
 `TotalMagicR` smallint NOT NULL,
 `TotalDiseaseR` smallint NOT NULL,
 `TotalPoisonR` smallint NOT NULL,
 `Class` smallint NOT NULL,
 `UserSkillUse` TINYINT UNSIGNED NOT NULL,
 `NeedItem` TINYINT UNSIGNED NOT NULL,
 `SkillSuccessRate` TINYINT UNSIGNED NOT NULL,
 `MonsterFriendly` TINYINT UNSIGNED NOT NULL,
 `Nation` TINYINT UNSIGNED NOT NULL,
 PRIMARY KEY 
(
 `iNum`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE7`(
 `nIndex` int NOT NULL,
 `strName` char(250) NULL,
 `strNote` varchar(1000) NULL,
 `byValidGroup` TINYINT UNSIGNED NOT NULL,
 `byNatoinChange` TINYINT UNSIGNED NOT NULL,
 `shMonsterNum` smallint NOT NULL,
 `byTargetChange` TINYINT UNSIGNED NOT NULL,
 `byStateChange` TINYINT UNSIGNED NOT NULL,
 `byRadius` TINYINT UNSIGNED NOT NULL,
 `shHitrate` smallint NOT NULL,
 `shDuration` smallint NOT NULL,
 `shDamage` smallint NOT NULL,
 `byVisoin` TINYINT UNSIGNED NOT NULL,
 `nNeedItem` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE8`(
 `iNum` int NOT NULL,
 `Name` char(250) NULL,
 `Description` varchar(1000) NULL,
 `Target` TINYINT UNSIGNED NOT NULL,
 `Radius` smallint NOT NULL,
 `WarpType` TINYINT UNSIGNED NOT NULL,
 `ExpRecover` smallint NOT NULL,
 `KickDistance` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAGIC_TYPE9`(
 `iNum` int NOT NULL,
 `Name` char(250) NULL,
 `Description` varchar(1000) NULL,
 `ValidGroup` TINYINT UNSIGNED NOT NULL,
 `NationChange` TINYINT UNSIGNED NOT NULL,
 `MonsterNum` smallint NOT NULL,
 `TargetChange` TINYINT UNSIGNED NOT NULL,
 `StateChange` TINYINT UNSIGNED NOT NULL,
 `Radius` smallint NOT NULL,
 `Hitrate` smallint NOT NULL,
 `Duration` smallint NOT NULL,
 `AddDamage` smallint NOT NULL,
 `Vision` smallint NOT NULL,
 `NeedItem` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAIL_BOX`(
 `nLetterID` int AUTO_INCREMENT NOT NULL,
 `dtSendDate` datetime NOT NULL,
 `dtReadDate` datetime NULL,
 `bStatus` TINYINT UNSIGNED NOT NULL,
 `strSenderID` char(21) NOT NULL,
 `strRecipientID` char(21) NOT NULL,
 `strSubject` char(31) NOT NULL,
 `strMessage` char(128) NOT NULL,
 `bType` TINYINT UNSIGNED NOT NULL,
 `nItemID` int NOT NULL,
 `sCount` smallint NOT NULL,
 `sDurability` smallint NOT NULL,
 `nSerialNum` bigint NOT NULL,
 `nCoins` int NOT NULL,
 `bDeleted` TINYINT UNSIGNED(1) NOT NULL,
PRIMARY KEY 
(
 `nLetterID`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAIL_ITEM`(
 `ID` int AUTO_INCREMENT NOT NULL,
 `Status` TINYINT UNSIGNED NULL,
 `sId` char(21) NULL,
 `tId` char(21) NULL,
 `Title` char(32) NULL,
 `bAccess` TINYINT UNSIGNED NULL,
 `Content` char(130) NULL,
 `Type` TINYINT UNSIGNED NULL,
 `Gold` int NULL,
 `nNum` int NULL,
 `sCount` smallint NULL,
 `sTime` DATETIME NULL,
 `bDay` TINYINT UNSIGNED NULL,
 KEY `ix_auto_ID` (`ID`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAKE_DEFENSIVE`(
 `byLevel` TINYINT UNSIGNED NOT NULL,
 `sClass_1` smallint NULL,
 `sClass_2` smallint NULL,
 `sClass_3` smallint NULL,
 `sClass_4` smallint NULL,
 `sClass_5` smallint NULL,
 `sClass_6` smallint NULL,
 `sClass_7` smallint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAKE_ITEM`(
 `sIndex` smallint NOT NULL,
 `strItemInfo` char(20) NULL,
 `iItemCode` int NOT NULL,
 `byItemLevel` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAKE_ITEM_GRADECODE`(
 `byItemIndex` TINYINT UNSIGNED NOT NULL,
 `byGrade_1` smallint NOT NULL,
 `byGrade_2` smallint NULL,
 `byGrade_3` smallint NULL,
 `byGrade_4` smallint NULL,
 `byGrade_5` smallint NULL,
 `byGrade_6` smallint NULL,
 `byGrade_7` smallint NULL,
 `byGrade_8` smallint NULL,
 `byGrade_9` smallint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAKE_ITEM_GROUP`(
 `iItemGroupNum` int NOT NULL,
 `iItem_1` int NOT NULL,
 `iItem_2` int NOT NULL,
 `iItem_3` int NOT NULL,
 `iItem_4` int NOT NULL,
 `iItem_5` int NOT NULL,
 `iItem_6` int NOT NULL,
 `iItem_7` int NOT NULL,
 `iItem_8` int NOT NULL,
 `iItem_9` int NOT NULL,
 `iItem_10` int NOT NULL,
 `iItem_11` int NOT NULL,
 `iItem_12` int NOT NULL,
 `iItem_13` int NOT NULL,
 `iItem_14` int NOT NULL,
 `iItem_15` int NOT NULL,
 `iItem_16` int NOT NULL,
 `iItem_17` int NOT NULL,
 `iItem_18` int NOT NULL,
 `iItem_19` int NOT NULL,
 `iItem_20` int NOT NULL,
 `iItem_21` int NOT NULL,
 `iItem_22` int NOT NULL,
 `iItem_23` int NOT NULL,
 `iItem_24` int NOT NULL,
 `iItem_25` int NOT NULL,
 `iItem_26` int NOT NULL,
 `iItem_27` int NOT NULL,
 `iItem_28` int NOT NULL,
 `iItem_29` int NOT NULL,
 `iItem_30` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAKE_ITEM_LARECODE`(
 `byLevelGrade` TINYINT UNSIGNED NOT NULL,
 `sUpgradeItem` smallint NOT NULL,
 `sLareItem` smallint NOT NULL,
 `sMagicItem` smallint NOT NULL,
 `sGereralItem` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MAKE_WEAPON`(
 `byLevel` TINYINT UNSIGNED NOT NULL,
 `sClass_1` smallint NULL,
 `sClass_2` smallint NULL,
 `sClass_3` smallint NULL,
 `sClass_4` smallint NULL,
 `sClass_5` smallint NULL,
 `sClass_6` smallint NULL,
 `sClass_7` smallint NULL,
 `sClass_8` smallint NULL,
 `sClass_9` smallint NULL,
 `sClass_10` smallint NULL,
 `sClass_11` smallint NULL,
 `sClass_12` smallint NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MONSTER_CHALLENGE`(
 `sIndex` smallint NOT NULL,
 `bStartTime1` TINYINT UNSIGNED NOT NULL,
 `bStartTime2` TINYINT UNSIGNED NOT NULL,
 `bStartTime3` TINYINT UNSIGNED NOT NULL,
 `bLevelMin` TINYINT UNSIGNED NOT NULL,
 `bLevelMax` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MONSTER_CHALLENGE_SUMMON_LIST`(
 `sIndex` smallint NOT NULL,
 `bLevel` TINYINT UNSIGNED NOT NULL,
 `bStage` TINYINT UNSIGNED NOT NULL,
 `bStageLevel` TINYINT UNSIGNED NOT NULL,
 `sTime` smallint NOT NULL,
 `sSid` smallint NOT NULL,
 `strName` varchar(50) NULL,
 `sCount` smallint NOT NULL,
 `sPosX` smallint NOT NULL,
 `sPosZ` smallint NOT NULL,
 `bRange` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MONSTER_RESPAWN_LIST`(
 `sIndex` smallint NOT NULL,
 `sSid` smallint NOT NULL,
 `sCount` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MONSTER_RESPAWN_LIST_INFORMATION`(
 `sIndex` smallint NULL,
 `sSid` smallint NULL,
 `ZoneID` TINYINT UNSIGNED NULL,
 `bType` TINYINT UNSIGNED NULL,
 `X` smallint NULL,
 `Y` smallint NULL,
 `Z` smallint NULL,
 `sCount` smallint NULL,
 `bRadius` TINYINT UNSIGNED NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MONSTER_SUMMON_LIST`(
 `sSid` smallint NOT NULL,
 `strName` varchar(30) NULL,
 `sLevel` smallint NOT NULL,
 `sProbability` smallint NOT NULL,
 `bType` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `MONSTER_SUMMON_LIST_ZONE`(
 `nIndex` int NOT NULL,
 `ZoneID` smallint NOT NULL,
 `sSid` smallint NOT NULL,
 `strName` varchar(50) NULL,
 `byFamily` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `PREMIUM_ITEM`(
 `Type` TINYINT UNSIGNED NOT NULL,
 `ExpRestorePercent` smallint NOT NULL,
 `NoahPercent` smallint NOT NULL,
 `DropPercent` smallint NOT NULL,
 `BonusLoyalty` int NOT NULL,
 `RepairDiscountPercent` smallint NOT NULL,
 `ItemSellPercent` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `PREMIUM_ITEM_EXP`(
 `nIndex` smallint NOT NULL,
 `Type` TINYINT UNSIGNED NOT NULL,
 `MinLevel` TINYINT UNSIGNED NOT NULL,
 `MaxLevel` TINYINT UNSIGNED NOT NULL,
 `sPercent` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `PUS_CASH`(
 `cashid` int AUTO_INCREMENT NOT NULL,
 `Total` int NOT NULL,
 `Cash` int NOT NULL,
 `Aciklama` varchar(150) NOT NULL,
 `PID` int NOT NULL,
 PRIMARY KEY 
(
 `cashid`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `PUS_ITEMLERI`(
 `id` int NOT NULL,
 `PID` int NULL,
 `ExtraItem` TINYINT UNSIGNED(1) NOT NULL,
 `ItemIsmi` varchar(50) NULL,
 `ItemResmi` varchar(50) NULL,
 `itemkodu` int NOT NULL,
 `adet` int NOT NULL,
 `ucret` int NULL,
 `type` varchar(20) NULL,
 `alindi` int NOT NULL,
 `aciklama` varchar(250) NULL,
 `islev` varchar(150) NULL,
 `sure` varchar(50) NULL,
 `kullanim` varchar(250) NULL,
 `kosul` varchar(150) NULL,
 `reqtitle` TINYINT UNSIGNED NOT NULL,
 `ItemVer` TINYINT UNSIGNED(1) NOT NULL,
 `KomutCalistir` TINYINT UNSIGNED(1) NOT NULL,
 `Komut` varchar(50) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `PUS_ITEMLERI_PAKET`(
 `PID` int AUTO_INCREMENT NOT NULL,
 `PItem1` int NOT NULL,
 `Adet1` int NOT NULL,
 `PItem2` int NOT NULL,
 `Adet2` int NOT NULL,
 `PItem3` int NOT NULL,
 `Adet3` int NOT NULL,
 `PItem4` int NOT NULL,
 `Adet4` int NOT NULL,
 `PItem5` int NOT NULL,
 `Adet5` int NOT NULL,
 PRIMARY KEY 
(
 `PID`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `PUS_PAYPAL_LOG`(
 `strAccountID` varchar(50) NULL,
 `strUserID` varchar(50) NULL,
 `token` varchar(150) NULL,
 `cashID` int NULL,
 `eskiCash` int NULL,
 `yeniCash` int NULL,
 `tutar` int NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `QUEST_HELPER`(
 `nIndex` int NOT NULL,
 `bMessageType` TINYINT UNSIGNED NOT NULL,
 `bLevel` TINYINT UNSIGNED NOT NULL,
 `nExp` int NOT NULL,
 `bClass` TINYINT UNSIGNED NOT NULL,
 `bNation` TINYINT UNSIGNED NOT NULL,
 `bQuestType` TINYINT UNSIGNED NOT NULL,
 `bZone` TINYINT UNSIGNED NOT NULL,
 `sNpcId` smallint NOT NULL,
 `sEventDataIndex` smallint NOT NULL,
 `bEventStatus` TINYINT UNSIGNED NOT NULL,
 `nEventTriggerIndex` int NOT NULL,
 `nEventCompleteIndex` int NOT NULL,
 `nExchangeIndex` int NOT NULL,
 `nEventTalkIndex` int NOT NULL,
 `strLuaFilename` char(40) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `QUEST_LOG`(
 `sQuestNum` smallint NOT NULL,
 `nCount` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `QUEST_MENU`(
 `iNum` int NOT NULL,
 `strMenu` char(100) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `QUEST_MONSTER`(
 `sQuestNum` smallint NOT NULL,
 `sNum1a` smallint NOT NULL,
 `sNum1b` smallint NOT NULL,
 `sNum1c` smallint NOT NULL,
 `sNum1d` smallint NOT NULL,
 `sCount1` smallint NOT NULL,
 `sNum2a` smallint NOT NULL,
 `sNum2b` smallint NOT NULL,
 `sNum2c` smallint NOT NULL,
 `sNum2d` smallint NOT NULL,
 `sCount2` smallint NOT NULL,
 `sNum3a` smallint NOT NULL,
 `sNum3b` smallint NOT NULL,
 `sNum3c` smallint NOT NULL,
 `sNum3d` smallint NOT NULL,
 `sCount3` smallint NOT NULL,
 `sNum4a` smallint NOT NULL,
 `sNum4b` smallint NOT NULL,
 `sNum4c` smallint NOT NULL,
 `sNum4d` smallint NOT NULL,
 `sCount4` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `QUEST_TALK`(
 `iNum` int NOT NULL,
 `strTalk` varchar(1000) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `RENTAL_ITEM`(
 `nRentalIndex` int NOT NULL,
 `nItemIndex` int NULL,
 `sDurability` smallint NULL,
 `nSerialNumber` bigint NULL,
 `byRegType` TINYINT UNSIGNED NULL,
 `byItemType` TINYINT UNSIGNED NULL,
 `byClass` TINYINT UNSIGNED NULL,
 `sRentalTime` smallint NULL,
 `nRentalMoney` int NULL,
 `strLenderCharID` char(21) NULL,
 `strLenderAcID` char(21) NULL,
 `strBorrowerCharID` char(21) NULL,
 `strBorrowerAcID` char(21) NULL,
 `timeLender` DATETIME NULL,
 `timeRegister` DATETIME NOT NULL,
 PRIMARY KEY 
(
 `nRentalIndex`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `RENTAL_ITEM_LIST`(
 `nRentalIndex` int NOT NULL,
 `nItemIndex` int NULL,
 `sDurability` smallint NULL,
 `nSerialNumber` bigint NULL,
 `byRegType` TINYINT UNSIGNED NULL,
 `byItemType` TINYINT UNSIGNED NULL,
 `byClass` TINYINT UNSIGNED NULL,
 `sRentalTime` smallint NULL,
 `nRentalMoney` int NULL,
 `strLenderCharID` char(21) NULL,
 `strLenderAcID` char(21) NULL,
 `strBorrowerCharID` char(21) NULL,
 `strBorrowerAcID` char(21) NULL,
 `timeLender` DATETIME NULL,
 `timeRegister` DATETIME NULL,
 PRIMARY KEY 
(
 `nRentalIndex`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `S_ITEM`(
 `iClass` smallint NOT NULL,
 `strClass` char(10) NULL,
 `Gold` bigint NOT NULL,
 `strItem` varchar(1200) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `SEALED_ITEMS`(
 `ID` int AUTO_INCREMENT NOT NULL,
 `strAccountID` char(21) NOT NULL,
 `strUserID` char(21) NOT NULL,
 `nItemSerial` bigint NOT NULL,
 `nItemID` int NOT NULL,
 `bSealType` TINYINT UNSIGNED NOT NULL,
PRIMARY KEY 
(
 `ID`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `SERVER_RESOURCE`(
 `nResourceID` int NOT NULL,
 `strResource` varchar(250) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `SERVERS`(
 `sNum` bigint AUTO_INCREMENT NOT NULL,
 `sName` char(250) NULL,
 `sIP` char(15) NULL,
 `DatabaseName` char(100) NULL,
PRIMARY KEY 
(
 `sNum`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `SET_ITEM`(
 `SetIndex` int NOT NULL,
 `SetName` varchar(50) NULL,
 `ACBonus` smallint NOT NULL,
 `HPBonus` smallint NOT NULL,
 `MPBonus` smallint NOT NULL,
 `StrengthBonus` smallint NOT NULL,
 `StaminaBonus` smallint NOT NULL,
 `DexterityBonus` smallint NOT NULL,
 `IntelBonus` smallint NOT NULL,
 `CharismaBonus` smallint NOT NULL,
 `FlameResistance` smallint NOT NULL,
 `GlacierResistance` smallint NOT NULL,
 `LightningResistance` smallint NOT NULL,
 `PoisonResistance` smallint NOT NULL,
 `MagicResistance` smallint NOT NULL,
 `CurseResistance` smallint NOT NULL,
 `XPBonusPercent` smallint NOT NULL,
 `CoinBonusPercent` smallint NOT NULL,
 `APBonusPercent` smallint NOT NULL,
 `APBonusClassType` smallint NOT NULL,
 `APBonusClassPercent` smallint NOT NULL,
 `ACBonusClassType` smallint NOT NULL,
 `ACBonusClassPercent` smallint NOT NULL,
 `MaxWeightBonus` smallint NOT NULL,
 `NPBonus` TINYINT UNSIGNED NOT NULL,
 `Unk10` TINYINT UNSIGNED NOT NULL,
 `Unk11` TINYINT UNSIGNED NOT NULL,
 `Unk12` TINYINT UNSIGNED NOT NULL,
 `Unk13` TINYINT UNSIGNED NOT NULL,
 `Unk14` TINYINT UNSIGNED NOT NULL,
 `Unk15` TINYINT UNSIGNED NOT NULL,
 `Unk16` TINYINT UNSIGNED NOT NULL,
 `Unk17` TINYINT UNSIGNED NOT NULL,
 `Unk18` TINYINT UNSIGNED NOT NULL,
 `Unk19` TINYINT UNSIGNED NOT NULL,
 `Unk20` TINYINT UNSIGNED NOT NULL,
 `Unk21` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `SN_NEWS`(
 `id` int AUTO_INCREMENT NOT NULL,
 `type` varchar(150) NULL,
 date varchar(50) NULL,
 `titletr` varchar(80) NULL,
 `newstr` varchar(4096) NULL,
 `titleen` varchar(80) NULL,
 `newsen` varchar(4096) NULL,
 `titlees` varchar(80) NULL,
 `newses` varchar(4096) NULL,
 KEY `ix_auto_id` (`id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `SN_NEWS_MAIN`(
 `id` int AUTO_INCREMENT NOT NULL,
 `titletr` varchar(80) NULL,
 `newstr` varchar(4096) NULL,
 `titleen` varchar(80) NULL,
 `newsen` varchar(4096) NULL,
 `titlees` varchar(80) NULL,
 `newses` varchar(4096) NULL,
 `newsid` int NULL,
 `newsimage` int NULL,
 KEY `ix_auto_id` (`id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `START_POSITION`(
 `ZoneID` smallint NOT NULL,
 `sKarusX` smallint NOT NULL,
 `sKarusZ` smallint NOT NULL,
 `sElmoradX` smallint NOT NULL,
 `sElmoradZ` smallint NOT NULL,
 `sKarusGateX` smallint NULL,
 `sKarusGateZ` smallint NULL,
 `sElmoGateX` smallint NULL,
 `sElmoGateZ` smallint NULL,
 `bRangeX` TINYINT UNSIGNED NOT NULL,
 `bRangeZ` TINYINT UNSIGNED NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `START_POSITION_RANDOM`(
 `ZoneID` TINYINT UNSIGNED NULL,
 `PosX` smallint NULL,
 `PosZ` smallint NULL,
 `Radius` TINYINT UNSIGNED NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `TB_USER`(
 `ID` int AUTO_INCREMENT NOT NULL,
 `strAccountID` varchar(21) NOT NULL,
 `strPasswd` varchar(28) NULL,
 `strSealPasswd` char(8) NOT NULL,
 `strClientIP` char(15) NULL,
 `bPremiumType` TINYINT UNSIGNED NOT NULL,
 `dtPremiumTime` datetime NULL,
 `sHours` smallint NOT NULL,
 `dtCreateTime` datetime NULL,
 `strAuthority` TINYINT UNSIGNED NULL,
 `CashPoint` int NOT NULL,
 `PusAdmin` TINYINT UNSIGNED NULL,
 `Sepet` varchar(250) NULL,
 `StrSoru` varchar(500) NULL,
 `StrYanit` varchar(500) NULL,
 `Nick` varchar(21) NULL,
 `Access` varchar(12) NULL,
 `ulke` varchar(50) NULL,
 `PremiumExpire` datetime NULL,
 KEY `ix_auto_ID` (`ID`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USER_DAILY_OP`(
 `strUserID` char(21) NOT NULL,
 `ChaosMapTime` int NOT NULL,
 `UserRankRewardTime` int NOT NULL,
 `PersonalRankRewardTime` int NOT NULL,
 `KingWingTime` int NOT NULL,
 `WarderKillerTime1` int NOT NULL,
 `WarderKillerTime2` int NOT NULL,
 `KeeperKillerTime` int NOT NULL,
 `UserLoyaltyWingRewardTime` int NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USER_ITEMS`(
 `nItemID` int NULL,
 `nItemSerial` varchar(586) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USER_KNIGHTS_RANK`(
 `shIndex` smallint NOT NULL,
 `strName` char(21) NOT NULL,
 `strElmoUserID` varchar(21) NULL,
 `strElmoKnightsName` varchar(21) NULL,
 `nElmoLoyalty` int NULL,
 `strKarusUserID` varchar(21) NULL,
 `strKarusKnightsName` varchar(21) NULL,
 `nKarusLoyalty` int NULL,
 `nMoney` int NOT NULL,
 PRIMARY KEY 
(
 `shIndex`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USER_PERSONAL_RANK`(
 `nRank` smallint NOT NULL,
 `strPosition` char(21) NOT NULL,
 `nElmoUP` smallint NOT NULL,
 `strElmoUserID` char(21) NULL,
 `nElmoLoyaltyMonthly` int NULL,
 `nElmoCheck` int NOT NULL,
 `nKarusUP` smallint NOT NULL,
 `strKarusUserID` char(21) NULL,
 `nKarusLoyaltyMonthly` int NULL,
 `nKarusCheck` int NOT NULL,
 `nSalary` int NOT NULL,
 `UpdateDate` DATETIME NOT NULL,
 PRIMARY KEY 
(
 `nRank`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USER_RENTAL_ITEM`(
 `strUserID` char(21) NOT NULL,
 `strAccountID` char(21) NULL,
 `byRentalType` TINYINT UNSIGNED NULL,
 `byRegType` TINYINT UNSIGNED NULL,
 `nRentalIndex` int NULL,
 `nItemIndex` int NULL,
 `sDurability` smallint NULL,
 `nSerialNumber` bigint NULL,
 `nRentalMoney` int NULL,
 `sRentalTime` smallint NULL,
 `sDuringTime` smallint NULL,
 `timeRental` DATETIME NULL,
 `timeRegister` DATETIME NULL,
 PRIMARY KEY 
(
 `strUserID`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USER_SAVED_MAGIC`(
 `strCharID` char(21) NOT NULL,
 `nSkill1` int NOT NULL,
 `nDuring1` smallint NOT NULL,
 `nSkill2` int NOT NULL,
 `nDuring2` smallint NOT NULL,
 `nSkill3` int NOT NULL,
 `nDuring3` smallint NOT NULL,
 `nSkill4` int NOT NULL,
 `nDuring4` smallint NOT NULL,
 `nSkill5` int NOT NULL,
 `nDuring5` smallint NOT NULL,
 `nSkill6` int NOT NULL,
 `nDuring6` smallint NOT NULL,
 `nSkill7` int NOT NULL,
 `nDuring7` smallint NOT NULL,
 `nSkill8` int NOT NULL,
 `nDuring8` smallint NOT NULL,
 `nSkill9` int NOT NULL,
 `nDuring9` smallint NOT NULL,
 `nSkill10` int NOT NULL,
 `nDuring10` smallint NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USERDATA`(
 `strUserID` char(21) NOT NULL,
 `Nation` TINYINT UNSIGNED NOT NULL,
 `Race` TINYINT UNSIGNED NOT NULL,
 `Class` smallint NOT NULL,
 `HairRGB` TINYINT UNSIGNED NOT NULL,
 `Rank` TINYINT UNSIGNED NOT NULL,
 `Title` TINYINT UNSIGNED NOT NULL,
 `Level` TINYINT UNSIGNED NOT NULL,
 `Exp` int NOT NULL,
 `Loyalty` int NOT NULL,
 `Face` TINYINT UNSIGNED NOT NULL,
 `City` TINYINT UNSIGNED NOT NULL,
 `Knights` smallint NOT NULL,
 `Fame` TINYINT UNSIGNED NOT NULL,
 `Hp` smallint NOT NULL,
 `Mp` smallint NOT NULL,
 `Sp` smallint NOT NULL,
 `Strong` TINYINT UNSIGNED NOT NULL,
 `Sta` TINYINT UNSIGNED NOT NULL,
 `Dex` TINYINT UNSIGNED NOT NULL,
 `Intel` TINYINT UNSIGNED NOT NULL,
 `Cha` TINYINT UNSIGNED NOT NULL,
 `Authority` TINYINT UNSIGNED NOT NULL,
 `Points` smallint NOT NULL,
 `Gold` int NOT NULL,
 `Zone` TINYINT UNSIGNED NOT NULL,
 `Bind` smallint NULL,
 `PX` int NOT NULL,
 `PZ` int NOT NULL,
 `PY` int NOT NULL,
 `dwTime` int NOT NULL,
 `strSkill` varchar(10) NULL,
 `strItem` varbinary(584) NULL,
 `strSerial` varbinary(584) NULL,
 `sQuestCount` smallint NOT NULL,
 `strQuest` varbinary(600) NULL,
 `MannerPoint` int NOT NULL,
 `LoyaltyMonthly` int NOT NULL,
 `dtCreateTime` datetime NOT NULL,
 `dtUpdateTime` datetime NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `USERDATA_SKILLSHORTCUT`(
 `strCharID` char(21) NOT NULL,
 `nCount` smallint NOT NULL,
 `strSkillData` varchar(260) NULL,
 PRIMARY KEY 
(
 `strCharID`
)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `VERSION`(
 `sVersion` smallint NOT NULL,
 `sHistoryVersion` smallint NOT NULL,
 `strFilename` char(13) NOT NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `WAREHOUSE`(
 `strAccountID` char(21) NOT NULL,
 `nMoney` int NOT NULL,
 `dwTime` int NOT NULL,
 `WarehouseData` varbinary(1536) NULL,
 `strSerial` varbinary(1536) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `WEB_ITEMMALL`(
 `strAccountID` char(21) NOT NULL,
 `strCharID` char(21) NOT NULL,
 `ServerNo` smallint NOT NULL,
 `ItemID` int NOT NULL,
 `ItemCount` smallint NOT NULL,
 `BuyTime` DATETIME NOT NULL,
 `img_file_name` varchar(50) NULL,
 `strItemName` varchar(100) NULL,
 `price` int NULL,
 `pay_type` int NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `WEB_ITEMMALL_LOG`(
 `strAccountID` char(21) NOT NULL,
 `strCharID` char(21) NOT NULL,
 `ServerNo` smallint NOT NULL,
 `ItemID` int NOT NULL,
 `ItemCount` smallint NOT NULL,
 `BuyTime` DATETIME NOT NULL,
 `img_file_name` varchar(50) NULL,
 `strItemName` varchar(100) NULL,
 `price` int NULL,
 `pay_type` int NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

CREATE TABLE `ZONE_INFO`(
 `ServerNo` TINYINT UNSIGNED NOT NULL,
 `ZoneNo` smallint NOT NULL,
 `strZoneName` varchar(50) NOT NULL,
 `InitX` int NOT NULL,
 `InitZ` int NOT NULL,
 `InitY` int NOT NULL,
 `Type` TINYINT UNSIGNED NOT NULL,
 `RoomEvent` TINYINT UNSIGNED NOT NULL,
 `bz` varchar(50) NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC;

ALTER TABLE `_Aktivite` ALTER COLUMN `tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Anket` ALTER COLUMN `Oy1` SET DEFAULT 0;
ALTER TABLE `_Anket` ALTER COLUMN `Oy2` SET DEFAULT 0;
ALTER TABLE `_Anket` ALTER COLUMN `Oy3` SET DEFAULT 0;
ALTER TABLE `_Anket` ALTER COLUMN `Tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Anket` ALTER COLUMN `Haber_ID` SET DEFAULT 0;
ALTER TABLE `_Anket` ALTER COLUMN `Yer` SET DEFAULT 'N';
ALTER TABLE `_Haber` ALTER COLUMN `tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_IP_Engelliler` ALTER COLUMN `Tarih_Default` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_K2DLoyalty` ALTER COLUMN `Sayi` SET DEFAULT 1;
ALTER TABLE `_K2DLoyalty` ALTER COLUMN `Tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_K2DLoyalty` ALTER COLUMN `Son_Tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Link` ALTER COLUMN `indirilme` SET DEFAULT 0;
ALTER TABLE `_Link` ALTER COLUMN `tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Mail` ALTER COLUMN `tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Mail` ALTER COLUMN `okundu` SET DEFAULT 0;
ALTER TABLE `_Sifre_Degistir` ALTER COLUMN `Tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Sifre_Degistir` ALTER COLUMN `Durum` SET DEFAULT 'Pasif';
ALTER TABLE `_Sikayet` ALTER COLUMN `tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Ticket` ALTER COLUMN `tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `_Ticket` ALTER COLUMN `durum` SET DEFAULT 1;
ALTER TABLE `_Ticket_Cevap` ALTER COLUMN `Tarih` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `ACCOUNT_CHAR` ALTER COLUMN `bCharNum` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone3X` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone3Z` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone3LX` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone3LZ` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone4X` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone4Z` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone4LX` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone4LZ` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone5X` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone5Z` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone5LX` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone5LZ` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone6X` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone6Z` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone6LX` SET DEFAULT 0;
ALTER TABLE `HOME` ALTER COLUMN `BattleZone6LZ` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nIndex` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nNpcNum` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `bRandomFlag` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum1` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount1` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum2` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount2` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum3` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount3` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum4` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount4` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum5` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount5` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum6` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount6` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum7` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount7` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum8` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount8` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum9` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount9` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum10` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount10` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemNum11` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nOriginItemCount11` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemNum1` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemCount1` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemNum2` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemCount2` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemNum3` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemCount3` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemNum4` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemCount4` SET DEFAULT 0;
ALTER TABLE `ITEM_EXCHANGE` ALTER COLUMN `nExchangeItemNum5` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `Ranking` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `Members` SET DEFAULT 1;
ALTER TABLE `KNIGHTS` ALTER COLUMN `Gold` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `Domination` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `Points` SET DEFAULT 720000;
ALTER TABLE `KNIGHTS` ALTER COLUMN `sMarkVersion` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `sMarkLen` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `sCape` SET DEFAULT -1;
ALTER TABLE `KNIGHTS` ALTER COLUMN `bCapeR` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `bCapeG` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `bCapeB` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `sAllianceKnights` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `ClanPointFund` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `ClanPointMethod` SET DEFAULT 0;
ALTER TABLE `KNIGHTS` ALTER COLUMN `dtCreateTime` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sChallengeList_7` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sChallengeList_8` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sChallengeList_9` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sChallengeList_10` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `byWarRequestDay` SET DEFAULT 3;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `byWarRequestTime` SET DEFAULT 9;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `byWarRequestMinute` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `byGuerrillaWarDay` SET DEFAULT 1;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `byGuerrillaWarTime` SET DEFAULT 20;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `byGuerrillaWarMinute` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sMoradonTariff` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sDellosTariff` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `nDungeonCharge` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `nMoradonTax` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `nDellosTax` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_1` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_2` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_3` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_4` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_5` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_6` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_7` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_8` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_9` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_SIEGE_WARFARE` ALTER COLUMN `sRequestList_10` SET DEFAULT 0;
ALTER TABLE `KNIGHTS_USER` ALTER COLUMN `nDonatedNP` SET DEFAULT 0;
ALTER TABLE `MAGIC` ALTER COLUMN `BeforeAction` SET DEFAULT 0;
ALTER TABLE `MAGIC` ALTER COLUMN `Etc` SET DEFAULT 0;
ALTER TABLE `MAGIC` ALTER COLUMN `UseStanding` SET DEFAULT 0;
ALTER TABLE `MAGIC_TYPE3` ALTER COLUMN `EndDamage` SET DEFAULT 0;
ALTER TABLE `MAGIC_TYPE3` ALTER COLUMN `Angle` SET DEFAULT 0;
ALTER TABLE `MAGIC_TYPE8` ALTER COLUMN `KickDistance` SET DEFAULT 0;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `dtSendDate` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `bStatus` SET DEFAULT 1;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `bType` SET DEFAULT 1;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `nItemID` SET DEFAULT 0;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `sCount` SET DEFAULT 0;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `sDurability` SET DEFAULT 0;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `nSerialNum` SET DEFAULT 0;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `nCoins` SET DEFAULT 0;
ALTER TABLE `MAIL_BOX` ALTER COLUMN `bDeleted` SET DEFAULT 0;
ALTER TABLE `PUS_CASH` ALTER COLUMN `Total` SET DEFAULT 0;
ALTER TABLE `PUS_CASH` ALTER COLUMN `Cash` SET DEFAULT 0;
ALTER TABLE `PUS_CASH` ALTER COLUMN `PID` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI` ALTER COLUMN `ExtraItem` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI` ALTER COLUMN `adet` SET DEFAULT 1;
ALTER TABLE `PUS_ITEMLERI` ALTER COLUMN `alindi` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI` ALTER COLUMN `reqtitle` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI` ALTER COLUMN `ItemVer` SET DEFAULT 1;
ALTER TABLE `PUS_ITEMLERI` ALTER COLUMN `KomutCalistir` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `PItem1` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `Adet1` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `PItem2` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `Adet2` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `PItem3` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `Adet3` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `PItem4` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `Adet4` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `PItem5` SET DEFAULT 0;
ALTER TABLE `PUS_ITEMLERI_PAKET` ALTER COLUMN `Adet5` SET DEFAULT 0;
ALTER TABLE `RENTAL_ITEM` ALTER COLUMN `sDurability` SET DEFAULT 0;
ALTER TABLE `RENTAL_ITEM` ALTER COLUMN `byRegType` SET DEFAULT 0;
ALTER TABLE `RENTAL_ITEM` ALTER COLUMN `timeRegister` SET DEFAULT 0;
ALTER TABLE `RENTAL_ITEM_LIST` ALTER COLUMN `byRegType` SET DEFAULT 0;
ALTER TABLE `RENTAL_ITEM_LIST` ALTER COLUMN `timeRegister` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `S_ITEM` ALTER COLUMN `Gold` SET DEFAULT 0;
ALTER TABLE `TB_USER` ALTER COLUMN `strSealPasswd` SET DEFAULT 12345678;
ALTER TABLE `TB_USER` ALTER COLUMN `bPremiumType` SET DEFAULT 0;
ALTER TABLE `TB_USER` ALTER COLUMN `dtPremiumTime` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `TB_USER` ALTER COLUMN `sHours` SET DEFAULT 0;
ALTER TABLE `TB_USER` ALTER COLUMN `dtCreateTime` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `TB_USER` ALTER COLUMN `strAuthority` SET DEFAULT 1;
ALTER TABLE `TB_USER` ALTER COLUMN `CashPoint` SET DEFAULT 0;
ALTER TABLE `USER_PERSONAL_RANK` ALTER COLUMN `nElmoCheck` SET DEFAULT 0;
ALTER TABLE `USER_PERSONAL_RANK` ALTER COLUMN `nKarusCheck` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill1` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring1` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill2` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring2` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill3` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring3` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill4` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring4` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill5` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring5` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill6` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring6` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill7` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring7` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill8` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring8` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill9` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring9` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nSkill10` SET DEFAULT 0;
ALTER TABLE `USER_SAVED_MAGIC` ALTER COLUMN `nDuring10` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Rank` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Title` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Level` SET DEFAULT 1;
ALTER TABLE `USERDATA` ALTER COLUMN `Exp` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Loyalty` SET DEFAULT 100;
ALTER TABLE `USERDATA` ALTER COLUMN `City` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Knights` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Fame` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Hp` SET DEFAULT 100;
ALTER TABLE `USERDATA` ALTER COLUMN `Mp` SET DEFAULT 100;
ALTER TABLE `USERDATA` ALTER COLUMN `Sp` SET DEFAULT 100;
ALTER TABLE `USERDATA` ALTER COLUMN `Authority` SET DEFAULT 1;
ALTER TABLE `USERDATA` ALTER COLUMN `Points` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Gold` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `Zone` SET DEFAULT 21;
ALTER TABLE `USERDATA` ALTER COLUMN `PX` SET DEFAULT 31000;
ALTER TABLE `USERDATA` ALTER COLUMN `PZ` SET DEFAULT 36000;
ALTER TABLE `USERDATA` ALTER COLUMN `PY` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `dwTime` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `sQuestCount` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `MannerPoint` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `LoyaltyMonthly` SET DEFAULT 0;
ALTER TABLE `USERDATA` ALTER COLUMN `dtCreateTime` SET DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE `WAREHOUSE` ALTER COLUMN `nMoney` SET DEFAULT 0;
ALTER TABLE `WAREHOUSE` ALTER COLUMN `dwTime` SET DEFAULT 0;

SET FOREIGN_KEY_CHECKS=1;
