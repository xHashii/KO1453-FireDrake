-- BEGINNER_ITEM.Skill arrived from the MSSQL conversion as varchar(10) holding
-- the literal 'Ž' - a binary-in-varchar mangle of the same family as the
-- strItem dumps: the original column carried the single byte 0x8E (cp1252 /
-- cp1254 'Ž'), which a utf8mb4 varchar cannot represent.
--
-- CREATE_NEW_CHAR copies this value into USERDATA.strSkill, which IS
-- varbinary(10), so a new character's skill blob inherited the mangle. Make the
-- source column binary and restore the original byte. Applied scripts are
-- immutable (DatabaseMigrator refuses changed checksums), hence this follow-up
-- script instead of an edit of 0001_schema.sql / 0002_data.sql; fresh imports
-- and already-migrated databases converge on the same state.
ALTER TABLE `BEGINNER_ITEM` MODIFY `Skill` varbinary(10) NULL;
UPDATE `BEGINNER_ITEM` SET `Skill` = 0x8E WHERE `Skill` IS NOT NULL;
