using System.Collections.Concurrent;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Verification;

internal static class ChatVerification
{
    public static void Run(Action<string, bool> check)
    {
        void Test(string name, bool ok) => check("chat: " + name, ok);
        var hub = new ChatHub();
        var self = new ConcurrentQueue<Packet>();
        var near = new ConcurrentQueue<Packet>();
        var far = new ConcurrentQueue<Packet>();
        var otherZone = new ConcurrentQueue<Packet>();
        var sender = new ChatMember(1001, "Hashii", 2, 21, 100, 100, false, self.Enqueue);
        var recipient = new ChatMember(1002, "Nearby", 1, 21, 131.9f, 100, false, near.Enqueue);
        hub.Publish(sender);
        hub.Publish(recipient);
        hub.Publish(new ChatMember(1003, "Far", 2, 21, 132, 100, false, far.Enqueue));
        hub.Publish(new ChatMember(1004, "OtherZone", 2, 1, 100, 100, false, otherZone.Enqueue));
        Test("general delivers to self and nearby player", hub.Local(1001, "hi") == 2 && self.Count == 1 && near.Count == 1);
        Test("general excludes distance >=32 and other zones", far.IsEmpty && otherZone.IsEmpty);
        self.TryPeek(out var echoed);
        Test("general reply matches C++ golden layout", Convert.ToHexString(echoed!.Payload) == "0102E9030648617368696902006869");
        Test("target lookup case-insensitive", hub.Find("nEaRbY")?.Id == 1002);
        Test("private delivered to selected recipient", hub.Whisper(1001, 1004, out var targetName, "secret") && targetName == "OtherZone" && otherZone.Count == 1);
        otherZone.TryPeek(out var privateMessage);
        Test("private reply uses private channel and trusted sender", privateMessage!.Payload[0] == 2 && privateMessage.Payload[1] == 2);
        hub.Publish(recipient with { BlockPrivate = true });
        var previous = near.Count;
        Test("blocked PM not delivered", !hub.Whisper(1001, 1002, out _, "secret") && near.Count == previous);
        Test("self/unknown PM rejected", !hub.Whisper(1001, 1001, out _, "secret") && !hub.Whisper(1001, 9999, out _, "secret"));
        hub.Remove(1004);
        Test("disconnect removes private target", hub.Find("OtherZone") is null && !hub.Whisper(1001, 1004, out _, "secret"));
        hub.Publish(recipient with { Zone = 1 });
        previous = near.Count;
        hub.Local(1001, "after travel");
        Test("position/zone refresh prevents cross-zone delivery", near.Count == previous);
        hub.Publish(recipient with { Id = 1005 });
        Test("ambiguous duplicate names cannot receive private messages", hub.Find("Nearby") is null);
        hub.Remove(1005);
        hub.Remove(1001);
        Test("unpublished sender cannot broadcast", hub.Local(1001, "bad") == 0);

        Packet Input(byte type, string message) => new Packet(WizOpcode.Chat).WriteByte(type).WriteString(message);
        Test("valid client chat parsed", ChatPackets.TryRead(Input(1, "hello"), out var type, out var text) && type == 1 && text == "hello");
        Test("128-byte boundary accepted", ChatPackets.TryRead(Input(1, new string('x', 128)), out _, out _));
        Test("oversize/empty/blank/control text rejected", !ChatPackets.TryRead(Input(1, new string('x', 129)), out _, out _) &&
            !ChatPackets.TryRead(Input(1, ""), out _, out _) && !ChatPackets.TryRead(Input(1, "   "), out _, out _) && !ChatPackets.TryRead(Input(1, "hello\nforged log"), out _, out _));
        Test("malformed/trailing payload rejected", !ChatPackets.TryRead(new Packet(WizOpcode.Chat).WriteByte(1).WriteUInt16(128), out _, out _) &&
            !ChatPackets.TryRead(Input(1, "hi").WriteByte(0), out _, out _));
        var command = "/town";
        Test("command text preserved for command dispatcher", ChatPackets.TryRead(Input(1, command), out _, out var parsedCommand) && parsedCommand == command);
        hub.Publish(sender);
        Parallel.For(0, 100, i => { hub.Publish(recipient with { X = 100 + i % 10 }); hub.Local(1001, "parallel"); });
        hub.Remove(1001);
        hub.Remove(1002);
        Test("concurrent routing/presence cleanup completes", hub.Find("Hashii") is null && hub.Find("Nearby") is null);
    }
}
