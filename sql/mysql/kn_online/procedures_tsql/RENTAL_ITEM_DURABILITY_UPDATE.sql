CREATE PROCEDURE [dbo].[RENTAL_ITEM_DURABILITY_UPDATE]
@nRentalIndex  int,
@nDurability   int

AS

DECLARE @Row tinyint
DECLARE @LendercharID char(21), @LenderAccountID char(21)

SET @Row = 0

SELECT @LendercharID = strLenderCharID, @LenderAccountID = strLenderAcID
FROM RENTAL_ITEM_LIST 
WHERE nRentalIndex = @nRentalIndex

UPDATE USER_RENTAL_ITEM
SET sDurability = @nDurability
WHERE strAccountID = @LenderAccountID and nRentalIndex = @nRentalIndex
