CREATE PROCEDURE [dbo].[GAME_LOGIN]
@strAccountID varchar(21),
@strPasswd varchar(28)
AS

DECLARE @bNation tinyint
DECLARE @strHashPassword varchar(28)

SELECT @strHashPassword = strPasswd FROM TB_USER WHERE strAccountID = @strAccountID

IF (@@ROWCOUNT = 0)
	RETURN -1
ELSE IF (@strHashPassword <> @strPasswd)
	RETURN -1

SELECT @bNation = bNation FROM ACCOUNT_CHAR WHERE strAccountID = @strAccountID

IF (@@ROWCOUNT = 0)
	RETURN 0

RETURN @bNation
