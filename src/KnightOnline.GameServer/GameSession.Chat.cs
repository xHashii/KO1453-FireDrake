using KnightOnline.Shared.Diagnostics;
using KnightOnline.Shared.Network;
using KnightOnline.Shared.Protocol;

internal sealed partial class GameSession
{
    private readonly ChatHub _chat;
    private bool _blockPrivateChat;
    private ushort _privateChatId;
    private string _privateChatName = "";
    private long _nextChatAt;

    private void UpdateChatPresence()
    {
        if (!IsConnected || !_inGame || _travelStage != 0 || _inventoryFaulted || _user is null)
        {
            _chat.Remove(_socketId);
            return;
        }
        _chat.Publish(new ChatMember(_socketId, _charId, _user.Nation, _user.Zone, PlayerX, PlayerZ,
            _blockPrivateChat, reply => { Send(reply); }));
    }

    private void PlayerChat(byte type, string message)
    {
        var now = Environment.TickCount64;
        if (now < _nextChatAt) return;
        _nextChatAt = now + 500;
        UpdateChatPresence();
        if (type == 1)
        {
            var count = _chat.Local(_socketId, message);
            KoLog.Info($"[ CHAT ] {_charId} general zone={_user!.Zone} recipients={count}: {message}");
        }
        else if (type == 2)
        {
            var target = _chat.Find(_privateChatName);
            if (target is null || target.Id != _privateChatId || !_chat.Whisper(_socketId, target.Id, out var targetName, message))
                TravelMessage("Private message not sent: select an online player who accepts private messages.");
            else
            {
                TravelMessage($"To {targetName}: {message}");
                KoLog.Info($"[ CHAT ] {_charId} private to={targetName}: {message}");
            }
        }
        else if (type == 3)
        {
            UpdatePartyPresence();
            if (!_parties.Chat(_socketId, message)) TravelMessage("Join a party before using party chat.");
        }
        else
            TravelMessage("That chat channel is not implemented yet. Use normal chat or select a player for private chat.");
    }

    private void ChatTargetSelect(Packet packet)
    {
        if (!_inGame || _travelStage != 0 || packet.RemainingToRead < 1) return;
        var type = packet.ReadByte();
        if (type == 1)
        {
            _privateChatId = 0;
            _privateChatName = "";
            if (packet.RemainingToRead < 2) return;
            var length = packet.ReadUInt16();
            if (length is 0 or > 21 || packet.RemainingToRead != length) return;
            packet.ReadPosition -= 2;
            var name = packet.ReadString();
            var target = _chat.Find(name);
            var reply = new Packet(WizOpcode.ChatTarget).WriteByte(1);
            if (target is null || target.Id == _socketId) reply.WriteInt16(0);
            else if (target.BlockPrivate) reply.WriteInt16(-1).WriteString(target.Name);
            else
            {
                _privateChatId = target.Id;
                _privateChatName = target.Name;
                reply.WriteInt16(1).WriteString(target.Name);
            }
            Send(reply);
        }
        else if (type == 2 && packet.RemainingToRead == 1)
        {
            var block = packet.ReadByte();
            if (block > 1) return;
            _blockPrivateChat = block == 1;
            UpdateChatPresence();
            TravelMessage(_blockPrivateChat ? "Private messages blocked." : "Private messages enabled.");
        }
    }
}
