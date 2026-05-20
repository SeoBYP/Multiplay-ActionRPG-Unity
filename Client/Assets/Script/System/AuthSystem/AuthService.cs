using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Auth;
using UnityEngine;
using Application = UnityEngine.Application;
using SystemInfo = UnityEngine.SystemInfo;

namespace Script.System.Auth
{
    /// <summary>
    /// 로그인, 자동 로그인, 토큰 갱신 흐름을 담당하는 인증 서비스.
    /// UI는 이 서비스를 통해 서버 인증과 로컬 세션 관리를 수행한다.
    /// </summary>
    public class AuthService : IAuthService
    {
        private const int UserNotFoundErrorCode = 1003;

        private readonly IAuthGrpcService _authGrpcClient;
        private readonly GrpcChannelProvider _channelProvider;
        private readonly AuthSession _authSession;

        public AuthService(
            IAuthGrpcService authGrpcClient,
            GrpcChannelProvider channelProvider,
            AuthSession authSession)
        {
            _authGrpcClient = authGrpcClient;
            _channelProvider = channelProvider;
            _authSession = authSession;
            _channelProvider.AccessTokenProvider = () => _authSession.AccessToken;
        }

        public bool IsAuthenticated => _authSession is { IsAuthenticated: true };

        public UniTask AuthenticatedAsync() => _authSession.AuthenticatedAsync();

        /// <summary>
        /// 저장된 토큰으로 자동 로그인을 시도한다.
        /// access token이 아직 유효하면 바로 성공시키고, 만료되었으면 refresh를 수행한다.
        /// </summary>
        public async UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct)
        {
            // 저장된 토큰이 없으면 즉시 로그인 UI로 보낸다.
            if (!TokenStorage.TryLoad(out var accessToken, out var refreshToken, out var expirationTime))
            {
                Debug.Log("Token is not exist");
                return AuthResult.NeedLogin;
            }

            // 저장된 값을 메모리 세션에 먼저 복구해 refresh 요청에서도 사용할 수 있게 한다.
            _authSession.Update(accessToken, refreshToken, expirationTime);

            var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // access token이 아직 유효하면 서버 호출 없이 바로 인증 완료로 본다.
            if (expirationTime > nowUnixSeconds)
            {
                Debug.Log("Token is valid");
                return AuthResult.Success;
            }

            // 만료된 경우 refresh token으로 세션 갱신을 시도한다.
            var refreshResult = await RefreshTokenAsync(ct);
            if (refreshResult == AuthResult.Success)
            {
                return AuthResult.Success;
            }

            // refresh 실패 시 손상된 세션으로 간주하고 저장된 토큰을 비운다.
            _authSession.Clear();
            Debug.Log("Token refresh failed");
            return AuthResult.NeedLogin;
        }

        /// <summary>
        /// 먼저 로그인 시도 후, 계정이 없을 때만 회원가입 후 재로그인을 수행한다.
        /// </summary>
        public async UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct)
        {
            var loginResult = await TryLoginAsync(email, password, ct);
            // 기존 계정 로그인 성공.
            if (loginResult.Result.Success)
            {
                ApplyLogin(loginResult);
                return AuthResult.Success;
            }

            // 잘못된 비밀번호 등 다른 실패는 회원가입 fallback 대상이 아니다.
            if (loginResult.Result.ErrorCode != UserNotFoundErrorCode)
            {
                // INVALID_CREDENTIALS 같은 사용자 입력 오류는 예상 가능한 실패이므로 error 로그로 올리지 않는다.
                Debug.Log(loginResult.Result.Message);
                return AuthResult.Failed;
            }

            // 계정이 없는 경우에만 회원가입 후 로그인 흐름으로 진입한다.
            var register = await _authGrpcClient.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = password
            }, ct);

            Debug.Log(register.Result.Message);
            if (!register.Result.Success)
            {
                Debug.Log(register.Result.Message);
                return AuthResult.Failed;
            }

            // 회원가입 직후 다시 로그인해서 access/refresh token을 발급받는다.
            loginResult = await TryLoginAsync(email, password, ct);
            if (!loginResult.Result.Success)
            {
                Debug.Log(loginResult.Result.Message);
                return AuthResult.Failed;
            }

            ApplyLogin(loginResult);
            return AuthResult.Success;
        }

        /// <summary>
        /// 서버 로그인 API를 호출해 계정 인증을 수행한다.
        /// </summary>
        private async UniTask<LoginResponse> TryLoginAsync(string email, string password, CancellationToken ct)
        {
            var loginRes = await _authGrpcClient.LoginAsync(new LoginRequest
            {
                DeviceId = GetDeviceId(),
                Email = email,
                Password = password
            }, ct);

            Debug.Log(loginRes.Result.Message);
            return loginRes;
        }

        /// <summary>
        /// 성공한 로그인/리프레시 응답을 현재 세션에 반영한다.
        /// </summary>
        private void ApplyLogin(LoginResponse loginRes)
        {
            _authSession.Update(loginRes.AccessToken, loginRes.RefreshToken, loginRes.ExpiresAt);
        }

        /// <summary>
        /// 로컬 세션을 종료하고 서버 로그아웃을 비동기로 요청한다.
        /// </summary>
        public void Logout()
        {
            // 로그아웃 RPC가 Authorization 헤더를 만들 수 있도록 현재 토큰을 먼저 유지한 채 요청을 보낸다.
            if (_authSession.IsAuthenticated)
            {
                FireAndForgetLogoutAsync().Forget();
            }

            _authSession.Clear();
            Debug.Log("Logout");
        }

        /// <summary>
        /// 현재 세션의 refresh token으로 새 토큰 세트를 발급받는다.
        /// </summary>
        public async UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct)
        {
            // 메모리 세션이 없으면 refresh 자체를 시도할 수 없다.
            if (!_authSession.IsAuthenticated || string.IsNullOrWhiteSpace(_authSession.RefreshToken))
            {
                return AuthResult.NeedLogin;
            }

            var res = await _authGrpcClient.RefreshAsync(new RefreshRequest
            {
                DeviceId = GetDeviceId(),
                RefreshToken = _authSession.RefreshToken
            }, ct);
            Debug.Log(res.Result.Message);

            // refresh 실패는 상위 흐름에서 로그인 화면 복귀로 처리한다.
            if (!res.Result.Success)
            {
                return AuthResult.Failed;
            }

            // 서버가 회전시킨 토큰 세트를 현재 세션에 반영한다.
            _authSession.Update(res.AccessToken, res.RefreshToken, res.ExpiresAt);
            return AuthResult.Success;
        }

        /// <summary>
        /// refresh token 검증에 사용할 기기 식별값을 반환한다.
        /// 기기 고유 식별자를 우선 사용하고, 비어 있으면 플랫폼명으로 fallback 한다.
        /// </summary>
        private static string GetDeviceId()
        {
            return string.IsNullOrWhiteSpace(SystemInfo.deviceUniqueIdentifier)
                ? Application.platform.ToString()
                : SystemInfo.deviceUniqueIdentifier;
        }

        /// <summary>
        /// 서버 로그아웃 호출에서 발생하는 예외를 내부에서 흡수한다.
        /// 로컬 세션은 이미 종료됐으므로 네트워크 실패를 사용자 흐름에 다시 전파하지 않는다.
        /// </summary>
        private async UniTask FireAndForgetLogoutAsync()
        {
            try
            {
                await _authGrpcClient.LogoutAsync(new LogoutRequest());
            }
            catch
            {
            }
        }
    }
}
