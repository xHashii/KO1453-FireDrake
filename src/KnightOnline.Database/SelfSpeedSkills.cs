using KnightOnline.Shared.Protocol;
using MySqlConnector;
namespace KnightOnline.Database;
public sealed record SelfSpeedSkill(CombatSkill Skill, short Duration, byte Speed);
public sealed class SpeedEffect
{
    public uint Skill { get; private set; }
    public byte Percent { get; private set; } = 100;
    private long _until;
    public bool Active(long now) => Skill != 0 && now < _until;
    public void Apply(SelfSpeedSkill definition,long now)
    { Skill=definition.Skill.Id; Percent=definition.Speed; _until=checked(now+definition.Duration*1000L); }
    public bool Clear() { var had=Skill!=0;Skill=0;Percent=100;_until=0;return had; }
    public bool Expire(long now) => Skill!=0 && !Active(now) && Clear();
}
public static class SelfSpeedPackets
{
    public static Packet Applied(SelfSpeedSkill skill,ushort caster) 
    {
        var p=new Packet(WizOpcode.MagicProcess).WriteByte(3).WriteUInt32(skill.Skill.Id).WriteUInt16(caster).WriteUInt16(caster);
        for(var i=0;i<7;i++)p.WriteInt16(i==1?(short)1:i==3?skill.Duration:i==5?(short)skill.Speed:(short)0);
        return p;
    }
    public static Packet Removed()=>new Packet(WizOpcode.MagicProcess).WriteByte(5).WriteByte(6);
}
public sealed partial class MariaDbCharacterRepository
{
    public async Task<SelfSpeedSkill?> LoadSelfSpeedSkillAsync(uint id)
    {
        await using var c=new MySqlConnection(_connectionString);await c.OpenAsync();await using var cmd=c.CreateCommand();
        // Only a pure beneficial self-speed effect. Never silently discard secondary
        // effects or pretend HP/AC/attack/stat buffs are implemented as speed.
        cmd.CommandText="SELECT m.Skill,m.SkillLevel,m.Msp,m.CastTime,m.ReCastTime,t.Duration,t.Speed FROM MAGIC m JOIN MAGIC_TYPE4 t ON t.iNum=m.MagicNum WHERE m.MagicNum=@id AND m.Moral=1 AND m.Type1=4 AND m.Type2=0 AND m.UseItem=0 AND m.HP=0 AND m.Msp>=0 AND m.Skill>0 AND m.SuccessRate=100 AND t.BuffType=6 AND t.Radius=0 AND t.Duration BETWEEN 1 AND 3600 AND t.Speed BETWEEN 101 AND 200 AND t.AttackSpeed=100 AND t.AC=0 AND t.ACPct=100 AND t.Attack=100 AND t.MagicAttack=100 AND t.MaxHP=0 AND t.MaxHPPct=100 AND t.MaxMP=0 AND t.MaxMPPct=100 AND t.HitRate=100 AND t.AvoidRate=100 AND t.Str=0 AND t.Sta=0 AND t.Dex=0 AND t.Intel=0 AND t.Cha=0 AND t.FireR=0 AND t.ColdR=0 AND t.LightningR=0 AND t.MagicR=0 AND t.DiseaseR=0 AND t.PoisonR=0 AND t.ExpPct=100 AND t.SpecialAmount=0";
        cmd.Parameters.AddWithValue("@id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        var skill=new CombatSkill(id,Convert.ToInt32(r[0]),Convert.ToInt32(r[1]),Convert.ToInt32(r[2]),Math.Max(0,Convert.ToInt32(r[3]))*100,Math.Max(500,Convert.ToInt32(r[4])*100),0,4,0,0,0,0);
        var result=new SelfSpeedSkill(skill,Convert.ToInt16(r[5]),Convert.ToByte(r[6]));return await r.ReadAsync()?null:result;
    }
}
