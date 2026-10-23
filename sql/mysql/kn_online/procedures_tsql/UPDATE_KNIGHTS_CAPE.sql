CREATE PROCEDURE [dbo].[UPDATE_KNIGHTS_CAPE]
@KnightsIndex	smallint,
@sCapeC	smallint,
@sCapeR 	smallint,
@sCapeG 	smallint,
@sCapeB 	smallint,
@nRet		smallint OUTPUT
AS

/*
Nero Chaos Expansion v1.886
14.06.2012
*/

SET @nRet = 0

BEGIN TRAN	

	UPDATE KNIGHTS Set sCapeC = @sCapeC, sCapeR = @sCapeR, sCapeG = @sCapeG, sCapeB = @sCapeB Where IDNum = @KnightsIndex

	IF @@ERROR <> 0
	BEGIN	 
		ROLLBACK TRAN 
		SET @nRet =  0
		RETURN
	END

COMMIT TRAN

SET @nRet =  1
RETURN
