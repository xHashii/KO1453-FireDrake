using MySqlConnector;
namespace KnightOnline.Database;
public sealed record SelfHealingSkill(CombatSkill Skill,int Amount);
public static class HealingRules
{
    public static int? SelfHp(SelfHealingSkill definition,int hp,int maxHp)
    {
        if(hp<=0 || maxHp<=0 || hp>=maxHp || definition.Amount<=0)return null;
        return (int)Math.Min(maxHp,(long)hp+definition.Amount);
    }
}
public sealed partial class MariaDbCharacterRepository
{
    public async Task<SelfHealingSkill?> LoadSelfHealingSkillAsync(uint id)
    {
        await using var c=new MySqlConnection(_connectionString);await c.OpenAsync();await using var cmd=c.CreateCommand();
        // Moral 2 permits self as well as friendly targets; this first slice only
        // executes self. Never reinterpret enemy drains, group healing or HoT.
        cmd.CommandText="SELECT m.Skill,m.SkillLevel,m.Msp,m.CastTime,m.ReCastTime,t.FirstDamage FROM MAGIC m JOIN MAGIC_TYPE3 t ON t.iNum=m.MagicNum WHERE m.MagicNum=@id AND m.Moral IN (1,2) AND m.Type1=3 AND m.Type2=0 AND m.Skill>0 AND m.Msp>=0 AND m.HP=0 AND m.UseItem=0 AND m.SuccessRate=100 AND t.DirectType=1 AND t.FirstDamage>0 AND t.TimeDamage=0 AND t.EndDamage=0 AND t.Duration=0 AND t.Radius=0 AND t.Attribute=0";
        cmd.Parameters.AddWithValue("@id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        var skill=new CombatSkill(id,Convert.ToInt32(r[0]),Convert.ToInt32(r[1]),Convert.ToInt32(r[2]),Math.Max(0,Convert.ToInt32(r[3]))*100,Math.Max(500,Convert.ToInt32(r[4])*100),0,3,0,0,0,0);
        var result=new SelfHealingSkill(skill,Convert.ToInt32(r[5]));return await r.ReadAsync()?null:result;
    }
}
