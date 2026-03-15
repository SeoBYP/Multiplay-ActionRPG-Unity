using System.Security.Cryptography;
using GameServer.Application.Common;
using GameServer.Application.Domains.Auth.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.Application.Domains.Auth;

using User = Domain.Entities.User.User;

public class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUserSessionRepository userSessionRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IOptions<JwtOptions> jwtOptions)
    : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    
    // TODO : Server Stream으로 하면 동시접속자를 알 수 있다
    public async Task<Result<User>> RegisterAsync(string password, string email, CancellationToken ct = default)
    {
        if(string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        // 이메일 중복 체크
        var existingEmail = await userRepository.IsEmailExistsAsync(email, ct);
        if (existingEmail)
            return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);
        
        // 비밀번호 해싱
        var hash = passwordHasher.HashPassword(password);

        // User Entity 생성
        var user = await userRepository.AddAsync(hash, email, ct);
        
        // Response
        return Result<User>.Success(user);
    }

    public async Task<Result<LoginResult>> LoginAsync(string email, string password, string deviceId, CancellationToken ct = default)
    {
        // 잘못된 요청
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(password))
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(email)) missingFields.Add("email");
            if (string.IsNullOrWhiteSpace(password)) missingFields.Add("password");
            if (string.IsNullOrWhiteSpace(deviceId)) missingFields.Add("deviceId");
            
            var msg = $"{ErrorMessages.InvalidRequest} (Missing: {string.Join(", ", missingFields)})";
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, msg);
        }

        // User 조회: 먼저 닉네임으로, 없으면 이메일로 재시도
        var user = await userRepository.GetByEmailAsync(email, ct);
        if (user is null)
            return Result<LoginResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);

        // 비밀번호 검증
        var isValid = passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isValid)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);

        // 세션 생성
        var userSession = await userSessionRepository.CreateSessionAsync(user.UserId, user.NickName, user.Email, user.PublicId, ct);
        if (userSession is null)
            return Result<LoginResult>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);

        // TODO : AccessToken에 대한 만료에 대한 처리가 없다
        var accessToken =
            jwtTokenGenerator.GenerateAccessToken(user.UserId, user.NickName, user.Email, userSession.SessionId);
        var expiresAt = _jwtOptions.GetExpirationTime();

        var refreshToken = jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenExpirationHours);
        
        // 유저 정보에 리프레시 토큰 저장
        var hashedRefreshToken = HashRefreshToken(refreshToken, deviceId);
        await userRepository.UpdateRefreshTokenAsync(user.UserId, hashedRefreshToken, refreshTokenExpiry, ct);

        // 성공
        return Result<LoginResult>.Success(new LoginResult(
            user, 
            userSession, 
            accessToken,
            refreshToken,
            expiresAt));
    }
    

    public async Task<Result> LogoutAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Result.Failure(ErrorCodes.InvalidRequest,
                ErrorMessages.InvalidRequest);

        // 로그아웃 시 리프레시 토큰도 삭제 (세션 정보를 통해 유저 ID를 알아내야 함)
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is not null)
        {
            await userRepository.ClearRefreshTokenAsync(userSession.UserId, ct);
        }

        await userSessionRepository.RemoveSessionAsync(sessionId, ct);
        return Result.Success();
    }

    public async Task<Result<LoginResult>> RefreshTokenAsync(string accessToken, string refreshToken, string deviceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(deviceId))
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
        var claimsPrincipal = await jwtTokenGenerator.ValidateToken(accessToken, false);
        if (claimsPrincipal is null)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        var sessionIdClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
        if (sessionIdClaim is null)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionIdClaim.Value, ct);
        if (userSession is null)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        // 3. UserId → User 조회 → RefreshToken 꺼내기
        var user = await userRepository.GetByIdAsync(userSession.UserId, ct);
        if (user is null)
        {
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // 4. RefreshToken null 체크
        if (user.RefreshToken is null)
        {
            await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct); // 세션 강제 종료
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }
        
        // 5. RefreshToken 및 DeviceId 검증 (Binding 확인)
        // !=는 타이밍 공격(Timing Attack)에 이론적으로 취약해
        /*
         * 1. CryptographicOperations.FixedTimeEquals란?
         * 일반적인 비교 연산자(== 또는 SequenceEqual)는 두 배열을 앞에서부터 비교하다가 다른 값이 발견되는 즉시 비교를 중단합니다. 이로 인해 비교하는 데이터가 앞부분에서 틀렸을 때와 뒷부분에서 틀렸을 때의 실행 시간이 미세하게 달라집니다.
         * 공격자는 이 미세한 시간 차이를 측정하여 비밀 값(이 경우 RefreshToken)을 한 글자씩 유추해낼 수 있는데, 이를 **타이밍 공격(Side-channel attack)**이라고 합니다.
         * FixedTimeEquals는 데이터의 일치 여부와 상관없이 항상 모든 바이트를 끝까지 비교하여 실행 시간을 일정하게 유지함으로써 이러한 공격을 원천 차단합니다.
         */
        var hashedInputToken = HashRefreshToken(refreshToken, deviceId);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(user.RefreshToken),
                Convert.FromHexString(hashedInputToken)))
        {
            // Binding 실패 시 보안 위험으로 간주하고 세션 종료
            user.ClearRefreshToken();
            await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct);
            return Result<LoginResult>.Failure(ErrorCodes.SessionExpired, ErrorMessages.SessionExpired);
        }

        // 6. RefreshToken 만료일 검사
        if (user.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            return Result<LoginResult>.Failure(ErrorCodes.SessionExpired, ErrorMessages.SessionExpired);
        }

        // 7. 새 AccessToken 발급 + RefreshToken Rotation
        var newAccessToken = jwtTokenGenerator.GenerateAccessToken(user.UserId, user.NickName, user.Email, userSession.SessionId);
        var accessTokenExpiresAt = _jwtOptions.GetExpirationTime();

        var newRawRefreshToken = jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenExpirationHours);

        // 로테이션된 토큰 저장
        var hashedNewRefreshToken = HashRefreshToken(newRawRefreshToken, deviceId);
        await userRepository.UpdateRefreshTokenAsync(user.UserId, hashedNewRefreshToken, refreshTokenExpiry, ct);

        return Result<LoginResult>.Success(new LoginResult(
            user, 
            userSession, 
            newAccessToken, 
            newRawRefreshToken,
            accessTokenExpiresAt));
    }
    
    private static string HashRefreshToken(string refreshToken,string deviceId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(refreshToken + deviceId));
        return Convert.ToHexString(bytes);
    }

    
    public async Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        // 토큰이 유효한 값인지 검증
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // 토큰 검증
        var claimsPrincipal = await jwtTokenGenerator.ValidateToken(token);
        if (claimsPrincipal is null)
            return false;

        // sessionID 가져오기
        var sessionId = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return false;

        // 현재 session이 활성화 되었는지 
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId.Value, ct);
        if (userSession is null)
            return false;
        
        return true;
    }
}