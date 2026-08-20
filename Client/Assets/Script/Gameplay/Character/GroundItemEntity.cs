using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 월드 바닥 아이템 표시 + 줍기 의도 송신(서버 권위). 몬스터 사망 드랍으로 서버가 스폰을 통지하면
    /// GroundItemSpawner가 이 컴포넌트를 가진 프리팹을 인스턴스화한다.
    ///
    /// IInteractable 이라 InteractionDetector가 근처에서 최근접으로 선택 → 로컬 드라이버가
    /// ConsumeInteractPressed(E키) 폴링 시 Interact 호출 → C_PickupItem 송신. "먹을지"는 요청일 뿐,
    /// 줍기 확정(경쟁 중재)·바닥 제거는 서버 권위(S_GroundItemRemoved 수신 시 스포너가 디스폰).
    /// 프리팹에는 InteractionDetector 감지 레이어의 Collider 가 있어야 한다(Unity 저작).
    /// </summary>
    public class GroundItemEntity : MonoBehaviour, IInteractable
    {
        /// <summary>드랍 위치를 가슴 높이로 띄우는 오프셋. 바닥(y≈0)에 두면 플레이어의
        /// InteractionDetector 감지 구체(가슴 높이 ~y1)에 닿지 않아 E 줍기가 안 된다.</summary>
        private const float SpawnYOffset = 0.7f;

        public int GroundId { get; private set; }
        public int ItemId { get; private set; }
        public int Qty { get; private set; }

        private ISocketSession _session;

        public void Initialize(SocketGroundItemSnapshot snapshot, ISocketSession session)
        {
            GroundId = snapshot.GroundId;
            ItemId   = snapshot.ItemId;
            Qty      = snapshot.Qty;
            _session = session;
            transform.position = new Vector3(snapshot.PosX, snapshot.PosY + SpawnYOffset, snapshot.PosZ);
        }

        public string InteractionPrompt => "줍기";

        public void Interact(GameObject interactor)
        {
            if (_session == null || _session.State != SocketSessionState.Joined)
                return;

            // 줍기 의도만 송신. 로컬에서 바닥을 지우지 않는다(서버 확정 후 S_GroundItemRemoved 로 디스폰).
            _session.SendAsync(new C_PickupItem { GroundId = GroundId }, destroyCancellationToken).Forget();
        }
    }
}
