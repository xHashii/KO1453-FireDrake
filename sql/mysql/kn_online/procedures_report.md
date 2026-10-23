# Routines in KN_Online needing a manual T-SQL -> MariaDB/C# port

Original T-SQL in `procedures_tsql/`. Port each as a MariaDB procedure
or lift it into C# repository code (see docs/CSHARP_REWRITE_PLAN.md).

| Type | Name | Signature (truncated) |
|---|---|---|
| FUNCTION | GET_ACCOUNT_NAME | `` |
| FUNCTION | GET_RACE_FOR_ACCOUNT_NATION_TRANSFER | `` |
| FUNCTION | HashPasswordString | `(@inputPassword varchar(50)) RETURNS varchar(50)` |
| FUNCTION | IsValidCharacters | `varchar(8000)) RETURNS INT` |
| PROCEDURE | ACCOUNT_LOGIN | `@strAccountID varchar(21), @strPasswd varchar(28)` |
| PROCEDURE | ACCOUNT_LOGOUT | `@strAccountID varchar(21)` |
| PROCEDURE | ACCOUNT_PREMIUM | `@strAccountID char(20)` |
| PROCEDURE | ADD_KESN_CODES | `@Owner int, @KCID int, @KESNCodeCount int` |
| PROCEDURE | ADD_KNIGHT_CASH | `@AccountID varchar(30), @Knight_Cash int, @OID int` |
| PROCEDURE | CHANGE_HAIR | `@strAccountID varchar(21), @strCharID varchar(21), @bType tinyint, @bFace tinyint, @nHair int` |
| PROCEDURE | CHANGE_NEW_ID | `@byType char(21), @AccountID char(21), @OldCharID char(21), @NewCharID char(21), @nRet smallint output` |
| PROCEDURE | CLEAR_REMAIN_USERS | `@strServerIP varchar(50)` |
| PROCEDURE | CREATE_INSERT_SCRIPT | `@Table varchar(50)` |
| PROCEDURE | CREATE_KNIGHTS | `@sClanID smallint, @bNation tinyint, @bFlag tinyint, @strKnightsName char(21), @strChief char(21)` |
| PROCEDURE | CREATE_NEW_CHAR | `@strAccountID varchar(21), @index tinyint, @strCharID varchar(21), @bRace tinyint, @sClass smallint, @nHair int, @bFace tinyint, @bStr tinyint, @bSta tinyint, @bDex tinyint, @bIntel tinyint, @bCha tinyint` |
| PROCEDURE | DELETE_ACCOUNT | `@AccountID char(21)` |
| PROCEDURE | DELETE_FRIEND_LIST | `@strUserID char(21), @strFriend char(21)` |
| PROCEDURE | DELETE_KNIGHTS | `@sClanID smallint` |
| PROCEDURE | DELETE_USER | `@strUserId char(21)` |
| PROCEDURE | DONATE_CLAN_POINTS | `@strUserID char(21), @sClanID smallint, @nNationalPoints int` |
| PROCEDURE | EDIT_QUEST | `( @strUserId varchar(21), @sQuestId smallint, @bQuestStatus tinyint )` |
| PROCEDURE | EDIT_SKILLS | `( @strUserId varchar(21), @newFree tinyint = null, @newCat1 tinyint = null, @newCat2 tinyint = null, @newCat3 tinyint = null, @newMasters tinyint = null )` |
| PROCEDURE | GAME_LOGIN | `@strAccountID varchar(21), @strPasswd varchar(28)` |
| PROCEDURE | INSERT_FRIEND_LIST | `@strUserID char(21), @strFriend char(21)` |
| PROCEDURE | INSERT_UPGRADE | `@itemname varchar(50), -- NAME OF ITEM TO UPGRADE @index int, -- RANDOMNUMBER` |
| PROCEDURE | INSERT_USER_DAILY_OP | `@strCharID char(21), @iChaosMapTime int, @iUserRankRewardTime int, @iPersonalRankRewardTime int, @iKingWingTime int, @iWarderKillerTime1 int, @iWarderKillerTime2 int, @iKeeperKillerTime int, @iUserLoyaltyWingRewardTime int` |
| PROCEDURE | LOAD_ACCOUNT_CHARID | `@AccountID char(21)` |
| PROCEDURE | LOAD_CHAR_INFO | `@CharId varchar(21), @nRet smallint OUTPUT` |
| PROCEDURE | LOAD_KNIGHTS_MEMBERS | `@knightsindex smallint` |
| PROCEDURE | LOAD_PET_DATA | `@id char(21), @nRet smallint OUTPUT` |
| PROCEDURE | LOAD_PREMIUM_SERVICE_USER123 | `@strAccountID char(20), @bType tinyint OUTPUT, @sTime smallint OUTPUT` |
| PROCEDURE | LOAD_RENTAL_DATA | `@strAccountID char(21)` |
| PROCEDURE | LOAD_SAVED_MAGIC | `@strUserId char(21)` |
| PROCEDURE | LOAD_SEALED_ITEM_DATA | `@strAccountID char(21)` |
| PROCEDURE | LOAD_USER_DATA | `@strAccountID varchar(21), @strCharID varchar(21)` |
| PROCEDURE | LOAD_WEB_ITEMMALL | `@strCharID char(21)` |
| PROCEDURE | MAIL_BOX_CHECK_COUNT | `@strRecipientID char(21)` |
| PROCEDURE | MAIL_BOX_DELETE_LETTER | `@strRecipientID varchar(21), @nLetterID int` |
| PROCEDURE | MAIL_BOX_GET_ITEM | `@strRecipientID char(21), @nLetterID int` |
| PROCEDURE | MAIL_BOX_READ | `@strRecipientID char(21), @nLetterID int` |
| PROCEDURE | MAIL_BOX_REQUEST_LIST | `@strRecipientID char(21), @bNewLettersOnly tinyint` |
| PROCEDURE | MAIL_BOX_SEND | `@strSenderID char(21), @strRecipientID char(21), @strSubject char(31), @strMessage char(128), @bType tinyint, @nItemID int, @sCount smallint, @sDurability smallint, @nSerialNum bigint` |
| PROCEDURE | MAIL_CHECK_NEWLETTER | `@strUserId varchar(21), @Status tinyint, @nRet smallint OUTPUT` |
| PROCEDURE | MAIL_CHECK_USER | `@strUserID varchar(21), @nRet smallint OUTPUT` |
| PROCEDURE | MAIL_DELETE_OLDLETTER | `@strUserId varchar(21), @ID int, @nRet smallint OUTPUT` |
| PROCEDURE | MAIL_OPEN_NEWLETTER | `@strUserId varchar(21), @ID int` |
| PROCEDURE | MAIL_TAKE_LETTERITEM | `@strUserId char(21), @ID int` |
| PROCEDURE | MAIN_LOGIN | `@AccountID varchar(21), @Password varchar(32), @nRet smallint OUTPUT` |
| PROCEDURE | NATION_SELECT | `@strAccountID varchar(21), @bNation tinyint` |
| PROCEDURE | PRE_VER | `@strUserId varchar(30)` |
| PROCEDURE | RENTAL_ITEM_CANCEL | `@AccountID char(21), @CharID char(21), @nRentalIndex int, @nItemNumber int, @nRet smallint OUTPUT` |
| PROCEDURE | RENTAL_ITEM_DESTORY | `@AccountID char(21), @CharID char(21), @nItemNumber int, @nRentalIndex int, @nDurability smallint, @nRet smallint OUTPUT` |
| PROCEDURE | RENTAL_ITEM_DURABILITY_UPDATE | `@nRentalIndex int, @nDurability int` |
| PROCEDURE | RENTAL_ITEM_LEND | `@AccountID char(21), -- 대여자 계정 @CharID char(21), -- 대여자 캐릭 아이디 @nRentalIndex int, @nItemNumber int, @nRet smallint OUTPUT` |
| PROCEDURE | RENTAL_ITEM_REGISTER | `@charID char(21), @AccountID char(21), @sRentalTime smallint, @nItemID int, @sDurability smallint, @nMoney int, @bGameBangType tinyint, @bItemType tinyint, @bItemClass tinyint, @nSerialNumber bigint, @nRet_Index int OUTPUT, @nRet smallint OUTPUT` |
| PROCEDURE | RESET_LOYALTY_MONTHLY | `` |
| PROCEDURE | SAVE_PREMIUM_SERVICE_USER | `@strAccountID char(20), @strCharID char(20), @bType tinyint, @sTime smallint` |
| PROCEDURE | SET_LOGIN_INFO | `@strAccountID varchar(20), @strCharID varchar(20), @nServerno smallint, @strServerIP varchar(50), @strClientIP varchar(50), @bInit tinyint` |
| PROCEDURE | SHOPPINGMALL_BUY | `@nRet smallint OUTPUT, @strCharID char(21), @strAccountID char(21), @nItemID int, @sCount smallint` |
| PROCEDURE | SKILLSHORTCUT_LOAD | `@strCharID varchar(21)` |
| PROCEDURE | SKILLSHORTCUT_SAVE | `@strCharID varchar(21), @nCount smallint, @strSkillData varchar(260)` |
| PROCEDURE | UPDATE_KNIGHTS | `@bType tinyint, @strCharID char(21), @sClanID smallint, @bDomination tinyint` |
| PROCEDURE | UPDATE_KNIGHTS_ALLIANCE | `@byType tinyint, @shAlliancIndex smallint, @shKnightsIndex smallint, @byEmptyIndex tinyint, @bySiegeFlag tinyint` |
| PROCEDURE | UPDATE_KNIGHTS_CAPE | `@KnightsIndex smallint, @sCapeC smallint, @sCapeR smallint, @sCapeG smallint, @sCapeB smallint, @nRet smallint OUTPUT` |
| PROCEDURE | UPDATE_KNIGHTS_MARK | `@IDNum smallint, @MarkVersion smallint, @MarkLen smallint, @KnightMark varbinary(2400), @nRet smallint OUTPUT` |
| PROCEDURE | UPDATE_KNIGHTS_RATING | `` |
| PROCEDURE | UPDATE_RANKS | `` |
| PROCEDURE | UPDATE_SAVED_MAGIC | `@strCharID varchar(21), @Skill1 int, @During1 smallint, @Skill2 int, @During2 smallint, @Skill3 int, @During3 smallint, @Skill4 int, @During4 smallint, @Skill5 int, @During5 smallint, @Skill6 int, @During6 smallint, @Skill7 int, @During7 smallint, @Skill8 int, @During8 smallint, @Skill9 int, @During9 smallint, @Skill10 int, @During10 smallint` |
| PROCEDURE | UPDATE_USER_DAILY_OP | `@strCharID char(21), @bType tinyint, @iUnixTime int` |
| PROCEDURE | UPDATE_USER_DATA | `@strCharID char(21), @bNation tinyint, @bRace tinyint, @sClass smallint, @nHair int, @bRank tinyint, @bTitle tinyint, @bLevel tinyint, @iExp bigint, @nLoyalty int, @bFace tinyint, @bCity tinyint, @sClanID smallint, @bFame tinyint, @sHp smallint, @sMp smallint, @sSp smallint, @bStr tinyint, @bSta tinyint, @bDex tinyint, @bIntel tinyint, @bCha tinyint, @bAuthority tinyint, @sPoints smallint, @nCoins` |
| PROCEDURE | UPDATE_USER_KNIGHTS_RANK | `` |
| PROCEDURE | UPDATE_USER_PERSONAL_RANK | `` |
| PROCEDURE | UPDATE_USER_STYLE | `@strUserId char(21), @Face tinyint, @HairR smallint, @HairG smallint, @HairB smallint, @HairColor tinyint, @nRet smallint OUTPUT` |
| PROCEDURE | UPDATE_WAREHOUSE | `@accountid varchar(21), @Money int, @dwTime int, @strItem binary(1536), @strSerial binary(1536)` |
| PROCEDURE | USER_ITEM_SEAL | `@strAccountID char(21), @strCharID char(21), @strPasswd char(8), @nItemSerial bigint, @nItemID int, @bSealType tinyint` |
| PROCEDURE | VIEW_QUESTS | `( @strUserId varchar(21) )` |
| PROCEDURE | VIEW_SKILLS | `( @strUserId varchar(21) )` |
