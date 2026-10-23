CREATE PROCEDURE [dbo].[LOAD_SAVED_MAGIC]
@strUserId	char(21)
AS

SELECT nSkill1, nDuring1, nSkill2, nDuring2,nSkill3, nDuring3,nSkill4, nDuring4,nSkill5, nDuring5,nSkill6, nDuring6,nSkill7, nDuring7,nSkill8, nDuring8,nSkill9, nDuring9,nSkill10, nDuring10 FROM USER_SAVED_MAGIC WHERE strCharID = @strUserId
