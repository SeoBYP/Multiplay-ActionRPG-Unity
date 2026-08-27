using Game.System.Progression;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 로컬 플레이어 ASC 의 Health 를 서버 권위 레벨 스탯(PlayerProgressionHolder.Stats.MaxHealth)으로 정렬한다.
    /// 클라 prefab HP(100)와 서버 레벨 MaxHealth(120/140…) 불일치로 로컬 사망 예측이 서버보다 먼저 떨어지던
    /// 문제(=다운 후에도 몬스터가 계속 때림)를 해소한다. Main·던전 공통(로컬 캐릭터 스폰 시 부착).
    ///
    /// 스폰 시 풀피로 설정. 레벨업으로 MaxHealth 가 늘면 새 Max + 풀회복. holder 는 킬마다 OnChanged 를
    /// 쏘므로(레벨 변화 없어도) MaxHealth 가 실제로 바뀐 경우만 반영(킬마다 풀힐 방지).
    /// </summary>
    public sealed class PlayerStatApplier : MonoBehaviour
    {
        private PlayerProgressionHolder _progression;
        private GasComponent _asc;
        private int _appliedMaxHealth;

        private void Awake() => _asc = GetComponent<GasComponent>();

        /// <summary>
        /// 진행/스탯 홀더 연결(로컬 스폰 시 CharacterSpawner 가 호출). 즉시 1회 적용 + 레벨업(OnChanged) 구독.
        /// holder 가 없으면(스코프 미등록 — 일부 테스트 하네스) 호출되지 않아 prefab 기준선이 유지된다.
        /// </summary>
        public void Bind(PlayerProgressionHolder holder)
        {
            if (_progression != null) _progression.OnChanged -= Apply;
            _progression = holder;
            if (_progression != null) _progression.OnChanged += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            if (_progression != null) _progression.OnChanged -= Apply;
        }

        /// <summary>holder 의 현재 MaxHealth 를 ASC 에 반영(런타임 진입점).</summary>
        public void Apply()
        {
            if (_progression != null)
                ApplyMaxHealth(_progression.Current.Stats.MaxHealth);
        }

        /// <summary>
        /// 서버 권위 MaxHealth 를 ASC Health 에 적용(테스트 직접 호출 가능).
        /// 0(미갱신)·직전 적용값과 동일하면 스킵. 적용 시 Max 정렬 + 풀충전.
        /// </summary>
        public void ApplyMaxHealth(int maxHealth)
        {
            if (_asc == null) _asc = GetComponent<GasComponent>();
            if (_asc == null || maxHealth <= 0 || maxHealth == _appliedMaxHealth) return;
            if (!_asc.Has(EGameplayAttribute.Health)) return;

            _asc.SetMax(EGameplayAttribute.Health, maxHealth);
            _asc.SetCurrent(EGameplayAttribute.Health, maxHealth); // 스폰=풀피 / 레벨업=풀회복(흔한 RPG 처리)
            _appliedMaxHealth = maxHealth;
        }
    }
}
