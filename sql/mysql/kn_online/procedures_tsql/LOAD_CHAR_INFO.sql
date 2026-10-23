CREATE PROCEDURE [dbo].[LOAD_CHAR_INFO]
	@CharId	varchar(21),
	@nRet	smallint OUTPUT
AS
SELECT @nRet = COUNT(strUserId) FROM USERDATA WHERE strUserId = @CharId
IF @nRet = 0
	RETURN

SET @nRet = 1
SELECT Race, Class, HairRGB, [Level], Face, Zone, strItem FROM USERDATA WHERE strUserID = @CharId
