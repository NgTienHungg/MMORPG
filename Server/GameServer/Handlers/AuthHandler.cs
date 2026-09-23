using MMORPG.GameServer.Auth;
using MMORPG.GameServer.Boot;
using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Dto.Auth;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    /// <summary>
    /// Ba dòng mỗi handler. Nếu có ngày một hàm ở đây dài quá 10 dòng thì nghiệp vụ đang rò rỉ
    /// ra khỏi <see cref="AuthService"/> — kéo nó về.
    /// </summary>
    public static class AuthHandler
    {
        // Handler là hàm static nên không có constructor để nhận inject — lấy service từ sổ chung.
        // Property chứ không field: tra lúc DÙNG, không lúc class được nạp. Field static khởi tạo
        // sớm hơn ServerBootstrap.Build() một nhịp và sẽ giữ null vĩnh viễn.
        private static AuthService AuthService => ServerServices.Get<AuthService>();

        // MinState = Verified: chưa kiểm phiên bản thì chưa được gõ cửa nào khác. Phép chặn nằm ở
        // dispatcher, không nằm ở thiện chí của client.
        [TcpHandler(NetCmd.Register, MinState = SessionState.Verified)]
        public static async Task<NetResult> OnRegister(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await AuthService.RegisterAsync(req.Session, req.GetData<RegisterRequest>()));
        }

        [TcpHandler(NetCmd.Login, MinState = SessionState.Verified)]
        public static async Task<NetResult> OnLogin(NetRequest req)
        {
            if (req.Session.State >= SessionState.Authenticated)
                return NetResult.Ok(new AuthResponse { Success = false, Error = ErrorCode.AlreadyAuthenticated });

            return NetResult.Ok(await AuthService.LoginAsync(req.Session, req.GetData<LoginRequest>()));
        }

        [TcpHandler(NetCmd.Logout, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnLogout(NetRequest req)
        {
            // Logout khi đang trong world: rời world trước, cùng một đường dọn dẹp với mất kết nối.
            // Hỏi sổ chung chứ không gọi sang CharacterHandler: handler nói chuyện với SERVICE, không
            // nói chuyện với handler khác — nếu không thì dispatch table có thêm một đồ thị phụ thuộc
            // thứ hai mà không ai vẽ ra.
            await ServerServices.Get<CharacterService>().LeaveWorldAsync(req.Session);

            return NetResult.Ok(AuthService.Logout(req.Session));
        }
    }
}
