using System.Net.Sockets;
using System.Threading.Channels;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;
namespace KnightOnline.Verification;
internal static class PlayerSessionVerification
{
    private sealed class Session(TcpClient client,ChannelWriter<ushort> accepted):KoSession(client)
    {
        protected override bool HandlePacket(Packet packet)=>true;
        protected override void OnConnected()=>accepted.TryWrite(SessionId);
    }
    public static async Task RunAsync(Action<string,bool> test)
    {
        var ids=Channel.CreateUnbounded<ushort>();
        var server=new KoTcpServer<Session>(c=>new(c,ids.Writer),maxSessionId:2);
        var running=server.RunAsync(0);
        using var first=new TcpClient();using var second=new TcpClient();using var excess=new TcpClient();using var recycled=new TcpClient();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await first.ConnectAsync("127.0.0.1",server.ListeningPort,timeout.Token);
            var one=await ids.Reader.ReadAsync(timeout.Token);
            await second.ConnectAsync("127.0.0.1",server.ListeningPort,timeout.Token);
            var two=await ids.Reader.ReadAsync(timeout.Token);
            test("simultaneous player IDs are unique and stay in configured band",one==1&&two==2);
            await excess.ConnectAsync("127.0.0.1",server.ListeningPort,timeout.Token);
            var closed=false;
            try{closed=await excess.GetStream().ReadAsync(new byte[1],timeout.Token)==0;}catch(IOException){closed=true;}
            test("full player-ID band refuses excess sockets without stopping server",closed&&!running.IsCompleted&&server.SessionCount==2);
            first.Dispose();
            while(server.SessionCount!=1) await Task.Delay(10,timeout.Token);
            await recycled.ConnectAsync("127.0.0.1",server.ListeningPort,timeout.Token);
            var reused=await ids.Reader.ReadAsync(timeout.Token);
            test("disconnected IDs recycle without colliding with live players or NPC band",reused==one&&reused!=two&&server.SessionCount==2);
        }
        finally
        {
            server.Stop();
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
