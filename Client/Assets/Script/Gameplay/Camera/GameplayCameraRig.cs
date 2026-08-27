using Game.Gameplay.Character;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Camera
{
    /// <summary>
    /// 게임플레이 3인칭 Cinemachine 카메라의 Follow 를 <b>런타임 스폰되는 로컬 플레이어</b>에 연결한다.
    ///
    /// 플레이어는 씬에 미리 없고 <see cref="CharacterSpawner"/> 가 동적으로 스폰하므로, vcam 의 Follow 가
    /// NULL 로 남아 3인칭이 동작하지 않았다. 이 컴포넌트는 <see cref="LocalPlayerContext.OnSet"/> 랑데부를
    /// 구독해 스폰 직후 Follow(+LookAt)를 플레이어의 CameraFollowTarget(마우스 Look 회전 피벗)으로 세팅한다.
    ///
    /// 씬 컴포넌트 + 런타임 Follow 세팅 패턴은 <see cref="DialogueCameraController"/> 와 동일.
    /// Main/Dungeon 두 씬 모두 동일 vcam 구성을 쓰므로 양 LifetimeScope 에서 RegisterComponentInHierarchy 한다.
    /// </summary>
    public sealed class GameplayCameraRig : MonoBehaviour
    {
        [Tooltip("게임플레이 3인칭 카메라(ThirdPersonFollow). Follow 는 로컬 플레이어 스폰 시 자동 연결된다.")]
        [SerializeField] private CinemachineCamera gameplayCamera;

        private LocalPlayerContext _localPlayer;

        [Inject]
        public void Construct(LocalPlayerContext localPlayer)
        {
            _localPlayer = localPlayer;
        }

        private void Start()
        {
            if (_localPlayer == null)
            {
                Debug.LogWarning("[GameplayCameraRig] LocalPlayerContext 미주입 — Follow 자동 연결 비활성.");
                return;
            }

            // 이미 스폰됐으면 즉시, 아직이면 OnSet 대기(스폰 타이밍 레이스 양쪽 가드).
            if (_localPlayer.AbilitySystem != null)
                Bind(_localPlayer.AbilitySystem);
            _localPlayer.OnSet += Bind;
        }

        private void OnDestroy()
        {
            if (_localPlayer != null)
                _localPlayer.OnSet -= Bind;
        }

        /// <summary>로컬 플레이어 ASC 준비 시 vcam Follow/LookAt 을 CameraFollowTarget 으로 연결.</summary>
        private void Bind(GasComponent abilitySystem)
        {
            if (gameplayCamera == null)
            {
                Debug.LogWarning("[GameplayCameraRig] gameplayCamera 미할당 — Inspector 에서 3인칭 vcam 을 연결하세요.");
                return;
            }
            if (abilitySystem == null) return;

            // CharacterCameraFollow 가 마우스 Look 으로 회전시키는 피벗(CameraFollowTarget)이 Follow 대상.
            // 없으면 플레이어 루트로 폴백(회전 추적은 빠지지만 추적 자체는 동작).
            var follow = abilitySystem.GetComponent<CharacterCameraFollow>();
            Transform target = follow != null && follow.CinemachineCameraTarget != null
                ? follow.CinemachineCameraTarget.transform
                : abilitySystem.transform;

            gameplayCamera.Follow = target;
            gameplayCamera.LookAt = target;
            Debug.Log($"[GameplayCameraRig] 3인칭 Follow 연결 — target={target.name}");
        }
    }
}
