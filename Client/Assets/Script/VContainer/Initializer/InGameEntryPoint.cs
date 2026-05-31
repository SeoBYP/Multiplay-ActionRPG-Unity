using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.System.Auth;
using UnityEngine;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// Dungeon 씬 진입 시 소켓 상태를 읽어 인게임 초기 설정을 수행한다.
    /// PlayerCharacter 스폰 등 A-3 작업은 여기에 추가한다.
    /// </summary>
    public class InGameEntryPoint : IAsyncStartable
    {
        private readonly ISocketPacketState _socketState;
        private readonly UserProfile _userProfile;
        private readonly AuthSession _authSession;

        public InGameEntryPoint(
            ISocketPacketState socketState,
            UserProfile userProfile,
            AuthSession authSession)
        {
            _socketState  = socketState;
            _userProfile  = userProfile;
            _authSession  = authSession;
        }

        public UniTask StartAsync(CancellationToken ct)
        {
            Debug.Log($"[InGameEntryPoint] Dungeon 씬 초기화 — NickName={_userProfile.NickName} UserId={_authSession.UserId}");

            // 현재 방에 입장한 플레이어 목록 로그 (A-3: 이후 캐릭터 스폰으로 대체)
            if (_socketState.TryGetPlayer(_authSession.UserId, out var me))
                Debug.Log($"[InGameEntryPoint] 내 스폰 위치 — ({me.PosX:F1}, {me.PosY:F1}, {me.PosZ:F1})");

            return UniTask.CompletedTask;
        }
    }
}
