using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;
internal sealed partial class GameSession
{
    private readonly SpeedEffect _speedEffect=new();
    private void ProcessMagic(Packet packet)
    {
        if(!MagicPackets.TryRead(packet,out var request))return;
        try
        {
            // Target=self does not imply potion. Only an authoritative consumable
            // definition may enter the inventory-consuming potion handler.
            var potion=_characters.LoadPotionAsync(request.Skill).GetAwaiter().GetResult();
            if(potion is not null)UsePotion(request,potion);
            else UseCombatSkill(request);
        }
        catch(Exception ex){InventoryUnavailable(ex);}
    }
    private void ClearSpeedEffect()
    { if(_speedEffect.Clear())Send(SelfSpeedPackets.Removed()); }
    private void TickSpeedEffect()
    { if(_speedEffect.Expire(Environment.TickCount64))Send(SelfSpeedPackets.Removed()); }
    private void UseSpeedSkill(MagicRequest request,SelfSpeedSkill definition)
    {
        void Reject(string reason)
        {
            Send(CombatSkillRules.Reply(request.Skill,_socketId,request.Target,4,false));
            KoLog.Info($"[ SKILL REJECT ] {_charId} skill={request.Skill}: {reason}");
        }
        if(!CanTravel() || _user is null || _sitting || request.Caster!=_socketId || request.Target!=_socketId || !CombatSkillRules.Learned(definition.Skill,_user.Class,_user.Level,_user.Skill))
        {Reject("self-speed state/caster/target/class/level");return;}
        if(request.Stage==5)
        {if(_speedEffect.Skill==request.Skill)ClearSpeedEffect();return;}
        if(request.Stage is not (1 or 3)){Reject("unsupported speed stage");return;}
        var now=Environment.TickCount64;
        TickSpeedEffect();
        if(_speedEffect.Active(now) || _currentMp<definition.Skill.Mp){Reject("speed effect already active or insufficient MP");return;}
        if(request.Stage==1)
        {
            if(_casting is {} pending && now<pending.Ready){Reject("already casting");return;}
            _pendingMelee=null;_casting=(request.Skill,_socketId,now+definition.Skill.CastMs);
            Send(CombatSkillRules.Reply(request.Skill,_socketId,_socketId,1,true));return;
        }
        if(definition.Skill.CastMs>0 && (_casting is not {} cast || cast.Skill!=request.Skill || cast.Target!=_socketId || now<cast.Ready || now>cast.Ready+5000))
        {Reject("speed cast timing");return;}
        _casting=null;
        var result=_characters.SpendCombatSkillAsync(_charId,definition.Skill,_user.Hp,_user.Mp,_currentHp,_currentMp).GetAwaiter().GetResult();
        if(result==InventorySaveResult.Cooldown){Reject("cooldown");return;}
        if(result!=InventorySaveResult.Saved)throw new InvalidOperationException("Stale self-speed vitals.");
        var mp=_currentMp-definition.Skill.Mp;
        _user=_user with {Hp=(short)_currentHp,Mp=(short)mp};RestoreVitals(0,mp-_currentMp);
        _speedEffect.Apply(definition,now);
        UpdatePlayerPresence();
        _players.Broadcast(_socketId,SelfSpeedPackets.Applied(definition,_socketId),true);
        KoLog.Info($"[ SKILL ] {_charId} skill={request.Skill} speed={definition.Speed}% duration={definition.Duration}s MP={_currentMp}/{_maxMp}");
    }
}
