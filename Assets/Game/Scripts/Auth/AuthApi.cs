using HungNT;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Gom mọi lệnh auth mà client GỬI ĐI. Đối xứng với <see cref="AuthNetHandler"/> ở chiều nhận.
    /// Tách ra để chỗ khác không phải biết `NetCmd` nào đi với DTO nào.
    /// </summary>
    public sealed class AuthApi
    {
        private readonly NetService _netService;

        public AuthApi(NetService netService)
        {
            _netService = netService;
        }

        public void Register(string username, string password)
        {
            this.Log($"Register: username:{username.Color("cyan")}, password:{password.Color("cyan")}");
            _netService.Send(NetCmd.Register, new RegisterRequest { Username = username, Password = password });
        }

        public void Login(string username, string password)
        {
            this.Log($"Login: username:{username.Color("cyan")}, password:{password.Color("cyan")}");
            _netService.Send(NetCmd.Login, new LoginRequest { Username = username, Password = password });
        }

        public void Logout()
        {
            this.Log("Logout");
            _netService.Send(NetCmd.Logout, new EmptyRequest());
        }
    }
}