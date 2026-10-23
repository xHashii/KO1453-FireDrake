CREATE PROCEDURE [dbo].[VIEW_SKILLS]
(
        @strUserId varchar(21)
)
AS
DECLARE @binSkill binary(10), @Free tinyint, @Cat1 tinyint, @Cat2 tinyint, @Cat3 tinyint, @Masters tinyint
BEGIN
        SELECT @binSkill = CAST(strSkill as varbinary(10)) FROM USERDATA WHERE strUserId = @strUserId
        
        SET @Free = SUBSTRING(@binSkill, 1, 1)
        SET @Cat1 = SUBSTRING(@binSkill, 6, 1)
        SET @Cat2 = SUBSTRING(@binSkill, 7, 1)
        SET @Cat3 = SUBSTRING(@binSkill, 8, 1)
        SET @Masters = SUBSTRING(@binSkill, 9, 1)

        SELECT @Free as Free, @Cat1 as Cat1, @Cat2 as Cat2, @Cat3 as Cat3, @Masters as Masters
END
