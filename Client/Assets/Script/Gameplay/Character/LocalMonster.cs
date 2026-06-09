using System;
using Game.System.Player;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main(싱글) 전용 클라 권위 몬스터. 던전의 MonsterEntity(서버 권위·보간 전용)와 달리
    /// HP·간단 AI·사망 판정을 클라가 로컬로 소유한다(loot-drop.md §1.5). 싱글이라 동기화·경쟁이 없어 가능.
    ///
    /// - AI: LocalPlayerContext 의 플레이어가 chaseRange 안이면 추격, 밖이면 제자리(Idle). (간단 — Patrol/Attack 없음)
    /// - 전투: LocalCombat 가 HitboxMath 로 적중 판정 후 TakeDamage 호출 → HP≤0 → Die.
    /// - 사망: OnDied(this) 발행(MainMonsterSpawner 가 디스폰·드랍 처리) 후 자기 파괴.
    /// 콜라이더 필요(LocalCombat 의 Physics.OverlapSphere 가 찾는다). UnityEngine 의존이므로 서버와 무관.
    /// </summary>
    public sealed class LocalMonster : MonoBehaviour
    {
        [Header("식별")]
        [Tooltip("드랍 테이블/카탈로그 키(예: slime, goblin). DropTableDefinition 과 정렬.")]
        [SerializeField] private string monsterId = "slime";

        [Header("전투")]
        [SerializeField] private int maxHp = 30;
        [Tooltip("HitboxMath 적중 판정용 타겟 반경(구).")]
        [SerializeField] private float targetRadius = 0.5f;

        [Header("간단 AI")]
        [SerializeField] private float chaseRange = 6f;
        [SerializeField] private float moveSpeed = 2f;

        [Inject] private readonly LocalPlayerContext _localPlayer = null;

        private int _hp;

        /// <summary>사망 시 발행(자기 자신 전달). MainMonsterSpawner 가 디스폰·드랍(9d)에 사용.</summary>
        public event Action<LocalMonster> OnDied;

        public string MonsterId => monsterId;
        public float TargetRadius => targetRadius;
        public bool IsDead { get; private set; }

        private void Awake() => _hp = maxHp;

        private void Update()
        {
            if (IsDead) return;

            var target = _localPlayer?.AbilitySystem != null ? _localPlayer.AbilitySystem.transform : null;
            if (target == null) return;

            var to = target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > chaseRange || dist < 0.01f) return; // 범위 밖이거나 너무 가까우면 정지

            var dir = to / dist;
            transform.position += dir * (moveSpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>로컬 피격. HP 차감 후 0 이하면 사망 처리(1회).</summary>
        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            _hp -= amount;
            if (_hp > 0) return;

            IsDead = true;
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
