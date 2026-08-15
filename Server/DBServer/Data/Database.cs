using Microsoft.Data.Sqlite;

namespace MMORPG.DBServer.Data
{
    /// <summary>
    /// Sở hữu chuỗi kết nối và việc mở kết nối. Mọi repository đi qua đây.
    ///
    /// Không giữ một <see cref="SqliteConnection"/> dùng chung: ADO.NET đã có connection pool,
    /// mở/đóng quanh mỗi thao tác là cách dùng đúng và tránh được việc hai request async
    /// cùng đạp lên một connection.
    /// </summary>
    public sealed class Database
    {
        private readonly string _connectionString;

        public Database(string filePath)
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            }.ToString();
        }

        public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            return connection;
        }

        /// <summary>
        /// Bật các PRAGMA cần thiết. Gọi một lần lúc khởi động.
        /// </summary>
        public async Task InitAsync(CancellationToken ct = default)
        {
            await using SqliteConnection connection = await OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            // WAL: cho phép đọc song song với một lượt ghi. Mặc định (journal DELETE) thì
            // một lượt ghi khoá toàn bộ file, mọi lượt đọc phải xếp hàng.
            // foreign_keys: SQLite mặc định TẮT ràng buộc khoá ngoại — không bật thì
            // "ON DELETE CASCADE" khai trong schema sẽ im lặng không chạy.
            cmd.CommandText = """
                              PRAGMA journal_mode = WAL;
                              PRAGMA synchronous = NORMAL;
                              PRAGMA foreign_keys = ON;
                              """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
