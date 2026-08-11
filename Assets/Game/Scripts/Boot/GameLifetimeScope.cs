using HungNT;
using MMORPG.Client.Network;
using MMORPG.Client.Network.Handlers;
using MMORPG.Game.Scripts.Network;
using MMORPG.Scripts.Network;
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
        protected override void Configure(IContainerBuilder builder)
        {
            builder.InstallCore(); // com.hungnt.core

            builder.Register<ITransport, TcpTransport>(Lifetime.Singleton);
            builder.Register<NetService>(Lifetime.Singleton);
            builder.Register<NetDispatcher>(Lifetime.Singleton);

            // Mỗi nhóm handler mới thêm một dòng ở đây.
            builder.Register<SystemNetHandler>(Lifetime.Singleton).AsSelf().As<INetHandlerGroup>();

            // test network
            builder.RegisterComponentInHierarchy<NetworkProbe>();
        }
    }
}