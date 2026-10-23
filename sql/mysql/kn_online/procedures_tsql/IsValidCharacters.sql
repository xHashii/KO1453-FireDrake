CREATE FUNCTION [dbo].[IsValidCharacters](@SData varchar(8000))
RETURNS INT 
AS
/*
Author : AKUMA
Update : 16.10.2009 - 15:30
*/
BEGIN
	-- # Game Options System Start # --
	
	DECLARE @ProcedureName nvarchar(255)
	
	SET @ProcedureName = 'IsValidCharacters'

	-- # Game Options System End # --

	DECLARE @SDataLen int
	DECLARE @Loop int
	DECLARE @Letter varchar(1)
	DECLARE @RXLetters varchar(8000)
	DECLARE @Match tinyint
	
	SET @RXLetters = 'qwertyuopasdfghjklizxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890'
	SET @SDataLen = LEN(@SData)
	SET @Loop = 1
	SET @Match = 0
	
	WHILE @Loop < (@SDataLen + 1)
	BEGIN
	
		SET @Letter = RTRIM(LTRIM(SUBSTRING(@SData,@Loop,1)))

		IF CHARINDEX(@Letter,@RXLetters) = 0
		BEGIN
			SET @Match = 1
			BREAK
		END
	
		SET @Loop = @Loop + 1
	END
	
	RETURN @Match
END
