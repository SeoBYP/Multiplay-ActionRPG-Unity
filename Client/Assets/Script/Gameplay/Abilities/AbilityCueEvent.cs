using System;
using Game.Gameplay.Character;
using UnityEngine;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 어빌리티 연출 이벤트의 종류. <b>연출 전용</b> — 서버는 이 값을 절대 보지 않는다(gas-architecture §2.5).
    /// </summary>
    public enum ECueKind
    {
        /// <summary>효과음 1발(CueCatalog 의 AudioClip). </summary>
        Sfx = 0,
        /// <summary>파티클/이펙트 프리팹 스폰(CueCatalog 의 프리팹, socket 에 부착).</summary>
        Vfx = 1,
        /// <summary>추가 애니 트리거(t=0 주 트리거는 cueTrigger 가 담당 — 이건 지연 발화용 예약).</summary>
        Anim = 2,
        /// <summary>액터(자신)의 컴포넌트 메서드를 시각 T 에 호출(P7, 참조 R3~R6 의 우리 판 — 대상=self, 클라 전용).</summary>
        Event = 3,
    }

    /// <summary>Event 이벤트가 호출할 메서드의 인자 타입(P7). 대상=self 라 참조 8종 대신 실용 스칼라/문자열만(YAGNI).</summary>
    public enum EInvokeArgType
    {
        None = 0, Float = 1, Int = 2, Bool = 3, String = 4,
    }

    /// <summary>
    /// 어빌리티 타임라인 위의 <b>연출 이벤트 한 점</b> — 발동(t=0) 기준 <see cref="timeMs"/> 오프셋에 재생된다.
    ///
    /// <b>클라 로컬 재생, 네트워크 0</b>. `AbilityDefinition.cueEvents` 로만 저작되고 <b>bake 되지 않는다</b>
    /// (서버는 hitbox·데미지만 abilities.json 으로 읽는다 — "서버는 Cue 를 모른다" 보존).
    ///
    /// 시계 = 발동 순간부터의 ms. 던전(서버 발동 신호)·Main(로컬 발동) 어디서 발동하든 같은 오프셋으로 재생된다.
    /// UnityEngine 비의존(순수) — 플래너/테스트가 엔진 없이 검증한다.
    /// </summary>
    [Serializable]
    public sealed class AbilityCueEvent
    {
        /// <summary>발동(t=0) 기준 오프셋(ms). 음수는 플래너가 0 으로 클램프.</summary>
        public float timeMs;

        /// <summary>이벤트 지속 길이(ms). 0 = 즉발(점). VFX 는 이 길이만큼 살아있다 파괴(0 이면 카탈로그 autoDestroySec).
        /// SFX/Anim 은 즉발이라 길이는 편집·표시용(타임라인 클립 폭). 음수는 플래너가 0 으로 클램프.</summary>
        public float durationMs;

        /// <summary>같은 종류(kind) 안에서 어느 <b>레인(행)</b>에 놓일지(W-B). 0=첫 레인. <b>편집 전용</b> — 런타임 재생은 무시.</summary>
        public int lane;

        /// <summary>재생 종류(SFX/VFX/추가 Anim).</summary>
        public ECueKind kind;

        /// <summary>SFX 클립을 <b>직접</b> 지정(카탈로그 없이 드래그). 지정하면 <see cref="id"/> 보다 우선.</summary>
        public AudioClip sfxClip;

        /// <summary>VFX 프리팹을 <b>직접</b> 지정(카탈로그 없이 드래그). 지정하면 <see cref="id"/> 보다 우선.</summary>
        public GameObject vfxPrefab;

        /// <summary>(선택) CueCatalog 키 — 직접 리소스(<see cref="sfxClip"/>/<see cref="vfxPrefab"/>)가 없을 때만 폴백 조회.
        /// 직접 지정이 기본이고, 여러 어빌리티가 리소스를 공유할 때만 id+카탈로그를 쓴다.</summary>
        public string id;

        /// <summary>VFX 부착 소켓(자식 Transform 이름, 예: hand_r). 빈 문자열=루트. SFX 는 무시.</summary>
        public string socket;

        /// <summary>Anim 종류일 때 발동할 애니 트리거(주 cueTrigger 는 t=0, 이건 지연 발화 — W7). 액터 CharacterAgentAnimations 로 전달.</summary>
        public AnimationTriggerType animTrigger;

        /// <summary>Event 종류일 때 호출할 <b>액터 컴포넌트의 public 메서드 이름</b>(예: ActivateWindow). P7.</summary>
        public string invokeMethod;

        /// <summary>Event 메서드에 넘길 인자 타입(None=0-인자). 참조 R5 의 우리 판.</summary>
        public EInvokeArgType argType;
        public float argFloat;
        public int argInt;
        public bool argBool;
        public string argString;
    }
}
