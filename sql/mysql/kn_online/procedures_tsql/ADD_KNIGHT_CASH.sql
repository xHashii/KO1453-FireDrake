CREATE PROCEDURE [dbo].[ADD_KNIGHT_CASH]
@AccountID varchar(30),
@Knight_Cash int,
@OID int
AS

DECLARE @CheckOID tinyint
DECLARE @CheckAccountID tinyint

SELECT @CheckOID = COUNT(OID) FROM PUS_ORDERS WHERE AccountID = @AccountID And OID = @OID

DECLARE @ONID int,@Status tinyint

SELECT @ONID = ONID,@Status = Status FROM PUS_ORDERS WHERE AccountID = @AccountID And OID = @OID

DECLARE @GetDonative int

SET @GetDonative = 0

SELECT @GetDonative = Donative FROM PUS_SELLING_KNIGHT_CASH_INFORMATION WHERE Quantity = @Knight_Cash

IF @CheckOID = 1
BEGIN
	IF @Status = 1
	BEGIN
		PRINT '"' + RTRIM(LTRIM(STR(@OID))) + '" Numaralı Sipariş Zaten Aktif.'
	END
	ELSE
	BEGIN
		IF @Knight_Cash = 0
		BEGIN
		DELETE FROM PUS_ORDERS WHERE AccountID = @AccountID And OID = @OID
		DELETE FROM PUS_ORDER_NOTIFICATIONS WHERE ONID = @ONID
		PRINT RTRIM(LTRIM(@AccountID)) + ' Hesabının Siparişi Silinmiştir..!'
		END
		ELSE
		BEGIN
		UPDATE TB_USER SET Knight_Cash = Knight_Cash + @Knight_Cash + @GetDonative WHERE strAccountID = @AccountID
		UPDATE PUS_ORDERS SET Status = 1 WHERE AccountID = @AccountID And OID = @OID
		PRINT RTRIM(LTRIM(@AccountID)) + ' Hesabına "' + RTRIM(LTRIM(STR(@Knight_Cash + @GetDonative))) + '" Knight Cash Eklenmiştir.'
		END
	END
END
ELSE IF @OID = 0
BEGIN
	IF @Knight_Cash = 0
	BEGIN
		DELETE FROM PUS_ORDERS WHERE AccountID = @AccountID And OID = @OID
		DELETE FROM PUS_ORDER_NOTIFICATIONS WHERE ONID = @ONID
		PRINT RTRIM(LTRIM(@AccountID)) + ' Hesabının Siparişi Silinmiştir..!'
	END
	ELSE
	BEGIN
		UPDATE TB_USER SET Knight_Cash = Knight_Cash + @Knight_Cash + @GetDonative WHERE strAccountID = @AccountID		UPDATE PUS_ORDERS SET Status = 1 WHERE AccountID = @AccountID And OID = @OID
		PRINT RTRIM(LTRIM(@AccountID)) + ' Hesabına "' + RTRIM(LTRIM(STR(@Knight_Cash + @GetDonative))) + '" Knight Cash Eklenmiştir.'
	END
END
ELSE
BEGIN
	PRINT '"' + RTRIM(LTRIM(STR(@OID))) + '" Numaralı Sipariş Bulunamadı.'
END
