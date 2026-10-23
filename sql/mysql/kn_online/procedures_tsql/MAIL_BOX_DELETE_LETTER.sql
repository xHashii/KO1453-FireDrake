CREATE PROCEDURE [dbo].[MAIL_BOX_DELETE_LETTER]
	@strRecipientID varchar(21),
	@nLetterID int
AS
UPDATE MAIL_BOX SET bDeleted = 1 WHERE id = @nLetterID AND strRecipientID = @strRecipientID
