using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using VContainer.Unity;

namespace Game.Tests.EditMode.VContainerWiring
{
    /// <summary>
    /// 루트 스코프는 <see cref="VContainerSettings.RootLifetimeScope"/> 가 자동 생성한다. 씬에 같은 스코프를 또 두면
    /// 그 인스턴스는 루트가 아니라 <b>루트의 자식</b>이 되어 모든 인스톨러를 한 번 더 등록한다.
    /// VContainer 는 <c>IEnumerable&lt;T&gt;</c> 를 부모 스코프 등록까지 모아 주입하므로
    /// 2026-09-30 Title 씬에서 <c>Duplicate packet handler: S_PlayerJoined</c> 로 터졌다(싱글톤·엔트리포인트도 전부 이중 생성).
    /// </summary>
    public class RootScopeSceneTests
    {
        [Test]
        public void 빌드_씬에는_루트_LifetimeScope를_두지_않는다()
        {
            var settings = AssetDatabase.LoadAssetAtPath<VContainerSettings>("Assets/Resources/VContainerSettings.asset");
            Assert.That(settings != null && settings.RootLifetimeScope != null, "VContainerSettings.RootLifetimeScope 가 비어 있다");

            var prefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(settings.RootLifetimeScope));
            var scriptGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(settings.RootLifetimeScope)));

            var offenders = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .Where(path =>
                {
                    var yaml = File.ReadAllText(path);
                    return yaml.Contains(prefabGuid) || yaml.Contains(scriptGuid);
                })
                .ToList();

            Assert.That(offenders, Is.Empty, "루트 스코프가 씬에 배치돼 있다 — 자식 스코프로 이중 생성된다");
        }
    }
}
