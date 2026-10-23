CREATE PROCEDURE [dbo].[SKILLSHORTCUT_SAVE]
@strCharID 	varchar(21),
@nCount	smallint,
@strSkillData	varchar(260)
AS

SELECT strCharID FROM USERDATA_SKILLSHORTCUT WHERE strCharID = @strCharID
IF(@@rowcount = 0)
BEGIN
	INSERT USERDATA_SKILLSHORTCUT VALUES(@strCharID, 0, '')
END

UPDATE USERDATA_SKILLSHORTCUT
SET nCount = @nCount, strSkillData = @strSkillData
WHERE strCharID = @strCharID
