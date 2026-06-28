using Cysharp.Threading.Tasks;
using Game.Gameplay.Character.Input;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Co-op 부활 시전자(던전 로컬 플레이어 전용). CharacterSpawner 가 Joined 시 동적 AddComponent.
    ///
    /// 조건 = <b>사거리(<see cref="ReviveConfig.RangeMeters"/>) 안 다운 아군 + Interact(E)</b> → C_Revive 송신.
    /// 서버가 거리·상태를 재검증(권위) → S_PlayerRevived 브로드캐스트. 입력은 같은 GO 컴포넌트
    /// (`CharacterInputBuffer`, <see cref="ICharacterInputSource"/>)에서 GetComponent — 에이전트와 동일(DI 아님).
    /// (홀드 채널은 우선 제거 — 범위+E 즉시. 추후 시전바 폴리시로 재도입 가능.)
    /// </summary>
    [DefaultExecutionOrder(-10)] // PlayerCharacterAgent(0)보다 먼저 Interact 소비(다운 아군 근처면 부활 우선)
    public sealed class ReviveInteractor : MonoBehaviour
    {
        private static readonly GameplayTag DeadTag = GameplayTags.Dead;

        private ISocketSession _session;
        private ICharacterInputSource _input;
        private AbilitySystemComponent _asc;

        private DownedAllyMarker _lastReported;
        private bool _everReported;

        private void Awake()
        {
            _asc = GetComponent<AbilitySystemComponent>();
            // 입력은 DI 가 아니라 같은 GO 의 컴포넌트(CharacterInputBuffer) — CharacterAgent 와 동일 방식.
            _input = GetComponent<ICharacterInputSource>();
            Debug.Log($"[ReviveInteractor] 활성화 — 입력={(_input != null ? "OK" : "없음")} · 다운 아군 사거리({ReviveConfig.RangeMeters}m)에서 E로 부활.");
        }

        /// <summary>CharacterSpawner 가 부착 직후 호출 — 송신용 세션 주입(ISocketSession 은 DI 싱글톤).</summary>
        public void Configure(ISocketSession session) => _session = session;

        private void Update()
        {
            if (_input == null) return;

            // 자기 다운 중이면 부활 불가.
            if (_asc != null && _asc.HasTag(DeadTag)) { ReportAvailability(null); return; }

            var nearest = DownedAllyMarker.FindNearest(transform.position, ReviveConfig.RangeMeters);
            ReportAvailability(nearest); // "지금 E 누르면 되는지" 가시화(상태 전이 시).

            // 사거리 안 다운 아군 + E → 즉시 부활 시도. (없으면 Consume 안 함 → 일반 상호작용은 Agent 가 처리)
            if (nearest != null && _input.ConsumeInteractPressed())
                SendRevive(nearest.UserId);
        }

        /// <summary>다운 아군 사거리 진입/이탈을 <b>상태 전이 시에만</b> 로그(첫 평가 1회 포함).</summary>
        private void ReportAvailability(DownedAllyMarker nearest)
        {
            if (_everReported && nearest == _lastReported) return;
            _everReported = true;
            if (nearest != null)
                Debug.Log($"[ReviveInteractor] 부활 가능 ✔ — 다운 아군(UserId={nearest.UserId}) 사거리 안. E를 눌러 부활.");
            else
                Debug.Log("[ReviveInteractor] 부활 불가 ✘ — 사거리 안 다운 아군 없음.");
            _lastReported = nearest;
        }

        private void SendRevive(long targetUserId)
        {
            if (_session == null || _session.State != SocketSessionState.Joined)
            {
                Debug.LogWarning("[ReviveInteractor] 부활 송신 실패 — 세션 미연결.");
                return;
            }
            _session.SendAsync(new C_Revive { TargetUserId = targetUserId }, destroyCancellationToken).Forget();
            Debug.Log($"[ReviveInteractor] 부활 시도 → C_Revive 송신(target={targetUserId}).");
        }
    }
}
