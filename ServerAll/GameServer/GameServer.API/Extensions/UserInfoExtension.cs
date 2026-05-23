using GameServer.Domain.Entities.User;
using GameServer.Grpc.User;

namespace GameServer.API.Extension;

public static class UserInfoExtension
{
    public static UserInfo ToUserInfo(this User value, long currentRoomId = 0)
    {
        return new UserInfo
        {
            PublicId      = value.PublicId,
            // NickName: User 엔티티가 아닌 UserProfile 엔티티에 있음 — 필요 시 별도 조회 후 전달
            CurrentRoomId = currentRoomId,
        };
    }
}