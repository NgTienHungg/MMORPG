using HungNT;
using MMORPG.Client.Auth;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MMORPG.Client.Boot
{
    /// <summary>
    /// Container gốc của client. Mọi service dùng chung toàn game đăng ký tại đây.
    /// Đặt trên 1 GameObject trong scene Bootstrap, DontDestroyOnLoad.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private string _serverHost = "127.0.0.1";
        [SerializeField] private int _serverPort = 7778;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.InstallCore(); // com.hungnt.core

            // Địa chỉ server chỉ khai báo ở ĐÂY — ai cần thì inject NetworkSettings.
            builder.RegisterInstance(new NetworkSettings(_serverHost, _serverPort));

            builder.Register<ITransport, TcpTransport>(Lifetime.Singleton);
            builder.Register<NetDispatcher>(Lifetime.Singleton);
            builder.Register<NetService>(Lifetime.Singleton);

            // Mỗi nhóm handler mới thêm một dòng ở đây. Thiếu là lệnh của nhóm đó rơi vào hư không.
            // .As<INetHandlerGroup>() để NetDispatcher gom được; .AsSelf() để UI subscribe được event.
            builder.Register<SystemNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();

            // Ép VContainer inject vào NetworkProbe có sẵn trong scene (chạy ngay lúc build container).
            builder.RegisterComponentInHierarchy<NetworkProbe>();

            builder.Register<AuthApi>(Lifetime.Singleton);
            builder.Register<AuthNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();
            builder.RegisterComponentInHierarchy<LoginPresenter>();
        }
    }
}
