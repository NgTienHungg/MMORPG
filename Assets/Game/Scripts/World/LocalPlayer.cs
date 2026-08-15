using MMORPG.Shared.Dto.Character;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Bản sao dữ liệu nhân vật của CHÍNH mình, do server gửi xuống.
    ///
    /// Đây là cache chỉ-đọc, không phải nguồn sự thật. Không có setter công khai:
    /// mọi thay đổi đều đi qua <see cref="Apply"/> và chỉ được gọi từ handler nhận gói của server.
    /// Ngày nào có `player.Level++` ở đâu đó trong code client là ngày golden rule #2 bị phá.
    /// </summary>
    public sealed class LocalPlayer
    {
        public bool IsInWorld { get; private set; }
        public int EntityId { get; private set; }
        public long CharacterId { get; private set; }
        public string Name { get; private set; }
        public int ClassId { get; private set; }
        public int Level { get; private set; }
        public int MapId { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }

        public void Apply(EnterWorldResponse response)
        {
            IsInWorld = true;
            EntityId = response.EntityId;
            CharacterId = response.CharacterId;
            Name = response.Name;
            ClassId = response.ClassId;
            Level = response.Level;
            MapId = response.MapId;
            X = response.X;
            Y = response.Y;
        }

        public void Clear()
        {
            IsInWorld = false;
            EntityId = 0;
            CharacterId = 0;
            Name = string.Empty;
        }
    }
}
