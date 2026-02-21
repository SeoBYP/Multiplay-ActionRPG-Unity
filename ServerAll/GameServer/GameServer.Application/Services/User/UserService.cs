using GameServer.Application.Common;
using GameServer.Application.Services.User.Interfaces;
using GameServer.Infrastructure.Interfaces.User;

namespace GameServer.Application.Services.User;
using User = Domain.Entities.User.User;
public class UserService(IUserRepository userRepository,
    IUserSessionRepository userSessionRepository) : IUserService
{
    public async Task<Result<User>> GetProfileAsync(string sessionId)
    {
        try
        {
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var user = await userRepository.GetByIdAsync(session.UserId);
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

    public async Task<Result<User>> SetNicknameAsync(string sessionId, string nickname)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(nickname))
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            if (await userRepository.IsNicknameExistsAsync(nickname))
                return Result<User>.Failure(ErrorCodes.NickNameAlreadyTaken, ErrorMessages.NickNameAlreadyTaken);
 
            var user = await userRepository.GetByIdAsync(session.UserId);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            user.SetNickName(nickname);
            if (await userRepository.UpdateAsync(user))
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

    public async Task<Result<User>> SetEmailAsync(string sessionId, string email)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(email))
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            if (await userRepository.IsEmailExistsAsync(email))
                return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);

            var user = await userRepository.GetByIdAsync(session.UserId);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            user.SetEmail(email);
            if (await userRepository.UpdateAsync(user))
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

    public async Task<Result<User>> UpdateProfileAsync(string sessionId, string nickname, string email)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nickname))
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            var session = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if(session is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            if (await userRepository.IsNicknameExistsAsync(nickname))
                return Result<User>.Failure(ErrorCodes.NickNameAlreadyTaken, ErrorMessages.NickNameAlreadyTaken);

            if (await userRepository.IsEmailExistsAsync(email))
                return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);
      
            var user = await userRepository.GetByIdAsync(session.UserId);
            if(user is null)
                return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
            user.SetProfile(nickname, email);
            if (await userRepository.UpdateAsync(user))
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