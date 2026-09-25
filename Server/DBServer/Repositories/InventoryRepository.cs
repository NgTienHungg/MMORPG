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

        /// <summary>
        /// Ghi TOÀN BỘ túi: xoá sạch rồi ghi lại.
        ///
        /// Trông thô, và nó thô thật — nhưng cách "đúng bài" (diff từng dòng, sinh INSERT/UPDATE/DELETE)
        /// là ~80 dòng với ba nhánh, mỗi nhánh một cách sai. Cái giá của cách này: itemId ĐỔI sau mỗi
        /// lần lưu, vì AUTOINCREMENT cấp số mới. Hôm nay chưa ai dựa vào itemId; ngày có log giao dịch
        /// hoặc đồ khoá theo id thì đây là chỗ phải sửa — và nó là một LỰA CHỌN, không phải sơ suất.
        ///
        /// TRANSACTION là bắt buộc, không phải cẩn thận thừa: giữa DELETE và INSERT mà process chết
        /// thì người chơi mất sạch túi. Một transaction biến "mất sạch" thành "không đổi gì".
        /// </summary>
        public async Task SaveAsync(InventorySaveRequest request, CancellationToken ct = default)
        {
            await using SqliteConnection connection = await _database.OpenAsync(ct);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

            await using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM inventory_item WHERE character_id = $characterId;";
                delete.Parameters.AddWithValue("$characterId", request.CharacterId);
                await delete.ExecuteNonQueryAsync(ct);
            }

            await using (SqliteCommand insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                                     INSERT INTO inventory_item (character_id, template_id, quantity, slot)
                                     VALUES ($characterId, $templateId, $quantity, $slot);
                                     """;

                // Dựng tham số MỘT LẦN rồi chỉ đổi giá trị trong vòng lặp: SQLite chuẩn bị lại câu
                // lệnh mỗi khi tập tham số đổi, và 30 lần chuẩn bị lại cho một lần lưu là lãng phí
                // không có lý do nào.
                insert.Parameters.AddWithValue("$characterId", request.CharacterId);

                SqliteParameter templateId = insert.Parameters.AddWithValue("$templateId", 0);
                SqliteParameter quantity = insert.Parameters.AddWithValue("$quantity", 0);
                SqliteParameter slot = insert.Parameters.AddWithValue("$slot", 0);

                foreach (InventoryRow row in request.Items)
                {
                    templateId.Value = row.TemplateId;
                    quantity.Value = row.Quantity;
                    slot.Value = row.Slot;
                    await insert.ExecuteNonQueryAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);
        }
    }
}
