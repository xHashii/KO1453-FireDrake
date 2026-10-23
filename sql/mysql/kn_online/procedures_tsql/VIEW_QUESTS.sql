CREATE PROCEDURE [dbo].[VIEW_QUESTS]
(
        @strUserId varchar(21)
)
AS
DECLARE @binQuest binary(400), @sQuestCount smallint, @tmpQuestId smallint, @tmpQuestStatus tinyint, @tmpStrStatus varchar(9), @i int
BEGIN
        SET NOCOUNT ON
        
        SELECT @binQuest = CAST(strQuest as varbinary(400)), @sQuestCount = sQuestCount FROM USERDATA WHERE strUserId = @strUserId

        CREATE TABLE #user_quests
        (
                pos int,
                sQuestID int,
                bQuestStatus tinyint,
                strStatus varchar(9)
        )

        SET @i = 1
        
        WHILE (@i < (@sQuestCount * 3))
        BEGIN
                SET @tmpQuestId = SUBSTRING(@binQuest, @i, 2) + SUBSTRING(@binQuest, @i, 1)
                SET @tmpQuestStatus = SUBSTRING(@binQuest, @i + 2, 1)
                SET @tmpStrStatus = 'Unknown'
                
                IF (@tmpQuestStatus = 0)
                BEGIN
                        SET @tmpStrStatus = 'None'
                END
                ELSE IF (@tmpQuestStatus = 1)
                BEGIN
                        SET @tmpStrStatus = 'Started'
                END
                ELSE IF (@tmpQuestStatus = 2)
                BEGIN
                        SET @tmpStrStatus = 'Completed'
                END
                
                IF (@tmpQuestId != 0)
                BEGIN
                        INSERT INTO #user_quests VALUES(@i, @tmpQuestId, @tmpQuestStatus, @tmpStrStatus)
                END
                SET @i = @i + 3
        END     
        
        SET NOCOUNT OFF
        SELECT * FROM #user_quests
        
        SET NOCOUNT ON
        DROP TABLE #user_quests
END
