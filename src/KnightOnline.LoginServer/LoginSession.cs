using System.Net.Sockets;
using KnightOnline.Database;
using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.LoginServer;

/// <summary>
/// Port of LoginSession (LogInServer/LoginSession.cpp) - the 7 login opcodes.
/// The original dispatched through a function-pointer table indexed by opcode
/// (any opcode without a handler disconnected the client); the switch below
/// keeps that behaviour.
/// </summary>
public sealed class LoginSession : KoSession
{
    private const int MaxIdSize = 20; // shared/globals.h
    private const int MaxPwSize = 28; // shared/globals.h

    private readonly LoginServerApp _app;

    public LoginSession(TcpClient client, LoginServerApp app)
        : base(client, maxPayloadSize: 8192) // original used small buffers; login packets are tiny
    {
        _app = app;
    }

    protected override bool HandlePacket(Packet packet)
    {
        switch (packet.Opcode)
        {
            case LsOpcode.VersionReq: HandleVersion(packet); return true;
            case LsOpcode.DownloadInfoReq: HandlePatches(packet); return true;
            case LsOpcode.LoginReq: HandleLogin(packet); return true;
            case LsOpcode.ServerList: HandleServerlist(packet); return true;
            case LsOpcode.News: HandleNews(packet); return true;
            case LsOpcode.Cryption: HandleSetEncryptionPublicKey(packet); return true;
            case LsOpcode.UnkF7: HandleUnkF7(packet); return true;
            default: return false; // unknown packet -> disconnect, as the original
        }
    }

    private void HandleVersion(Packet packet)
    {
        var result = new Packet(packet.Opcode);
        result.WriteUInt16(_app.LatestVersion);
        Send(result);
    }

    private void HandlePatches(Packet packet)
    {
        var version = packet.ReadUInt16();

        // std::set<std::string> - distinct, sorted file names newer than the client's version
        var downloads = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var info in _app.VersionList)
        {
            if (info.Version > version && info.FileName.Length > 0)
                downloads.Add(info.FileName);
        }

        var result = new Packet(packet.Opcode);
        result.WriteString(_app.Settings.FtpUrl);
        result.WriteString(_app.Settings.FtpPath);
        result.WriteUInt16((ushort)downloads.Count);
        foreach (var file in downloads)
            result.WriteString(file);

        Send(result);
    }

    private void HandleLogin(Packet packet)
    {
        var account = packet.ReadString();
        var password = packet.ReadString();

        AccountLoginResult resultCode;

        if (account.Length is 0 or > MaxIdSize ||
            password.Length is 0 or > MaxPwSize ||
            !WordGuardSystem(account))
        {
            resultCode = AccountLoginResult.NotFound;
        }
        else if (_app.Accounts is null)
        {
            resultCode = AccountLoginResult.Error;
        }
        else
        {
            try
            {
                // Original behaviour was a blocking ODBC call on the IO thread;
                // kept blocking here for now (handlers get an async rework
                // together with the GameServer's DatabaseThread port).
                resultCode = _app.Accounts.AccountLoginAsync(account, password)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                KoLog.Error($"AccountLogin failed for {account}.", ex);
                resultCode = AccountLoginResult.Error;
            }
        }

        var authMessage = resultCode switch
        {
            AccountLoginResult.Success => "SUCCESS",
            AccountLoginResult.NotFound => "NOT FOUND",
            AccountLoginResult.InvalidPassword => "INVALID",
            AccountLoginResult.Banned => "BANNED",
            AccountLoginResult.InGame => "IN GAME",
            AccountLoginResult.Error => "ERROR",
            AccountLoginResult.Agreement => "USER AGREEMENT",
            AccountLoginResult.Failed => "FAILED",
            _ => $"UNKNOWN ({(byte)resultCode})",
        };

        var now = DateTime.Now;
        var logLine = $"[ LOGIN - {now.Hour}:{now.Minute}:{now.Second} ] ID={account} Authentication={authMessage}\n";
        KoLog.Info(logLine.TrimEnd());
        // SECURITY FIX vs the original: its format-string bug wrote the
        // account *password* into the user log. We log the same line as the
        // console - never the password.
        _app.WriteUserLog(logLine);

        var result = new Packet(packet.Opcode);
        result.WriteByte((byte)resultCode);

        if (resultCode == AccountLoginResult.Success && _app.Accounts is not null)
        {
            short premiumHours = -1;
            try
            {
                premiumHours = _app.Accounts.AccountPremiumAsync(account)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                KoLog.Error($"AccountPremium failed for {account}.", ex);
            }

            result.WriteInt16(premiumHours);
            result.WriteString(account);
        }

        Send(result);
    }

    /// <summary>
    /// Port of WordGuardSystem(): account names may only contain letters/digits
    /// from the original's allowed set (all alphanumeric ASCII).
    /// </summary>
    private static bool WordGuardSystem(string word)
    {
        const string allowed = "qwertyuopasdfghjklizxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890";
        foreach (var c in word)
        {
            if (!allowed.Contains(c))
                return false;
        }
        return word.Length > 0;
    }

    private void HandleServerlist(Packet packet)
    {
        var result = new Packet(packet.Opcode);
        // 1453: the 1500+ echo field is not present.
        _app.AppendServerListTo(result);
        Send(result);
    }

    private void HandleNews(Packet packet)
    {
        var result = new Packet(packet.Opcode);
        var news = _app.Settings.NewsContent;

        if (news.Length > 0)
        {
            result.WriteString("Login Notice");
            result.WriteUInt16((ushort)news.Length);
            result.WriteBytes(news);
        }
        else // dummy news, the client skips past it
        {
            result.WriteString("Login Notice");
            result.WriteString("<empty>");
        }

        Send(result);
    }

    private void HandleSetEncryptionPublicKey(Packet packet)
    {
        var result = new Packet(packet.Opcode);
        result.WriteUInt64(Crypto.GenerateKey());
        Send(result);
        EnableCrypto();
    }

    private void HandleUnkF7(Packet packet)
    {
        var result = new Packet(packet.Opcode);
        result.WriteUInt16(0);
        Send(result);
    }
}
