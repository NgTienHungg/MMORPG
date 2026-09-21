using System;
using System.Collections.Generic;
using MMORPG.Shared.World.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Khai báo mọi thứ tool export cần biết về một map: lớp tilemap nào chứa lưới va chạm, tile nào
    /// mang nghĩa gì, map này là map số mấy, người chơi xuất hiện ở những đâu.
    ///
    /// Nằm trong Inspector chứ không hard-code trong tool: người vẽ map đổi tile đánh dấu hay dời điểm
    /// spawn thì không phải mở code ra sửa.
    ///
    /// Là MonoBehaviour runtime (không phải Editor script) để nó lưu được vào prefab; nhưng lúc chơi
    /// thì không ai đọc nó — client đọc file đã export, y như server.
    /// </summary>
    public sealed class MapCollisionSource : MonoBehaviour
    {
        /// <summary>Một điểm spawn đặt bằng tay trong Scene. Id là tên mà map khác sẽ trỏ tới.</summary>
        [Serializable]
        public struct SpawnMarker
        {
            public string Id;
            public Transform Point;
        }

        /// <summary>Một cổng đặt bằng tay trong Scene. Size là world unit, tâm ở Transform.</summary>
        [Serializable]
        public struct PortalMarker
        {
            public Transform Point;
            public Vector2 Size;
            public int ToMapId;
            public string ToSpawnId;
        }

        [SerializeField] private int _mapId = 1;
        [SerializeField] private string _mapName = "Map 1";
        [SerializeField] private Tilemap _collisionTilemap;

        [Header("Tile đánh dấu — mọi tile khác trên lớp Collision đều là lỗi")]
        [SerializeField] private TileBase _solidTile;
        [SerializeField] private TileBase _oneWayTile;

        [Header("Điểm spawn — cần ít nhất một điểm id \"default\"")]
        [SerializeField] private List<SpawnMarker> _spawns = new();

        [Header("Cổng sang map khác — để trống nếu map này chưa nối đi đâu")]
        [SerializeField] private List<PortalMarker> _portals = new();

        public int MapId => _mapId;
        public string MapName => _mapName;
        public Tilemap CollisionTilemap => _collisionTilemap;
        public TileBase SolidTile => _solidTile;
        public TileBase OneWayTile => _oneWayTile;
        public IReadOnlyList<SpawnMarker> Spawns => _spawns;
        public IReadOnlyList<PortalMarker> Portals => _portals;

        // Cổng và điểm spawn đều không có sprite, không có tile — không vẽ ra thì bạn đặt chúng bằng
        // trí tưởng tượng. Và sai một trong hai thì triệu chứng đều là "vào map rồi kẹt", loại lỗi
        // nhìn Inspector cả buổi không ra mà nhìn Scene một giây là thấy.
        private void OnDrawGizmos()
        {
            DrawSpawns();
            DrawPortals();
        }

        /// <summary>
        /// Điểm spawn: một vòng tròn ở chân nhân vật kèm id. Điểm "default" tô khác màu vì nó là điểm
        /// DUY NHẤT bắt buộc phải có, và cũng là chỗ mọi cổng trỏ sai tên sẽ rơi về.
        /// </summary>
        private void DrawSpawns()
        {
            foreach (SpawnMarker marker in _spawns)
            {
                if (marker.Point == null)
                    continue;

                Vector3 position = marker.Point.position;
                bool isDefault = marker.Id == MapGrid.DEFAULT_SPAWN_ID;

                Gizmos.color = isDefault ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.4f);
                Gizmos.DrawWireSphere(position, 0.35f);

                // Vạch dựng đứng: vòng tròn không cho biết nhân vật đứng ở đâu so với mặt sàn, mà đó
                // mới là thứ quyết định spawn có bị kẹt trong tường hay không.
                Gizmos.DrawLine(position, position + Vector3.up * 1.6f);

                DrawLabel(position + Vector3.up * 1.7f, string.IsNullOrWhiteSpace(marker.Id) ? "(chưa có id)" : marker.Id);
            }
        }

        /// <summary>Cổng: khối đặc mờ để thấy vùng phủ, viền để thấy đúng mép, nhãn để biết nó dẫn đi đâu.</summary>
        private void DrawPortals()
        {
            foreach (PortalMarker marker in _portals)
            {
                if (marker.Point == null)
                    continue;

                Vector3 position = marker.Point.position;
                var size = new Vector3(marker.Size.x, marker.Size.y, 0.1f);

                Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
                Gizmos.DrawCube(position, size);

                Gizmos.color = new Color(0f, 0.8f, 1f);
                Gizmos.DrawWireCube(position, size);

                DrawLabel(position + Vector3.up * (marker.Size.y * 0.5f + 0.3f),
                    $"→ map {marker.ToMapId} / {marker.ToSpawnId}");
            }
        }

        /// <summary>
        /// Chữ trong Scene view. Handles nằm trong UnityEditor nên phải cắt hẳn khỏi bản build bằng
        /// #if — bỏ đi là project chạy trong Editor bình thường rồi vỡ đúng lúc build player.
        /// </summary>
        private static void DrawLabel(Vector3 position, string text)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }
    }
}
