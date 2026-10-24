using KnightOnline.Database.World;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Verification;

internal static class MeleeVerification
{
    public static void Run(Action<string, bool> test)
    {
        var captured = Packet.FromReceived(WizOpcode.Attack, Convert.FromHexString("01014A3495000E00"));
        test("melee captured R packet parses", MeleeRules.TryRead(captured, out var request) && request.Target == 13386 && request.Type == 1);
        test("melee ignores client-supplied result/delay/distance", MeleeRules.TryRead(Packet.FromReceived(WizOpcode.Attack, Convert.FromHexString("01FF4A340000FFFF")), out var spoofed) && spoofed == request);
        test("melee malformed length/type rejected", !MeleeRules.TryRead(Packet.FromReceived(WizOpcode.Attack, new byte[7]), out _) && !MeleeRules.TryRead(Packet.FromReceived(WizOpcode.Attack, new byte[9]), out _) && !MeleeRules.TryRead(Packet.FromReceived(WizOpcode.Attack, new byte[8]), out _));
        test("melee success and kill packet golden", Convert.ToHexString(MeleeRules.Reply(request,1001,1).Payload) == "0101E9034A34" && Convert.ToHexString(MeleeRules.Reply(request,1001,2).Payload) == "0102E9034A34");
        test("melee server weapon cadence/range bounded", MeleeRules.DelayMs(139) == 1490 && MeleeRules.DelayMs(-1) == 1000 && MeleeRules.Range(14) == 29f && MeleeRules.Range(1000) == 115f);
        test("melee armed/long attack type accepted", MeleeRules.TryRead(Packet.FromReceived(WizOpcode.Attack, Convert.FromHexString("02014A3495000E00")), out var armed) && armed.Type == 2);
        test("melee damage uses hit/armor and cannot overflow", MeleeRules.Damage(100,200) == 50 && MeleeRules.Damage(int.MaxValue,0) == ushort.MaxValue && MeleeRules.Damage(0,int.MaxValue) == 1);
        var life = new MonsterCombat(100, 10);
        test("melee shared health decreases", life.Hit(30,1000) is { Hp:70, Damage:30, Killed:false } && life.Health(1001) == 70);
        test("melee killing blow clamps overkill", life.Hit(500,2000) is { Hp:0, Damage:70, Killed:true });
        test("melee corpse retained for death animation", life.CorpseVisible(2001) && life.DeathToken is not null);
        test("melee cannot repeatedly kill corpse", life.Hit(1,2001) is null && life.Health(11999) == 0);
        test("melee authored respawn restores full HP", life.Health(12000) == 100 && life.Hit(1,12001) is { Hp:99 });
        var concurrent = new MonsterCombat(100,10);
        var results = Enumerable.Range(0,50).AsParallel().Select(_ => concurrent.Hit(10,1000)).Where(r => r is not null).ToArray();
        test("melee concurrent attackers share one death and bounded damage", results.Sum(r => r!.Damage) == 100 && results.Count(r => r!.Killed) == 1 && concurrent.Health(1001) == 0);
        test("melee separate spawns have independent HP", new MonsterCombat(100,10).Health(1001) == 100);
    }
}
