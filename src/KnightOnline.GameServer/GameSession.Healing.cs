using KnightOnline.Database;
using KnightOnline.Database.Items;
using KnightOnline.Shared.Diagnostics;
internal sealed partial class GameSession
{
    private void UseSelfHealingSkill(MagicRequest request,SelfHealingSkill definition)
    {
        void Reject(string reason)
        {
            Send(CombatSkillRules.Reply(request.Skill,_socketId,request.Target,4,false));
            KoLog.Info($"[ SKILL REJECT ] {_charId} skill={request.Skill}: {reason}");
        }
        if(!CanTravel() || _user is null || _sitting || request.Caster!=_socketId || request.Target!=_socketId || request.Stage is not (1 or 3) || !CombatSkillRules.Learned(definition.Skill,_user.Class,_user.Level,_user.Skill))
        {Reject("heal state/caster/self-target/stage/class/level; healing other players is not implemented yet");return;}
        var hp=HealingRules.SelfHp(definition,_currentHp,_maxHp);
        if(hp is null || _currentMp<definition.Skill.Mp){Reject("no healing needed, dead or insufficient MP");return;}
        var now=Environment.TickCount64;
        if(request.Stage==1)
        {
            if(_casting is {} pending && now<pending.Ready){Reject("already casting");return;}
            _pendingMelee=null;_casting=(request.Skill,_socketId,now+definition.Skill.CastMs);
            Send(CombatSkillRules.Reply(request.Skill,_socketId,_socketId,1,true));return;
        }
        if(definition.Skill.CastMs>0 && (_casting is not {} cast || cast.Skill!=request.Skill || cast.Target!=_socketId || now<cast.Ready || now>cast.Ready+5000))
        {Reject("heal cast timing");return;}
        _casting=null;
        var result=_characters.SpendCombatSkillAsync(_charId,definition.Skill,_user.Hp,_user.Mp,hp.Value,_currentMp).GetAwaiter().GetResult();
        if(result==InventorySaveResult.Cooldown){Reject("cooldown");return;}
        if(result!=InventorySaveResult.Saved)throw new InvalidOperationException("Stale heal vitals snapshot.");
        var mp=_currentMp-definition.Skill.Mp;var delta=hp.Value-_currentHp;
        _user=_user with {Hp=(short)hp.Value,Mp=(short)mp};RestoreVitals(delta,mp-_currentMp);
        UpdatePlayerPresence();_players.Broadcast(_socketId,CombatSkillRules.Reply(request.Skill,_socketId,_socketId,3,true),true);
        KoLog.Info($"[ SKILL ] {_charId} skill={request.Skill} healed={delta} HP={_currentHp}/{_maxHp} MP={_currentMp}/{_maxMp}");
    }
}
