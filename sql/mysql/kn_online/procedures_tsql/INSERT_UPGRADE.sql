CREATE PROCEDURE [dbo].[INSERT_UPGRADE]
@itemname varchar(50), -- NAME OF ITEM TO UPGRADE
@index int, -- RANDOMNUMBER as indexNumber
@reqmon int, -- Required Money for upgrades (240K for unique's 0 for Normal Items)
@TYPE tinyint -- Type 1 Only Bus Upgrade, Type 2 only Trina Upgrade,  Type 3 Trina + Bus Upgrades at once.
AS

DECLARE @origitem varchar(4),@giveitem varchar(4), @give int, @otype int, @rate smallint, @reqitem int,@reqitem2 int
set @otype = -1
IF @TYPE = 1 or @TYPE = 3
BEGIN
set @rate=10000  -- Random rate for now. Can be editted lateron
set @reqitem=379021000  -- Required Item for Upgrading.
set @reqitem2=0
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and num like '%001)' and strname like '%1)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and num like '%002)' and strname like '%2)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+2241, 5001, @itemname, '+1 To +2', @otype, CAST(@origitem AS smallint), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 0, @rate, @give
--+2 to +3 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%2)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%3)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int )
insert into item_upgrade_new select @index+355, 5001, @itemname, '+2 To +3', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 0, @rate, @give
--+3 to +4 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%3)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%4)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+461, 5001, @itemname, '+3 To +4 ', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 3, @rate, @give
--+4 to +5 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%4)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%5)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int )
insert into item_upgrade_new select @index+521, 5001, @itemname, '+4 To +5', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 5, @rate, @give
--+5 to +6 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%5)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%6)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+6521, 5001, @itemname, '+5 To +6', @otype, CAST(@origitem AS smallint), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 8, @rate, @give
--+6 to +7 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%6)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%7)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+722, 5001, @itemname, '+6 To +7', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 10, @rate, @give
--+7 to +8 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%7)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%8)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int )
insert into item_upgrade_new select @index+81, 5001, @itemname, '+7 To +8', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 11 ,@rate, @give
--+8 to +9 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%8)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%9)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+933, 5001, @itemname, '+8 To +9', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 12, @rate, @give
--+9 to +10 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%9)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%10)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+101, 5001, @itemname, '+9 To +10', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 13, @rate, @give
END
 
IF @TYPE = 2 or @TYPE = 3
BEGIN
set @rate=10000
set @reqitem=700002000
set @reqitem2=379021000
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%1)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%2)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+2, 5001, @itemname, '1to2', @otype, CAST(@origitem AS smallint), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 0, @rate, @give
--+2 to +3 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%2)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%3)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int )
insert into item_upgrade_new select @index+3, 5001, @itemname, '2to3', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 0, @rate, @give
--+3 to +4 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%3)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%4)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+4, 5001, @itemname, '3to4', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 3, @rate, @give
--+4 to +5 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%4)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%5)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int )
insert into item_upgrade_new select @index+5, 5001, @itemname, '4to5', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 5, @rate, @give
--+5 to +6 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%5)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%6)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+6, 5001, @itemname, '5to6', @otype, CAST(@origitem AS smallint), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 8, @rate, @give
--+6 to +7 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%6)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%7)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+7, 5001, @itemname, '6to7', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 10, @rate, @give
--+7 to +8 code
 
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%7)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%8)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int )
insert into item_upgrade_new select @index+8, 5001, @itemname, '7to8', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 11, @rate, @give
--+8 to +9 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%8)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%9)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+9, 5001, @itemname, '8to9', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 12, @rate, @give
--+9 to +10 code
 
select top 1 @origitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%9)%'
select top 1 @giveitem= SUBSTRING(CAST ( num AS varchar ( 9)), 7,10) from item where strname like '%'+@itemname+'%' and strname like '%10)%'
set @give=CAST(@giveitem AS int)-CAST(@origitem AS int)
insert into item_upgrade_new select @index+10, 5001, @itemname, '9to10', @otype, CAST(@origitem AS smallint ), @reqitem, @reqitem2, 0, 0, 0, 0, 0, 0, @reqmon, 13, @rate, @give
END
select * from item_upgrade_new where strname=@itemname
