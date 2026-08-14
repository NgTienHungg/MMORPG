using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    public sealed class CharacterRepository
    {
        private const int SQLItE_CONSTRAINT_UNIQUE = 2067;

        private readonly Database _database;

        public CharacterRepository(Database database)
        {
            _database = database;
        }

        public async Task<CharacterGetOrCreateResponse> GetOrCreateCharacter(CharacterGetOrCreateRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);

            CharacterRow existing = await SelectByAccountAsync(connection, request.AccountId, ct);
            if (existing != null)
            {
                return new CharacterGetOrCreateResponse
                {
                    Created = false,
                    Character = existing
                };
            }

            bool created = true;

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.CommandText = """
                                     INSERT INTO character (account_id, name, class_id, map_id, pos_x, pos_y, created_at)
                                     VALUES ($accountId, $name, $classId, $mapId, $x, $y, datetime('now'));
                                     """;
                insert.Parameters.AddWithValue("$accountId", request.AccountId);
                insert.Parameters.AddWithValue("$name", request.Name);
                insert.Parameters.AddWithValue("$classId", request.ClassId);
                insert.Parameters.AddWithValue("$mapId", request.MapId);
                insert.Parameters.AddWithValue("$x", request.X);
                insert.Parameters.AddWithValue("$y", request.Y);

                try
                {
                    await insert.ExecuteNonQueryAsync(ct);
                }
                catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SQLItE_CONSTRAINT_UNIQUE)
                {
                    // Hai EnterWorld của cùng tài khoản chạy song song: cả hai SELECT thấy "chưa có"
                    // rồi cùng INSERT. UNIQUE(account_id) biến kẻ đến sau thành vô hại — chỉ việc
                    // đọc lại dòng kẻ đến trước vừa tạo. Cùng bài check-then-act của Phase 4.
                    created = false;
                }
            }

            CharacterRow row = await SelectByAccountAsync(connection, request.AccountId, ct);
            return new CharacterGetOrCreateResponse { Created = created, Character = row };
        }

        public async Task SavePositionAsync(CharacterSavePositionRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();

            command.CommandText = """
                                  UPDATE character SET map_id = $mapId, pos_x = $x, pos_y = $y
                                  WHERE id = $id;
                                  """;
            command.Parameters.AddWithValue("$id", request.CharacterId);
            command.Parameters.AddWithValue("$mapId", request.MapId);
            command.Parameters.AddWithValue("$x", request.X);
            command.Parameters.AddWithValue("$y", request.Y);

            await command.ExecuteNonQueryAsync(ct);
        }

        private async Task<CharacterRow> SelectByAccountAsync(SqliteConnection connection, long accountId, CancellationToken ct)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                                  SELECT id, account_id, name, class_id, level, exp, map_id, pos_x, pos_y FROM character
                                  WHERE account_id = $accountId;
                                  """;
            command.Parameters.AddWithValue("$accountId", accountId);

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return null;

            return new CharacterRow
            {
                CharacterId = reader.GetInt64(0),
                AccountId = reader.GetInt64(1),
                Name = reader.GetString(2),
                ClassId = reader.GetInt32(3),
                Level = reader.GetInt32(4),
                Exp = reader.GetInt64(5),
                MapId = reader.GetInt32(6),
                X = reader.GetFloat(7),
                Y = reader.GetFloat(8),
            };
        }
    }
}