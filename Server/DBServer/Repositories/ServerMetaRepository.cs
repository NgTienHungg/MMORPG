using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;

namespace MMORPG.DBServer.Repositories
{
    /// <summary>
    /// Truy cập bảng <c>server_meta</c>. Repository là nơi DUY NHẤT có chuỗi SQL —
    /// đó chính là thứ cho phép đổi database engine mà chỉ phải sửa tầng này.
    /// </summary>
    public sealed class ServerMetaRepository
    {
        private readonly Database _database;

        public ServerMetaRepository(Database database)
        {
            _database = database;
        }

        public async Task<string> GetAsync(string key, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            // Tham số hoá, không nội suy chuỗi. Ở đây `key` do GameServer đưa nên tưởng như an toàn,
            // nhưng thói quen phải đúng từ query đầu tiên — nhiều query khác nhận chuỗi do NGƯỜI CHƠI gõ.
            cmd.CommandText = "SELECT value FROM server_meta WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);

            object value = await cmd.ExecuteScalarAsync(ct);
            return value as string;
        }

        public async Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                              INSERT INTO server_meta (key, value) VALUES ($key, $value)
                              ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                              """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }
    }
}