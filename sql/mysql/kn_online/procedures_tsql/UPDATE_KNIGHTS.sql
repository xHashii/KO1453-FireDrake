CREATE PROCEDURE [dbo].[UPDATE_KNIGHTS]
@bType tinyint,
@strCharID char(21),
@sClanID smallint,
@bDomination tinyint
AS

DECLARE @bCount tinyint
DECLARE @bMembers tinyint
DECLARE @bUserMembers tinyint
DECLARE @sKnights smallint
DECLARE @strViceChief1 char(21)
DECLARE @strViceChief2 char(21)
DECLARE @strViceChief3 char(21)

SELECT @bCount = COUNT(*) FROM KNIGHTS WHERE IDNum = @sClanID
IF (@bCount = 0)
	RETURN 7

SELECT @bMembers = Members, @strViceChief1 = ViceChief_1, @strViceChief2 = ViceChief_2, @strViceChief3 = ViceChief_3 FROM KNIGHTS WHERE IDNum = @sClanID
IF (@bType = 18)
BEGIN
	SELECT @bUserMembers = COUNT(*) FROM USERDATA WHERE Knights = @sClanID
	
	IF (@bMembers >= 36 OR @bUserMembers >= 36)
		RETURN 8
END
ELSE IF (@bMembers > 36)
	RETURN 8

SELECT @bCount = COUNT(*) FROM USERDATA WHERE strUserID = @strCharID
IF (@bCount = 0)
	RETURN 2

BEGIN TRAN
	IF (@bType = 18)
	BEGIN
		UPDATE KNIGHTS SET Members += 1 WHERE IDNum = @sClanID
		INSERT INTO KNIGHTS_USER (sIDNum, strUserID) VALUES (@sClanID, @strCharID)
		UPDATE USERDATA SET Knights = @sClanID WHERE strUserID = @strCharID
	END
	ELSE IF (@bType IN(19, 20, 23))
	BEGIN
		IF (@bMembers <= 1)
			UPDATE KNIGHTS SET Members = 1 WHERE IDNum = @sClanID
		ELSE
			UPDATE KNIGHTS SET Members -= 1 WHERE IDNum = @sClanID

		DELETE FROM KNIGHTS_USER WHERE strUserID = @strCharID
		
		IF (@strViceChief1 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_1 = NULL WHERE IDNum = @sClanID	
		IF (@strViceChief2 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_2 = NULL WHERE IDNum = @sClanID	
		IF (@strViceChief3 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_3 = NULL WHERE IDNum = @sClanID	
	END
	ELSE IF (@bType = 25)
	BEGIN
		UPDATE KNIGHTS SET Chief = @strCharID WHERE IDNum = @sClanID
		
		IF (@strViceChief1 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_1 = NULL WHERE IDNum = @sClanID
		IF (@strViceChief2 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_2 = NULL WHERE IDNum = @sClanID
		IF (@strViceChief3 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_3 = NULL WHERE IDNum = @sClanID
	END
	ELSE IF (@bType = 26)
	BEGIN
		IF (@strViceChief1 IS NOT NULL AND @strViceChief2 IS NOT NULL AND @strViceChief3 IS NOT NULL)
		BEGIN
			COMMIT TRAN
			RETURN 8
		END
		IF (@strViceChief1 IS NULL)
			UPDATE KNIGHTS SET ViceChief_1 = @strCharID WHERE IDNum = @sClanID
		ELSE IF (@strViceChief2 IS NULL)
			UPDATE KNIGHTS SET ViceChief_2 = @strCharID WHERE IDNum = @sClanID
		ELSE IF (@strViceChief3 IS NULL)
			UPDATE KNIGHTS SET ViceChief_3 = @strCharID WHERE IDNum = @sClanID
	END
	ELSE IF (@bType = 27)
		UPDATE KNIGHTS SET ViceChief_2 = @strCharID WHERE IDNum = @sClanID
	ELSE IF (@bType = 76)
		UPDATE KNIGHTS SET ClanPointMethod = @bDomination WHERE IDNum = @sClanID
	ELSE IF (@bType = 95)
	BEGIN
		UPDATE KNIGHTS SET Chief = @strCharID WHERE IDNum = @sClanID
		
		IF (@strViceChief1 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_1 = NULL WHERE IDNum = @sClanID
		IF (@strViceChief2 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_2 = NULL WHERE IDNum = @sClanID
		IF (@strViceChief3 = @strCharID)
			UPDATE KNIGHTS SET ViceChief_3 = NULL WHERE IDNum = @sClanID
	END

	IF (@@ERROR <> 0)
	BEGIN	 
		ROLLBACK TRAN
		RETURN 2
	END

	IF (@bType = 20)
	BEGIN
		UPDATE USERDATA SET Knights = -1, Fame = 0 WHERE strUserID = @strCharID
		DELETE FROM KNIGHTS_USER WHERE strUserID = @strCharID
	END
COMMIT TRAN

RETURN 0
