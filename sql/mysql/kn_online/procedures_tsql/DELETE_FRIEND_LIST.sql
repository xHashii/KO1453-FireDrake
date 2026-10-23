CREATE PROCEDURE [dbo].[DELETE_FRIEND_LIST]
@strUserID char(21),
@strFriend char(21)
AS

DECLARE @strFriend1 char(21), @strFriend2 char(21), @strFriend3 char(21), @strFriend4 char(21), @strFriend5 char(21), @strFriend6 char(21)
DECLARE @strFriend7 char(21), @strFriend8 char(21), @strFriend9 char(21), @strFriend10 char(21), @strFriend11 char(21), @strFriend12 char(21)
DECLARE @strFriend13 char(21), @strFriend14 char(21), @strFriend15 char(21), @strFriend16 char(21), @strFriend17 char(21), @strFriend18 char(21)
DECLARE @strFriend19 char(21), @strFriend20 char(21), @strFriend21 char(21), @strFriend22 char(21), @strFriend23 char(21), @strFriend24 char(21) 

SELECT	@strFriend1 = strFriend1, @strFriend2 = strFriend2,@strFriend3 = strFriend3,@strFriend4 = strFriend4,
		@strFriend5 = strFriend5, @strFriend6 = strFriend6,@strFriend7 = strFriend7,@strFriend8 = strFriend8,
		@strFriend9 = strFriend9, @strFriend10 = strFriend10,@strFriend11 = strFriend11,@strFriend12 = strFriend12,
		@strFriend13 = strFriend13, @strFriend14 = strFriend14,@strFriend15 = strFriend15,@strFriend16 = strFriend16,
		@strFriend17 = strFriend17, @strFriend18 = strFriend18,@strFriend19 = strFriend19,@strFriend20 = strFriend20,
		@strFriend21 = strFriend21, @strFriend22 = strFriend22,@strFriend23 = strFriend23,@strFriend24 = strFriend24 
		FROM FRIEND_LIST WHERE strUserID = @strUserID

IF (@@ROWCOUNT = 0)
	RETURN 1

IF (@strFriend1 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend1 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend2 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend2 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend3 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend3 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend4 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend4 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend5 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend5 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend6 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend6 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend7 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend7 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend8 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend8 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend9 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend9 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend10 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend10 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend11 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend11 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend12 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend12 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend13 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend13 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend14 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend14 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend15 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend15 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend16 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend16 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend17 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend17 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend18 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend18 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend19 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend19 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend20 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend20 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend21 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend21 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend22 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend22 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend23 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend23 = NULL WHERE strUserID = @strUserID
ELSE IF (@strFriend24 = @strFriend)
	UPDATE FRIEND_LIST SET strFriend24 = NULL WHERE strUserID = @strUserID
ELSE
	RETURN 2

IF (@@ERROR <> 0)
	RETURN 1

RETURN 0
