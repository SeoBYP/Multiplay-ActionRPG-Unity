namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 로비 화면에서 발생할 수 있는 사용자 의도의 닫힌 집합.
    /// View는 이것만 생성한다 — 로직은 모른다.
    /// </summary>
    public abstract class LobbyIntent
    {
        private LobbyIntent() { }

        public sealed class LoadRooms : LobbyIntent
        {
            public static readonly LoadRooms Instance = new LoadRooms();
            private LoadRooms() { }
        }

        public sealed class CreateRoom : LobbyIntent
        {
            public readonly string Name;
            public readonly int MaxPlayers;
            /// <summary>플레이할 던전(spawn-layouts.json 키). 빈 문자열이면 서버가 기본 맵으로 영속.</summary>
            public readonly string MapId;

            public CreateRoom(string name, int maxPlayers, string mapId = "")
            {
                Name       = name;
                MaxPlayers = maxPlayers;
                MapId      = mapId ?? "";
            }
        }

        public sealed class JoinRoom : LobbyIntent
        {
            public readonly long RoomId;

            public JoinRoom(long roomId)
            {
                RoomId = roomId;
            }
        }

        /// <summary>방 목록에서 방 1개를 선택 (네트워크 없음, 즉시 처리).</summary>
        public sealed class SelectRoom : LobbyIntent
        {
            public readonly long RoomId;

            public SelectRoom(long roomId)
            {
                RoomId = roomId;
            }
        }

        public sealed class StartGame : LobbyIntent
        {
            public static readonly StartGame Instance = new StartGame();
            private StartGame() { }
        }

        /// <summary>대기실에서 자신의 준비 상태를 토글한다 (방장은 서버가 거부).</summary>
        public sealed class SetReady : LobbyIntent
        {
            public readonly bool IsReady;

            public SetReady(bool isReady)
            {
                IsReady = isReady;
            }
        }

        public sealed class LeaveRoom : LobbyIntent
        {
            public static readonly LeaveRoom Instance = new LeaveRoom();
            private LeaveRoom() { }
        }

        /// <summary>
        /// 재로그인 시 서버가 알려준 방 ID로 세션/구독을 복원한다.
        /// JoinRoom API를 다시 호출하지 않으므로 AlreadyInRoom 오류가 발생하지 않는다.
        /// </summary>
        public sealed class RestoreRoom : LobbyIntent
        {
            public readonly long RoomId;

            public RestoreRoom(long roomId)
            {
                RoomId = roomId;
            }
        }
    }
}
