using GameServer.Application.Common;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Security.Interface;
using Microsoft.Extensions.Logging;
using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.Account;

public class AccountService(
    IUserRepository userRepository,
    IUserProfileRepository userProfileRepository,
    IUserCredentialRepository userCredentialRepository,
    IPasswordHasher passwordHasher,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<Result<Domain.Entities.User.User>> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Register failed because email or password was empty");
            return Result<Domain.Entities.User.User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        if (await userCredentialRepository.IsEmailExistsAsync(email, ct))
        {
            logger.LogInformation("Register rejected because email already exists: {Email}", email);
            return Result<Domain.Entities.User.User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);
        }

        var passwordHash = passwordHasher.HashPassword(password);
        Domain.Entities.User.User? user = null;

        try
        {
            user = await userRepository.CreateAsync(ct);
            await userProfileRepository.CreateAsync(user.UserId, user.PublicId, ct);
            await userCredentialRepository.CreateAsync(user.UserId, email, passwordHash, ct);

            logger.LogInformation("Registered user {UserId} with email {Email}", user.UserId, email);
            return Result<Domain.Entities.User.User>.Success(user);
        }
        catch (Exception ex)
        {
            if (user is not null)
            {
                await userProfileRepository.RemoveAsync(user.UserId, ct);
                await userRepository.RemoveAsync(user.UserId, ct);
            }

            logger.LogError(ex, "Register failed for email {Email}", email);
            return Result<Domain.Entities.User.User>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<CredentialVerifyResult>> VerifyCredentialAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Credential verification failed because email or password was empty");
            return Result<CredentialVerifyResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var credential = await userCredentialRepository.FindByEmailAsync(email, ct);
        if (credential is null)
        {
            logger.LogInformation("Credential verification failed because user was not found for email {Email}", email);
            return Result<CredentialVerifyResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);
        }

        if (!passwordHasher.VerifyPassword(password, credential.PasswordHash))
        {
            logger.LogInformation("Credential verification failed because password was invalid for user {UserId}", credential.UserId);
            return Result<CredentialVerifyResult>.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);
        }

        var user = await userRepository.GetByIdAsync(credential.UserId, ct);
        if (user is null)
        {
            logger.LogError("Credential verification failed because user entity was missing for credential user {UserId}", credential.UserId);
            return Result<CredentialVerifyResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);
        }

        return Result<CredentialVerifyResult>.Success(new CredentialVerifyResult(user.UserId, user.PublicId));
    }

    public async Task<Result> UpdatePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            logger.LogWarning("UpdatePassword failed because request was invalid for user {UserId}", userId);
            return Result.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var credential = await userCredentialRepository.FindByIdAsync(userId, ct);
        if (credential is null)
        {
            logger.LogInformation("UpdatePassword failed because credential was not found for user {UserId}", userId);
            return Result.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);
        }

        if (!passwordHasher.VerifyPassword(currentPassword, credential.PasswordHash))
        {
            logger.LogInformation("UpdatePassword failed because current password was invalid for user {UserId}", userId);
            return Result.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);
        }

        var newPasswordHash = passwordHasher.HashPassword(newPassword);
        var updated = await userCredentialRepository.UpdatePasswordHashAsync(userId, newPasswordHash, ct);
        if (!updated)
        {
            logger.LogError("UpdatePassword failed because repository update returned false for user {UserId}", userId);
            return Result.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }

        logger.LogInformation("Updated password for user {UserId}", userId);
        return Result.Success();
    }

    public async Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        var credential = await userCredentialRepository.FindByIdAsync(userId, ct);
        if (credential is not null)
        {
            await userCredentialRepository.RemoveAsync(credential.UserId, ct);
        }

        return await userRepository.RemoveAsync(userId, ct);
    }
}
