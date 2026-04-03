using GameServer.Application.Common;

namespace GameServer.Application.Domains.Account;

public interface IAccountService
{
    Task<Result<Domain.Entities.User.User>> RegisterAsync(string email, string password, CancellationToken ct = default);

    Task<Result<CredentialVerifyResult>> VerifyCredentialAsync(string email, string password, CancellationToken ct = default);

    Task<Result> UpdatePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default);

    Task<bool> RemoveAsync(long userId, CancellationToken ct = default);
}
