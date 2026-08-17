using System.Collections;
using Game.Gameplay.Abilities;
using Game.Gameplay.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// CA-5 Phase 1b 스모크 — 툴로 저작한 Cue 이벤트가 <b>실제 리소스를 물고 소리까지 나는지</b> 한 번 관통시킨다.
    ///
    /// <para>Phase 1a(코드)는 끝났지만 파이프는 <b>두 곳이 비어 조용히 죽어 있었다</b>:
    /// ① 이벤트의 <c>sfxClip</c> 이 전부 <c>None</c> ② <c>PlayerCharacter.prefab</c> 에 <see cref="AbilityCuePlayer"/> 미부착.
    /// 둘 다 "없으면 아무것도 안 한다"가 정상 동작이라 로그도 에러도 안 난다 — 그래서 테스트로 고정한다.</para>
    /// </summary>
    public class AbilityCuePlayerSmokeTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
        }

        [UnityTest]
        public IEnumerator 플레이어_프리팹에_CuePlayer가_붙어있다()
        {
            // 미부착이면 PlayerCharacterAgent 의 `_cuePlayer?.Play(...)` 가 null 조건부로 통째로 스킵된다.
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/PlayerCharacter.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "PlayerCharacter 프리팹 로드 실패(에디터 외 실행)");

            Assert.IsNotNull(prefab.GetComponent<AbilityCuePlayer>(),
                "PlayerCharacter 프리팹에 AbilityCuePlayer 가 없으면 어빌리티 SFX/VFX 가 조용히 무시된다");
            yield break;
        }

        [UnityTest]
        public IEnumerator 저작된_SFX_이벤트가_시각에_맞춰_실제로_재생된다()
        {
            AbilityCatalogDefinition catalog = null;
#if UNITY_EDITOR
            catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityCatalogDefinition>(
                "Assets/GameData/Ability/AbilityCatalogDefinition.asset");
#endif
            Assume.That(catalog, Is.Not.Null, "AbilityCatalogDefinition 로드 실패(에디터 외 실행)");

            var ability = new AbilityCatalogProvider(catalog).Get("basic_swing");
            Assert.IsNotNull(ability, "basic_swing 어빌리티가 카탈로그에 없다");

            // 저작 상태 고정: SFX 이벤트가 실제 클립을 물고 있어야 한다(비어 있으면 Fire 가 조용히 지나간다).
            AbilityCueEvent sfx = null;
            foreach (var ev in ability.cueEvents)
                if (ev != null && ev.kind == ECueKind.Sfx && ev.sfxClip != null) { sfx = ev; break; }

            Assert.IsNotNull(sfx,
                "basic_swing 에 클립이 할당된 Sfx Cue 이벤트가 없다 — 타임라인 툴에서 이벤트만 찍고 리소스를 안 물리면 무음이다");

            // 재생 검증은 컴포넌트 단독으로 한다. 플레이어 프리팹 전체를 DI 없이 Instantiate 하면
            // CharacterAgent.Start() 가 주입 의존을 못 찾아 NRE 를 던진다 — Cue 파이프와 무관한 하네스 잡음이다.
            // (프리팹에 컴포넌트가 붙어 있는지는 위 테스트가 따로 고정한다.)
            _instance = new GameObject("cue-player-smoke");
            var player = _instance.AddComponent<AbilityCuePlayer>();

            player.Play(ability);

            // 이벤트 시각(ms)까지 기다린 뒤 소리가 났는지 본다. AudioSource 는 Awake 에서 자동 생성된다.
            var audio = _instance.GetComponent<AudioSource>();
            Assert.IsNotNull(audio, "AbilityCuePlayer 가 AudioSource 를 확보해야 한다");

            float waitSec = sfx.timeMs / 1000f + 0.5f;
            float t = 0f;
            bool played = false;
            while (t < waitSec && !played)
            {
                yield return null;
                t += Time.deltaTime;
                if (audio.isPlaying) played = true;
            }

            Assert.IsTrue(played,
                $"저작된 SFX(t={sfx.timeMs}ms)가 재생되지 않았다 — 클립 할당 또는 AbilityCuePlayer 재생 경로 확인");
        }
    }
}
