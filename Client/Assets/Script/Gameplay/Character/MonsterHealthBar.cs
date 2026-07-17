using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 몬스터 머리 위 월드공간 체력바(연출 전용). 부모의 <see cref="IMonsterHealth"/> 를 구독해
    /// fill(Image, Filled Horizontal)의 fillAmount 를 Hp/MaxHp 로 갱신하고, 매 프레임 카메라를 향해 빌보드한다.
    ///
    /// <para><b>던전·Main 공용</b> — 계약(<see cref="IMonsterHealth"/>)만 보므로 HP 권위가 서버(던전 MonsterEntity)든
    /// 클라(Main LocalMonster)든 같은 컴포넌트를 쓴다. 이 컴포넌트는 표시만 하며 판정에 관여하지 않는다.</para>
    /// </summary>
    public sealed class MonsterHealthBar : MonoBehaviour
    {
        [SerializeField] private Image fill;

        private IMonsterHealth _monster;
        private Transform _cam;

        private void Awake()
        {
            _monster = GetComponentInParent<IMonsterHealth>();
        }

        private void OnEnable()
        {
            if (_monster != null) _monster.HpChanged += Refresh;
        }

        private void OnDisable()
        {
            if (_monster != null) _monster.HpChanged -= Refresh;
        }

        private void Start()
        {
            if (_monster != null) Refresh(_monster); // 구독 전 seed 된 초기 HP 반영
        }

        private void Refresh(IMonsterHealth monster)
        {
            if (fill == null || monster.MaxHp <= 0) return;
            fill.fillAmount = Mathf.Clamp01((float)monster.Hp / monster.MaxHp);
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                var mainCam = UnityEngine.Camera.main;
                if (mainCam == null) return;
                _cam = mainCam.transform;
            }
            // 빌보드 — 카메라를 향하도록 회전(부모 몬스터가 회전해도 항상 정면).
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
        }
    }
}
