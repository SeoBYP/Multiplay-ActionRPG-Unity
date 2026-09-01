using Game.Presentation.GameScene;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>
    /// 로딩·페이드 화면의 **정렬 순서**.
    ///
    /// 화면을 덮는 캔버스가 전부 `sortingOrder = 0` 이었다(GUIRoot 포함, 실측).
    /// Screen Space - Overlay 는 동률이면 **나중에 생성된 캔버스가 위**라, 로딩 중 새 씬이 활성화되며
    /// 만들어지는 HUD·창이 로딩 화면 위로 튀어나왔다. 그래서 로딩·페이더는 항상 게임 UI 위여야 한다.
    /// </summary>
    public class LoadingOverlaySortingTests
    {
        /// <summary>게임 창(GUIRoot 캔버스 등)의 기본 정렬값 — 로딩은 이보다 확실히 위여야 한다.</summary>
        private const int GameUiSortingOrder = 0;

        [Test]
        public void 로딩과_페이더는_게임_UI보다_위다()
        {
            Assert.Greater(GameSceneManager.LoadingSortingOrder, GameUiSortingOrder,
                "로딩이 게임 UI와 같거나 아래면 로딩 중 창이 앞으로 튀어나온다.");
            Assert.Greater(GameSceneManager.FaderSortingOrder, GameSceneManager.LoadingSortingOrder,
                "페이더는 로딩까지 덮어야 하므로 로딩보다 위여야 한다.");
        }

        [Test]
        public void 프리팹_정렬값이_코드_상수와_일치한다()
        {
            AssertPrefabSorting(GameSceneManager.LoadingKey, GameSceneManager.LoadingSortingOrder);
            AssertPrefabSorting(GameSceneManager.FaderKey, GameSceneManager.FaderSortingOrder);
        }

        [Test]
        public void 중첩_캔버스도_스스로_정렬하도록_올린다()
        {
            // 자식 캔버스는 기본적으로 부모 정렬을 따르므로 overrideSorting 을 켜지 않으면 값이 무시된다.
            var root = new GameObject("Overlay", typeof(Canvas));
            var child = new GameObject("Child", typeof(Canvas));
            child.transform.SetParent(root.transform, false);

            try
            {
                GameSceneManager.ApplyOverlaySorting(root, GameSceneManager.LoadingSortingOrder);

                Assert.AreEqual(GameSceneManager.LoadingSortingOrder, root.GetComponent<Canvas>().sortingOrder);
                Assert.AreEqual(GameSceneManager.LoadingSortingOrder, child.GetComponent<Canvas>().sortingOrder);
                Assert.IsTrue(child.GetComponent<Canvas>().overrideSorting,
                    "자식 캔버스는 overrideSorting 이 꺼져 있으면 정렬값이 무시된다.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertPrefabSorting(string assetPath, int expected)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.IsNotNull(prefab, $"프리팹을 찾지 못했다: {assetPath}");

            foreach (var canvas in prefab.GetComponentsInChildren<Canvas>(true))
                Assert.AreEqual(expected, canvas.sortingOrder,
                    $"{assetPath} 의 '{canvas.name}' 정렬값이 코드 상수와 다르다(프리팹을 열어 저장하면 되돌아갈 수 있다).");
        }
    }
}
