CREATE PROCEDURE [dbo].[EDIT_SKILLS]
(
        @strUserId varchar(21),
        @newFree tinyint = null,
        @newCat1 tinyint = null,
        @newCat2 tinyint = null,
        @newCat3 tinyint = null,
        @newMasters tinyint = null
)
AS
DECLARE @binSkill binary(10), @Free tinyint, @Cat1 tinyint, @Cat2 tinyint, @Cat3 tinyint, @Masters tinyint, @strSkill varchar(10)
BEGIN
        SELECT @binSkill = CAST(strSkill as varbinary(10)) FROM USERDATA WHERE strUserId = @strUserId
        
        SET @Free = SUBSTRING(@binSkill, 1, 1)
        SET @Cat1 = SUBSTRING(@binSkill, 6, 1)
        SET @Cat2 = SUBSTRING(@binSkill, 7, 1)
        SET @Cat3 = SUBSTRING(@binSkill, 8, 1)
        SET @Masters = SUBSTRING(@binSkill, 9, 1)

        IF (@newFree IS NOT NULL)
                SET @Free = @newFree
                
        IF (@newCat1 IS NOT NULL)
                SET @Cat1 = @newCat1
                
        IF (@newCat2 IS NOT NULL)
                SET @Cat2 = @newCat2
                
        IF (@newCat3 IS NOT NULL)
                SET @Cat3 = @newCat3
                
        IF (@newMasters IS NOT NULL)
                SET @Masters = @newMasters

        SET @strSkill = CONVERT(VARBINARY(1), @Free)            /* free points */
                                        + CONVERT(VARBINARY(4), 0)                      /* leadership, politics, etc */
                                        + CONVERT(VARBINARY(1), @Cat1)          /* cat 1 */
                                        + CONVERT(VARBINARY(1), @Cat2)          /* cat 2 */
                                        + CONVERT(VARBINARY(1), @Cat3)          /* cat 3 */
                                        + CONVERT(VARBINARY(1), @Masters)       /* masters */
                                        + CONVERT(VARBINARY(1), 0)                      /* padding */
        
        UPDATE USERDATA SET strSkill = @strSkill WHERE strUserId = @strUserId
END
