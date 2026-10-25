using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;
namespace KnightOnline.Verification;

internal static class PartyVerification
{
    private static Packet Invite(string name, byte action = 1) => new Packet(WizOpcode.Party).WriteByte(action).WriteString(name);
    private static Packet Permit(byte accept = 1) => new Packet(WizOpcode.Party).WriteByte(2).WriteByte(accept);
    private static Packet Member(byte action, ushort target) => new Packet(WizOpcode.Party).WriteByte(action).WriteUInt16(target);
    public static void Rules(Action<string,bool> test)
    {
        long now = 1000;
        var hub = new PartyHub(() => now);
        var packets = new Dictionary<ushort,List<Packet>>();
        PartyMember Player(ushort id, byte nation = 1, ushort zone = 21, byte level = 10, bool hidden = false)
        {
            if (!packets.ContainsKey(id)) packets[id] = [];
            return new(id, ((char)('A' + id - 1)).ToString(), nation, zone, level, 105, 100, 80, 50, 40, hidden, p => packets[id].Add(p));
        }
        for (ushort i = 1; i <= 10; i++) hub.Publish(Player(i));
        test("party wire: native insertion short HP/MP fields",Convert.ToHexString(PartyHub.Insert(Player(1)).Payload)=="03010001010041640050000A69003200280001");
        test("party requires invitation before acceptance", !hub.Process(2, Permit()) && hub.Members(2).Length == 0);
        test("party self invitation refused", !hub.Process(1, Invite("A")));
        test("party unknown recipient refused", !hub.Process(1, Invite("missing")));
        test("party invite creates no membership before acceptance", hub.Process(1, Invite("B")) && hub.Members(1).Length == 0 && hub.Members(2).Length == 0);
        test("party wire: invite target sees leader ID and name", Convert.ToHexString(packets[2].Last().Payload)=="020100010041");
        test("party competing invitation cannot steal pending recipient", !hub.Process(3, Invite("B")));
        test("party explicit refusal clears pending invitation", hub.Process(2, Permit(0)) && hub.Process(1, Invite("B")));
        test("party acceptance establishes shared leader-first membership", hub.Process(2, Permit()) && hub.Members(1).SequenceEqual(new ushort[]{1,2}) && hub.Members(2).SequenceEqual(new ushort[]{1,2}));
        test("party acceptance replay does not duplicate members", !hub.Process(2, Permit()) && hub.Members(2).Length == 2);
        test("party wire: leader state uses uint32 value", Convert.ToHexString(packets[1].Last().Payload)=="01000601000000");
        var before = packets[3].Count;
        test("party chat routes only to established members",hub.Chat(1,"hello") && packets[1].Last().Opcode==WizOpcode.Chat && packets[2].Last().Opcode==WizOpcode.Chat && packets[3].Count==before && !hub.Chat(3,"spoof"));
        test("party member cannot invite or disband", !hub.Process(2, Invite("C",3)) && !hub.Process(2,new Packet(WizOpcode.Party).WriteByte(5)));
        test("party existing group invitation",hub.Process(1,Invite("C",3)) && hub.Process(3,Permit()) && hub.Members(3).Length==3);
        test("party nonleader cannot kick another member", !hub.Process(2, Member(4,3)));
        test("party cannot kick nonmember", !hub.Process(1,Member(4,4)));
        test("party nonleader cannot promote", !hub.Process(2,Member(28,3)));
        test("party promotion swaps leader slot",hub.Process(1,Member(28,3)) && hub.Members(1).SequenceEqual(new ushort[]{3,2,1}));
        test("party former leader loses authority",!hub.Process(1,new Packet(WizOpcode.Party).WriteByte(5)));
        test("party current leader can kick",hub.Process(3,Member(4,2)) && hub.Members(2).Length==0 && hub.Members(3).Length==2);
        var start = packets[3].Count;
        hub.Publish(Player(1) with {Hp=60, Mp=30});
        test("party HP/MP update sends native short fields",packets[3].Count==start+1 && Convert.ToHexString(packets[3].Last().Payload)=="06010064003C0032001E00");
        hub.Remove(1);
        test("party last member departure disbands remaining solo member",hub.Members(3).Length==0 && hub.Members(1).Length==0);
        hub.Publish(Player(1));
        test("party timeout invite setup",hub.Process(1,Invite("B")));
        now += 30000;
        test("party expiry at deadline refuses late acceptance",!hub.Process(2,Permit()) && hub.Members(1).Length==0);
        test("party disconnect invite setup",hub.Process(1,Invite("B")));
        hub.Remove(2);hub.Publish(Player(2));
        test("party socket-ID reuse cannot accept old invitation",!hub.Process(2,Permit()));
        test("party zone-change acceptance setup",hub.Process(1,Invite("B")));
        hub.Publish(Player(2,zone:1));
        test("party zone change cancels pending invitation",!hub.Process(2,Permit()));
        hub.Publish(Player(2,level:80));
        test("party source level window enforced",!hub.Process(1,Invite("B")));
        hub.Publish(Player(2,hidden:true));
        test("party hidden GM cannot be invited",!hub.Process(1,Invite("B")));
        hub.Publish(Player(1,zone:1));hub.Publish(Player(2,nation:2,zone:1));
        test("party opposing nation outside safe exception refused",!hub.Process(1,Invite("B")));
        hub.Publish(Player(1));hub.Publish(Player(2,nation:2));
        test("party Moradon opposing nation exception",hub.Process(1,Invite("B")) && hub.Process(2,Permit()));
        for (ushort i=3;i<=8;i++)
            test($"party capacity member {i}",hub.Process(1,Invite(((char)('A'+i-1)).ToString(),3))&&hub.Process(i,Permit()));
        test("party ninth member refused",hub.Members(1).Length==8&&!hub.Process(1,Invite("I",3)));
        hub.Remove(1);
        test("party leader disconnect disbands all members",Enumerable.Range(1,8).All(i=>hub.Members((ushort)i).Length==0));
        foreach(var bytes in new byte[][] {[],[2],[2,2],[4,1],[5,1],[28],[1,2,0,65],[3,1,0,65,0]})
            test("party malformed request refused: "+Convert.ToHexString(bytes),!hub.Process(2,Packet.FromReceived(WizOpcode.Party,bytes)));
        hub.Publish(Player(1));
        test("party concurrent permit setup",hub.Process(1,Invite("B")));
        var permits=Task.WhenAll(Task.Run(()=>hub.Process(2,Permit())),Task.Run(()=>hub.Process(2,Permit()))).GetAwaiter().GetResult();
        test("party concurrent permits commit exactly one join",permits.Count(ok=>ok)==1&&hub.Members(1).Length==2);
        test("party self leave disbands two-member group",hub.Process(2,Member(4,2))&&hub.Members(1).Length==0&&hub.Members(2).Length==0);
        test("party departure cancels leader invitation setup",hub.Process(1,Invite("B")));
        hub.Remove(1);hub.Publish(Player(1));
        test("party leader ID reuse cannot revive invite",!hub.Process(2,Permit()));
        hub.Publish(Player(10) with {Name="B"});
        test("party ambiguous character identity refused",!hub.Process(1,Invite("B")));
    }
}
