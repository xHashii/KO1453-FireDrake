CREATE PROCEDURE [dbo].[MAIL_CHECK_NEWLETTER]
@strUserId	varchar(21),
@Status	tinyint,
@nRet		smallint OUTPUT
AS
/* NERO 24.06.2012 */
SELECT  @nRet =  COUNT(*) FROM MAIL_ITEM WHERE Status = @Status AND tId = @strUserId
RETURN
