CREATE PROCEDURE [dbo].[ACCOUNT_PREMIUM]
@strAccountID char(20)
AS
DECLARE @sHours smallint
DECLARE @dtPremiumTime datetime

-- Get the current premium expire time and current premium type.
SELECT @dtPremiumTime = dtPremiumTime FROM TB_USER WHERE strAccountID = @strAccountID

-- How many time differ between now and the last premium expire date.
SET @sHours = DATEDIFF(HH, GETDATE(), @dtPremiumTime)

-- Check if the last premium expire date is null/empty or
-- the hours between now and the last premium expire date is below or equal to 0
IF (@sHours <= 0 OR @sHours IS NULL OR @@ERROR <> 0 )
	RETURN -1

RETURN @sHours
