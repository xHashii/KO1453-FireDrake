CREATE PROCEDURE [dbo].[LOAD_WEB_ITEMMALL]
@strCharID	char(21)
AS
BEGIN TRAN
-- Adding Inventory Item
SELECT strAccountID, ItemID, ItemCount, BuyTime FROM WEB_ITEMMALL WHERE strCharID = @strCharID
-- Deleting Record
DELETE FROM WEB_ITEMMALL WHERE strCharID = @strCharID

COMMIT TRAN
