CREATE PROCEDURE [dbo].[PRE_VER]

@strUserId varchar(30)

AS

--Çizen : AKI  ** 27.06.2010

--kullanım : EXEC PRE_VER 'Nick'

DECLARE @strAccountID varchar(30),@RCount int
SELECT @RCount = Count(bCharNum) FROM ACCOUNT_CHAR WHERE strCharID1 = @strUserID OR strCharID2 = @strUserID OR strCharID3 = @strUserID
	IF @RCount = 0
	BEGIN
		PRINT 'Veritabanında ' + @strAccountID + ' Adında Karakter Bulunamadı.'
	END
	ELSE
	BEGIN
		SELECT @strAccountID = strAccountId FROM ACCOUNT_CHAR WHERE strCharID1 = @strUserID OR strCharID2 = @strUserID OR strCharID3 = @strUserID
	UPDATE TB_USER SET dtPremiumTime = GETDATE() , bPremiumType = 7 , sHours = 720 WHERE strAccountID = @strAccountID
	PRINT @strUserId + 'Karakterinin Bağlı Bulunduğu ' + @strAccountId + ' Hesaba Başarıyla Premium Verilmiştir.'
	end
