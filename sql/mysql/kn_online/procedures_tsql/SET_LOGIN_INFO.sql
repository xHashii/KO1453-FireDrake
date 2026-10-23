CREATE PROCEDURE [dbo].[SET_LOGIN_INFO]
@strAccountID varchar(20),
@strCharID varchar(20),
@nServerno smallint,
@strServerIP varchar(50),
@strClientIP varchar(50),
@bInit tinyint
AS

IF (@bInit = 1)
	INSERT INTO CURRENTUSER (strAccountID, strCharID, nServerNo, strServerIP, strClientIP) VALUES (@strAccountID, @strCharID, @nServerno, @strServerIP, @strClientIP)
ELSE
	UPDATE CURRENTUSER SET nServerNo = @nServerno, strServerIP = @strServerIP WHERE strAccountID = @strAccountID

UPDATE TB_USER SET strClientIP = @strClientIP WHERE strAccountID = @strAccountID

RETURN 1
