using MMORPG.ServerCore;
using MMORPG.Shared.World;
using Newtonsoft.Json;

namespace MMORPG.GameServer
{
    /// <summary>
    /// Nguồn DUY NHẤT của mọi con số vận hành. Đọc file lúc boot, đọc lại khi được yêu cầu, và không
    /// bao giờ để một file hỏng giết server.
    /// </summary>
    public sealed class ConfigService
    {
        private const string FILE_NAME = "game.json";

        // MissingMemberHandling.Error, KHÁC file map: game.json do người gõ tay. Gõ nhầm "Gravty" mà
        // bỏ qua trong im lặng thì trọng lực về mặc định còn người ta đi tìm bug trong MovementRules.
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>
        /// Bộ số hiện hành. Đọc property này ra BIẾN CỤC BỘ rồi dùng, đừng đọc nhiều lần trong một
        /// phép tính: reload thay nguyên object, nên hai lần đọc có thể rơi vào hai bộ khác nhau.
        /// </summary>
        public GameConfigData Current { get; private set; } = new GameConfigData();

        /// <summary>Luật thế giới đã quy ra tick, dựng lại mỗi lần Load. Chỗ gọi không phải tự quy đổi.</summary>
        public WorldRules World { get; private set; }

        public ConfigService()
        {
            Load();
        }

        /// <summary>
        /// Đọc lại file. Gọi lúc boot và mỗi lần bấm phím reload.
        ///
        /// Thay NGUYÊN object chứ không sửa từng field: gán reference là thao tác nguyên tử nên luồng
        /// tick hoặc thấy trọn bộ cũ, hoặc trọn bộ mới. Sửa tại chỗ thì nó có thể đọc được Gravity mới
        /// ghép với MaxFallSpeed cũ — một tổ hợp chưa từng tồn tại trong file nào.
        /// </summary>
        public void Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "Config", FILE_NAME);
            GameConfigData data;

            try
            {
                data = JsonConvert.DeserializeObject<GameConfigData>(File.ReadAllText(path), Settings)
                       ?? new GameConfigData();
            }
            // Bắt ĐÚNG hai loại lỗi dự kiến. catch (Exception) ở đây sẽ nuốt luôn NullReferenceException
            // của chính code này — và đó là thứ CLAUDE.md cấm.
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                Log.Warn($"Không đọc được {path.Red()}: {ex.Message}. " +
                         "Server chạy bằng GIÁ TRỊ MẶC ĐỊNH — số trong file KHÔNG có hiệu lực.");
                data = new GameConfigData();
            }

            Validate(data);

            Current = data;
            World = new WorldRules(data.World);

            Log.Info($"Config: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t · " +
                     $"aoi={data.Server.AoiRadiusX} · startMap={data.Server.StartingMapId}");
        }

        /// <summary>
        /// Kẹp từng trường về khoảng dùng được. Trả riêng trường hỏng về mặc định chứ không vứt cả
        /// file: một dòng sai không nên xoá sổ ba mươi dòng đúng.
        /// </summary>
        private static void Validate(GameConfigData data)
        {
            var fallback = new WorldRulesData();

            // Trần tuyệt đối cho mọi vận tốc: đi quá một ô trong một tick là vượt qua giả định của
            // phép kiểm va chạm ở Phase 10 (ngang và lên chỉ kiểm ĐIỂM CUỐI, xuống thì quét từng hàng
            // ô nhưng chỉ bảo đảm trong phạm vi một ô mỗi tick).
            float speedCap = MapGrid.CELL_SIZE / MovementRules.TICK_DT;

            data.World.Gravity = Clamp(data.World.Gravity, 0.001f, float.MaxValue, fallback.Gravity, "Gravity");
            data.World.MaxFallSpeed = Clamp(data.World.MaxFallSpeed, 0.001f, speedCap, fallback.MaxFallSpeed, "MaxFallSpeed");
            data.World.CoyoteSeconds = Clamp(data.World.CoyoteSeconds, 0f, 5f, fallback.CoyoteSeconds, "CoyoteSeconds");
            data.World.JumpBufferSeconds = Clamp(data.World.JumpBufferSeconds, 0f, 5f, fallback.JumpBufferSeconds, "JumpBufferSeconds");
            data.World.DropThroughSeconds = Clamp(data.World.DropThroughSeconds, 0f, 5f, fallback.DropThroughSeconds, "DropThroughSeconds");

            data.Server.AoiRadiusX = Clamp(data.Server.AoiRadiusX, 0.001f, float.MaxValue, 24f, "AoiRadiusX");
        }

        private static float Clamp(float value, float min, float max, float fallback, string field)
        {
            if (value >= min && value <= max)
                return value;

            // LA LỚN chứ không sửa im lặng: người vận hành phải biết số họ gõ đã bị từ chối, nếu không
            // họ sẽ đi tìm lý do vì sao "sửa rồi mà không thấy khác gì".
            Log.Warn($"Config {field.Red()} = {value} ngoài khoảng [{min}, {max}] — dùng {fallback}.");

            return fallback;
        }
    }
}
