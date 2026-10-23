CREATE PROCEDURE [dbo].[CHANGE_HAIR]
@strAccountID varchar(21),
@strCharID varchar(21),
@bType tinyint,
@bFace tinyint,
@nHair int
AS

IF (NOT EXISTS(SELECT strAccountID FROM ACCOUNT_CHAR WHERE strAccountID = @strAccountID AND @strCharID IN(strCharID1, strCharID2, strCharID3)))
	RETURN 1

UPDATE USERDATA SET HairRGB = @nHair, Face = @bFace WHERE strUserID = @strCharID 

RETURN 0
