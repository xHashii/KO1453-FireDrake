CREATE PROCEDURE [dbo].[UPDATE_USER_STYLE]
@strUserId	char(21),
@Face		tinyint,
@HairR	 	smallint,
@HairG	 	smallint,
@HairB 	smallint,
@HairColor	tinyint,
@nRet		smallint OUTPUT
AS

/*
Nero Chaos Expansion v1.886
21.06.2012
*/

SET @nRet = 0

BEGIN TRAN	

	UPDATE USERDATA Set Face = @Face, HairR = @HairR, HairG = @HairG, HairB = @HairB, HairColor = @HairColor Where strUserId = @strUserId

	IF @@ERROR <> 0
	BEGIN	 
		ROLLBACK TRAN 
		SET @nRet =  0
		RETURN
	END

COMMIT TRAN

SET @nRet =  1
RETURN
