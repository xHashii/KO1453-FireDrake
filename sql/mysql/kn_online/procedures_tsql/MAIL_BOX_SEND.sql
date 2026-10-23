CREATE PROCEDURE [dbo].[MAIL_BOX_SEND]
@strSenderID char(21),
@strRecipientID char(21),
@strSubject char(31),
@strMessage char(128),
@bType tinyint,
@nItemID int,
@sCount smallint,
@sDurability smallint,
@nSerialNum bigint
AS

IF (NOT EXISTS(SELECT strUserID FROM USERDATA WHERE strUserID = @strRecipientID))
	RETURN -1

INSERT INTO MAIL_BOX (strSenderID, strRecipientID, strSubject, strMessage, bType, nItemID, sCount, sDurability, nSerialNum) 
VALUES (@strSenderID, @strRecipientID, @strSubject, @strMessage, @bType, @nItemID, @sCount, @sDurability, @nSerialNum)

RETURN 1
