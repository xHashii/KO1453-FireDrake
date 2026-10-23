CREATE PROCEDURE [dbo].[UPDATE_USER_KNIGHTS_RANK]
AS
DECLARE @strUserId char(21)
DECLARE @Loyalty int
DECLARE @Knights int
DECLARE @KnightName char(50)
DECLARE @IsValidKnight int
DECLARE @Index smallint
DECLARE @IsValidCount tinyint
DECLARE @RankName varchar(30)
SELECT @IsValidCount = Count(*) FROM USER_KNIGHTS_RANK
IF @IsValidCount < 100
BEGIN

    SET @Index = 1

    WHILE @Index < 101
    BEGIN

        IF @Index = 1
        BEGIN
            SET @RankName = 'Gold Knight'
        END
        IF @Index > 1 AND @Index <= 4
        BEGIN
            SET @RankName = 'Silver Knight'
        END
        IF @Index > 4 AND @Index <= 9
        BEGIN
            SET @RankName = 'Mirage Knight'
        END
        IF @Index > 9 AND @Index <= 10
        BEGIN
            SET @RankName = 'Shadow Knight'
        END
        IF @Index > 25 AND @Index <= 50
        BEGIN
            SET @RankName = 'Mist Knight'
        END
        IF @Index > 50 AND @Index <= 100
        BEGIN
            SET @RankName = 'Training Knight'
        END
       
        INSERT INTO USER_KNIGHTS_RANK (shIndex,strName,strElmoUserID,strElmoKnightsName,nElmoLoyalty,strKarusUserID,strKarusKnightsName,nKarusLoyalty,nMoney) VALUES (@Index,@RankName,NULL,NULL,0,NULL,NULL,0,1000000)
   
        SET @Index = @Index + 1
    END
END

SET @Index = 1
SET @strUserId = NULL
SET @Loyalty = 0
SET @Knights = 0
DECLARE RANKING_CRS CURSOR FOR
SELECT TOP 100 strUserId,Loyalty,Knights FROM USERDATA WHERE Nation = 1 AND Authority = 1 AND Knights <> 0 ORDER BY Loyalty DESC

OPEN RANKING_CRS
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@Loyalty,@Knights
WHILE @@FETCH_STATUS = 0 
BEGIN

SET @KnightName = NULL
SET @IsValidKnight = 0
IF @Knights <> 0
BEGIN
    SELECT @IsValidKnight = COUNT(IDName) FROM KNIGHTS WHERE IDNum = @Knights
   
    IF @IsValidKnight <> 0
    BEGIN
        SELECT @KnightName = IDName FROM KNIGHTS WHERE IDNum = @Knights
    END
END
   
    UPDATE USER_KNIGHTS_RANK SET strKarusUserID = @strUserId, strKarusKnightsName = @KnightName, nKarusLoyalty = @Loyalty WHERE shIndex = @Index
   
    SET @Index = @Index + 1
            
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@Loyalty,@Knights
END
CLOSE RANKING_CRS
DEALLOCATE RANKING_CRS

SET @Index = 1
SET @strUserId = NULL
SET @Loyalty = 0
SET @Knights = 0
DECLARE RANKING_CRS CURSOR FOR
SELECT TOP 100 strUserId,Loyalty,Knights FROM USERDATA WHERE Nation = 2 AND Authority = 1 AND Knights <> 0 ORDER BY Loyalty DESC

OPEN RANKING_CRS
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@Loyalty,@Knights
WHILE @@FETCH_STATUS = 0 
BEGIN

SET @KnightName = NULL
SET @IsValidKnight = 0
IF @Knights <> 0
BEGIN
    SELECT @IsValidKnight = COUNT(IDName) FROM KNIGHTS WHERE IDNum = @Knights
   
    IF @IsValidKnight <> 0
    BEGIN
        SELECT @KnightName = IDName FROM KNIGHTS WHERE IDNum = @Knights
    END
END
   
    UPDATE USER_KNIGHTS_RANK SET strElmoUserID = @strUserId, strElmoKnightsName = @KnightName, nElmoLoyalty = @Loyalty WHERE shIndex = @Index
   
    SET @Index = @Index + 1
            
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@Loyalty,@Knights
END
CLOSE RANKING_CRS
DEALLOCATE RANKING_CRS
