using MMORPG.ServerCore;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Chỗ duy nhất biết thư mục script nằm ở đâu. Cố tình đọc thẳng từ thư mục nguồn thay vì bản
    /// copy trong bin/: có vậy sửa file lúc server đang chạy mới có tác dụng.
    /// </summary>
    public static class LuaScriptPaths
    {
        private const string FOLDER_NAME = "LuaScripts";

        private static string _cached;

        /// <summary>Dò ngược từ thư mục chạy (bin/Debug/net8.0) lên tới khi thấy LuaScripts.</summary>
        public static string Dir
        {
            get
            {
                if (_cached != null)
                    return _cached;

                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, FOLDER_NAME);
                    if (Directory.Exists(candidate))
                    {
                        _cached = candidate;
                        return _cached;
                    }

                    dir = dir.Parent;
                }

                Log.Error($"Không tìm thấy thư mục {FOLDER_NAME} từ {AppContext.BaseDirectory} trở lên");
                _cached = AppContext.BaseDirectory;
                return _cached;
            }
        }

        /// <summary>Đọc nội dung một script. Trả chuỗi rỗng nếu không có file — chỗ gọi tự báo lỗi.</summary>
        public static string Read(string relativePath)
        {
            string path = Path.Combine(Dir, relativePath);
            if (!File.Exists(path))
            {
                Log.Error($"Không có file {relativePath} trong {Dir}");
                return string.Empty;
            }

            return File.ReadAllText(path);
        }
    }
}
