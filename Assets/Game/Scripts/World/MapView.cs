using HungNT;
using MMORPG.Shared.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MMORPG.Client.World
{
    /// <summary>
    /// Phần HÌNH của map: dựng prefab trang trí theo khoá ghi trong file map. Phần LUẬT (lưới va
    /// chạm) ở MapService — hai thứ đi cùng nhau nhưng không phải một, và đó chính là lý do file map
    /// chỉ mang một KHOÁ trỏ tới prefab chứ không mang cả hình.
    ///
    /// Map không còn nằm sẵn trong scene: người chơi đứng ở map nào là dữ liệu trong DB, nên hình
    /// chỉ dựng được sau khi server nói ra đó là map nào.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        private GameObject _current;
        private int _currentMapId;

        public void Show(MapGrid map)
        {
            if (_current != null && _currentMapId == map.MapId)
                return;

            // Toạ độ ô trong file LÀ toạ độ world, nên chuỗi cha của map phải là đơn vị — đúng điều
            // kiện MapExporter bắt lúc export. Lệch ở đây thì hình lệch khỏi luật mà KHÔNG có phép
            // kiểm nào kêu, vì server chẳng biết client vẽ ở đâu.
            if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity ||
                transform.lossyScale != Vector3.one)
            {
                this.LogError($"MapView phải ở (0,0,0), không xoay, scale 1 — đang ở " +
                              $"{transform.position}, scale {transform.lossyScale}.");
            }

            Clear();

            if (string.IsNullOrEmpty(map.PrefabKey))
            {
                this.LogWarning($"Map {map.MapId} không có PrefabKey — world sẽ trống. Export lại map.");
                return;
            }

            var prefab = Resources.Load<GameObject>(map.PrefabKey);

            if (prefab == null)
            {
                this.LogError($"Không thấy prefab \"{map.PrefabKey}\" trong Resources (map {map.MapId}).");
                return;
            }

            _current = Instantiate(prefab, transform);
            _current.transform.localPosition = Vector3.zero;
            _current.transform.localRotation = Quaternion.identity;
            _current.transform.localScale = Vector3.one;
            _current.name = $"Map_{map.MapId}_{map.Name}";
            _currentMapId = map.MapId;

            HideCollisionLayer(_current);
        }

        public void Clear()
        {
            if (_current == null)
                return;

            Destroy(_current);
            _current = null;
            _currentMapId = 0;
        }

        /// <summary>
        /// Lớp Collision là LUẬT, không phải hình — tắt renderer của nó ngay khi dựng.
        ///
        /// Làm bằng code chứ không bằng cách nhớ tắt trong prefab: bạn PHẢI bật nó lên mới vẽ map
        /// được, và thứ gì phải nhớ bật/tắt bằng tay thì sớm muộn có một lần quên. Hỏi
        /// MapCollisionSource chứ không dò theo tên object — nó đã khai báo sẵn tilemap nào là lớp
        /// va chạm, và đó là cùng cái khai báo mà tool export đọc.
        /// </summary>
        private void HideCollisionLayer(GameObject mapObject)
        {
            var source = mapObject.GetComponentInChildren<MapCollisionSource>(true);

            if (source == null || source.CollisionTilemap == null)
                return;

            var tilemapRenderer = source.CollisionTilemap.GetComponent<TilemapRenderer>();

            if (tilemapRenderer != null)
                tilemapRenderer.enabled = false;
        }
    }
}
