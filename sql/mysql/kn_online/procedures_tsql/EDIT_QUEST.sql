CREATE PROCEDURE [dbo].[EDIT_QUEST]
(
        @strUserId varchar(21),
        @sQuestId smallint,
        @bQuestStatus tinyint
)
AS
DECLARE @binQuest binary(400), @sQuestCount smallint, @tmpQuestId smallint, @tmpQuestStatus tinyint, @strQuest binary(600), @i int, @freePos int, @bFound tinyint
BEGIN
        SELECT @binQuest = CAST(strQuest as varbinary(400)), @sQuestCount = sQuestCount FROM USERDATA WHERE strUserId = @strUserId

        SET @freePos = null
        SET @bFound = 0
        SET @i = 1
        
        IF (@binQuest IS NULL)
                SET @binQuest = CONVERT(varbinary(400), 0)
        
        WHILE (@i < 400 AND @bFound = 0)
        BEGIN
                SET @tmpQuestId = SUBSTRING(@binQuest, @i, 2) + SUBSTRING(@binQuest, @i, 1)
                SET @tmpQuestStatus = SUBSTRING(@binQuest, @i + 2, 1)

                IF (@tmpQuestId = @sQuestId)
                BEGIN
                        SET @bFound = 1
                        IF (@bQuestStatus IS NULL)
                        BEGIN
                                SET @sQuestCount = @sQuestCount - 1
                                SET @sQuestId = 0
                                SET @bQuestStatus = 0
                        END             
                                                                                
                        SET @strQuest = SUBSTRING(@binQuest, 1, @i - 1) 
                                                        + CAST(SUBSTRING(CONVERT(varbinary(2), @sQuestId), 2, 1) AS varbinary(1))
                                                        + CAST(SUBSTRING(CONVERT(varbinary(2), @sQuestId), 1, 1) AS varbinary(1))
                                                        + CAST(@bQuestStatus as varbinary(1)) 
                                                        + SUBSTRING(@binQuest, @i + 3, 400 - (@i + 3))
                END
                ELSE IF (@tmpQuestId = 0 AND @freePos IS NULL)
                BEGIN
                        SET @freePos = @i
                END

                SET @i = @i + 3
        END
        
        IF (@bFound = 0 AND @freePos IS NOT NULL)
        BEGIN
                        SET @sQuestCount = @sQuestCount + 1
                        SET @strQuest = SUBSTRING(@binQuest, 1, @freePos - 1) 
                                                        + CAST(SUBSTRING(CONVERT(varbinary(2), @sQuestId), 2, 1) AS varbinary(1))
                                                        + CAST(SUBSTRING(CONVERT(varbinary(2), @sQuestId), 1, 1) AS varbinary(1))
                                                        + CAST(@bQuestStatus as varbinary(1)) 
                                                        + SUBSTRING(@binQuest, @freePos + 3, 400 - (@freePos + 3))
        END
        
        
        IF (@bFound = 0 AND @freePos IS NULL)
        BEGIN
                PRINT 'Could not insert quest ' + CONVERT(varchar, @sQuestID) + ' with status ' + CONVERT(varchar, @bQuestStatus) + ' as there''s no room left.'
        END
        ELSE
        BEGIN
                UPDATE USERDATA SET strQuest = @strQuest, sQuestCount = @sQuestCount WHERE strUserId = @strUserId
        END
END
