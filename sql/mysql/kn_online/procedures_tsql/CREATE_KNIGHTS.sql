CREATE PROCEDURE [dbo].[CREATE_KNIGHTS]
@sClanID smallint,
@bNation tinyint,
@bFlag tinyint,
@strKnightsName char(21),
@strChief char(21)
AS

DECLARE @bCount tinyint

SELECT @bCount = COUNT(*) FROM KNIGHTS WHERE IDNum = @sClanID OR IDName = @strKnightsName

IF (@bCount > 0)
	RETURN 3

BEGIN TRAN
	INSERT INTO KNIGHTS (IDNum, Nation, Flag, IDName, Chief) VALUES (@sClanID, @bNation, @bFlag, @strKnightsName, @strChief)
	INSERT INTO KNIGHTS_USER (sIDNum, strUserID) VALUES (@sClanID, @strChief)

	IF (@@ERROR <> 0)
	BEGIN
		ROLLBACK TRAN
		RETURN 6
	END
COMMIT TRAN

RETURN 0
