using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main(싱글) 전용 로컬 전투. 던전의 CombatSyncSender(C_Attack→서버 권위)와 달리,
    /// 클라가 직접 적중을 판정한다(loot-drop.md §1.5). 싱글이라 치팅 일관성 문제가 없어 클라 권위 수용.
    ///
    /// PlayerCharacterAgent.OnAttackPerformed → 근처 LocalMonster 를 Physics.OverlapSphere 로 모은 뒤,
    /// 서버와 동일한 HitboxMath.Overlaps(SkillCatalog "basic_swing")로 정밀 판정 → TakeDamage.
    /// 판정 로직(HitboxMath/SkillCatalog)은 Shared.Gameplay DLL 공유 — 던전(서버)과 같은 함수.
    /// CharacterSpawner 가 Main 브랜치에서 로컬 캐릭터에 동적 부착.
    /// </summary>
    public sealed class LocalCombat : MonoBehaviour
    {
        private const string SkillId = "basic_swing";
        private const int Damage = 10;          // GameplayEffectCatalog "basic_attack_dmg"(Instant Health -10)과 정렬
        private const float QueryRadius = 3f;   // 광역 1차 수집 반경(정밀 판정은 HitboxMath)

        private PlayerCharacterAgent _agent;
        private HitboxSpec _hitbox;
        private readonly HashSet<LocalMonster> _hitThisSwing = new();

        private void Awake()
        {
            _agent = GetComponent<PlayerCharacterAgent>();
            var skill = new SkillCatalog().Get(SkillId);
            if (skill != null) _hitbox = skill.Hitbox;
        }

        private void OnEnable()
        {
            if (_agent != null) _agent.OnAttackPerformed += PerformHit;
        }

        private void OnDisable()
        {
            if (_agent != null) _agent.OnAttackPerformed -= PerformHit;
        }

        private void PerformHit()
        {
            var pos = transform.position;
            float yaw = transform.eulerAngles.y;
            var attackerPos = new NVector3(pos.x, pos.y, pos.z);

            _hitThisSwing.Clear();
            var cols = Physics.OverlapSphere(pos, QueryRadius);
            foreach (var col in cols)
            {
                var monster = col.GetComponentInParent<LocalMonster>();
                if (monster == null || monster.IsDead || !_hitThisSwing.Add(monster))
                    continue;

                var mp = monster.transform.position;
                var targetPos = new NVector3(mp.x, mp.y, mp.z);
                if (HitboxMath.Overlaps(attackerPos, yaw, _hitbox, targetPos, monster.TargetRadius))
                    monster.TakeDamage(Damage);
            }
        }
    }
}
