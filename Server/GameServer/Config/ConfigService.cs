using MemoryPack;
using MMORPG.ServerCore;
using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Item;
using Newtonsoft.Json;

namespace MMORPG.GameServer.Config
{
    /// <summary>
    /// Nguồn duy nhất của mọi con số vận hành phía server. Đọc file lúc boot, đọc lại khi được yêu
    /// cầu, và không bao giờ để một file hỏng giết server.
    ///
    /// Danh sách trong <see cref="Load"/> là danh sách của SERVER. Nó sẽ dài hơn của client — bảng
    /// tỉ lệ rơi đồ, bảng AI quái, bảng thưởng nhiệm vụ đều chỉ server đọc — và không có phép so nào
    /// ép hai danh sách bằng nhau; xem <see cref="ConfigFingerprint"/>.
    ///
    /// Hai loại file khác nhau đúng một điểm: hỏng thì làm gì. <c>game.json</c> về mặc định vì mọi
    /// trường của nó đều có mặc định hợp lệ; bảng dữ liệu giữ nguyên bản đang chạy vì "bảng mặc
    /// định" là bảng rỗng. Khác biệt ấy là một tham số <see cref="WhenBroken"/>, không phải hai hàm.
    ///
    /// Riêng bảng dữ liệu còn phân biệt boot với reload: lúc boot chưa có bản nào để giữ, nên file
    /// hỏng là <b>chết ngay</b> kèm tên file. Chỉ từ lần reload thứ nhất trở đi mới có nghĩa là
    /// "giữ nguyên".
    /// </summary>
    public sealed class ConfigService
    {
        /// <summary>Tên file loại A. Ở đây chứ không ở <see cref="ConfigFiles"/> vì client không bao giờ có nó.</summary>
        private const string GAME = "game";

        /// <summary>
        /// <c>MissingMemberHandling.Error</c>, khác file map: file trong Config/ do người gõ tay, nên
        /// gõ nhầm "Gravty" phải là lỗi chứ không phải một giá trị âm thầm về mặc định. Client dùng
        /// đúng bộ settings này — hai luật parse khác nhau trên cùng một file là một cách lệch.
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>
        /// Bộ số loại A hiện hành. Đọc ra biến cục bộ rồi dùng, đừng đọc nhiều lần trong một phép
        /// tính: reload thay nguyên object, nên hai lần đọc có thể rơi vào hai bộ khác nhau.
        /// </summary>
        public GameConfigData Current { get; private set; } = new();

        /// <summary>Lối tắt cho chỗ gọi hay dùng nhất. Không phải bản sao — vẫn đúng object trong <see cref="Current"/>.</summary>
        public WorldConfig World => Current.World;

        /// <summary>Đã qua lần nạp đầu chưa, tức là đã có bản đang chạy để giữ khi file hỏng chưa.</summary>
        private bool _booted;

        /// <summary>Nạp ngay lúc dựng: không có trạng thái "đã có service nhưng chưa có số".</summary>
        public ConfigService()
        {
            Load();

            // Đặt SAU Load(): mọi lần Load() từ đây là reload, và chỉ lúc đó "giữ nguyên bản đang
            // chạy" mới có nghĩa.
            _booted = true;
        }

        /// <summary>
        /// Đọc lại TẤT CẢ file config. Gọi lúc boot và mỗi lần bấm phím reload — một lần nạp, một
        /// phím, nên không có đường nào nạp lại cái này mà quên cái kia.
        ///
        /// <b>Thêm file mới thì thêm đúng một dòng ở đây.</b>
        /// </summary>
        public void Load()
        {
            LoadFile<GameConfigData>(GAME, ValidateGame, ApplyGame, WhenBroken.UseDefaults);
            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, ValidateCharacters, CharacterConfigContainer.Load);
            LoadTable<ItemTableData>(ConfigFiles.ITEMS, ValidateItems, ItemConfigContainer.Load);
        }

        //--------------------------------------------------------------------------------------------
        // Nạp file
        //--------------------------------------------------------------------------------------------

        /// <summary>Phải làm gì khi file hỏng — thứ duy nhất khác nhau giữa loại A và loại B.</summary>
        private enum WhenBroken
        {
            /// <summary>Dựng một object mặc định rồi dùng nó. Chỉ hợp lệ khi mọi trường đều có mặc định dùng được.</summary>
            UseDefaults,

            /// <summary>Không áp dụng gì cả, để nguyên thứ đang chạy. Lúc boot thì chưa có gì để giữ nên nó ném.</summary>
            KeepCurrent,
        }

        /// <summary>
        /// Đọc một file config, kiểm, rồi áp dụng. Dùng cho mọi file, kể cả <c>game.json</c>.
        ///
        /// Bốn tham số vì đó đúng là bốn thứ khác nhau giữa các file: đọc ở đâu, kẹp số thế nào, đổ
        /// đi đâu, hỏng thì sao. Phần còn lại giống hệt nhau nên nó ở đây một lần.
        /// </summary>
        /// <returns>File đã áp dụng, hoặc <c>null</c> khi reload hỏng và <see cref="WhenBroken.KeepCurrent"/>.</returns>
        /// <exception cref="InvalidOperationException">File loại B hỏng ngay ở lần nạp đầu.</exception>
        private TFile LoadFile<TFile>(string name, Action<TFile> validate, Action<TFile> apply, WhenBroken whenBroken)
            where TFile : class, IConfigFile, new()
        {
            string path = PathOf(name);

            try
            {
                var file = JsonConvert.DeserializeObject<TFile>(File.ReadAllText(path), Settings);

                // File rỗng ("{}") và file thiếu hẳn mảng dữ liệu có cùng một hậu quả: apply() sẽ thay
                // bảng đang chạy bằng một bảng 0 dòng, hỏng nặng hơn hẳn so với không nạp gì.
                if (file == null || file.RowCount == 0)
                    throw new JsonException("File rỗng — thiếu dữ liệu hoặc không có nội dung.");

                validate(file);

                // apply nằm TRONG try vì container ném khi bảng tự mâu thuẫn (hai dòng trùng id) —
                // lỗi dữ liệu, phải rơi vào cùng nhánh với file gõ sai cú pháp.
                apply(file);

                return file;
            }
            // Bắt đúng ba loại lỗi dự kiến: catch (Exception) ở đây sẽ nuốt luôn NullReferenceException
            // của chính code này.
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is InvalidOperationException)
            {
                if (whenBroken == WhenBroken.KeepCurrent)
                {
                    // Lúc boot chưa có bản đang chạy nào để giữ, nên "giữ nguyên" là giữ một bảng
                    // rỗng: lỗi sẽ chỉ lộ ra ở gói tin đầu tiên tra tới bảng, xa chỗ gây ra nó. Chết
                    // ngay tại đây, kèm tên file. Không Log.Error trước khi ném để cùng một câu
                    // không in ra hai lần.
                    if (!_booted)
                        throw new InvalidOperationException($"Không nạp được {path}: {ex.Message}", ex);

                    Log.Error($"Không nạp được {path.Red()}: {ex.Message}. GIỮ NGUYÊN bản đang chạy.");
                    return null;
                }

                Log.Warn($"Không đọc được {path.Red()}: {ex.Message}. " +
                         "Chạy bằng GIÁ TRỊ MẶC ĐỊNH — số trong file KHÔNG có hiệu lực.");

                // Vẫn validate bản mặc định: một mặc định biên dịch sẵn nằm ngoài khoảng cho phép là
                // lỗi lập trình, và nó phải lộ ra ở đây.
                var fallback = new TFile();

                validate(fallback);
                apply(fallback);

                return fallback;
            }
        }

        /// <summary>
        /// <see cref="LoadFile{TFile}"/> cộng một dòng log có vân tay, dành cho file mà client cũng đọc.
        ///
        /// Vân tay tính SAU khi apply vì apply gọi <c>Prepare()</c>: <c>ActionData</c> là struct toàn
        /// kiểu unmanaged nên mấy con tick nó điền vẫn nằm trong byte tuần tự hoá. Client băm ở đúng
        /// chỗ này nên hai số so được với nhau.
        /// </summary>
        private void LoadTable<TTable>(string name, Action<TTable> validate, Action<TTable> load)
            where TTable : class, IConfigFile, IMemoryPackable<TTable>, new()
        {
            TTable table = LoadFile(name, validate, load, WhenBroken.KeepCurrent);

            if (table == null)
                return;

            Log.Info($"Bảng {name.Cyan()}: {table.RowCount} dòng, version {table.Version}, " +
                     $"vân tay {ConfigFingerprint.Of(table):X8}");
        }

        /// <summary>
        /// Dựng đường dẫn cạnh file exe. <c>AppContext.BaseDirectory</c> chứ không phải thư mục hiện
        /// hành: đường dẫn tương đối tới gốc repo chỉ đúng trên đúng một máy. Đuôi <c>.json</c> thêm
        /// ở đây vì tên trong <see cref="ConfigFiles"/> không có đuôi.
        /// </summary>
        private static string PathOf(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, "Data", "Config", name + ".json");
        }

        //--------------------------------------------------------------------------------------------
        // Áp dụng — mỗi file một hàm, vì "đổ đi đâu" không generic hoá được
        //--------------------------------------------------------------------------------------------

        /// <summary>
        /// Thay nguyên object chứ không sửa từng field: gán reference là thao tác nguyên tử nên luồng
        /// tick hoặc thấy trọn bộ cũ, hoặc trọn bộ mới. Sửa tại chỗ thì nó đọc được Gravity mới ghép
        /// với MaxFallSpeed cũ — một tổ hợp chưa từng tồn tại trong file nào.
        /// </summary>
        private void ApplyGame(GameConfigData data)
        {
            Current = data;

            Log.Info($"Config: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t · " +
                     $"aoi={data.Server.AoiRadiusX} · startMap={data.Server.StartingMapId}");
        }

        //--------------------------------------------------------------------------------------------
        // Kiểm miền giá trị — mỗi file một hàm, và tất cả nằm ở SERVER
        //--------------------------------------------------------------------------------------------

        /// <summary>Kẹp từng trường về khoảng dùng được. Trả riêng trường hỏng về mặc định chứ không vứt cả file.</summary>
        private static void ValidateGame(GameConfigData data)
        {
            var fallback = new WorldConfig();

            data.World.Gravity = Clamp(data.World.Gravity, 0.001f, float.MaxValue, fallback.Gravity, "Gravity");
            data.World.MaxFallSpeed = Clamp(data.World.MaxFallSpeed, 0.001f, ConfigLimits.SPEED_CAP, fallback.MaxFallSpeed, "MaxFallSpeed");
            data.World.CoyoteSeconds = Clamp(data.World.CoyoteSeconds, 0f, 5f, fallback.CoyoteSeconds, "CoyoteSeconds");
            data.World.JumpBufferSeconds = Clamp(data.World.JumpBufferSeconds, 0f, 5f, fallback.JumpBufferSeconds, "JumpBufferSeconds");
            data.World.DropThroughSeconds = Clamp(data.World.DropThroughSeconds, 0f, 5f, fallback.DropThroughSeconds, "DropThroughSeconds");
            data.Server.AoiRadiusX = Clamp(data.Server.AoiRadiusX, 0.001f, float.MaxValue, 24f, "AoiRadiusX");
        }

        /// <summary>
        /// Kẹp bộ số của từng lớp nhân vật. Nằm ở server chứ không ở <c>CharacterConfigContainer.Load</c>
        /// vì client cũng gọi Load — client kẹp số của chính nó là sửa dữ liệu trong im lặng, và hai
        /// bên cùng chạy một giá trị khác với giá trị trong file.
        /// </summary>
        private static void ValidateCharacters(CharacterTableData table)
        {
            var fallback = new CharacterConfig();

            foreach (CharacterConfig config in table.Classes)
            {
                string tag = $"class {config.ClassId}";

                config.MoveSpeed = Clamp(config.MoveSpeed, 0.001f, ConfigLimits.SPEED_CAP - 0.001f, fallback.MoveSpeed, $"{tag}.MoveSpeed");
                config.JumpSpeed = Clamp(config.JumpSpeed, 0.001f, ConfigLimits.SPEED_CAP - 0.001f, fallback.JumpSpeed, $"{tag}.JumpSpeed");

                // Trần của ba trường thân người đến từ thuật toán va chạm, không phải từ cân bằng game
                // — xem ConfigLimits để biết mỗi con số đến từ đâu.
                config.BodyHalfWidth = Clamp(config.BodyHalfWidth, 0.001f, ConfigLimits.BODY_HALF_WIDTH_CAP,
                    fallback.BodyHalfWidth, $"{tag}.BodyHalfWidth");

                config.BodyHeight = Clamp(config.BodyHeight, 0.001f, ConfigLimits.BODY_HEIGHT_CAP,
                    fallback.BodyHeight, $"{tag}.BodyHeight");

                config.BodyHeightCrouch = Clamp(config.BodyHeightCrouch, 0.001f,
                    MathF.Min(config.BodyHeight, ConfigLimits.CROUCH_HEIGHT_CAP), fallback.BodyHeightCrouch,
                    $"{tag}.BodyHeightCrouch");
            }
        }

        /// <summary>Kẹp bảng item. Ngắn hơn bảng nhân vật vì bảng này có ít con số đi vào mô phỏng.</summary>
        private static void ValidateItems(ItemTableData table)
        {
            foreach (ItemConfig config in table.Items)
            {
                string tag = $"item {config.TemplateId}";

                // Id là KHOÁ: sai thì cả dòng vô dụng, không có mặc định nào lùi về được. Ném để
                // người sửa bảng biết ngay, cùng cách với trùng id.
                if (config.TemplateId <= 0)
                    throw new InvalidOperationException($"{tag}: TemplateId phải > 0.");

                if (string.IsNullOrWhiteSpace(config.Name))
                    throw new InvalidOperationException($"{tag}: thiếu Name.");

                // MaxStack = 0 là một ô bị bỏ trống trong file, không phải một lựa chọn thiết kế.
                config.MaxStack = (int)Clamp(config.MaxStack, 1f, 9999f, 1f, $"{tag}.MaxStack");
            }
        }

        /// <summary>Trả giá trị nếu nó nằm trong khoảng, ngược lại LA LỚN rồi trả mặc định.</summary>
        private static float Clamp(float value, float min, float max, float fallback, string field)
        {
            if (value >= min && value <= max)
                return value;

            // Không sửa im lặng: người vận hành phải biết số họ gõ đã bị từ chối, nếu không họ sẽ đi
            // tìm lý do vì sao "sửa rồi mà không thấy khác gì".
            Log.Warn($"Config {field.Red()} = {value} ngoài khoảng [{min}, {max}] — dùng {fallback}.");

            return fallback;
        }
    }
}
