CREATE PROCEDURE [dbo].[LOAD_USER_DATA]
@strAccountID varchar(21),
@strCharID varchar(21)
AS

DECLARE @strCharID1 varchar(21), @strCharID2 varchar(21), @strCharID3 varchar(21)

SELECT	@strCharID1 = ISNULL(strCharID1, ''), 
		@strCharID2 = ISNULL(strCharID2, ''), 
		@strCharID3 = ISNULL(strCharID3, '') 
FROM ACCOUNT_CHAR WHERE strAccountID = @strAccountID

IF (@strCharID NOT IN(@strCharID1, @strCharID2, @strCharID3))
	RETURN
	UPDATE USERDATA SET Class = Class + 1 WHERE strUserId = @strCharID AND Level > 59 AND Class IN(105, 107, 109, 111, 205, 207, 209, 211)
SELECT Nation, Race, Class, HairRGB, [Rank], Title, [Level], [Exp], Loyalty, Face, City, Knights, Fame, Hp, Mp, Sp, Strong, Sta, Dex, Intel, Cha, Authority, Points, Gold, Zone, Bind, PX, PZ, PY, dwTime, strSkill, strItem,strSerial, sQuestCount, strQuest, MannerPoint, LoyaltyMonthly FROM USERDATA WHERE strUserID = @strCharID
EXEC UPDATE_USER_KNIGHTS_RANK
EXEC UPDATE_USER_PERSONAL_RANK
EXEC UPDATE_KNIGHTS_RATING
