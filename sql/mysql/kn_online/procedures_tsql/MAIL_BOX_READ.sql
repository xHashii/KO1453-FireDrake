CREATE PROCEDURE [dbo].[MAIL_BOX_READ]
@strRecipientID char(21),
@nLetterID int
AS

SELECT strMessage FROM MAIL_BOX WHERE strRecipientID = @strRecipientID AND nLetterID = @nLetterID
	
IF (@@ROWCOUNT != 0)
BEGIN
	UPDATE MAIL_BOX SET dtReadDate = GETDATE(), bStatus = 2 WHERE nLetterID = @nLetterID AND bType = 1 AND dtReadDate IS NULL
	UPDATE MAIL_BOX SET dtReadDate = GETDATE() WHERE nLetterID = @nLetterID AND bType = 2 AND dtReadDate IS NULL
END
