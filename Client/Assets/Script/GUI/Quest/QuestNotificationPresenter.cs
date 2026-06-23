using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI.Common;
using Game.Presentation.Quest;
using R3;
using VContainer.Unity;

namespace Game.GUI.Quest
{
    /// <summary>
    /// 퀘스트 알림(수락/완료/보상) → AlertPopup 표시. QuestNotifier.OnNotice 구독.
    /// 보상받기 버튼 폐지 후 유일한 퀘스트 피드백 경로(LobbyViewController 의 AlertPopup 로드 패턴 재사용).
    /// GUI 는 QuestNotifier(Presentation)만 알고, 알림 종류→glow 매핑은 여기(GUI)에서.
    /// </summary>
    public sealed class QuestNotificationPresenter : IInitializable, IDisposable
    {
        private readonly QuestNotifier _notifier;
        private readonly CancellationTokenSource _cts = new();
        private IDisposable _sub;

        public QuestNotificationPresenter(QuestNotifier notifier) => _notifier = notifier;

        public void Initialize() => _sub = _notifier.OnNotice.Subscribe(ShowPopup);

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _sub?.Dispose();
        }

        private void ShowPopup(QuestNotice notice) => ShowAsync(notice).Forget();

        private async UniTaskVoid ShowAsync(QuestNotice notice)
        {
            if (GUIRoot.Instance == null) return;

            var inst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.AlertPopup, GUIRoot.Instance.transform, _cts.Token);
            if (inst == null) return;

            var popup = inst.GameObject.GetComponent<AlertPopup>();
            if (popup == null) { inst.Dispose(); return; }

            popup.SetAddressableOwner(inst);
            popup.Setup(notice.Title, notice.Message, glow: GlowOf(notice.Kind));
        }

        private static PopupGlowType GlowOf(QuestNoticeKind kind) => kind switch
        {
            QuestNoticeKind.Accepted => PopupGlowType.Info,
            QuestNoticeKind.Completed => PopupGlowType.Warning,
            QuestNoticeKind.Claimed => PopupGlowType.Success,
            _ => PopupGlowType.Info,
        };
    }
}
