CREATE FUNCTION [dbo].[GET_RACE_FOR_ACCOUNT_NATION_TRANSFER]
(@Race tinyint,@Class tinyint)
/*
Author : AKUMA
Update : 20.06.2010 - 18:20
*/
RETURNS tinyint

BEGIN

		IF @Race = 1
			SET @Race = 12
		ELSE IF @Race = 2
			SET @Race = 12
		ELSE IF @Race = 3
			SET @Race = 12
		ELSE IF @Race = 4
			SET @Race = 13
		ELSE IF @Race = 11
			SET @Race = 1
		ELSE IF @Race = 12 AND @Class = 201
			SET @Race = 1
		ELSE IF @Race = 12 AND @Class = 205
			SET @Race = 1
		ELSE IF @Race = 12 AND @Class = 206
			SET @Race = 1
		ELSE IF @Race = 13 AND @Class = 201
			SET @Race = 1
		ELSE IF @Race = 13 AND @Class = 205
			SET @Race = 1
		ELSE IF @Race = 13 AND @Class = 206
			SET @Race = 1
		ELSE IF @Race = 12 AND @Class = 202
			SET @Race = 2
		ELSE IF @Race = 12 AND @Class = 207
			SET @Race = 2
		ELSE IF @Race = 12 AND @Class = 208
			SET @Race = 2
		ELSE IF @Race = 13 AND @Class = 202
			SET @Race = 2
		ELSE IF @Race = 13 AND @Class = 207
			SET @Race = 2
		ELSE IF @Race = 13 AND @Class = 208
			SET @Race = 2
		ELSE IF @Race = 12 AND @Class = 203
			SET @Race = 3
		ELSE IF @Race = 12 AND @Class = 209
			SET @Race = 3
		ELSE IF @Race = 12 AND @Class = 210
			SET @Race = 3
		ELSE IF @Race = 13 AND @Class = 203
			SET @Race = 3
		ELSE IF @Race = 13 AND @Class = 209
			SET @Race = 3
		ELSE IF @Race = 13 AND @Class = 210
			SET @Race = 3
		ELSE IF @Race = 12 AND @Class = 204
			SET @Race = 2
		ELSE IF @Race = 12 AND @Class = 211
			SET @Race = 2
		ELSE IF @Race = 12 AND @Class = 212
			SET @Race = 2
		ELSE IF @Race = 13 AND @Class = 204
			SET @Race = 4
		ELSE IF @Race = 13 AND @Class = 211
			SET @Race = 4
		ELSE IF @Race = 13 AND @Class = 212
			SET @Race = 4
	
		RETURN @Race
END
