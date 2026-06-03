using Game.Gameplay.Spawn;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 결정론적 스폰 검증(클라이언트 미러).
    ///
    /// 이 기대 벡터는 서버 SocketServer.Tests 의 SpawnLayoutTests 와 동일해야 한다(미러 drift 방지).
    /// SpawnLayoutProvider 는 Resources/spawn-layouts.json(서버 정본의 미러)을 읽는다.
    /// </summary>
    public class SpawnResolverTests
    {
        private static void AssertPoint(SpawnPoint p, float x, float y, float z, float rotY)
        {
            Assert.AreEqual(x, p.X, 0.0001f);
            Assert.AreEqual(y, p.Y, 0.0001f);
            Assert.AreEqual(z, p.Z, 0.0001f);
            Assert.AreEqual(rotY, p.RotY, 0.0001f);
        }

        [Test]
        public void dungeon_01_레이아웃은_인덱스별_고정_좌표를_반환한다()
        {
            var layout = new SpawnLayoutProvider().Get("dungeon_01");

            AssertPoint(SpawnResolver.Resolve(layout, 0), 0f, 0f, 0f, 0f);
            AssertPoint(SpawnResolver.Resolve(layout, 1), 2f, 0f, 0f, 180f);
            AssertPoint(SpawnResolver.Resolve(layout, 2), -2f, 0f, 0f, 90f);
            AssertPoint(SpawnResolver.Resolve(layout, 3), 0f, 0f, 2f, 270f);
        }

        [Test]
        public void 인덱스가_포인트_수를_넘으면_모듈러로_순환한다()
        {
            var layout = new SpawnLayoutProvider().Get("dungeon_01"); // 4개 포인트

            AssertPoint(SpawnResolver.Resolve(layout, 4), 0f, 0f, 0f, 0f);   // == 0
            AssertPoint(SpawnResolver.Resolve(layout, 5), 2f, 0f, 0f, 180f); // == 1
            AssertPoint(SpawnResolver.Resolve(layout, -1), 0f, 0f, 2f, 270f); // == 3
        }

        [Test]
        public void 알수없는_맵은_예외를_던진다()
        {
            var provider = new SpawnLayoutProvider();
            Assert.Throws<global::System.Collections.Generic.KeyNotFoundException>(() => provider.Get("no_such_map"));
        }
    }
}
