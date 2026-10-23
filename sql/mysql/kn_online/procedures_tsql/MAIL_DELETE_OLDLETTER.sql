CREATE PROCEDURE [dbo].[MAIL_DELETE_OLDLETTER]
@strUserId	varchar(21),
@ID		int,
@nRet		smallint OUTPUT
AS
/* NERO 24.06.2012 */
UPDATE MAIL_ITEM Set bAccess = 2 Where ID = @ID And tId = @strUserId And bAccess = 1 And Status = 2
SET @nRet = 1
RETURN
