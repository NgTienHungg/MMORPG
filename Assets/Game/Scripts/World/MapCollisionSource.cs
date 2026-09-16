using System;
using System.Collections.Generic;
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

        // Cổng không có sprite, không có tile — không vẽ ra thì bạn đặt nó bằng trí tưởng tượng.
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);

            foreach (PortalMarker marker in _portals)
            {
                if (marker.Point == null)
                    continue;

                Gizmos.DrawCube(marker.Point.position, new Vector3(marker.Size.x, marker.Size.y, 0.1f));
            }
        }
    }
}
