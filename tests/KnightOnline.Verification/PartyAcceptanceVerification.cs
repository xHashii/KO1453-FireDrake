using System.Buffers.Binary;
using System.Net.Sockets;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Verification;

/// <summary>Real TCP/KO frame harness around production PartyHub. Fixture-only
/// opcodes publish trusted snapshots; this is NOT a Windows UI or full login test.</summary>
internal static class PartyAcceptanceVerification
{
    private const byte Snapshot = 0xFC, Barrier = 0xFD, Ack = 0xFE;
    private sealed class Session(TcpClient client, PartyHub hub) : KoSession(client)
    {
        private PartyMember _member = null!;
        protected override void OnConnected()
        {
            _member = new(SessionId, "P"+SessionId, 1, 21, 80, 105, 1000, 1000, 500, 500, false, p=>{Send(p);});
            hub.Publish(_member);
            Send(new Packet(Ack).WriteUInt16(SessionId));
        }
        protected override bool HandlePacket(Packet packet)
        {
            if (packet.Opcode == WizOpcode.Party) hub.Process(SessionId, packet);
            else if (packet.Opcode == WizOpcode.Chat && ChatPackets.TryRead(packet,out var type,out var text) && type==3)
                hub.Chat(SessionId,text);
            else if (packet.Opcode == Snapshot && packet.RemainingToRead==10)
            {
                var previousZone=_member.Zone;
                _member=_member with {Hp=packet.ReadInt16(),Mp=packet.ReadInt16(),Zone=packet.ReadUInt16(),Level=packet.ReadByte(),Class=packet.ReadUInt16(),Hidden=packet.ReadByte()!=0};
                hub.Travel(SessionId,previousZone,_member.Zone);
                hub.Publish(_member);
            }
            Send(new Packet(Ack));
            return true;
        }
        protected override void OnDisconnected()=>hub.Remove(SessionId);
    }
    private sealed class Client : IDisposable
    {
        private readonly TcpClient _tcp = new();
        public ushort Id {get;private set;}
        public List<Packet> Received {get;}=[];
        public async Task Connect(int port)
        {
            await _tcp.ConnectAsync("127.0.0.1",port);
            var hello=await Read();
            if(hello.Opcode!=Ack) throw new InvalidDataException("Missing fixture greeting");
            Id=hello.ReadUInt16();
        }
        private async Task<Packet> Read()
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var stream=_tcp.GetStream();var header=new byte[4];
            await stream.ReadExactlyAsync(header,timeout.Token);
            if(header[0]!=0xAA||header[1]!=0x55) throw new InvalidDataException("KO header");
            var length=BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
            var payload=new byte[length+2];await stream.ReadExactlyAsync(payload,timeout.Token);
            if(length==0||payload[^2]!=0x55||payload[^1]!=0xAA) throw new InvalidDataException("KO footer");
            return Packet.FromReceived(payload[0],payload.AsSpan(1,length-1));
        }
        public async Task Exchange(Packet packet)
        {
            await _tcp.GetStream().WriteAsync(KoPacketCodec.Encode(packet,null,false,0));
            while(true)
            {
                var reply=await Read();
                if(reply.Opcode==Ack) return;
                Received.Add(reply);
            }
        }
        public Task Flush()=>Exchange(new Packet(Barrier));
        public void Dispose()=>_tcp.Dispose();
    }
    private static Packet Invite(Client target,byte action=1)=>new Packet(WizOpcode.Party).WriteByte(action).WriteString("P"+target.Id);
    private static Packet Member(byte action,Client target)=>new Packet(WizOpcode.Party).WriteByte(action).WriteUInt16(target.Id);
    private static Packet Permit()=>new Packet(WizOpcode.Party).WriteByte(2).WriteByte(1);
    private static Packet Publish(short hp=1000,short mp=500,ushort zone=21,byte level=80,ushort cls=105,bool hidden=false)=>
        new Packet(Snapshot).WriteInt16(hp).WriteInt16(mp).WriteUInt16(zone).WriteByte(level).WriteUInt16(cls).WriteByte(hidden?(byte)1:(byte)0);

    public static async Task RunAsync(Action<string,bool> test)
    {
        var hub=new PartyHub();
        var server=new KoTcpServer<Session>(c=>new(c,hub));var running=server.RunAsync(0);
        using var a=new Client();using var b=new Client();using var c=new Client();using var outsider=new Client();
        var clients=new[]{a,b,c,outsider};
        async Task Flush(){foreach(var client in clients) await client.Flush();}
        async Task Clear(){await Flush();foreach(var client in clients) client.Received.Clear();}
        bool Has(Client client,byte opcode,string hex)=>client.Received.Any(p=>p.Opcode==opcode&&Convert.ToHexString(p.Payload)==hex);
        try
        {
            foreach(var client in clients) await client.Connect(server.ListeningPort);
            await a.Exchange(Invite(b));await b.Exchange(Permit());
            await a.Exchange(Invite(c,3));await c.Exchange(Permit());await Flush();
            test("party acceptance/TCP: three clients form matching leader-first roster",clients.Take(3).All(p=>hub.Members(p.Id).SequenceEqual(new[]{a.Id,b.Id,c.Id}))&&hub.Members(outsider.Id).Length==0);
            test("party acceptance/TCP: outsider gets no invitation/roster/state packets",outsider.Received.Count==0);
            await Clear();
            await a.Exchange(Publish());await b.Exchange(Publish());await Flush();
            test("party acceptance/TCP: same-map leader and member travel retain roster without removal/disband packets",clients.Take(3).All(p=>hub.Members(p.Id).SequenceEqual(new[]{a.Id,b.Id,c.Id}))&&clients.All(p=>p.Received.Count==0));
            await b.Exchange(new Packet(WizOpcode.Chat).WriteByte(3).WriteString("party-only"));await Flush();
            var expected=Convert.ToHexString(ChatPackets.Message(3,1,b.Id,"P"+b.Id,"party-only").Payload);
            test("party acceptance/TCP: chat has trusted sender and reaches exactly three members",clients.Take(3).All(p=>Has(p,WizOpcode.Chat,expected))&&outsider.Received.Count==0);
            await Clear();
            await b.Exchange(Publish(hp:250,mp:125));await Flush();
            expected=Convert.ToHexString(new Packet(WizOpcode.Party).WriteByte(6).WriteUInt16(b.Id).WriteInt16(1000).WriteInt16(250).WriteInt16(500).WriteInt16(125).Payload);
            test("party acceptance/TCP: damage/MP consumption updates every member with native short fields",clients.Take(3).All(p=>Has(p,WizOpcode.Party,expected))&&outsider.Received.Count==0);
            await Clear();await b.Exchange(Publish(hp:250,mp:125));await Flush();
            test("party acceptance/TCP: unchanged vitals do not spam updates",clients.All(p=>p.Received.Count==0));
            await b.Exchange(Publish(hp:0,mp:125));await Flush();
            test("party acceptance/TCP: death keeps membership and reports zero HP",hub.Members(b.Id).Length==3&&b.Received.Any(p=>p.Opcode==WizOpcode.Party&&p.Payload[0]==6&&p.Payload[5]==0&&p.Payload[6]==0));
            await Clear();await b.Exchange(Publish());await Flush();
            test("party acceptance/TCP: recovery restores full HP/MP without rejoining",clients.Take(3).All(p=>p.Received.Any(x=>x.Opcode==WizOpcode.Party&&x.Payload[0]==6))&&hub.Members(b.Id).Length==3);
            await Clear();await a.Exchange(Member(28,b));await Flush();
            var oldFlag=Convert.ToHexString(new Packet(WizOpcode.StateChange).WriteUInt16(a.Id).WriteByte(6).WriteUInt32(0).Payload);
            var newFlag=Convert.ToHexString(new Packet(WizOpcode.StateChange).WriteUInt16(b.Id).WriteByte(6).WriteUInt32(1).Payload);
            test("party acceptance/TCP: promotion updates flags and leader-position roster",clients.Take(3).All(p=>Has(p,WizOpcode.StateChange,oldFlag)&&Has(p,WizOpcode.StateChange,newFlag)&&p.Received.Any(x=>x.Opcode==WizOpcode.Party&&x.Payload[0]==3&&x.Payload[3]==100))&&hub.Members(a.Id)[0]==b.Id);
            await Clear();await a.Exchange(Member(4,c));await Flush();
            test("party acceptance/TCP: former leader cannot kick and emits no removal",hub.Members(c.Id).Length==3&&clients.All(p=>p.Received.Count==0));
            await c.Exchange(Member(4,c));await Flush();
            var removed=Convert.ToHexString(Member(4,c).Payload);
            test("party acceptance/TCP: voluntary leave updates all three rosters",hub.Members(c.Id).Length==0&&hub.Members(a.Id).Length==2&&clients.Take(3).All(p=>Has(p,WizOpcode.Party,removed)));
            await Clear();await c.Exchange(new Packet(WizOpcode.Chat).WriteByte(3).WriteString("not-member"));await Flush();
            test("party acceptance/TCP: departed member cannot send party chat",clients.All(p=>p.Received.Count==0));
            await b.Exchange(Invite(c,3));await c.Exchange(Permit());await Clear();
            await c.Exchange(Publish(zone:1));await Flush();
            test("party acceptance/TCP: direct zone transition clears membership and chat routing",hub.Members(c.Id).Length==0&&hub.Members(b.Id).Length==2);
            await c.Exchange(Publish());await b.Exchange(Invite(c,3));await c.Exchange(Permit());await Clear();
            c.Dispose();
            // Barrier on another socket does not prove EOF processing. Wait for the
            // authoritative membership transition with a bounded deadline.
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                while(hub.Members(c.Id).Length!=0) await Task.Delay(10,timeout.Token);
            await a.Flush();await b.Flush();await outsider.Flush();
            test("party acceptance/TCP: forced client close removes exactly that nonleader",hub.Members(a.Id).Length==2&&Has(a,WizOpcode.Party,removed)&&Has(b,WizOpcode.Party,removed)&&outsider.Received.Count==0);
            b.Dispose();
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                while(hub.Members(a.Id).Length!=0) await Task.Delay(10,timeout.Token);
            await a.Flush();await outsider.Flush();
            test("party acceptance/TCP: leader disconnect disbands survivor",Has(a,WizOpcode.Party,"05")&&hub.Members(a.Id).Length==0&&outsider.Received.Count==0);
        }
        finally {server.Stop();await running.WaitAsync(TimeSpan.FromSeconds(10));}
    }
}
