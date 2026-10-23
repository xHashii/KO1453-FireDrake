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
        command.CommandText = "SELECT sSid,strName,sPid,sSize,iWeapon1,iWeapon2,byGroup,byType,iSellingGroup,sLevel FROM K_NPC";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
            {
                var p = new NpcPrototype(Convert.ToUInt16(r.GetValue(0)), r.IsDBNull(1) ? "" : r.GetString(1),
                    Convert.ToUInt16(r.GetValue(2)), Convert.ToUInt16(r.GetValue(3)),
                    unchecked((uint)Convert.ToInt32(r.GetValue(4))), unchecked((uint)Convert.ToInt32(r.GetValue(5))),
                    Convert.ToByte(r.GetValue(6)), Convert.ToByte(r.GetValue(7)), Convert.ToInt32(r.GetValue(8)),
                    checked((byte)Math.Clamp(Convert.ToInt32(r.GetValue(9)), 0, 255)));
                protos.Add(p.Id, p);
            }
        var spawns = new List<NpcSpawn>();
        // AIServer::LoadSpawnCallback: ActType >= 100 selects K_NPC, below 100 selects K_MONSTER.
        command.CommandText = "SELECT ZoneID,NpcID,LeftX,TopZ,RightX,BottomZ,NumNPC,byDirection FROM K_NPCPOS WHERE ActType>=100 " +
            "ORDER BY ZoneID,NpcID,LeftX,TopZ,RightX,BottomZ,byDirection";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
                spawns.Add(new NpcSpawn(Convert.ToUInt16(r.GetValue(0)), Convert.ToUInt16(r.GetValue(1)),
                    Convert.ToInt32(r.GetValue(2)), Convert.ToInt32(r.GetValue(3)), Convert.ToInt32(r.GetValue(4)),
                    Convert.ToInt32(r.GetValue(5)), Convert.ToInt32(r.GetValue(6)), Convert.ToByte(r.GetValue(7))));
        var objects = new List<WorldObject>();
        command.CommandText = "SELECT ZoneID,sIndex,Type,ControlNpcID,Belong,Status,PosX,PosY,PosZ FROM K_OBJECTPOS ORDER BY ZoneID,sIndex";
        await using (var r = await command.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false))
                objects.Add(new WorldObject(Convert.ToUInt16(r.GetValue(0)), Convert.ToUInt16(r.GetValue(1)),
                    Convert.ToByte(r.GetValue(2)), Convert.ToUInt16(r.GetValue(3)), Convert.ToByte(r.GetValue(4)),
                    Convert.ToByte(r.GetValue(5)), Convert.ToSingle(r.GetValue(6)), Convert.ToSingle(r.GetValue(7)), Convert.ToSingle(r.GetValue(8))));
        return new WorldData(zones, protos, spawns, objects);
    }
}
