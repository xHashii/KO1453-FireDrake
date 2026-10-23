using KnightOnline.Shared;
using KnightOnline.Shared.Diagnostics;

// Milestone 4+ will grow this into the port of GameServer/ + AIServer/
// (merged, per the rewrite plan). For now it exists so the solution layout
// is complete and building.

Console.Title = $"Game Server for Knight Online v{GameVersion.Version} (.NET)";
KoLog.Info("GameServer port has not started yet - see docs/CSHARP_REWRITE_PLAN.md.");
KoLog.Info("Milestone order: wire core -> LoginServer (done) -> data layer -> login/char-select vertical slice.");
return 0;
