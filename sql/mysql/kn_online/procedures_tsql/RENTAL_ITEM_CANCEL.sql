CREATE PROCEDURE [dbo].[RENTAL_ITEM_CANCEL]
@AccountID	char(21),		
@CharID	char(21),			
@nRentalIndex  int,
@nItemNumber   int,
@nRet		smallint OUTPUT

AS

DELETE FROM RENTAL_ITEM WHERE nRentalIndex = @nRentalIndex and nItemIndex = @nItemNumber


DELETE FROM USER_RENTAL_ITEM WHERE nRentalIndex = @nRentalIndex and nItemIndex = @nItemNumber

SET @nRet =  1
