CREATE PROCEDURE [dbo].[MAIL_BOX_REQUEST_LIST]
@strRecipientID char(21),
@bNewLettersOnly tinyint
AS

DECLARE @expiresInDays int = 7

IF (@bNewLettersOnly = 1)
	SELECT TOP 12 nLetterID, bStatus, bType, strSubject, strSenderID, bType, nItemID, sCount, nCoins, YEAR(dtSendDate) * 10000 + MONTH(dtSendDate) * 100 + DAY(dtSendDate), DATEDIFF(DD, GETDATE(), dtSendDate + @expiresInDays) FROM MAIL_BOX WHERE strRecipientID = @strRecipientID AND bStatus = 1 AND bDeleted = 0
ELSE
	SELECT TOP 20 nLetterID, bStatus, bType, strSubject, strSenderID, bType, nItemID, sCount, nCoins, YEAR(dtReadDate) * 10000 + MONTH(dtReadDate) * 100 + DAY(dtReadDate), DATEDIFF(DD, dtReadDate, GETDATE()) FROM MAIL_BOX WHERE strRecipientID = @strRecipientID AND bStatus = 2 AND bDeleted = 0

RETURN @@ROWCOUNT
