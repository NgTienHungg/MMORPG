using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    public sealed class AccountRepository
    {
        private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

        private readonly Database _database;

        public AccountRepository(Database database)
        {
            _database = database;
        }

        public async Task<AccountGetResponse> GetByUsernameAsync(string username, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                              SELECT id, username, password_hash, salt, iterations, is_banned
                              FROM account WHERE username = $username;
                              """;
            cmd.Parameters.AddWithValue("$username", username);

            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync(ct);

            if (!await reader.ReadAsync(ct))
                return new AccountGetResponse { Found = false };

            return new AccountGetResponse
            {
                Found = true,
                AccountId = reader.GetInt64(0),
                Username = reader.GetString(1),
                PasswordHash = (byte[])reader.GetValue(2),
                Salt = (byte[])reader.GetValue(3),
                Iterations = reader.GetInt32(4),
                IsBanned = reader.GetInt32(5) != 0,
            };
        }

        public async Task<AccountCreateResponse> CreateAsync(AccountCreateRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """
                              INSERT INTO account (username, password_hash, salt, iterations, created_at)
                              VALUES ($username, $hash, $salt, $iterations, datetime('now'))
                              RETURNING id;
                              """;
            cmd.Parameters.AddWithValue("$username", request.Username);
            cmd.Parameters.AddWithValue("$hash", request.PasswordHash);
            cmd.Parameters.AddWithValue("$salt", request.Salt);
            cmd.Parameters.AddWithValue("$iterations", request.Iterations);

            try
            {
                long id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
                return new AccountCreateResponse { Created = true, AccountId = id };
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SQLITE_CONSTRAINT_UNIQUE)
            {
                // Để DB phát hiện trùng, KHÔNG "SELECT xem có chưa rồi mới INSERT".
                // Giữa hai câu lệnh đó có một khe thời gian; hai người đăng ký cùng tên
                // trong cùng một phần nghìn giây sẽ cùng thấy "chưa có" và cùng INSERT.
                // Ràng buộc UNIQUE là thứ duy nhất không có khe đó.
                return new AccountCreateResponse { Created = false };
            }
        }

        public async Task TouchLoginAsync(long accountId, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = """UPDATE account SET last_login_at = datetime('now') WHERE id = $id;""";
            cmd.Parameters.AddWithValue("$id", accountId);

            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
