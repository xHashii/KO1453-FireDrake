CREATE PROCEDURE [dbo].[ADD_KESN_CODES]
@Owner int,
@KCID int,
@KESNCodeCount int
AS
/*
Author : AKUMA
Update : 30.06.2009 - 10:52
*/

DECLARE @KESNCurrentCountNumber int

-- # KESN Creator Strings Start # --
DECLARE @SmallCharacter1 char(1)
DECLARE @SmallCharacter2 char(1)
DECLARE @RandomNumber1 tinyint
DECLARE @RandomNumber2 tinyint
DECLARE @RandomNumber3 tinyint
DECLARE @RandomNumber4 tinyint
DECLARE @RandomNumber5 tinyint
DECLARE @RandomNumber6 tinyint
DECLARE @RandomNumber7 tinyint
DECLARE @RandomNumber8 tinyint
DECLARE @RandomNumber9 tinyint
DECLARE @KESNTour tinyint
DECLARE @KESNKey0 char(9)
DECLARE @KESNKey1 char(4)
DECLARE @KESNKey2 char(4)
DECLARE @KESNKey3 char(4)
DECLARE @KESNKey4 char(4)
DECLARE @KESNKey5 char(4)
DECLARE @KESNKey6 char(4)
DECLARE @KESNCode char(39)
DECLARE @KESNPasswordKey1 char(4)
DECLARE @KESNPasswordKey2 char(4)
DECLARE @KESNPassword char(9)
DECLARE @KESNPasswordTour tinyint
-- # KESN Creator Strings End # --

SET @KESNCurrentCountNumber = 1

WHILE @KESNCurrentCountNumber < @KESNCodeCount + 1
BEGIN

	-- # KESN Code Creator Start #
	SET @KESNCode = NULL
	SET @KESNKey0 = NULL
	SET @KESNKey1 = NULL
	SET @KESNKey2 = NULL
	SET @KESNKey3 = NULL
	SET @KESNKey4 = NULL
	SET @KESNKey5 = NULL
	SET @KESNKey6 = NULL
	SET @KESNTour = 1

	WHILE @KESNTour < 7 + 1
	BEGIN
	
		SET @SmallCharacter1 = NULL
		SET @SmallCharacter2 = NULL
		SET @SmallCharacter1 = CHAR(((122 + 1) - 97) * RAND() + 97)
		SET @SmallCharacter2 = CHAR(((122 + 1) - 97) * RAND() + 97)
		SET @RandomNumber1 = NULL
		SET @RandomNumber2 = NULL
		SET @RandomNumber3 = NULL
		SET @RandomNumber4 = NULL
		SET @RandomNumber5 = NULL
		SET @RandomNumber6 = NULL
		SET @RandomNumber7 = NULL
		SET @RandomNumber8 = NULL
		SET @RandomNumber9 = NULL
		SET @RandomNumber1 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber2 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber3 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber4 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber5 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber6 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber7 = ((9 + 1) - 0) * RAND() + 0
		SET @RandomNumber8 = ((9 + 1) - 0) * RAND() + 0	
		SET @RandomNumber9 = ((9 + 1) - 0) * RAND() + 0

		IF @KESNKey0 IS NULL
			SET @KESNKey0 = RTRIM(LTRIM(STR(@RandomNumber1))) + RTRIM(LTRIM(STR(@RandomNumber2))) + RTRIM(LTRIM(STR(@RandomNumber3))) + RTRIM(LTRIM(STR(@RandomNumber4))) + RTRIM(LTRIM(STR(@RandomNumber5))) + RTRIM(LTRIM(STR(@RandomNumber6))) + RTRIM(LTRIM(STR(@RandomNumber7))) + RTRIM(LTRIM(STR(@RandomNumber8))) + RTRIM(LTRIM(STR(@RandomNumber9)))
		ELSE IF @KESNKey1 IS NULL
			SET @KESNKey1 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
		ELSE IF @KESNKey2 IS NULL
			SET @KESNKey2 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
		ELSE IF @KESNKey3 IS NULL
			SET @KESNKey3 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
		ELSE IF @KESNKey4 IS NULL
			SET @KESNKey4 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
		ELSE IF @KESNKey5 IS NULL
			SET @KESNKey5 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
		ELSE IF @KESNKey6 IS NULL
			SET @KESNKey6 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
					
		SET @KESNCode = @KESNKey0 + '-' + @KESNKey1 + '-' + @KESNKey2 + '-' + @KESNKey3 + '-' + @KESNKey4 + '-' + @KESNKey5 + '-' + @KESNKey6
	
		SET @KESNTour = @KESNTour + 1
	END
	-- # KESN Code Creator End #
	
	
	-- # KESN Code Password Creator Start #
	SET @KESNPassword = NULL
	SET @KESNPasswordKey1 = NULL
	SET @KESNPasswordKey2 = NULL
	SET @KESNPasswordTour = 1

	WHILE @KESNPasswordTour < 2 + 1
	BEGIN
	
		SET @SmallCharacter1 = NULL
		SET @SmallCharacter2 = NULL
		SET @RandomNumber1 = NULL
		SET @RandomNumber2 = NULL
		
		SET @SmallCharacter1 = CHAR(((122 + 1) - 97) * RAND() + 97)
		SET @RandomNumber1 = ((9 + 1) - 0) * RAND() + 0
		SET @SmallCharacter2 = CHAR(((122 + 1) - 97) * RAND() + 97)
		SET @RandomNumber2 = ((9 + 1) - 0) * RAND() + 0
		
		IF @KESNPasswordKey1 IS NULL
			SET @KESNPasswordKey1 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))
		ELSE IF @KESNPasswordKey2 IS NULL
			SET @KESNPasswordKey2 = @SmallCharacter1 + RTRIM(LTRIM(STR(@RandomNumber1))) + @SmallCharacter2 + RTRIM(LTRIM(STR(@RandomNumber2)))

		SET @KESNPassword = @KESNPasswordKey1 + '-' + @KESNPasswordKey2
			
		SET @KESNPasswordTour = @KESNPasswordTour + 1
	END
	-- # KESN Code Creator End #
	
	INSERT INTO PUS_KESN_CODES VALUES (@Owner,@KESNCode,@KESNPassword,@KCID,0,GETDATE())
	 
	SET @KESNCurrentCountNumber = @KESNCurrentCountNumber + 1
END

SELECT TOP(@KESNCodeCount) * FROM PUS_KESN_CODES ORDER BY UpdateTime DESC
