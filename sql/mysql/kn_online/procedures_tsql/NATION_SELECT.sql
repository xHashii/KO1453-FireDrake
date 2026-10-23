CREATE PROCEDURE [dbo].[NATION_SELECT]
@strAccountID varchar(21),
@bNation tinyint
AS

DECLARE @bCount tinyint

BEGIN TRAN
	SELECT @bCount = COUNT(strAccountID) FROM ACCOUNT_CHAR WHERE strAccountID = @strAccountID

	IF (@bCount = 0)
		INSERT INTO ACCOUNT_CHAR (strAccountID, bNation) VALUES (@strAccountID, @bNation)

	SELECT @bCount = COUNT(strAccountID) FROM WAREHOUSE WHERE strAccountID = @strAccountID

	IF (@bCount = 0)
		INSERT INTO WAREHOUSE (strAccountID, nMoney, dwTime, WarehouseData, strSerial) VALUES (@strAccountID, 0, 0, 0x00, 0x00)

	IF (@@ERROR <> 0)
	BEGIN
		ROLLBACK TRAN
		RETURN 0
	END
COMMIT TRAN

RETURN @bNation
