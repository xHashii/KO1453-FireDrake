CREATE PROCEDURE [dbo].[MAIL_TAKE_LETTERITEM]
@strUserId	char(21),
@ID		int
AS
/* NERO 24.06.2012 */
UPDATE MAIL_ITEM Set Status = 2 Where tId = @strUserId And ID = @ID And Type = 2
SELECT nNum, sCount, Gold FROM MAIL_ITEM Where tId = @strUserId And ID = @ID
