using MMORPG.GameServer.Db;
using MMORPG.ServerCore;
using MMORPG.Shared.Db;
using MMORPG.Shared.Dto.Character;
using MMORPG.Shared.Dto.Db;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.World
{
    /// <summary>
    /// Nghiệp vụ vào / rời thế giới. Handler không chứa gì ngoài lời gọi vào đây.
    /// </summary>
    public sealed class CharacterService
    {
        private readonly DbClient _dbClient;
        private readonly WorldService _worldService;
        private readonly MapRegistry _maps;

        public CharacterService(DbClient dbClient, WorldService worldService, MapRegistry maps)
        {
            _dbClient = dbClient;
            _worldService = worldService;
            _maps = maps;
        }

        public async Task<EnterWorldResponse> EnterWorldAsync(ClientSession session)
        {
            // InWorld >= Authenticated nên MinState không chặn được lần gọi thứ hai — phải tự chặn.
            if (session.Entity != null)
                return new EnterWorldResponse { Success = false, Error = ErrorCode.CharacterInUse };

            // Khe thời gian giữa lúc session mới đăng nhập (đá session cũ) và lúc session cũ dọn xong.
            if (_worldService.TryGetByAccount(session.AccountId, out _))
            {
                Log.Warn($"{session.Tag} EnterWorld bị chặn: account {session.AccountId} " +
                         "đang có entity của session khác chưa dọn xong");
                return Fail(ErrorCode.CharacterInUse);
            }

            var result = await _dbClient.CallAsync<CharacterGetOrCreateRequest, CharacterGetOrCreateResponse>(
                DbCmd.CharacterGetOrCreate, new CharacterGetOrCreateRequest
                {
                    AccountId = session.AccountId,
                    Name = session.Username,
                    ClassId = WorldService.DEFAULT_CLASS_ID,
                    // Ba dòng này CHỈ dùng khi tạo nhân vật mới. Người chơi cũ thì DB trả về map và
                    // toạ độ của chính họ, và WorldService.Spawn tra map theo đúng row đó.
                    MapId = MapRegistry.STARTING_MAP_ID,
                    X = _maps.Starting.DefaultSpawn.X,
                    Y = _maps.Starting.DefaultSpawn.Y,
                }
            );

            if (result.Created)
            {
                Log.Info($"{session.Tag} Lần vào world đầu tiên — tạo nhân vật {result.Character.Name.Cyan()} " +
                         $"(id {result.Character.CharacterId.ToString().Green()})");
            }

            PlayerEntity entity = _worldService.Spawn(result.Character, session);
            session.MarkInWorld(entity);

            return new EnterWorldResponse
            {
                Success = true,
                EntityId = entity.EntityId,
                CharacterId = entity.CharacterId,
                Name = entity.Name,
                ClassId = entity.ClassId,
                Level = entity.Level,
                MapId = entity.MapId,
                X = entity.X,
                Y = entity.Y,
                ServerTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        /// <summary>
        /// Rời world: bỏ entity rồi lưu vị trí. Gọi cả khi logout chủ động lẫn khi mất kết nối —
        /// hai đường phải đi qua đúng một hàm, nếu không sớm muộn một đường sẽ quên lưu.
        /// </summary>
        public async Task LeaveWorldAsync(ClientSession session)
        {
            PlayerEntity entity = session.Entity;
            if (entity == null)
                return;

            session.MarkLeftWorld();
            _worldService.Despawn(entity);

            try
            {
                await _dbClient.CallAsync<CharacterSavePositionRequest, DbOkResponse>(
                    DbCmd.CharacterSavePosition, new CharacterSavePositionRequest
                    {
                        CharacterId = entity.CharacterId,
                        MapId = entity.MapId,
                        X = entity.X,
                        Y = entity.Y
                    }
                );
            }
            catch (DbUnavailableException ex)
            {
                // Mất vị trí của một lần chơi thì khó chịu, nhưng làm sập đường ngắt kết nối
                // thì tệ hơn nhiều: session không dọn được, entity treo lại trong world mãi mãi.
                Log.Warn($"Không lưu được vị trí của {entity.Name.Cyan()}: {ex.Message}");
            }
        }

        private EnterWorldResponse Fail(ErrorCode error)
        {
            return new EnterWorldResponse { Success = false, Error = error };
        }
    }
}
