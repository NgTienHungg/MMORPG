using System;
using System.Collections.Generic;
using System.IO;
using HungNT;
using MMORPG.Client.World;
using MMORPG.Shared.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Có cả "using System" (cần cho StringComparison) lẫn "using UnityEngine" thì cái tên trần Object nhập
// nhằng giữa System.Object và UnityEngine.Object — CS0104, và file không biên dịch. Một dòng alias là
// cách rẻ nhất; đừng gỡ nó ra cùng lúc với việc gõ Object.FindFirstObjectByType bên dưới.
using Object = UnityEngine.Object;

namespace MMORPG.Client.EditorTools
{
    /// <summary>
    /// Xuất lớp Tilemap "Collision" trong scene ra file map JSON mà cả server lẫn client cùng đọc.
    ///
    /// Tool này KHÔNG tự sinh JSON: nó dựng một MapGrid rồi nhờ MapFile.Write ghi. Tự nối chuỗi lấy là
    /// tạo ra bản mô tả định dạng thứ hai, và lệch giữa người-ghi với người-đọc thì không có bài test
    /// nào bắt được.
    /// </summary>
    public static class MapExporter
    {
        private const string OUTPUT_FOLDER = "Assets/Game/Resources/Maps";

        [MenuItem("Tools/MMORPG/Export Map")]
        public static void Export()
        {
            var source = Object.FindFirstObjectByType<MapCollisionSource>();

            if (source == null)
            {
                Fail("Không thấy MapCollisionSource nào trong scene đang mở.");
                return;
            }

            Tilemap tilemap = source.CollisionTilemap;

            if (tilemap == null || source.SolidTile == null || source.OneWayTile == null)
            {
                Fail("MapCollisionSource còn ô trống trong Inspector.");
                return;
            }

            if (!TryCollectSpawns(source, out List<SpawnPoint> spawns))
                return;

            if (!TryCollectPortals(source, out List<Portal> portals))
                return;

            // Hỏi khoá prefab TRƯỚC khi quét lưới: hỏng ở đây thì hỏng ngay, khỏi quét mấy trăm ô rồi
            // mới báo lỗi.
            if (!TryResolvePrefabKey(source, out string prefabKey))
                return;

            // Một phép kiểm bắt trọn ba lỗi: Grid bị dời, cellSize khác 1, object Collision có offset
            // cục bộ. Thiếu nó thì triệu chứng là "map lệch nửa ô" — mất cả buổi tối để lần ra.
            var probe = new Vector3Int(3, 5, 0);

            if (tilemap.CellToWorld(probe) != new Vector3(3f, 5f, 0f))
            {
                Fail($"Hệ toạ độ ô không trùng world: ô (3,5) rơi vào {tilemap.CellToWorld(probe)}. " +
                     "Grid và Tilemap phải ở (0,0,0) với cellSize = 1.");
                return;
            }

            // CompressBounds trước khi đọc: cellBounds giữ lại cả vùng từng vẽ rồi xoá, nên không nén
            // thì map phình ra hàng chục cột rỗng — và origin ghi trong file sẽ sai so với hình.
            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;

            int width = bounds.size.x;
            int height = bounds.size.y;
            var cells = new CellType[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var position = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                    TileBase tile = tilemap.GetTile(position);

                    if (tile == null)
                    {
                        cells[y * width + x] = CellType.Empty;
                    }
                    else if (tile == source.SolidTile)
                    {
                        cells[y * width + x] = CellType.Solid;
                    }
                    else if (tile == source.OneWayTile)
                    {
                        cells[y * width + x] = CellType.OneWay;
                    }
                    else
                    {
                        // Dừng hẳn thay vì "coi như rỗng": một viên tile lạc vào lớp Collision mà bị bỏ
                        // qua âm thầm là một lỗ trên sàn mà không ai nhìn thấy.
                        Fail($"Tile lạ ở ô ({position.x}, {position.y}): {tile.name}. " +
                             "Lớp Collision chỉ được chứa hai tile đã khai báo.");
                        return;
                    }
                }
            }

            var map = new MapGrid(source.MapId, source.MapName, prefabKey, bounds.xMin, bounds.yMin,
                width, height, spawns, portals, cells);

            Directory.CreateDirectory(OUTPUT_FOLDER);
            string path = $"{OUTPUT_FOLDER}/{string.Format(MapService.FILE_MAP_FORMAT, map.MapId)}.json";
            File.WriteAllText(path, MapFile.Write(map));

            // Không có dòng này thì file mới nằm trên đĩa nhưng Unity chưa biết, và Resources.Load vẫn
            // trả về nội dung cũ cho tới lần focus lại cửa sổ Editor.
            AssetDatabase.ImportAsset(path);

            DebugEx.Log($"[MapExporter] Đã ghi {path} — {width}×{height} ô, origin ({bounds.xMin}, {bounds.yMin}), " +
                        $"{spawns.Count} điểm spawn, prefab \"{prefabKey}\", checksum {map.Checksum():X8}");
            DebugEx.Log("[MapExporter] Nhớ build lại GameServer để file map sang được thư mục output của server.");
        }

        /// <summary>
        /// Gom danh sách điểm spawn từ Inspector, và chặn ba kiểu sai mà người điền tay hay mắc.
        /// Tool sinh dữ liệu thì phải khó tính ở đây — vì mọi thứ đọc file về sau đều tin nó.
        /// </summary>
        private static bool TryCollectSpawns(MapCollisionSource source, out List<SpawnPoint> spawns)
        {
            spawns = new List<SpawnPoint>();
            bool hasDefault = false;

            foreach (MapCollisionSource.SpawnMarker marker in source.Spawns)
            {
                if (marker.Point == null || string.IsNullOrWhiteSpace(marker.Id))
                {
                    Fail("Có một dòng trong danh sách Spawns còn thiếu Id hoặc Transform.");
                    return false;
                }

                // Trùng id thì map khác trỏ sang sẽ tới nhầm chỗ — và không ai biết là đã tới nhầm.
                foreach (SpawnPoint existing in spawns)
                {
                    if (existing.Id != marker.Id)
                        continue;

                    Fail($"Hai điểm spawn trùng id \"{marker.Id}\".");
                    return false;
                }

                if (marker.Id == MapGrid.DEFAULT_SPAWN_ID)
                    hasDefault = true;

                Vector3 position = marker.Point.position;
                spawns.Add(new SpawnPoint { Id = marker.Id, X = position.x, Y = position.y });
            }

            if (!hasDefault)
            {
                Fail($"Map phải có một điểm spawn id \"{MapGrid.DEFAULT_SPAWN_ID}\" — đó là chỗ người chơi vào lần đầu.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Khoá tài nguyên của prefab chứa hình map — hỏi thẳng Unity thay vì bắt người ta gõ tay vào
        /// Inspector. Gõ tay là tạo bản sao thứ hai của thứ Unity đã biết, và bản sao ấy sai lặng lẽ
        /// ngay lần đầu có người đổi tên prefab.
        ///
        /// "Assets/Game/Resources/Maps/Map1.prefab" → "Maps/Map1".
        /// </summary>
        private static bool TryResolvePrefabKey(MapCollisionSource source, out string prefabKey)
        {
            prefabKey = string.Empty;

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source.gameObject);

            if (string.IsNullOrEmpty(assetPath))
            {
                Fail("Map trong scene không phải một prefab instance (hoặc bạn đang mở Prefab Mode). " +
                     "Kéo prefab map vào scene rồi export từ scene đó.");
                return false;
            }

            const string RESOURCES_MARKER = "/Resources/";
            int start = assetPath.IndexOf(RESOURCES_MARKER, StringComparison.Ordinal);

            if (start < 0)
            {
                // Không chặn ở đây thì lỗi dời tới tận lúc chạy, dưới dạng Resources.Load trả về null
                // — xa chỗ gây ra nó cả một quãng, và không có gì chỉ về đây.
                Fail($"Prefab map nằm ngoài mọi thư mục Resources: {assetPath}. Resources.Load sẽ không thấy nó.");
                return false;
            }

            // Cắt phần trước "/Resources/" và đuôi ".prefab" — còn lại đúng chuỗi Resources.Load nhận.
            prefabKey = Path.ChangeExtension(assetPath.Substring(start + RESOURCES_MARKER.Length), null);
            return true;
        }

         /// <summary>
        /// Gom danh sách cổng từ Inspector. KHÔNG kiểm được ToSpawnId có tồn tại ở map đích không —
        /// map đích có thể chưa được export lần nào. Đó là phép kiểm của lúc CHẠY, và MapGrid.FindSpawn
        /// đã lùi về điểm mặc định thay vì ném.
        /// </summary>
        private static bool TryCollectPortals(MapCollisionSource source, out List<Portal> portals)
        {
            portals = new List<Portal>();

            foreach (MapCollisionSource.PortalMarker marker in source.Portals)
            {
                if (marker.Point == null)
                {
                    Fail("Có một dòng trong danh sách Portals còn thiếu Transform.");
                    return false;
                }

                // Cổng rộng hoặc cao 0 thì không ai bước vào được — và không có triệu chứng nào ngoài
                // "cái cổng đó không hoạt động", loại lỗi mất cả buổi để nghĩ ra chỗ mà nhìn.
                if (marker.Size.x <= 0f || marker.Size.y <= 0f)
                {
                    Fail($"Cổng tại {marker.Point.name} có Size = {marker.Size}. Cả hai chiều phải lớn hơn 0.");
                    return false;
                }

                if (marker.ToMapId == source.MapId)
                {
                    Fail($"Cổng tại {marker.Point.name} trỏ về chính map {source.MapId}.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(marker.ToSpawnId))
                {
                    Fail($"Cổng tại {marker.Point.name} chưa điền ToSpawnId.");
                    return false;
                }

                Vector3 position = marker.Point.position;

                portals.Add(new Portal
                {
                    X = position.x,
                    Y = position.y,
                    Width = marker.Size.x,
                    Height = marker.Size.y,
                    ToMapId = marker.ToMapId,
                    ToSpawnId = marker.ToSpawnId,
                });
            }

            return true;
        }

        private static void Fail(string message)
        {
            // Hai đường: dialog để người đang bấm menu thấy ngay, log để còn dấu vết mà đọc lại.
            EditorUtility.DisplayDialog("Export Map thất bại", message, "OK");
            DebugEx.LogError($"[MapExporter] {message}");
        }
    }
}
