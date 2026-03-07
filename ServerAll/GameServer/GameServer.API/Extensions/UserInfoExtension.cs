using GameServer.Domain.Entities.User;
using GameServer.Grpc.User;

namespace GameServer.API.Extension;

public static class UserInfoExtension
{
    public static UserInfo ToUserInfo(this User value)
    {
        return new UserInfo
        {
            PublicId = value.PublicId,
            NickName = value.NickName,
            // Email = value.Email,
            // CreatedAt = new DateTimeOffset(value.CreatedAt).ToUnixTimeSeconds(),
        };
    }
}