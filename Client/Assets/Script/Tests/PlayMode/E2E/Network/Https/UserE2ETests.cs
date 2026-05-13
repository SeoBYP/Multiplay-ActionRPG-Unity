using System.Collections;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.User;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class UserE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator SetNickname_정상_설정() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            var response = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = UniqueNickname()
            }, Timeout());

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success, response.Result.Message);
        });

        [UnityTest]
        public IEnumerator SetNickname_중복_닉네임_실패() => UniTask.ToCoroutine(async () =>
        {
            var nickname = UniqueNickname();

            var email1 = UniqueEmail();
            await RegisterAndLoginAsync(email1, "Test1234!");
            var first = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = nickname
            }, Timeout());
            Assert.IsTrue(first.Result.Success, first.Result.Message);

            var email2 = UniqueEmail();
            await RegisterAndLoginAsync(email2, "Test1234!");

            var response = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = nickname
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator SetNickname_너무_짧은_닉네임_실패() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = "a"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator SetNickname_허용되지_않은_문자_실패() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = "bad-name!"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator SetNickname_욕설_포함_실패() => UniTask.ToCoroutine(async () =>
        {
            await RegisterAndLoginAsync(UniqueEmail(), "Test1234!");

            var response = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = "badword_hero"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });
    }
}
