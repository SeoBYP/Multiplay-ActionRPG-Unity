using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// 몬스터 발 슬라이딩 보정 배선(절충안 C) — 컨트롤러에 배속 파라미터를 심고 프리팹에 저작값을 넣는다.
    ///
    /// <b>왜 필요한가</b>(실측): 몬스터 보행 클립은 전부 제자리라 이동 속도와 무관하게 재생된다.
    /// 클립이 상정한 속도와 실제 이동 속도가 최대 <b>3.4배</b>까지 어긋나 발이 미끄러진다.
    ///
    /// <b>클립 속도는 왜 표에 박아두나</b>: 제자리 클립은 <c>averageSpeed</c> 가 0 이라 자동으로 알 수 없다.
    /// 발 본의 후방 이동 속도(접지 구간 중앙값)로 측정한 값을 상수로 저작한다 — 리그마다 본 이름이 달라
    /// 자동 측정을 런타임/도구에 넣으면 오측정이 그대로 데이터가 된다(리바이어던이 실제로 10m/s 로 잘못 나왔다).
    /// 값을 모르는 몬스터는 <b>0(무보정)</b> 으로 두는 것이 안전하다.
    /// </summary>
    public static class MonsterWalkSpeedSetup
    {
        public const string MulParameter = "MoveSpeedMul";

        /// <summary>프리팹 이름 → 보행 클립이 상정한 속도(m/s). 0 = 미측정 → 보정하지 않는다.</summary>
        private static readonly Dictionary<string, float> ClipSpeeds = new Dictionary<string, float>
        {
            { "Monster_creepy_demon",     0.65f },
            { "CreepyDemonLocal",         0.65f }, // Main 로컬 몬스터 — 같은 클립
            { "Monster_undead_axemaster", 0.77f },
            { "Monster_demon_girl",       1.25f },
            { "Monster_wild_centaur",     1.76f },
            { "Monster_arachnya",         3.19f }, // 유일하게 클립이 더 빠르다(발이 헛돎)
            // 미측정(보정 안 함): Monster_gargoyle(발 본 이름 매칭 실패)
            //                     Monster_leviathan(다리가 촉수형이라 측정 신뢰 낮음)
            //                     Monster_vampire_bat(보행 클립 없음 — 비행)
        };

        [MenuItem("Tools/Monster/발 슬라이딩 보정 배선")]
        public static void SetupMenu() => Debug.Log(SetupAll());

        /// <summary>모달 없는 진입점(CLI/CI 용).</summary>
        public static string SetupAll()
        {
            var sb = new StringBuilder();
            int wiredCtrl = 0, wiredPrefab = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponent<Game.Gameplay.Character.LocalMonster>() == null &&
                    prefab.GetComponent<Game.Gameplay.Character.MonsterEntity>() == null) continue;

                var animator = prefab.GetComponentInChildren<Animator>(true);
                var controller = animator?.runtimeAnimatorController as AnimatorController;
                if (controller == null) { sb.AppendLine("  " + prefab.name + " : 컨트롤러 없음 — 건너뜀"); continue; }

                if (WireController(controller, sb)) wiredCtrl++;
                if (WirePrefab(path, prefab.name, sb)) wiredPrefab++;
            }

            AssetDatabase.SaveAssets();
            return "[MonsterWalkSpeedSetup] 컨트롤러 " + wiredCtrl + "개 · 프리팹 " + wiredPrefab + "개 배선\n" + sb;
        }

        /// <summary>컨트롤러에 배속 파라미터 추가 + 보행 상태의 speedParameter 지정.</summary>
        private static bool WireController(AnimatorController controller, StringBuilder sb)
        {
            if (controller.parameters.All(p => p.name != MulParameter))
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = MulParameter,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1f, // 미구동 상태(다른 씬/툴)에서도 등속 재생
                });

            int states = 0;
            foreach (var layer in controller.layers)
                foreach (var st in layer.stateMachine.states)
                {
                    string n = st.state.name.ToLower();
                    if (!(n.Contains("walk") || n.Contains("run") || n.Contains("move"))) continue;
                    st.state.speedParameterActive = true;
                    st.state.speedParameter = MulParameter;
                    states++;
                }

            EditorUtility.SetDirty(controller);
            sb.AppendLine("  " + controller.name + " : 보행 상태 " + states + "개에 배속 연결");
            return states > 0;
        }

        /// <summary>프리팹에 파라미터명 + 클립 속도 저작값 주입.</summary>
        private static bool WirePrefab(string path, string prefabName, StringBuilder sb)
        {
            float clipSpeed = ClipSpeeds.TryGetValue(prefabName, out float v) ? v : 0f;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var anims = root.GetComponentInChildren<Game.Gameplay.Character.CharacterAgentAnimations>(true);
                if (anims == null) { sb.AppendLine("  " + prefabName + " : CharacterAgentAnimations 없음"); return false; }

                var so = new SerializedObject(anims);
                so.FindProperty("m_animationMoveSpeedMulFloat").stringValue = MulParameter;
                so.ApplyModifiedPropertiesWithoutUndo();

                foreach (var comp in root.GetComponents<MonoBehaviour>())
                {
                    var cso = new SerializedObject(comp);
                    var prop = cso.FindProperty("walkClipSpeed");
                    if (prop == null) continue;
                    prop.floatValue = clipSpeed;
                    cso.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                sb.AppendLine("  " + prefabName + " : walkClipSpeed=" + clipSpeed.ToString("0.00")
                              + (clipSpeed <= 0f ? " (미측정 → 무보정)" : ""));
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
