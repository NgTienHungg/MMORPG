using System;
using HungNT.DataSave;

namespace MMORPG.Client.Auth
{
    /// <summary>
    /// Tài khoản người chơi chọn ghi nhớ ở màn đăng nhập, ghi thành file JSON riêng trong thư mục
    /// persistent của máy. Đây là setting của máy người chơi chứ không phải state game — state game
    /// vẫn nằm hết ở server.
    /// </summary>
    [Serializable]
    public class LoginSaveData : BaseSaveData
    {
        /// <summary>Lần đăng nhập thành công gần nhất người chơi có tick "Ghi nhớ đăng nhập" không.</summary>
        public bool Remember;

        /// <summary>Tên đăng nhập đúng như server trả về (đã chuẩn hoá chữ thường).</summary>
        public string Username = string.Empty;

        /// <summary>
        /// Mật khẩu đã đi qua <see cref="PasswordScrambler"/>. Đọc trực tiếp field này ra thì chỉ thấy
        /// chuỗi base64 vô nghĩa — muốn dùng phải gọi <see cref="PasswordScrambler.Unscramble"/>.
        /// </summary>
        public string ScrambledPassword = string.Empty;
    }
}
