using System;
using HungNT;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Item;
using Newtonsoft.Json;
using UnityEngine;

namespace MMORPG.Client.Config
{
    /// <summary>
    /// Chỗ duy nhất phía client nạp config, và cửa duy nhất để phần còn lại của client hỏi "luật thế
    /// giới đang là gì". Đối ứng của <c>MMORPG.GameServer.Config.ConfigService</c>.
    ///
    /// Danh sách trong <see cref="LoadTables"/> là danh sách của CLIENT, không phải bản sao của
    /// server: server có bảng tỉ lệ rơi đồ và bảng AI quái mà client không đọc tới, còn client sẽ có
    /// bảng thoại và mô tả kỹ năng mà server không cần.
    ///
    /// Bảng dữ liệu thì client đọc file của chính nó ngay lúc khởi động, nên tên và icon item hiện
    /// được trước khi vào world. Luật thế giới (<see cref="World"/>) thì server gửi, vì người vận
    /// hành chỉnh nó giữa hai lần restart mà không patch client.
    /// </summary>
    public sealed class ConfigService
    {
        /// <summary>Thư mục trong Resources. Cùng nguồn với bản server đọc — xem GameServer.csproj.</summary>
        private const string RESOURCE_FOLDER = "Config";

        /// <summary>
        /// Đúng bộ settings của server: hai bên đọc cùng một file bằng hai luật parse khác nhau là
        /// cùng byte vào, hai object khác nhau ra.
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        /// <summary>
        /// Luật thế giới của phiên này. Có mặc định để code chạm vào nó trước khi vào world không
        /// phải kiểm null — nhưng <see cref="IsInWorld"/> mới là thứ nói nó đã thật hay chưa.
        /// </summary>
        public WorldConfig World { get; private set; } = new WorldConfig();

        /// <summary>
        /// Đã nhận luật thế giới từ server chưa. Sai nghĩa là <see cref="World"/> đang là mặc định
        /// biên dịch sẵn — dùng nó để dự đoán là tự tạo ra rubber-band.
        /// </summary>
        public bool IsInWorld { get; private set; }

        /// <summary>Nạp bảng ngay lúc VContainer dựng container: trước màn hình login, trước mọi gói tin.</summary>
        public ConfigService()
        {
            LoadTables();
        }

        /// <summary>
        /// <b>Thêm bảng mới thì thêm đúng một dòng ở đây.</b>
        ///
        /// Nạp hết lúc khởi động là lựa chọn của hôm nay, không phải ràng buộc: hai bảng đọc xong
        /// trong vài mili giây. Ngày cần tải lười hoặc tải từ xa thì chỗ phải sửa là
        /// <see cref="LoadTable{TTable}"/>, và không ai khác biết.
        /// </summary>
        private void LoadTables()
        {
            LoadTable<CharacterTableData>(ConfigFiles.CHARACTERS, CharacterConfigContainer.Load);
            LoadTable<ItemTableData>(ConfigFiles.ITEMS, ItemConfigContainer.Load);
        }

        /// <summary>
        /// Đọc một bảng từ Resources rồi nạp vào container của nó. Cùng tham số như bên server trừ
        /// hàm validate, và đó là khác biệt cố ý: phép kẹp miền giá trị chỉ chạy ở server.
        ///
        /// Client kẹp số của chính nó là client âm thầm sửa dữ liệu — một file có
        /// <c>MoveSpeed = 999</c> sẽ thành 5 ở cả hai bên và không ai phát hiện ra. Client băm bản
        /// thô, server băm bản đã kẹp, hai dòng log lệch nhau, và người ta đi sửa file.
        /// </summary>
        private void LoadTable<TTable>(string name, Action<TTable> load)
            where TTable : class, IConfigFile, MemoryPack.IMemoryPackable<TTable>
        {
            string path = $"{RESOURCE_FOLDER}/{name}";

            // Không có đuôi .json trong đường dẫn: Resources.Load luôn bỏ phần đuôi file.
            var asset = Resources.Load<TextAsset>(path);

            if (asset == null)
            {
                this.LogError($"Không thấy Resources/{path}.json — bảng {name} sẽ rỗng, và mọi chỗ tra nó sẽ hỏng.");
                return;
            }

            try
            {
                var table = JsonConvert.DeserializeObject<TTable>(asset.text, Settings);

                if (table == null || table.RowCount == 0)
                    throw new JsonException("Bảng rỗng — thiếu mảng dữ liệu hoặc file không có nội dung.");

                load(table);

                // Băm SAU load, đúng chỗ server băm. Lệch thời điểm thì hai con số thôi so được với
                // nhau, mà chẳng có gì báo là chúng đã thôi so được.
                this.Log($"Bảng {name}: {table.RowCount} dòng, version {table.Version}, " +
                         $"vân tay {ConfigFingerprint.Of(table):X8}");
            }
            // Cùng danh sách với server. InvalidOperationException vì container ném nó khi bảng tự
            // mâu thuẫn (hai dòng trùng id).
            //
            // Khác server ở chỗ client KHÔNG chết: bảng giữ nguyên (rỗng, nếu đây là lần nạp đầu) và
            // lỗi lộ ra ở lần tra đầu tiên. Server chặn boot vì một bảng hỏng nghĩa là không ai chơi
            // được và người vận hành phải biết ngay; ở client thì chỉ máy đó hỏng, và log là chỗ đúng.
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
            {
                this.LogError($"Bảng {name} hỏng: {ex.Message}");
            }
        }

        /// <summary>
        /// Nhận luật thế giới. Gọi ngay khi nhận <c>EnterWorldResponse</c>, trước khi spawn bất cứ thứ gì.
        ///
        /// Trả false chỉ có một lý do, và nó không phải lỗi dữ liệu: gói thiếu hẳn khối World, tức
        /// hai bên đang chạy hai bản MMORPG.Shared khác nhau.
        /// </summary>
        public bool Apply(EnterWorldResponse response)
        {
            if (response.World == null)
            {
                this.LogError("Gói EnterWorld thiếu WorldConfig — server và client đang chạy hai bản " +
                              "MMORPG.Shared khác nhau? Build lại Server/Shared.");
                return false;
            }

            World = response.World;
            IsInWorld = true;

            this.Log($"Luật thế giới: gravity={World.Gravity} maxFall={World.MaxFallSpeed} " +
                     $"coyote={World.CoyoteTicks}t jumpBuf={World.JumpBufferTicks}t drop={World.DropThroughTicks}t");

            return true;
        }

        /// <summary>
        /// Quên luật thế giới khi rời world. Cố tình không xoá container: bảng đọc từ file của client
        /// nên chúng đúng cả khi không ở trong world, và một túi đồ đang mở vẫn cần tra tên item.
        /// </summary>
        public void Clear()
        {
            World = new WorldConfig();
            IsInWorld = false;
        }
    }
}
