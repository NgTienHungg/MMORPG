using HungNT;
using HungNT.DataSave;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Nơi duy nhất biết tài khoản được ghi nhớ nằm ở đâu và mật khẩu được xáo thế nào.
    /// Presenter chỉ cần <see cref="HasSavedLogin"/> để điền sẵn, rồi <see cref="Save"/> / <see cref="Clear"/>
    /// sau khi đăng nhập xong.
    /// </summary>
    public sealed class SavedLoginStore
    {
        private readonly IDataSaveService _dataSaveService;

        public SavedLoginStore(IDataSaveService dataSaveService)
        {
            _dataSaveService = dataSaveService;
        }

        /// <summary>Có tài khoản để điền sẵn vào ô nhập không.</summary>
        public bool HasSavedLogin
        {
            get
            {
                var data = _dataSaveService.GetData<LoginSaveData>();
                return data.Remember && !string.IsNullOrEmpty(data.Username);
            }
        }

        public string Username => _dataSaveService.GetData<LoginSaveData>().Username;

        public string Password => PasswordScrambler.Unscramble(_dataSaveService.GetData<LoginSaveData>().ScrambledPassword);

        public void Save(string username, string password)
        {
            var data = _dataSaveService.GetData<LoginSaveData>();
            data.Remember = true;
            data.Username = username;
            data.ScrambledPassword = PasswordScrambler.Scramble(password);

            // Ghi thẳng xuống đĩa thay vì chỉ đánh dấu dirty: sau khi đăng nhập xong người chơi vào thẳng
            // game và tắt bằng Alt+F4 hay đóng cửa sổ là chuyện thường, mà đường đó không đảm bảo
            // chạy kịp nhịp flush 5 giây.
            _dataSaveService.Save(data);
            this.Log($"Ghi nhớ tài khoản {username.Color("cyan")}");
        }

        /// <summary>Quên tài khoản đã lưu — gọi khi người chơi đăng nhập lúc ô ghi nhớ đang tắt.</summary>
        public void Clear()
        {
            var data = _dataSaveService.GetData<LoginSaveData>();

            // Không lưu gì thì cũng không có gì để xoá; bỏ qua luôn để khỏi ghi đĩa mỗi lần đăng nhập.
            if (!data.Remember && string.IsNullOrEmpty(data.Username))
                return;

            data.Remember = false;
            data.Username = string.Empty;
            data.ScrambledPassword = string.Empty;

            _dataSaveService.SaveImmediate(data);
            this.Log("Đã quên tài khoản lưu trước đó");
        }
    }
}
