CREATE PROCEDURE [dbo].[MAIL_BOX_CHECK_COUNT]
@strRecipientID char(21)
AS

DECLARE @bCount tinyint

SELECT TOP 12 @bCount = COUNT(*) FROM MAIL_BOX WHERE strRecipientID = @strRecipientID AND bStatus = 1 AND bDeleted = 0

RETURN @bCount
