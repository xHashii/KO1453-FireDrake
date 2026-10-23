CREATE PROCEDURE [dbo].[SAVE_PREMIUM_SERVICE_USER]
@strAccountID char(20),
@strCharID char(20),
@bType tinyint,
@sTime smallint
AS
DECLARE @dtExpiry datetime

SET @dtExpiry = DATEADD(HH, @sTime, GETDATE())

UPDATE TB_USER SET bPremiumType = @bType, dtPremiumTime = @dtExpiry, sHours = @sTime WHERE strAccountID = @strAccountID
