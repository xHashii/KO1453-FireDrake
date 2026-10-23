CREATE PROCEDURE [dbo].[UPDATE_USER_DAILY_OP]
@strCharID char(21),
@bType tinyint,
@iUnixTime int
AS

IF (@bType = 1)
	UPDATE USER_DAILY_OP SET ChaosMapTime = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 2)
	UPDATE USER_DAILY_OP SET UserRankRewardTime = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 3)
	UPDATE USER_DAILY_OP SET PersonalRankRewardTime = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 4)
	UPDATE USER_DAILY_OP SET KingWingTime = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 5)
	UPDATE USER_DAILY_OP SET WarderKillerTime1 = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 6)
	UPDATE USER_DAILY_OP SET WarderKillerTime2 = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 7)
	UPDATE USER_DAILY_OP SET KeeperKillerTime = @iUnixTime WHERE strUserID = @strCharID
ELSE IF (@bType = 8)
	UPDATE USER_DAILY_OP SET UserLoyaltyWingRewardTime = @iUnixTime WHERE strUserID = @strCharID
