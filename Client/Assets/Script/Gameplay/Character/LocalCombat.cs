using System.Collections.Generic;
using Game.Gameplay.Abilities;
using Game.System.Progression;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer;
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
        private const int BaseDamage = 10;      // 스킬 기본값. 던전 GameplayEffectCatalog "basic_attack_dmg"(Instant Health -10)과 정렬
        private const float QueryRadius = 3f;   // 광역 1차 수집 반경(정밀 판정은 HitboxMath)

        private PlayerCharacterAgent _agent;
        private SkillCatalogProvider _skills;   // skillId→hitbox 데이터 진실원(서버 CombatHandler 와 동일 skills.json)
        private PlayerProgressionHolder _progression; // Main 클라 스탯 캐시(AttackPower). 동적 부착이라 method 주입.
        private readonly HashSet<LocalMonster> _hitThisSwing = new();

        // 무기 콜라이더 기반 판정(있으면 이걸 우선). 없으면 아래 레거시 OverlapSphere 폴백.
        private WeaponHitbox _weapon;
        private int _swingDamage;      // 이번 스윙 데미지(OnAttackPerformed 에서 확정 → 활성 구간 히트에 적용)
        private string _swingSkillId;  // 로그용

        /// <summary>skillId(int 패킷) → 스킬 데이터 키. 서버 CombatHandler.ResolveSkill 과 동일 규약.</summary>
        private static string SkillName(int skillId) => skillId switch { 1 => "heavy_swing", _ => "basic_swing" };

        // CharacterSpawner.AttachLocalCombat 에서 AddComponent 후 _container.Inject 로 주입.
        [Inject]
        public void Construct(PlayerProgressionHolder progression, SkillCatalogProvider skills)
        {
            _progression = progression;
            _skills = skills;
        }

        private void Awake()
        {
            _agent = GetComponent<PlayerCharacterAgent>();
            _weapon = GetComponentInChildren<WeaponHitbox>(true); // 손 본에 부착된 무기 프롭
        }

        private void OnEnable()
        {
            if (_agent != null) _agent.OnAttackPerformed += PerformHit;
            if (_weapon != null) _weapon.OnHit += ApplyWeaponHit;
        }

        private void OnDisable()
        {
            if (_agent != null) _agent.OnAttackPerformed -= PerformHit;
            if (_weapon != null) _weapon.OnHit -= ApplyWeaponHit;
        }

        private void PerformHit(int skillId)
        {
            var skill = _skills?.Get(SkillName(skillId));
            if (skill == null) return; // 데이터 미로드 — 판정 스킵

            // 데미지 = 던전(서버 권위)과 동일 산식. 레벨업으로 AttackPower 오르면 다음 스윙부터 강해진다.
            // 홀더 미갱신(default) 시 AttackPower=0 → BaseDamage 그대로(하위호환).
            int damage = StatCombatMath.MeleeDamage(BaseDamage, _progression?.AttackPower ?? 0, 0);

            // 무기 콜라이더가 있으면: 입력 순간이 아니라 "휘두르는 활성 구간"에 무기가 닿을 때 판정.
            // 데미지만 이번 스윙용으로 확정해 두고, 실제 적중은 WeaponHitbox.OnHit → ApplyWeaponHit 에서 처리.
            if (_weapon != null)
            {
                _swingDamage = damage;
                _swingSkillId = skill.Id;
                return;
            }

            // 폴백(무기 미부착): 기존 입력순간 OverlapSphere + HitboxMath 즉발 판정.
            var hitbox = skill.Hitbox; // 스킬별 hitbox(basic=정면 박스 / heavy=넓은 박스)
            var pos = transform.position;
            float yaw = transform.eulerAngles.y;
            var attackerPos = new NVector3(pos.x, pos.y, pos.z);

            _hitThisSwing.Clear();
            int hitCount = 0;
            var cols = Physics.OverlapSphere(pos, QueryRadius);
            foreach (var col in cols)
            {
                var monster = col.GetComponentInParent<LocalMonster>();
                if (monster == null || monster.IsDead || !_hitThisSwing.Add(monster))
                    continue;

                var mp = monster.transform.position;
                var targetPos = new NVector3(mp.x, mp.y, mp.z);
                if (HitboxMath.Overlaps(attackerPos, yaw, hitbox, targetPos, monster.TargetRadius))
                {
                    monster.TakeDamage(damage);
                    hitCount++;
                }
            }

            // 스윙 결과 — 적중 수(0=헛스윙)로 "데미지가 들어갔는가"를 즉시 확인.
            Debug.Log($"[Combat] '{skill.Id}' 스윙 → {hitCount}마리 적중 (dmg {damage})");
        }

        /// <summary>무기 콜라이더가 활성 구간에 LocalMonster 에 닿으면 호출(스윙당 대상 1회). 확정된 스윙 데미지 적용.</summary>
        private void ApplyWeaponHit(LocalMonster monster)
        {
            if (monster == null || monster.IsDead) return;
            monster.TakeDamage(_swingDamage);
            Debug.Log($"[Combat] '{_swingSkillId}' 무기 적중 → {monster.MonsterId} (dmg {_swingDamage})");
        }
    }
}
