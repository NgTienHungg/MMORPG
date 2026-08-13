namespace MMORPG.Client.Network
{
    /// <summary>
    /// Nguồn DUY NHẤT cho địa chỉ GameServer phía client — chỉnh trong Inspector của GameLifetimeScope.
    /// Đừng thêm [SerializeField] host/port vào MonoBehaviour khác: giá trị serialize bị chôn rải rác
    /// trong scene, đổi một chỗ không kéo các chỗ còn lại theo được.
    /// </summary>
    public sealed class NetworkSettings
    {
        public string Host { get; }
        public int Port { get; }

        public NetworkSettings(string host, int port)
        {
            Host = host;
            Port = port;
        }
    }
}
