CREATE PROC [dbo].[UPDATE_KNIGHTS_MARK]
@IDNum	smallint,
@MarkVersion	smallint,
@MarkLen	smallint,
@KnightMark	varbinary(2400),
@nRet		smallint OUTPUT
AS

/*
Nero Chaos Expansion v1.886
15.06.2012
*/
SET @nRet = 0

BEGIN TRAN	

	UPDATE KNIGHTS SET sMarkLen = @MarkLen, sMarkVersion = @MarkVersion, Mark = @KnightMark WHERE IDNum = @IDNum

	IF @@ERROR <> 0
	BEGIN	 
		ROLLBACK TRAN 
		SET @nRet =  0
		RETURN
	END
	ELSE
	BEGIN
		SET @nRet =  1
		RETURN
	END

COMMIT TRAN

SET @nRet =  0
