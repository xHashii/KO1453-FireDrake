CREATE PROCEDURE [dbo].[INSERT_USER_DAILY_OP]
@strCharID char(21),
@iChaosMapTime int,
@iUserRankRewardTime int,
@iPersonalRankRewardTime int,
@iKingWingTime int,
@iWarderKillerTime1 int,
@iWarderKillerTime2 int,
@iKeeperKillerTime int,
@iUserLoyaltyWingRewardTime int
AS

DELETE FROM USER_DAILY_OP WHERE strUserID = @strCharID
INSERT INTO USER_DAILY_OP (strUserID, ChaosMapTime, UserRankRewardTime, PersonalRankRewardTime, KingWingTime, WarderKillerTime1, WarderKillerTime2, KeeperKillerTime, UserLoyaltyWingRewardTime) VALUES (@strCharID, @iChaosMapTime, @iUserRankRewardTime, @iPersonalRankRewardTime, @iKingWingTime, @iWarderKillerTime1, @iWarderKillerTime2, @iKeeperKillerTime, @iUserLoyaltyWingRewardTime)
