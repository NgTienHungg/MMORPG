using MMORPG.ServerCore;
using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Map;
using MMORPG.Shared.World.Movement;
using Newtonsoft.Json;

namespace MMORPG.GameServer
{
    /// <summary>
    /// Nguồn DUY NHẤT của mọi con số vận hành. Đọc file lúc boot, đọc lại khi được yêu cầu, và không
    /// bao giờ để một file hỏng giết server.
    /// </summary>
    public sealed class ConfigService
    {
        private const string GAME_FILE = "game.json";

        private const string CHARACTERS_FILE = "characters.json";

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
        public GameConfigData Current { get; private set; } = new();

        /// <summary>Lối tắt cho chỗ gọi hay dùng nhất. Không phải bản sao — vẫn đúng object trong Current.</summary>
        public WorldConfig World => Current.World;

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
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "Config", GAME_FILE);
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

            Log.Info($"Config: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t · " +
                     $"aoi={data.Server.AoiRadiusX} · startMap={data.Server.StartingMapId}");

            // Bảng nhân vật (Bước 2) đi cùng đường, và cố ý nằm cùng hàm này: hai file, một lần nạp,
            // một phím R. Tách ra hai hàm là mở đường cho "nạp lại cái này mà quên cái kia".
            LoadCharacters();
        }

        /// <summary>
        /// Kẹp từng trường về khoảng dùng được. Trả riêng trường hỏng về mặc định chứ không vứt cả
        /// file: một dòng sai không nên xoá sổ ba mươi dòng đúng.
        /// </summary>
        private static void Validate(GameConfigData data)
        {
            var fallback = new WorldConfig();

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

        /// <summary>
        /// Bảng nhân vật (Bước 2). Hỏng thì GIỮ NGUYÊN bảng đang chạy thay vì về mặc định — khác hẳn
        /// game.json, và khác vì một lý do: ở đây không có "giá trị mặc định an toàn" nào. Một bảng
        /// nhân vật rỗng nghĩa là mọi người chơi mất hết bộ số, tức là server sống mà không ai chơi
        /// được. Giữ bảng cũ thì ít nhất người đang online không bị gì.
        /// </summary>
        private void LoadCharacters()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "Config", CHARACTERS_FILE);

            try
            {
                var table = JsonConvert.DeserializeObject<CharacterTableData>(File.ReadAllText(path), Settings);

                if (table == null || table.Classes.Length == 0)
                    throw new JsonException("Bảng rỗng — thiếu mảng \"Classes\".");

                ValidateCharacters(table);
                CharacterConfigContainer.Load(table);

                Log.Info($"Bảng nhân vật: {table.Classes.Length} lớp, checksum {CharacterConfigContainer.Checksum:X8}");
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException)
            {
                Log.Error($"Không nạp được {path.Red()}: {ex.Message}. " +
                          "GIỮ NGUYÊN bảng đang chạy.");
            }
        }

        /// <summary>
        /// Kẹp bộ số của từng lớp. Nằm ở SERVER chứ không ở CharacterProfiles.Load, vì client cũng gọi
        /// Load — và client không có quyền phán xét dữ liệu server gửi xuống, nó chỉ có quyền tin. Đặt
        /// phép kiểm vào Load là để client âm thầm sửa số của server, tức tạo ra đúng cái lệch mà cả
        /// phase này đang chống.
        /// </summary>
        private static void ValidateCharacters(CharacterTableData table)
        {
            var fallback = new CharacterConfig();
            float speedCap = MapGrid.CELL_SIZE / MovementRules.TICK_DT;

            foreach (CharacterConfig profile in table.Classes)
            {
                string tag = $"class {profile.ClassId}";

                profile.MoveSpeed = Clamp(profile.MoveSpeed, 0.001f, speedCap - 0.001f, fallback.MoveSpeed, $"{tag}.MoveSpeed");
                profile.JumpSpeed = Clamp(profile.JumpSpeed, 0.001f, speedCap - 0.001f, fallback.JumpSpeed, $"{tag}.JumpSpeed");

                // < 0.5 chứ không <= : rộng đúng nửa ô là vừa khít khe 1 ô, và "vừa khít" trong số
                // thực dấu phẩy động nghĩa là lúc lọt lúc không.
                profile.BodyHalfWidth = Clamp(profile.BodyHalfWidth, 0.001f, MapGrid.CELL_SIZE * 0.5f - 0.001f,
                    fallback.BodyHalfWidth, $"{tag}.BodyHalfWidth");

                // Trần 2.0 đến từ OverlapsSolid: nó quét ba mức cao, và ba mức chỉ phủ kín khi khoảng
                // cách giữa hai mức nhỏ hơn cạnh ô. Cao hơn 2.0 là có ô lọt qua khe kiểm.
                profile.BodyHeight = Clamp(profile.BodyHeight, 0.001f, 2f, fallback.BodyHeight, $"{tag}.BodyHeight");

                profile.BodyHeightCrouch = Clamp(profile.BodyHeightCrouch, 0.001f,
                    MathF.Min(profile.BodyHeight, MapGrid.CELL_SIZE), fallback.BodyHeightCrouch, $"{tag}.BodyHeightCrouch");
            }
        }
    }
}
