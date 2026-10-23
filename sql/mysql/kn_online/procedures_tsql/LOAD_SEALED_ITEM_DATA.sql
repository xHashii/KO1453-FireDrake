--Add the procedure that'll handle the loading of your character's sealed item(s)
CREATE PROCEDURE [dbo].[LOAD_SEALED_ITEM_DATA]
@strAccountID	char(21)
AS

SELECT nItemSerial, nItemID, bSealType
FROM SEALED_ITEMS
WHERE strAccountID = @strAccountID
