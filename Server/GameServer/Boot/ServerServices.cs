using MMORPG.ServerCore;

namespace MMORPG.GameServer.Boot
{
    /// <summary>
    /// Sổ tra service của GameServer — đối ứng của <c>GameLifetimeScope</c> bên client. Static vì
    /// handler của server là hàm <c>static</c> (xem <c>TcpDispatcher</c>), nên không có chỗ nào nhét
    /// constructor injection vào.
    ///
    /// Đây là mẫu SERVICE LOCATOR với cái giá thật của nó: nhìn chữ ký hàm không biết nó cần gì. Chấp
    /// nhận giá đó ở ĐÚNG một chỗ — biên giới giữa dispatch table static và các service instance;
    /// mọi service khác nhận phụ thuộc qua constructor. Đăng ký hết trong <see cref="ServerBootstrap"/>
    /// lúc boot, gọi <see cref="Seal"/>, rồi từ đó chỉ còn đọc — đó là thứ giữ cho sổ này an toàn
    /// khi nhiều luồng đọc song song.
    /// </summary>
    public static class ServerServices
    {
        /// <summary>Sổ tra chính: một kiểu, một instance.</summary>
        private static readonly Dictionary<Type, object> _byType = new();

        /// <summary>Thứ tự đăng ký, để lúc tắt thì dispose ngược lại — A đăng ký sau B thường là A dùng B.</summary>
        private static readonly List<object> _order = new();

        /// <summary>Đã đóng sổ chưa. Đăng ký sau khi đóng là ném.</summary>
        private static bool _sealed;

        /// <summary>Thêm một service vào sổ và trả lại chính nó, để chỗ gọi xâu chuỗi được.</summary>
        public static T Register<T>(T service) where T : class
        {
            if (_sealed)
                throw new InvalidOperationException($"Đăng ký {typeof(T).Name} sau khi đã Seal(). Mọi service phải dựng xong trong ServerBootstrap.");

            if (service == null)
                throw new ArgumentNullException(nameof(service));

            // Đăng ký hai lần cùng một kiểu là dấu hiệu của hai composition root, hoặc một dòng copy
            // sót. Cái nào thắng là chuyện của thứ tự dòng, tức là không ai kiểm được — ném ngay.
            if (!_byType.TryAdd(typeof(T), service))
                throw new InvalidOperationException($"{typeof(T).Name} đã được đăng ký rồi.");

            _order.Add(service);

            return service;
        }

        /// <summary>
        /// Service đã đăng ký. Ném khi không có — và ném là hành vi đúng: thiếu một service nghĩa là
        /// server đang chạy dở dang, và biết ngay lúc gói tin đầu tiên chạm tới nó vẫn tốt hơn một
        /// NullReferenceException ở tầng sâu hơn năm phút sau.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object service))
                return (T)service;

            throw new InvalidOperationException($"Chưa đăng ký {typeof(T).Name}. Thêm một dòng ServerServices.Register vào ServerBootstrap.Build().");
        }

        /// <summary>Như <see cref="Get{T}"/> nhưng không ném — dành cho đường dọn dẹp, nơi service có thể chưa kịp dựng.</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>Đóng sổ. Từ đây chỉ còn đọc, nên nhiều luồng đọc song song không cần khoá gì.</summary>
        public static void Seal()
        {
            _sealed = true;
            Log.Info($"Đã đăng ký {_byType.Count.ToString().Green()} service.");
        }

        /// <summary>
        /// Dọn theo chiều ngược thứ tự đăng ký. Bọc từng cái trong try riêng: một service ném lúc
        /// đóng không được phép chặn những cái sau nó — lúc tắt thì dọn được bao nhiêu tốt bấy nhiêu.
        /// </summary>
        public static async ValueTask ShutdownAsync()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                object service = _order[i];

                try
                {
                    switch (service)
                    {
                        case IAsyncDisposable asyncDisposable:
                            await asyncDisposable.DisposeAsync();
                            break;

                        case IDisposable disposable:
                            disposable.Dispose();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Lỗi khi đóng {service.GetType().Name}");
                }
            }

            _order.Clear();
            _byType.Clear();
            _sealed = false;
        }
    }
}
