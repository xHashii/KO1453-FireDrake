CREATE PROCEDURE [dbo].[UPDATE_USER_PERSONAL_RANK]
AS
BEGIN TRAN
DECLARE @strUserId char(21)
DECLARE @LoyaltyMonthly int
DECLARE @Index smallint
DECLARE @IsValidCount tinyint
DECLARE @RankName varchar(30)
DECLARE @DifferenceBetweenUser int

SELECT @IsValidCount = Count(*) FROM USER_PERSONAL_RANK
IF @IsValidCount < 200
BEGIN
   
	TRUNCATE TABLE USER_PERSONAL_RANK
    SET @Index = 1

    WHILE @Index < 201
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
        IF @Index > 25 AND @Index <= 100
        BEGIN
            SET @RankName = 'Mist Knight'
        END
		IF @Index > 100 AND @Index <= 200
        BEGIN
            SET @RankName = 'Training Knight'
        END
       
        INSERT INTO USER_PERSONAL_RANK (nRank,strPosition,nElmoUP,strElmoUserID,nElmoLoyaltyMonthly,nElmoCheck,nKarusUP,strKarusUserID,nKarusLoyaltyMonthly,nKarusCheck,nSalary,UpdateDate) VALUES (@Index,@RankName,0,NULL,0,1000000,0,NULL,0,1000000,1000000,GETDATE())
           
        SET @Index = @Index + 1
    END
END

SET @Index = 1
SET @strUserId = NULL
SET @LoyaltyMonthly = 0
DECLARE RANKING_CRS CURSOR FOR
SELECT TOP 200 strUserId,LoyaltyMonthly FROM USERDATA WHERE Nation = 1 AND Authority = 1 AND Knights <> 0 ORDER BY LoyaltyMonthly DESC

OPEN RANKING_CRS
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@LoyaltyMonthly
WHILE @@FETCH_STATUS = 0 
BEGIN
    
    UPDATE USER_PERSONAL_RANK SET strKarusUserID = @strUserId, nKarusUP = @Index, nKarusLoyaltyMonthly = @LoyaltyMonthly WHERE nRank = @Index
   
    SET @DifferenceBetweenUser = 0
   
    IF @Index = 1
    BEGIN
   
        UPDATE USER_PERSONAL_RANK SET nKarusCheck = 0 WHERE nRank = @Index
   
    END
    ELSE
    BEGIN
   
        SELECT @DifferenceBetweenUser = nKarusLoyaltyMonthly FROM USER_PERSONAL_RANK WHERE nRank = @Index + 1
   
        SET @DifferenceBetweenUser = @LoyaltyMonthly - @DifferenceBetweenUser
   
        UPDATE USER_PERSONAL_RANK SET nKarusCheck = @DifferenceBetweenUser WHERE nRank = @Index + 1
       
    END
       
    SET @Index = @Index + 1
            
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@LoyaltyMonthly
END
CLOSE RANKING_CRS
DEALLOCATE RANKING_CRS

SET @Index = 1
SET @strUserId = NULL
SET @LoyaltyMonthly = 0
DECLARE RANKING_CRS CURSOR FOR
SELECT TOP 200 strUserId,LoyaltyMonthly FROM USERDATA WHERE Nation = 2 AND Authority = 1 AND Knights <> 0 ORDER BY LoyaltyMonthly DESC

OPEN RANKING_CRS
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@LoyaltyMonthly
WHILE @@FETCH_STATUS = 0 
BEGIN
   
    UPDATE USER_PERSONAL_RANK SET strElmoUserID = @strUserId, nElmoUP = @Index, nElmoLoyaltyMonthly = @LoyaltyMonthly WHERE nRank = @Index   
   
    SET @DifferenceBetweenUser = 0
   
    IF @Index = 1
    BEGIN
   
        UPDATE USER_PERSONAL_RANK SET nElmoCheck = 0 WHERE nRank = @Index
   
    END
    ELSE
    BEGIN
   
        SELECT @DifferenceBetweenUser = nElmoLoyaltyMonthly FROM USER_PERSONAL_RANK WHERE nRank = @Index + 1
   
        SET @DifferenceBetweenUser = @LoyaltyMonthly - @DifferenceBetweenUser
   
        UPDATE USER_PERSONAL_RANK SET nElmoCheck = @DifferenceBetweenUser WHERE nRank = @Index + 1
       
    END
   
    SET @Index = @Index + 1
            
FETCH NEXT FROM RANKING_CRS INTO @strUserId,@LoyaltyMonthly
END
CLOSE RANKING_CRS
DEALLOCATE RANKING_CRS

UPDATE USER_PERSONAL_RANK SET nSalary = REPLACE(nElmoLoyaltyMonthly - nKarusLoyaltyMonthly,'-','')

COMMIT TRAN
