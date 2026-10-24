using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Verification;

internal static class LoggingVerification
{
    public static void Run(Action<string, bool> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ko-log-test-" + Guid.NewGuid().ToString("N"));
        var output = Console.Out;
        var captured = new StringWriter();
        bool mirrored, unique, isolated, fallback, invalidName;
        try
        {
            Console.SetOut(captured);
            var first = KoLog.StartRun("GameServer", directory)!;
            KoLog.Info("sample INFO");
            KoLog.Warn("sample WARN");
            KoLog.Error("sample ERROR", new InvalidOperationException("sample exception"));
            Parallel.For(0, 30, i => KoLog.Info($"parallel {i}"));
            // Read before closing: AutoFlush and FileShare.Read make live logs usable.
            var firstText = File.ReadAllText(first);
            mirrored = firstText == captured.ToString();
            var second = KoLog.StartRun("GameServer", directory)!;
            KoLog.Info("second run only");
            KoLog.CloseFile();
            unique = first != second && File.Exists(first) && File.Exists(second) && Path.GetFileName(first).StartsWith($"GameServer_{DateTime.Now:yyyy-MM-dd}_");
            isolated = File.ReadAllText(first) == firstText && !File.ReadAllText(second).Contains("sample INFO") && File.ReadAllText(second).Contains("second run only");
            var blocker = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blocker, "file blocks directory");
            fallback = KoLog.StartRun("GameServer", blocker) is null && KoLog.CurrentFilePath is null;
            KoLog.Info("console still usable");
            fallback &= captured.ToString().Contains("console still usable");
            invalidName = false;
            try { KoLog.StartRun("../outside", directory); } catch (ArgumentException) { invalidName = true; }
        }
        finally
        {
            KoLog.CloseFile();
            Console.SetOut(output);
            captured.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        check("logging: live file mirrors console including multiline errors and concurrent entries", mirrored);
        check("logging: separate timestamped files for consecutive runs", unique);
        check("logging: prior runs never overwritten or mixed", isolated);
        check("logging: unwritable location falls back to console", fallback);
        check("logging: service names cannot escape log directory", invalidName);

        var login = new Packet(WizOpcode.Login).WriteString("test-user").WriteString("test-password");
        var deletion = new Packet(WizOpcode.DelChar).WriteString("test-password");
        check("logging: credential packet contents redacted", PacketTrace.GamePayload(login).Contains("redacted") && !PacketTrace.GamePayload(login).Contains(Convert.ToHexString(login.Payload)) && PacketTrace.GamePayload(deletion).Contains("redacted"));
        check("logging: malformed login payload also redacted", PacketTrace.GamePayload(new Packet(WizOpcode.Login)).Contains("redacted"));
        var seal=new Packet(WizOpcode.ItemUpgrade).WriteByte(8).WriteByte(1).WriteString("secret-code");
        var characterSeal=new Packet(WizOpcode.ItemUpgrade).WriteByte(9).WriteString("secret-code");
        check("logging: item and character seal credentials never enter logs",PacketTrace.GamePayload(seal).Contains("redacted")&&PacketTrace.GamePayload(characterSeal).Contains("redacted"));
        check("logging: malformed seal request is still redacted",PacketTrace.GamePayload(new Packet(WizOpcode.ItemUpgrade).WriteByte(8)).Contains("redacted"));
        check("logging: ordinary packet payload remains traceable", PacketTrace.GamePayload(new Packet(WizOpcode.ItemTrade).WriteByte(3)) == "03");
    }
}
