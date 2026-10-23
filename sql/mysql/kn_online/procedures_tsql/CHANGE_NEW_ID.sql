CREATE PROCEDURE [dbo].[CHANGE_NEW_ID]
@byType char(21),
@AccountID char(21),
@OldCharID char(21),
@NewCharID char(21),
@nRet smallint output
AS
/*
Author : AKUMA
Update : 20.02.2010 - 18:43
*/

IF dbo.IsValidCharacters(@NewCharID) = 1
BEGIN
	SET @nRet = 1
	RETURN
END
ELSE
BEGIN
	
	DECLARE @IsFreeUserId int


	SELECT @IsFreeUserId = COUNT(strUserId) FROM USERDATA WHERE strUserId = @NewCharID

	IF @IsFreeUserId = 0 
	BEGIN
		
		-- Change Account All Information
		UPDATE ACCOUNT_CHAR SET strCharID1 = @NewCharID WHERE strCharID1 = @OldCharID AND strCharID1 is not null
		UPDATE ACCOUNT_CHAR SET strCharID2 = @NewCharID WHERE strCharID2 = @OldCharID AND strCharID2 is not null
		UPDATE ACCOUNT_CHAR SET strCharID3 = @NewCharID WHERE strCharID3 = @OldCharID AND strCharID3 is not null
		UPDATE USERDATA SET strUserId = @NewCharID WHERE strUserId = @OldCharID AND strUserId is not null
		
		-- Change Information a User in Knights
		UPDATE KNIGHTS_USER SET strUserID = @NewCharID WHERE strUserID = @OldCharID AND strUserID is not null
		UPDATE KNIGHTS SET Chief = @NewCharID WHERE Chief = @OldCharID AND Chief is not null
		UPDATE KNIGHTS SET ViceChief_1 = @NewCharID WHERE ViceChief_1 = @OldCharID AND ViceChief_1 is not null
		UPDATE KNIGHTS SET ViceChief_2 = @NewCharID WHERE ViceChief_2 = @OldCharID AND ViceChief_2 is not null
		UPDATE KNIGHTS SET ViceChief_3 = @NewCharID WHERE ViceChief_3 = @OldCharID AND ViceChief_3 is not null
			
		-- Change Information a User is King
		UPDATE KING_SYSTEM SET strKingName = @NewCharID WHERE strKingName = @OldCharID AND strKingName is not null
		UPDATE KING_ELECTION_LIST SET strName = @NewCharID WHERE strName = @OldCharID AND strName is not null
	
		-- Change User Friend List Information
		UPDATE FRIEND_LIST SET strUserID = @NewCharID where strUserID = @OldCharID
		
		-- User Rental Item
		UPDATE RENTAL_ITEM SET strLenderCharID = @NewCharID WHERE strLenderCharID = @OldCharID AND strLenderCharID is not null
		UPDATE RENTAL_ITEM SET strBorrowerCharID = @NewCharID WHERE strBorrowerCharID = @OldCharID AND strBorrowerCharID is not null
		UPDATE RENTAL_ITEM_LIST SET strBorrowerCharID = @NewCharID WHERE strBorrowerCharID = @OldCharID AND strBorrowerCharID is not null
			
		-- Change CurrentUser
		UPDATE CURRENTUSER SET strCharID = @NewCharID WHERE strCharID = @OldCharID
		
		-- Change Saved Magic
		UPDATE USER_SAVED_MAGIC SET strCharID = @NewCharID WHERE strCharID = @OldCharID
			
		-- Change Skill Shorcut
		UPDATE USERDATA_SKILLSHORTCUT SET strCharID = @NewCharID WHERE strCharID = @OldCharID
		
		-- Name Change is Sucessfull
		SET @nRet = 0
		RETURN
			
	END
	ELSE
	BEGIN
		-- Entered ID is Already Being Used Another Character
		SET @nRet = 1
		RETURN
	END
END
