using Microsoft.Data.Sqlite;
using MMORPG.ServerCore;

namespace MMORPG.DBServer.Data
{
    /// <summary>
    /// Nâng schema từ phiên bản hiện tại lên mới nhất, chạy lúc khởi động.
    ///
    /// Quy tắc bất di bất dịch: <b>migration đã chạy thì không bao giờ sửa nữa.</b> Cần đổi gì thì
    /// thêm migration mới. Sửa migration cũ nghĩa là máy bạn (chưa chạy nó) và máy đã chạy nó
    /// cho ra hai schema khác nhau mà không có gì báo.
    /// </summary>
    public static class Migrator
    {
        private static readonly (int Version, string Sql)[] _migrations =
        {
            (1, """
                CREATE TABLE server_meta (
                    key   TEXT PRIMARY KEY NOT NULL,
                    value TEXT NOT NULL
                );

                INSERT INTO server_meta (key, value) VALUES
                    ('server_name', 'local-dev'),
                    ('created_at',  datetime('now'));
                """),
        };

        public static async Task MigrateAsync(Database database, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await database.OpenAsync(ct);

            await using (SqliteCommand create = connection.CreateCommand())
            {
                create.CommandText = """
                                     CREATE TABLE IF NOT EXISTS schema_version (
                                         version    INTEGER PRIMARY KEY NOT NULL,
                                         applied_at TEXT NOT NULL
                                     );
                                     """;
                await create.ExecuteNonQueryAsync(ct);
            }

            int current;
            await using (SqliteCommand read = connection.CreateCommand())
            {
                read.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
                current = Convert.ToInt32(await read.ExecuteScalarAsync(ct));
            }

            foreach ((int version, string sql) in _migrations)
            {
                if (version <= current)
                    continue;

                // Mỗi migration nằm trọn trong một transaction: hoặc lên hẳn phiên bản mới,
                // hoặc không đổi gì cả. Không có trạng thái "lên được nửa chừng".
                await using SqliteTransaction tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

                await using (SqliteCommand apply = connection.CreateCommand())
                {
                    apply.Transaction = tx;
                    apply.CommandText = sql;
                    await apply.ExecuteNonQueryAsync(ct);
                }

                await using (SqliteCommand stamp = connection.CreateCommand())
                {
                    stamp.Transaction = tx;
                    stamp.CommandText =
                        "INSERT INTO schema_version (version, applied_at) VALUES ($v, datetime('now'));";
                    stamp.Parameters.AddWithValue("$v", version);
                    await stamp.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                Log.Info($"Áp dụng migration {version.ToString().Green()}.");
            }

            Log.Info($"Schema đang ở phiên bản {Math.Max(current, LatestVersion).ToString().Green()}.");
        }

        public static int LatestVersion => _migrations.Length == 0 ? 0 : _migrations[^1].Version;
    }
}
