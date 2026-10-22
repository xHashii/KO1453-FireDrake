using KnightOnline.LoginServer;
using KnightOnline.Shared;
using KnightOnline.Shared.Diagnostics;

// Port of LogInServer/main.cpp.

Console.Title = $"LogIn Server for Knight Online v{GameVersion.Version} (.NET)";
KoLog.ConfigureFile("Logs/LoginServer.log");

LoginServerSettings settings;
try
{
    settings = LoginServerSettings.Load();
}
catch (Exception ex)
{
    KoLog.Error($"Failed to read {LoginServerSettings.ConfigFileName}.", ex);
    return 1;
}

var app = new LoginServerApp(settings);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // let RunAsync unwind instead of hard-killing the process
    shutdown.Cancel();
    app.Stop();
};

try
{
    KoLog.Info($"LoginServer starting (port {settings.Port})...");
    await app.RunAsync(shutdown.Token);
}
catch (Exception ex)
{
    KoLog.Error("LoginServer failed to start.", ex);
    return 1;
}

KoLog.Info("LoginServer shut down.");
return 0;
