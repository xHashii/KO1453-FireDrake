CREATE PROCEDURE [dbo].[MAIL_OPEN_NEWLETTER]
@strUserId	varchar(21),
@ID		int
AS
/* NERO 24.06.2012 */
UPDATE MAIL_ITEM Set Status = 2 Where tID = @strUserId And ID = @ID And Type = 1
SELECT ID, Content FROM MAIL_ITEM Where tId = @strUserId And ID = @ID
