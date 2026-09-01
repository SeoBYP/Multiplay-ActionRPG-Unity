using Game.Presentation.Chat;
using TMPro;
using UnityEngine;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 채팅 로그 한 줄(말풍선). <see cref="ChatView"/> 가 템플릿을 복제해 쓴다.
    ///
    /// 발신자·본문 모두 <c>richText=false</c> 다 — 남이 친 <c>&lt;color&gt;</c>·<c>&lt;size&gt;</c> 가
    /// 내 화면 서식을 바꾸지 못하게 하는 가장 단순한 방법(이스케이프가 필요 없다).
    /// </summary>
    public sealed class ChatBubbleView : MonoBehaviour
    {
        // 채널 색 — 발신자 이름에만 입힌다(본문은 항상 읽기 쉬운 기본색).
        private static readonly Color GlobalColor  = new Color(0.91f, 0.93f, 0.94f);
        private static readonly Color RoomColor    = new Color(0.55f, 0.91f, 0.60f);
        private static readonly Color WhisperColor = new Color(0.97f, 0.51f, 0.67f);
        private static readonly Color SystemColor  = new Color(1.00f, 0.83f, 0.23f);

        [SerializeField] private TextMeshProUGUI sender;
        [SerializeField] private TextMeshProUGUI message;

        public void Bind(ChatLine line)
        {
            if (sender != null)
            {
                sender.richText = false;
                sender.color = ChannelColor(line.Channel);
                sender.text = SenderLabel(line);
            }

            if (message != null)
            {
                message.richText = false;
                message.text = line.Text;
            }

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>발신자 칸 표기 — 채널을 이름 앞에 붙여 어느 채널인지 색과 함께 이중으로 알린다.</summary>
        private static string SenderLabel(ChatLine line) => line.Channel switch
        {
            ChatChannel.Room    => $"[방] {line.Sender}:",
            ChatChannel.Whisper => $"[귓속말] {line.Sender}:",
            ChatChannel.System  => "[알림]",
            _                   => $"{line.Sender}:",
        };

        private static Color ChannelColor(ChatChannel channel) => channel switch
        {
            ChatChannel.Room    => RoomColor,
            ChatChannel.Whisper => WhisperColor,
            ChatChannel.System  => SystemColor,
            _                   => GlobalColor,
        };
    }
}
