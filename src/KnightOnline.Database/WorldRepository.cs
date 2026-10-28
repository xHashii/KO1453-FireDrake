using KnightOnline.Database.World;
using MySqlConnector;

namespace KnightOnline.Database;

public sealed partial class MariaDbCharacterRepository
{
    public async Task<WorldData> LoadWorldDataAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var zones = new List<ZoneDefinition>();
        command.CommandText = "SELECT ZoneNo,strZoneName FROM ZONE_INFO WHERE ServerNo=1 ORDER BY ZoneNo";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
                zones.Add(new ZoneDefinition(Convert.ToUInt16(r.GetValue(0)), r.GetString(1)));
        var protos = new Dictionary<ushort, NpcPrototype>();
        command.CommandText = "SELECT sSid,strName,sPid,sSize,iWeapon1,iWeapon2,byGroup,byType,iSellingGroup,sLevel,iHpPoint FROM K_NPC";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
            {
                var p = new NpcPrototype(Convert.ToUInt16(r.GetValue(0)), r.IsDBNull(1) ? "" : r.GetString(1),
                    Convert.ToUInt16(r.GetValue(2)), Convert.ToUInt16(r.GetValue(3)),
                    unchecked((uint)Convert.ToInt32(r.GetValue(4))), unchecked((uint)Convert.ToInt32(r.GetValue(5))),
                    Convert.ToByte(r.GetValue(6)), Convert.ToByte(r.GetValue(7)), Convert.ToInt32(r.GetValue(8)),
                    checked((byte)Math.Clamp(Convert.ToInt32(r.GetValue(9)), 0, 255)), Math.Max(1, Convert.ToInt32(r.GetValue(10))));
                protos.Add(p.Id, p);
            }
        var monsters = new Dictionary<ushort, NpcPrototype>();
        command.CommandText = "SELECT sSid,strName,sPid,sSize,iWeapon1,iWeapon2,byGroup,byType,sLevel,iHpPoint,sItem,sAc,iExp,iMoney,sDamage,sAttackDelay,byAttackRange,byActType,bySpeed2,bySearchRange,byTracingRange FROM K_MONSTER";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
            {
                var monster = new NpcPrototype(Convert.ToUInt16(r.GetValue(0)), r.IsDBNull(1) ? "" : r.GetString(1),
                    Convert.ToUInt16(r.GetValue(2)), Convert.ToUInt16(r.GetValue(3)),
                    unchecked((uint)Convert.ToInt32(r.GetValue(4))), unchecked((uint)Convert.ToInt32(r.GetValue(5))),
                    Convert.ToByte(r.GetValue(6)), Convert.ToByte(r.GetValue(7)), 0,
                    checked((byte)Math.Clamp(Convert.ToInt32(r.GetValue(8)), 0, 255)), Math.Max(1, Convert.ToInt32(r.GetValue(9))), true,
                    Convert.ToInt32(r.GetValue(10)), Math.Max(0, Convert.ToInt32(r.GetValue(11))), Math.Max(0, Convert.ToInt32(r.GetValue(12))),
                    Math.Max(0, Convert.ToInt32(r.GetValue(13))), Math.Max(1, Convert.ToInt32(r.GetValue(14))),
                    Math.Clamp(Convert.ToInt32(r.GetValue(15)), 500, 10000), Math.Clamp(Convert.ToInt32(r.GetValue(16)), 2, 30),
                    Convert.ToByte(r.GetValue(17)), Math.Clamp(Convert.ToInt32(r.GetValue(18))*1.5f,0,12),
                    Math.Clamp(Convert.ToInt32(r.GetValue(19)),0,40), Math.Clamp(Convert.ToInt32(r.GetValue(20)),8,80));
                monsters.Add(monster.Id, monster);
            }
        var spawns = new List<NpcSpawn>();
        // AIServer::LoadSpawnCallback: ActType >= 100 selects K_NPC, below 100 selects K_MONSTER.
        command.CommandText = "SELECT ZoneID,NpcID,LeftX,TopZ,RightX,BottomZ,NumNPC,byDirection,ActType,RegTime FROM K_NPCPOS " +
            "ORDER BY ZoneID,NpcID,LeftX,TopZ,RightX,BottomZ,byDirection";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
                spawns.Add(new NpcSpawn(Convert.ToUInt16(r.GetValue(0)), Convert.ToUInt16(r.GetValue(1)),
                    Convert.ToInt32(r.GetValue(2)), Convert.ToInt32(r.GetValue(3)), Convert.ToInt32(r.GetValue(4)),
                    Convert.ToInt32(r.GetValue(5)), Convert.ToInt32(r.GetValue(6)), Convert.ToByte(r.GetValue(7)), Convert.ToInt32(r.GetValue(8)) < 100, Math.Max(1, Convert.ToInt32(r.GetValue(9)))));
        var objects = new List<WorldObject>();
        command.CommandText = "SELECT ZoneID,sIndex,Type,ControlNpcID,Belong,Status,PosX,PosY,PosZ FROM K_OBJECTPOS ORDER BY ZoneID,sIndex";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
                objects.Add(new WorldObject(Convert.ToUInt16(r.GetValue(0)), Convert.ToUInt16(r.GetValue(1)),
                    Convert.ToByte(r.GetValue(2)), Convert.ToUInt16(r.GetValue(3)), Convert.ToByte(r.GetValue(4)),
                    Convert.ToByte(r.GetValue(5)), Convert.ToSingle(r.GetValue(6)), Convert.ToSingle(r.GetValue(7)), Convert.ToSingle(r.GetValue(8))));
        var drops = new DropCatalog();
        command.CommandText = "SELECT * FROM K_MONSTER_ITEM";
        await using (var r = await command.ExecuteReaderAsync())
            while (await r.ReadAsync())
                drops.Drops.Add(Convert.ToInt32(r["sIndex"]), new DropRow(
                    Enumerable.Range(1,5).Select(i => r[$"iItem{i:00}"] is DBNull ? 0 : Convert.ToInt32(r[$"iItem{i:00}"])).ToArray(),
                    Enumerable.Range(1,5).Select(i => r[$"sPersent{i:00}"] is DBNull ? 0 : Convert.ToInt32(r[$"sPersent{i:00}"])).ToArray()));
        async Task LoadWeights(string table, string key, string[] columns, Dictionary<int,int[]> target)
        {
            command.CommandText = $"SELECT `{key}`," + string.Join(",", columns.Select(c => $"`{c}`")) + $" FROM `{table}`";
            await using var r = await command.ExecuteReaderAsync();
            while (await r.ReadAsync()) target.Add(Convert.ToInt32(r.GetValue(0)),
                Enumerable.Range(1,columns.Length).Select(i => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i))).ToArray());
        }
        await LoadWeights("MAKE_ITEM_GROUP", "iItemGroupNum", Enumerable.Range(1,30).Select(i => $"iItem_{i}").ToArray(), drops.Groups);
        await LoadWeights("MAKE_ITEM_GRADECODE", "byItemIndex", Enumerable.Range(1,9).Select(i => $"byGrade_{i}").ToArray(), drops.Grades);
        await LoadWeights("MAKE_WEAPON", "byLevel", Enumerable.Range(1,12).Select(i => $"sClass_{i}").ToArray(), drops.Weapons);
        await LoadWeights("MAKE_DEFENSIVE", "byLevel", Enumerable.Range(1,7).Select(i => $"sClass_{i}").ToArray(), drops.Armor);
        await LoadWeights("MAKE_ITEM_LARECODE", "byLevelGrade", ["sLareItem","sMagicItem","sGereralItem"], drops.Rarity);
        return new WorldData(zones, protos, spawns, objects, monsters, drops);
    }
}
