using System;
using GameServer.Grpc.Chat;

namespace Game.Presentation.Chat
{
    /// <summary>채팅 한 줄이 속한 채널. 채널 결정은 서버 권위 — 클라는 받은 값을 표시만 한다.</summary>
    public enum ChatChannel
    {
        Global,
        Room,
        Whisper,
        System, // 서버 공지 + 클라 로컬 안내(연결 상태 등)
    }

    /// <summary>
    /// View 가 보는 채팅 한 줄. proto 타입(<see cref="ChatMessageInfo"/>)은 여기서 흡수한다 —
    /// GUI 레이어에 <c>GameServer.Grpc.*</c> 를 노출하지 않기 위한 경계(unity-client.md).
    /// </summary>
    public readonly struct ChatLine
    {
        public readonly long MessageId;
        public readonly ChatChannel Channel;
        public readonly string Sender;
        public readonly string Text;
        public readonly string TargetNickname; // 귓속말 수신자(그 외 빈 문자열)
        public readonly long SentAtUnix;

        public ChatLine(long messageId, ChatChannel channel, string sender, string text, string targetNickname, long sentAtUnix)
        {
            MessageId      = messageId;
            Channel        = channel;
            Sender         = sender ?? string.Empty;
            Text           = text ?? string.Empty;
            TargetNickname = targetNickname ?? string.Empty;
            SentAtUnix     = sentAtUnix;
        }

        public static ChatLine FromChat(ChatMessageInfo info) => new ChatLine(
            info.MessageId,
            ToChannel(info.ChatType),
            info.SenderNickname,
            info.Message,
            info.TargetUserNickname,
            info.SentAt);

        public static ChatLine FromNotice(SystemNotice notice) => new ChatLine(
            0, ChatChannel.System, string.Empty, notice.Message, string.Empty, notice.SentAt);

        /// <summary>클라가 스스로 만드는 안내 줄(연결 실패 등). 서버 기록에는 남지 않는다.</summary>
        public static ChatLine LocalNotice(string text) => new ChatLine(
            0, ChatChannel.System, string.Empty, text, string.Empty,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        private static ChatChannel ToChannel(ChatType type) => type switch
        {
            ChatType.Room    => ChatChannel.Room,
            ChatType.Whisper => ChatChannel.Whisper,
            _                => ChatChannel.Global,
        };
    }
}
