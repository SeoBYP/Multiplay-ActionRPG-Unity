using GameServer.Application.Common;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.User;

public class UserProfileService(
    IUserProfileRepository userProfileRepository,
    IUserSessionRepository userSessionRepository,
    IProfanityFilter profanityFilter,
    ILogger<UserProfileService> logger) : IUserProfileService
{
    public async Task<Result<UserProfile>> GetProfileAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (session is null)
                return Result<UserProfile>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var user = await userProfileRepository.GetByIdAsync(session.UserId, ct);

            return Result<UserProfile>.Success(user);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user profile");
            return Result<UserProfile>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<UserProfile>> UpdateProfileAsync(string sessionId, string nickname, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return Result<UserProfile>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            if(profanityFilter.IsProfane(nickname))
                return Result<UserProfile>.Failure(ErrorCodes.Profanity, ErrorMessages.Profanity);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (session is null)
                return Result<UserProfile>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var user = await userProfileRepository.GetByIdAsync(session.UserId, ct);
            if (user is null)
                return Result<UserProfile>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var duplicateProfile = await userProfileRepository.GetByNicknameAsync(nickname, ct);
            if (duplicateProfile is not null && duplicateProfile.UserId != user.UserId)
                return Result<UserProfile>.Failure(ErrorCodes.NickNameAlreadyTaken, ErrorMessages.NickNameAlreadyTaken);

            user.SetNickName(nickname);
            if (await userProfileRepository.UpdateAsync(user, ct))
            {
                return Result<UserProfile>.Success(user);
            }

            return Result<UserProfile>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
        catch (ArgumentException e)
        {
            return Result<UserProfile>.Failure(ErrorCodes.InvalidRequest, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update profile");
            return Result<UserProfile>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }
}
