-- CREATE_NEW_CHAR (both the T-SQL original and the port) never writes current
-- Hp/Mp, and the C++ load path does not refill a zero - fresh characters sit
-- at 0/x until something heals them. The port fills the bars at select time
-- in the session; this migration repairs the stored rows with the same
-- SetMaxHp/SetMaxMp formulas (COEFFICIENT factors, level and sta/int totals)
-- so persisted state matches what the client is shown.
UPDATE `USERDATA` u
JOIN `COEFFICIENT` c ON c.sClass = u.Class
SET u.Hp = IF(u.Hp <= 0,
              LEAST(14000, FLOOR(c.Hp * u.Level * u.Level * u.Sta + 0.1 * u.Level * u.Sta + FLOOR(u.Sta / 5) + 20)),
              u.Hp),
    u.Mp = IF(u.Mp <= 0,
              FLOOR(IF(c.Mp <> 0,
                       c.Mp * u.Level * u.Level * (u.Intel + 30) + 0.1 * u.Level * 2 * (u.Intel + 30) + FLOOR((u.Intel + 30) / 5) + 20,
                       IF(c.Sp <> 0,
                          c.Sp * u.Level * u.Level * u.Sta + 0.1 * u.Level * u.Sta + FLOOR(u.Sta / 5),
                          0))),
              u.Mp)
WHERE u.Hp <= 0 OR u.Mp <= 0;
