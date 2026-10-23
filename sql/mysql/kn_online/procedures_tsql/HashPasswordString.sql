CREATE FUNCTION [dbo].[HashPasswordString] (@inputPassword varchar(50)) RETURNS varchar(50)
AS
BEGIN
	DECLARE @encodingArray varbinary(64)
	SET @encodingArray = 0x1a1f110a1e1018021d08140f1c0b0d041317000c0e1b06121503090716011905121d07190f1f161b091a030d130e140b050217100a181c11061e00150c080401

	DECLARE @alphabetArray varchar(36)
	SET @alphabetArray = 0x303132333435363738394142434445464748494a4b4c4d4e4f505152535455565758595a

	DECLARE @startKey int
	SET @startKey = 0x03e8

	DECLARE @inputPasswordLen int
	SET @inputPasswordLen = LEN(@inputPassword)
	
	WHILE @inputPasswordLen % 4 != 0
		SET @inputPasswordLen = @inputPasswordLen + 1

	DECLARE @counter int, @tmp int, @inputKey int, @outHash int
	SET @counter = 0 SET @tmp = 0 SET @inputKey = 0 SET @outHash = 0

	DECLARE @outStringBytes varchar(28)
	SET @outStringBytes = ''

	DECLARE @i int
	SET @i = 0

	WHILE @i < @inputPasswordLen
	BEGIN
		DECLARE @encoded int
		SET @encoded = CAST(REVERSE(SUBSTRING(@inputPassword,@i+1,4)) AS VARBINARY(4))

		SET @tmp = @encoded + @startKey
		SET @inputKey = @tmp
		SET @counter = 0
		SET @outHash = 0
		
 		WHILE @inputKey > 0
		BEGIN
			SET @tmp = @inputKey			
			SET @inputKey = @inputKey / POWER(2,1)
			IF @tmp % 2 != 0
			BEGIN
				SET @tmp = SUBSTRING(@encodingArray,@counter+1,1)				
				SET @outHash = CAST(CONVERT(BIGINT,@outHash + POWER(2.0,@tmp)) AS VARBINARY(4))
			END
			SET @counter = @counter + 1
		END
		
		DECLARE @tmpProduct bigint		
		DECLARE @tmpPut bigint
		SET @tmpPut = CAST(@outHash AS VARBINARY(4))

		DECLARE @tmpInt int
		SET @tmpInt = 0

		WHILE @tmpInt < 7
		BEGIN
			SET @tmpProduct = @tmpPut * 0x38e38e39
			DECLARE @upper int
			SET @upper = @tmpProduct / POWER(2.0, 35)
			DECLARE @anotherTmp bigint
			SET @anotherTmp = (@upper * 8) + @upper
			SET @anotherTmp = @anotherTmp * POWER(2.0,2)
			DECLARE @difference int
			SET @difference = @tmpPut - @anotherTmp
			SET @outStringBytes = @outStringBytes + SUBSTRING(@alphabetArray,@difference+1,1)
			SET @tmpPut = @upper
			SET @tmpInt = @tmpInt + 1
		END
		SET @i = @i + 4
	END
	RETURN @outStringBytes
END
