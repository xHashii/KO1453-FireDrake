CREATE FUNCTION [dbo].[GET_ACCOUNT_NAME]
(@strUserId char(21))
/*
Author : AKUMA
Update : 30.06.2009 - 08:42
*/
RETURNS nvarchar(21)

BEGIN	
	DECLARE @AccountID nvarchar(21)
	
	SELECT @AccountID = strAccountID FROM ACCOUNT_CHAR WHERE strCharID1 = @strUserId and strCharID1 is not null
	SELECT @AccountID = strAccountID FROM ACCOUNT_CHAR WHERE strCharID2 = @strUserId and strCharID2 is not null
	SELECT @AccountID = strAccountID FROM ACCOUNT_CHAR WHERE strCharID3 = @strUserId and strCharID3 is not null
	
	--IF @AccountID is null
		--SET @AccountID = NULL
	
	RETURN @AccountID
END
