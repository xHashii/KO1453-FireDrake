CREATE PROCEDURE [dbo].[DELETE_USER]
@strUserId char(21)
AS

DECLARE @AccountID char(21)
DECLARE @ClanID smallint
DECLARE @ClanLeaderName char(21)
DECLARE @Fame tinyint
DECLARE @IsValidCharacter tinyint

SELECT @IsValidCharacter = COUNT(strUserId) FROM USERDATA WHERE strUserId = @strUserId

IF @IsValidCharacter  > 0 -- Eğer Kullanıcı Var ise
BEGIN
	SELECT @ClanID = Knights,@Fame = Fame FROM USERDATA WHERE strUserId = @strUserId
	SELECT @ClanID = Knights,@Fame = Fame FROM USERDATA WHERE strUserId = @strUserId

	IF @ClanID > 0 -- Kullanıcı Clanda
	BEGIN
		SELECT @ClanLeaderName = Chief FROM KNIGHTS WHERE IDNum = @ClanID

		IF @Fame = 1 AND @ClanLeaderName = @strUserId -- Clan Lideri ise
		BEGIN
			DELETE FROM KNIGHTS WHERE IDNum = @ClanID
			DELETE FROM KNIGHTS_USER WHERE sIDNum = @ClanID
			UPDATE USERDATA SET Knights = 0 WHERE Knights = @ClanID
		END
		ELSE
		BEGIN
			-- Kullancıyı Clandan Siliyoruz...
			DELETE FROM KNIGHTS_USER WHERE sIDNum = @ClanID AND strUserId = @strUserId
			UPDATE KNIGHTS SET Members = Members - 1 WHERE IDNum = @ClanID
		END
	END

	DELETE FROM USERDATA WHERE strUserId = @strUserId
	DELETE FROM FRIEND_LIST WHERE strUserId = @strUserId
	DELETE FROM RENTAL_ITEM_LIST WHERE strLenderCharID = @strUserId
	DELETE FROM RENTAL_ITEM WHERE strLenderCharID = @strUserId
	DELETE FROM SEALED_ITEMS WHERE strUserId = @strUserId
	DELETE FROM USER_SAVED_MAGIC WHERE strCharID = @strUserId
	DELETE FROM USERDATA_SKILLSHORTCUT WHERE strCharID = @strUserId
	DELETE FROM WEB_ITEMMALL WHERE strCharID = @strUserId
	DELETE FROM WEB_ITEMMALL_LOG WHERE strCharID = @strUserId

	UPDATE KING_SYSTEM SET strKingName = NULL WHERE strKingName = @strUserId
	UPDATE ACCOUNT_CHAR SET strCharID1 = NULL WHERE strCharID1 = @strUserId AND strCharID1 IS NOT NULL
	UPDATE ACCOUNT_CHAR SET strCharID2 = NULL WHERE strCharID2 = @strUserId AND strCharID2 IS NOT NULL
	UPDATE ACCOUNT_CHAR SET strCharID3 = NULL WHERE strCharID3 = @strUserId AND strCharID3 IS NOT NULL

	PRINT RTRIM(LTRIM(@strUserId)) + ' Adli Oyuncunun Tüm Verileri Silindi.'
END
ELSE
BEGIN
	PRINT RTRIM(LTRIM(@strUserId)) + ' Adli Oyuncu Veritabaninda Yok.'
END
