using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Net;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Ba dòng mỗi handler. Nếu có ngày một hàm ở đây dài quá 10 dòng thì nghiệp vụ đang rò rỉ
    /// ra khỏi <see cref="AuthService"/> — kéo nó về.
    /// </summary>
    public static class AuthHandler
    {
        /// <summary>Gán một lần trong <c>Program.cs</c>.</summary>
        public static AuthService AuthService { get; set; }

        [TcpHandler(NetCmd.Register)]
        public static async Task<NetResult> OnRegister(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await AuthService.RegisterAsync(req.Session, req.GetData<RegisterRequest>()));
        }

        [TcpHandler(NetCmd.Login)]
        public static async Task<NetResult> OnLogin(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await AuthService.LoginAsync(req.Session, req.GetData<LoginRequest>()));
        }

        [TcpHandler(NetCmd.Logout, MinState = SessionState.Authenticated)]
        public static Task<NetResult> OnLogout(NetRequest req)
        {
            return Task.FromResult(NetResult.Ok(AuthService.Logout(req.Session)));
        }
    }
}