CREATE PROCEDURE [dbo].[CREATE_INSERT_SCRIPT]
@Table varchar(50)
AS

DECLARE @Columns varchar(1000)
DECLARE @Col varchar(50)
SET @Columns=''

DECLARE COLCUR CURSOR FOR

SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table 

OPEN COLCUR
FETCH NEXT FROM COLCUR INTO @Col
WHILE @@FETCH_STATUS = 0
BEGIN
SELECT @Columns = @Columns + ', ' + @Col FETCH NEXT FROM COLCUR INTO @Col
END
CLOSE COLCUR
DEALLOCATE COLCUR

SELECT @Columns = SUBSTRING(@Columns,3,DATALENGTH(@Columns))

DECLARE @sql varchar(4000)
DECLARE @colname varchar(100), @coltype varchar(30)

SELECT @sql = 'SELECT REPLACE(''INSERT INTO ' + @Table + ' (' + @Columns + ') '

select @sql = @sql + 'VALUES ('''

DECLARE CCUR CURSOR FOR

SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table

OPEN CCUR
FETCH FROM CCUR INTO @colname, @coltype
WHILE @@FETCH_STATUS = 0
BEGIN
IF @coltype IN('varchar', 'char', 'datetime')
SELECT @sql = @sql + ''''''
SELECT @sql=@sql + ' + COALESCE(CONVERT(varchar, ' + @colname + '), ''NULL'') + '
IF @coltype IN('varchar', 'char', 'datetime')
SELECT @sql=@sql + ''''''
SELECT @sql = RTRIM(LTRIM(@sql)) + ''', '''
FETCH FROM CCUR INTO @colname, @coltype
END

CLOSE CCUR
DEALLOCATE CCUR

SELECT @sql = SUBSTRING(@sql,1,DATALENGTH(@sql)-3)
SELECT @sql=@sql + ')'', ''''''NULL'''''', ''NULL'') FROM ' + @table

EXEC (@sql)
