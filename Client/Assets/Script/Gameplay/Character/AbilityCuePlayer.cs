using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Abilities;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 액터(플레이어·원격·몬스터)에 부착. 어빌리티 발동 시 그 <see cref="AbilityDefinition.cueEvents"/> 를
    /// 발동(t=0) 기준 ms 오프셋에 맞춰 <b>로컬 재생</b>한다(SFX 원샷·VFX 소켓 스폰). AC-D3 · CA-5 Phase 1.
    ///
    /// <b>연출 전용 — 네트워크 0.</b> 발동 신호(던전=S_AbilityActivated / Main=로컬 발동)를 받은 호출자가
    /// 이미 손에 쥔 <see cref="AbilityDefinition"/> 를 넘겨 <see cref="Play"/> 만 부른다(뷰는 카탈로그를 모른다 — 호출자가 해석).
    ///
    /// 시계 = 스케일드 타임(Animator 와 동일) → HitStop 등 timeScale 변화에 연출이 함께 느려진다.
    /// 주 애니(t=0)는 기존 cueTrigger 경로(IActorView.PlayAbilityCue)가 담당 — 이 컴포넌트는 그 위에 얹는 소리·이펙트만.
    /// </summary>
    public sealed class AbilityCuePlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("연출 리소스 카탈로그(Cue id → AudioClip/VFX 프리팹). 던전·Main 공용 단일 에셋을 지정.")]
        private CueCatalog _catalog;

        private AudioSource _audio;
        private readonly Dictionary<string, Transform> _socketCache = new();

        /// <summary>DI/테스트용 런타임 주입(프리팹 SerializeField 대신 코드로 지정).</summary>
        public void SetCatalog(CueCatalog catalog) => _catalog = catalog;

        private CharacterAgentAnimations _anim;

        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1f; // 3D 위치 기반
            }
            _anim = GetComponent<CharacterAgentAnimations>(); // W7: 지연 Anim 트리거 발화용
        }

        /// <summary>어빌리티 연출 재생. cueEvents 가 없거나 카탈로그 미지정이면 조용히 아무것도 안 한다.</summary>
        public void Play(AbilityDefinition ability)
        {
            // 카탈로그는 이제 선택 — 이벤트가 직접 클립/프리팹을 들고 있을 수 있다(카탈로그는 id 폴백용).
            if (ability == null) return;
            var plan = AbilityCuePlan.Build(ability.cueEvents);
            if (plan.Length == 0) return;
            RunAsync(plan).Forget();
        }

        private async UniTaskVoid RunAsync(AbilityCueEvent[] plan)
        {
            var ct = this.GetCancellationTokenOnDestroy();
            float elapsedMs = 0f;
            foreach (var ev in plan)
            {
                int waitMs = Mathf.RoundToInt(ev.timeMs - elapsedMs);
                if (waitMs > 0)
                {
                    await UniTask.Delay(waitMs, cancellationToken: ct);
                    elapsedMs = ev.timeMs;
                }
                if (ct.IsCancellationRequested) return;
                Fire(ev);
            }
        }

        private void Fire(AbilityCueEvent ev)
        {
            switch (ev.kind)
            {
                case ECueKind.Sfx:
                {
                    // 직접 클립 우선 → 없으면 카탈로그 id 폴백.
                    if (ev.sfxClip != null) _audio.PlayOneShot(ev.sfxClip);
                    else if (_catalog != null && _catalog.TryGetSfx(ev.id, out var s)) _audio.PlayOneShot(s.clip, s.volume);
                    break;
                }
                case ECueKind.Vfx:
                {
                    GameObject prefab = ev.vfxPrefab;
                    float autoLife = 0f;
                    if (prefab == null && _catalog != null && _catalog.TryGetVfx(ev.id, out var v)) { prefab = v.prefab; autoLife = v.autoDestroySec; }
                    if (prefab != null)
                    {
                        var at = ResolveSocket(ev.socket);
                        var go = Instantiate(prefab, at.position, at.rotation, at);
                        // 지속 길이(durationMs) 우선 → 없으면 카탈로그 autoDestroySec 폴백.
                        float life = ev.durationMs > 0f ? ev.durationMs / 1000f : autoLife;
                        if (life > 0f) Destroy(go, life);
                    }
                    break;
                }
                case ECueKind.Anim:
                    // W7: 지연 애니 트리거를 액터 Animator 로 발화(주 애니 t=0 는 cueTrigger 경로).
                    _anim?.SetTrigger(ev.animTrigger);
                    break;

                case ECueKind.Event:
                    // P7: 액터(자신)의 컴포넌트 중 그 이름·시그니처의 public 메서드를 호출(예: WeaponHitbox.ActivateWindow).
                    if (!string.IsNullOrEmpty(ev.invokeMethod)) InvokeOnActor(ev);
                    break;
            }
        }

        /// <summary>액터의 컴포넌트 중 그 이름·시그니처의 public 메서드를 찾아 인자와 함께 호출(P7). 못 찾으면 조용히 무시.</summary>
        private void InvokeOnActor(AbilityCueEvent ev)
        {
            global::System.Type[] sig;
            object[] args;
            switch (ev.argType)
            {
                case EInvokeArgType.Float:  sig = new[] { typeof(float) };  args = new object[] { ev.argFloat };  break;
                case EInvokeArgType.Int:    sig = new[] { typeof(int) };    args = new object[] { ev.argInt };    break;
                case EInvokeArgType.Bool:   sig = new[] { typeof(bool) };   args = new object[] { ev.argBool };   break;
                case EInvokeArgType.String: sig = new[] { typeof(string) }; args = new object[] { ev.argString }; break;
                default:                    sig = global::System.Type.EmptyTypes; args = null;                   break;
            }

            foreach (var c in GetComponents<Component>())
            {
                if (c == null) continue;
                var m = c.GetType().GetMethod(ev.invokeMethod,
                    global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public,
                    null, sig, null);
                if (m != null) { m.Invoke(c, args); return; }
            }
        }

        /// <summary>소켓 이름으로 자식 Transform 조회(캐시). 빈 이름/미발견이면 루트.</summary>
        private Transform ResolveSocket(string socket)
        {
            if (string.IsNullOrEmpty(socket)) return transform;
            if (_socketCache.TryGetValue(socket, out var cached)) return cached != null ? cached : transform;

            Transform found = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == socket) { found = t; break; }

            _socketCache[socket] = found;
            return found != null ? found : transform;
        }
    }
}
