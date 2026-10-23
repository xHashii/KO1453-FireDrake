CREATE PROCEDURE [dbo].[SHOPPINGMALL_BUY]
@nRet		smallint OUTPUT, 
@strCharID 	char(21), 
@strAccountID	char(21), 
@nItemID	int,
@sCount	smallint
AS
BEGIN TRAN	

	DECLARE @ItemName varchar(255),@Price int
	SELECT @ItemName = strName FROM ITEM WHERE Num = @nItemID
	SELECT @Price = Price FROM PUS_ITEMS WHERE ItemCode = @nItemID

	INSERT INTO WEB_ITEMMALL  (strAccountID, strCharID, ServerNo, ItemID, ItemCount  ) VALUES (@strAccountID, @strCharID, 0, @nItemID, @sCount )
	
	IF @@ERROR <> 0
	BEGIN	 
		ROLLBACK TRAN 
		INSERT INTO PUS_PAYMENT_LOGS (AccountID,ItemCode,ItemName,Quantity,Status,Message,Order_Date) VALUES ('In Game : ' + RTRIM(LTRIM(@strAccountID)) + ' -> ' + RTRIM(LTRIM(@strCharID)),@nItemID,@ItemName,@sCount,1,'In Game Error',GETDATE())
		SET @nRet =  -1
		RETURN
	END
	ELSE
	BEGIN
		INSERT INTO WEB_ITEMMALL_LOG  (strAccountID, strCharID, ServerNo, ItemID, ItemCount, BuyTime, img_file_name, strItemName, price, pay_type ) VALUES (RTRIM(LTRIM(@strAccountID)), @strCharID, 0, @nItemID, @sCount,GETDATE(),STR(@nItemID) + '.gif',@ItemName,@Price,0)
		-- Adding PUS_PAYMENT_LOGS
		INSERT INTO PUS_PAYMENT_LOGS (AccountID,ItemCode,ItemName,Quantity,Status,Message,Order_Date) VALUES ('In Game : ' + RTRIM(LTRIM(@strAccountID)) + ' -> '  + RTRIM(LTRIM(@strCharID)),@nItemID,@ItemName,@sCount,1,'Başarılı',GETDATE())
	END

SET @nRet =  1

COMMIT TRAN
