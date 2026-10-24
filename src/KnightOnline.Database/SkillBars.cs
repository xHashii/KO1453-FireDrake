using System.Buffers.Binary;
using MySqlConnector;
using KnightOnline.Shared.Protocol;
namespace KnightOnline.Database;

public static class SkillBarPackets
{
    public static bool TryRead(Packet packet, out byte action, out uint[] shortcuts)
    {
        action=0; shortcuts=[];
        if (packet.RemainingToRead < 1) return false;
        action=packet.ReadByte();
        if (action == 2) return packet.RemainingToRead == 0;
        if (action != 1 || packet.RemainingToRead < 2) return false;
        var count=packet.ReadUInt16();
        if (count > 64 || packet.RemainingToRead != count*4) return false;
        shortcuts=new uint[count]; for(var i=0;i<count;i++) shortcuts[i]=packet.ReadUInt32();
        return true;
    }
    public static Packet Loaded(IEnumerable<uint> shortcuts)
    {
        var values=shortcuts.ToArray(); if (values.Length>64) throw new ArgumentOutOfRangeException(nameof(shortcuts));
        var p=new Packet(WizOpcode.Skilldata).WriteByte(2).WriteUInt16((ushort)values.Length);
        foreach(var value in values) p.WriteUInt32(value); return p;
    }
}
public sealed partial class MariaDbCharacterRepository
{
    public async Task EnsureFoundationSchemaAsync()
    {
        await using var c=new MySqlConnection(_connectionString); await c.OpenAsync();
        await using var cmd=c.CreateCommand();
        cmd.CommandText="CREATE TABLE IF NOT EXISTS USERDATA_SKILLSHORTCUT (strCharID char(21) NOT NULL PRIMARY KEY,nCount smallint NOT NULL,strSkillData blob NULL) ENGINE=InnoDB";
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task<uint[]> LoadSkillBarAsync(string character)
    {
        await using var c=new MySqlConnection(_connectionString); await c.OpenAsync();
        await using var cmd=c.CreateCommand(); cmd.CommandText="SELECT nCount,strSkillData FROM USERDATA_SKILLSHORTCUT WHERE strCharID=@id"; cmd.Parameters.AddWithValue("@id",character);
        await using var r=await cmd.ExecuteReaderAsync(); if(!await r.ReadAsync()) return [];
        var count=Convert.ToInt32(r[0]); var bytes=r.IsDBNull(1)?[]:(byte[])r[1];
        if(count is <0 or >64 || bytes.Length<count*4) throw new InvalidDataException("Invalid saved skill bar; not overwriting it.");
        var values=new uint[count]; for(var i=0;i<count;i++) values[i]=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i*4));
        if(await r.ReadAsync()) throw new InvalidDataException("Duplicate saved skill bars."); return values;
    }
    public async Task SaveSkillBarAsync(string character, uint[] values)
    {
        if(values.Length>64) throw new ArgumentOutOfRangeException(nameof(values));
        var bytes=new byte[260]; for(var i=0;i<values.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),values[i]);
        await using var c=new MySqlConnection(_connectionString); await c.OpenAsync();
        await using var tx=await c.BeginTransactionAsync(); await using var cmd=c.CreateCommand(); cmd.Transaction=tx; cmd.Parameters.AddWithValue("@id",character);
        cmd.CommandText="SELECT strUserID FROM USERDATA WHERE strUserID=@id FOR UPDATE";
        await using(var r=await cmd.ExecuteReaderAsync()) if(!await r.ReadAsync() || await r.ReadAsync()) throw new InvalidDataException("Ambiguous skill-bar owner.");
        cmd.CommandText="SELECT strCharID FROM USERDATA_SKILLSHORTCUT WHERE strCharID=@id FOR UPDATE";
        await using(var r=await cmd.ExecuteReaderAsync()) if(await r.ReadAsync() && await r.ReadAsync()) throw new InvalidDataException("Duplicate saved skill bars.");
        cmd.CommandText="DELETE FROM USERDATA_SKILLSHORTCUT WHERE strCharID=@id"; await cmd.ExecuteNonQueryAsync();
        cmd.CommandText="INSERT INTO USERDATA_SKILLSHORTCUT(strCharID,nCount,strSkillData) VALUES(@id,@count,@bytes)"; cmd.Parameters.AddWithValue("@count",values.Length);cmd.Parameters.AddWithValue("@bytes",bytes);
        await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }
}
