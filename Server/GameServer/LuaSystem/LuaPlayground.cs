using MMORPG.ServerCore;
using MoonSharp.Interpreter;

namespace MMORPG.GameServer.LuaSystem
{
    /// <summary>
    /// Chỗ chạy thử script khi học. Mỗi lần chạy dựng một máy ảo mới rồi vứt, nên không bao giờ
    /// chạm vào máy ảo đang phục vụ game — console là luồng khác, và máy ảo Lua không an toàn đa luồng.
    /// </summary>
    public static class LuaPlayground
    {
        public static void RunFile(string relativePath)
        {
            string code = LuaScriptPaths.Read(relativePath);
            if (string.IsNullOrEmpty(code))
                return;

            // Preset_SoftSandbox: có sẵn string/math/table/os.time, nhưng đã cắt io, os.execute,
            // require, load — script không đọc/ghi file, không chạy lệnh hệ thống, không nạp thêm mã.
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // print() của Lua mặc định đi thẳng ra stdout, không tag không màu. Nối vào Log.
            script.Options.DebugPrint = message => Log.Info($"[{relativePath}] {message}");

            try
            {
                // Tham số thứ 3 là tên hiển thị trong thông báo lỗi. Không truyền thì lỗi ghi
                // "chunk_1:12" và không ai biết chunk_1 là file nào.
                DynValue result = script.DoString(code, null, relativePath);

                if (result.Type != DataType.Void && result.Type != DataType.Nil)
                    Log.Info($"{relativePath} trả về: {result.ToPrintString().Green()}");
            }
            catch (SyntaxErrorException ex)
            {
                // Sai cú pháp: phát hiện ngay lúc nạp, chưa chạy dòng nào.
                Log.Error($"Sai cú pháp trong {relativePath}: {ex.DecoratedMessage.Red()}");
            }
            catch (ScriptRuntimeException ex)
            {
                // Lỗi lúc chạy: gọi hàm không tồn tại, cộng chuỗi với số... chỉ nổ khi tới đúng dòng đó.
                Log.Error($"Lỗi khi chạy {relativePath}: {ex.DecoratedMessage.Red()}");
            }
        }
    }
}
