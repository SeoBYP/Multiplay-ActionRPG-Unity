using GameServer.Application.Common;
using GameServer.Application.Domains.User.Interfaces;

namespace GameServer.Application.Domains.User;
using User = Domain.Entities.User.User;
public class UserService(IUserRepository userRepository,
    IUserSessionRepository userSessionRepository) : IUserService
{
    public async Task<Result<User>> GetProfileAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var user = await userRepository.GetByIdAsync(session.UserId, ct);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            return Result<User>.Success(user);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<User>> SetNicknameAsync(string sessionId, string nickname, CancellationToken ct = default)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(nickname))
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            if (await userRepository.IsNicknameExistsAsync(nickname, ct))
                return Result<User>.Failure(ErrorCodes.NickNameAlreadyTaken, ErrorMessages.NickNameAlreadyTaken);
 
            var user = await userRepository.GetByIdAsync(session.UserId, ct);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            user.SetNickName(nickname);
            if (await userRepository.UpdateAsync(user, ct))
            {
                return Result<User>.Success(user);
            }
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
        catch (ArgumentException e)
        {
            return Result<User>.Failure(ErrorCodes.InvalidRequest, e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<User>> SetEmailAsync(string sessionId, string email, CancellationToken ct = default)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(email))
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            if (await userRepository.IsEmailExistsAsync(email, ct))
                return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);

            var user = await userRepository.GetByIdAsync(session.UserId, ct);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            user.SetEmail(email);
            if (await userRepository.UpdateAsync(user, ct))
            {
                return Result<User>.Success(user);
            }
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
        catch (ArgumentException e)
        {
            return Result<User>.Failure(ErrorCodes.InvalidRequest, e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<User>> UpdateProfileAsync(string sessionId, string nickname, string email, CancellationToken ct = default)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nickname))
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            if (await userRepository.IsNicknameExistsAsync(nickname, ct))
                return Result<User>.Failure(ErrorCodes.NickNameAlreadyTaken, ErrorMessages.NickNameAlreadyTaken);

            if (await userRepository.IsEmailExistsAsync(email, ct))
                return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);
      
            var user = await userRepository.GetByIdAsync(session.UserId, ct);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            user.SetProfile(nickname, email);
            if (await userRepository.UpdateAsync(user, ct))
            {
                return Result<User>.Success(user);
            }
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
        catch (ArgumentException e)
        {
            return Result<User>.Failure(ErrorCodes.InvalidRequest, e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Result<User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }
}