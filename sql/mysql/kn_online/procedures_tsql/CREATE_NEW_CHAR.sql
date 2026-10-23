CREATE PROCEDURE [dbo].[CREATE_NEW_CHAR]
@strAccountID varchar(21),
@index tinyint,
@strCharID varchar(21),
@bRace tinyint,
@sClass smallint,
@nHair int,
@bFace tinyint,
@bStr tinyint,
@bSta tinyint,
@bDex tinyint,
@bIntel tinyint,
@bCha tinyint
AS

DECLARE @bNation tinyint
DECLARE @bCharCount tinyint
DECLARE @bCount tinyint

SELECT @bNation = bNation, @bCharCount = bCharNum FROM ACCOUNT_CHAR WHERE strAccountID = @strAccountID

IF (@bNation = 1 AND @bRace > 10)
	RETURN 2
ELSE IF (@bNation = 2 AND @bRace < 10)
	RETURN 2
ELSE IF (@bNation <> 1 AND @bNation <> 2)
	RETURN 2

SELECT @bCount = COUNT(strUserID) FROM USERDATA WHERE strUserID = @strCharID

IF (@bCount > 0)
	RETURN 3

BEGIN TRAN
	IF (@index = 0)
		UPDATE ACCOUNT_CHAR SET strCharID1 = @strCharID, bCharNum += 1 WHERE strAccountID = @strAccountID
	ELSE IF (@index = 1)
		UPDATE ACCOUNT_CHAR SET strCharID2 = @strCharID, bCharNum += 1 WHERE strAccountID = @strAccountID
	ELSE IF (@index = 2)
		UPDATE ACCOUNT_CHAR SET strCharID3 = @strCharID, bCharNum += 1 WHERE strAccountID = @strAccountID

	IF (@@ERROR <> 0 OR @@ROWCOUNT = 0)
	BEGIN
		ROLLBACK TRAN
		RETURN 4
	END

	INSERT INTO USERDATA (strUserID, Nation, Race, Class, HairRGB, Face, Strong, Sta, Dex, Intel, Cha) 
	VALUES (@strCharID, @bNation, @bRace, @sClass, @nHair, @bFace, @bStr, @bSta, @bDex, @bIntel, @bCha)
		/* Başlangıç İtemleri */
	
	DECLARE @bClass tinyint
	SET @bClass = SUBSTRING(RTRIM(LTRIM(STR(@sClass))),3,1)

	DECLARE @bLevel int
	DECLARE @bExp bigint
    DECLARE @bStrong smallint
    DECLARE @nDex smallint
    DECLARE @nIntel smallint
    DECLARE @nCha smallint
    DECLARE @nSta smallint
    DECLARE @bSkill varchar(10)
    DECLARE @bGold int
    DECLARE @bstrItem binary(584)
    DECLARE @bWarehouseData binary(1536)
	DECLARE @bLoyalty int
	DECLARE @bPoints smallint
	
	SELECT @bLevel = Level, @bExp = Exp,@bStrong = Strong,@bDex = Dex, @bIntel = Intel, @bCha = Cha, @bSta = Sta,@bSkill = Skill,@bGold = Gold, @bstrItem = strItem,@bWarehouseData = WarehouseData, @bLoyalty = Loyalty,@bPoints = Points FROM BEGINNER_ITEM WHERE Class = @bClass

	IF @bStrong = 0 SET @bStrong = @bStr
	IF @bDex = 0 SET @bDex = @bDex
	IF @bIntel = 0 SET @bIntel = @bIntel
	IF @bCha = 0 SET @bCha = @bCha
	IF @bSta = 0 SET @bSta = @bSta

	UPDATE USERDATA SET Level = @bLevel,Exp = @bExp/*,Strong = @bStrong,Dex = @bDex,Intel = @bIntel,Cha = @bCha,Sta = @bSta*/,strSkill = @bSkill,Gold = @bGold,strItem = @bstrItem,Loyalty = @bLoyalty,Points = @bPoints WHERE strUserId = @strCharID
	--UPDATE WAREHOUSE SET WarehouseData = @bWarehouseData WHERE strAccountID = @strAccountID

	UPDATE USERDATA SET Class = Class + 5 WHERE strUserId = @strCharID AND @bClass = 1
	UPDATE USERDATA SET Class = Class + 6 WHERE strUserId = @strCharID AND @bClass = 2
	UPDATE USERDATA SET Class = Class + 7 WHERE strUserId = @strCharID AND @bClass = 3
	UPDATE USERDATA SET Class = Class + 8 WHERE strUserId = @strCharID AND @bClass = 4
	
	/* Başlangıç İtemleri */
	--warrior
	EXEC EDIT_QUEST @strCharID,51 , 2
	EXEC EDIT_QUEST @strCharID,510 , 2
	EXEC EDIT_QUEST @strCharID,511 , 2
	--rogue
	EXEC EDIT_QUEST @strCharID,53 , 2
	EXEC EDIT_QUEST @strCharID,515 , 2
	EXEC EDIT_QUEST @strCharID,516 , 2
	EXEC EDIT_QUEST @strCharID,517 , 2
	--mage
	EXEC EDIT_QUEST @strCharID,52 , 2
	EXEC EDIT_QUEST @strCharID,512 , 2
	EXEC EDIT_QUEST @strCharID,513 , 2
	EXEC EDIT_QUEST @strCharID,514 , 2
	---pre
	EXEC EDIT_QUEST @strCharID,54 , 2
	EXEC EDIT_QUEST @strCharID,518 , 2
	EXEC EDIT_QUEST @strCharID,519 , 2
	EXEC EDIT_QUEST @strCharID,520 , 2
	EXEC EDIT_QUEST @strCharID,521 , 2
	EXEC EDIT_QUEST @strCharID,522 , 2
	EXEC EDIT_QUEST @strCharID,523 , 2

	IF (@@ERROR <> 0)
	BEGIN
		ROLLBACK TRAN
		RETURN 4
	END
COMMIT TRAN

RETURN 0
