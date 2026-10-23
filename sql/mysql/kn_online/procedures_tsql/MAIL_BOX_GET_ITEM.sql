CREATE PROCEDURE [dbo].[MAIL_BOX_GET_ITEM]
@strRecipientID char(21),
@nLetterID int
AS

SELECT nItemID, sCount, sDurability, nCoins, nSerialNum FROM MAIL_BOX WHERE nLetterID = @nLetterID AND strRecipientID = @strRecipientID AND bStatus = 1
UPDATE MAIL_BOX SET bStatus = 2 WHERE nLetterID = @nLetterID AND strRecipientID = @strRecipientID
