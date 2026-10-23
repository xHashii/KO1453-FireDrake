CREATE PROCEDURE [dbo].[LOAD_PREMIUM_SERVICE_USER123]
@strAccountID char(20),
@bType tinyint OUTPUT,
@sTime smallint OUTPUT
AS
DECLARE @bPremiumType tinyint
DECLARE @dtPremiumTime datetime

-- Get the current premium expire time and current premium type.
SELECT @bPremiumType = bPremiumType, @dtPremiumTime = dtPremiumTime FROM TB_USER WHERE strAccountID = @strAccountID

-- How many time differ between now and the last premium expire date.
SET @sTime = DATEDIFF(HH, GETDATE(), @dtPremiumTime)
SET @bType = @bPremiumType

-- Check if the last premium expire date is null/empty or
-- the hours between now and the last premium expire date is below or equal to 0
IF (@sTime <= 0 OR @sTime IS NULL OR @@ERROR <> 0 )
BEGIN
	SET @bType = 0
	SET @sTime = 0
END
