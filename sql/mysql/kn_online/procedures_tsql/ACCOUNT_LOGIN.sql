CREATE PROCEDURE [dbo].[ACCOUNT_LOGIN]
@strAccountID varchar(21),
@strPasswd varchar(28)
AS

DECLARE @strHashPassword varchar(28)

SELECT @strHashPassword = strPasswd FROM TB_USER WHERE strAccountID = @strAccountID

IF (@@ROWCOUNT = 0)
BEGIN
	INSERT INTO TB_USER (strAccountID, strPasswd) VALUES (@strAccountID, @strPasswd)
	RETURN 1
END
ELSE IF (@strHashPassword != @strPasswd)
	RETURN 3
ELSE
BEGIN
	DELETE FROM CURRENTUSER WHERE strAccountID = @strAccountID
	RETURN 1
END
