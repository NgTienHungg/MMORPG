using Microsoft.Data.Sqlite;
using MMORPG.DBServer.Data;
using MMORPG.Shared.Dto.Db;

namespace MMORPG.DBServer.Repositories
{
    /// <summary>
    /// Chỉ SQL, không biết gì về game: không biết túi có bao nhiêu ô, không biết MaxStack là gì,
    /// không biết đồ nào dùng được. Nhờ vậy Phase 20 đổi SQLite sang MySQL chỉ phải sửa tầng này.
    /// </summary>
    public class InventoryRepository
    {
        private readonly Database _database;

        public InventoryRepository(Database database)
        {
            _database = database;
        }

        public async Task<InventoryLoadResponse> LoadAsync(InventoryLoadRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteCommand command = connection.CreateCommand();

            command.CommandText = """
                                  SELECT id, template_id, quantity, slot
                                  FROM inventory_item
                                  WHERE character_id == $characterId
                                  ORDER BY slot;
                                  """;
            command.Parameters.AddWithValue("$characterId", request.CharacterId);

            var rows = new List<InventoryRow>();

            await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                rows.Add(new InventoryRow
                {
                    ItemId = reader.GetInt64(0),
                    TemplateId = reader.GetInt32(1),
                    Quantity = reader.GetInt32(2),
                    Slot = reader.GetInt32(3),
                });
            }

            return new InventoryLoadResponse { Items = rows.ToArray() };
        }
    }
}
