using HungNT;
using MMORPG.Game.Scripts.Network;
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
            builder.RegisterComponentInHierarchy<NetworkProbe>();
        }
    }
}