using MMORPG.GameServer.Net;
using MMORPG.GameServer.World;
using MMORPG.Shared.Net;

namespace MMORPG.GameServer.Handlers
{
    public static class CharacterHandler
    {
        public static CharacterService CharacterService { get; set; }

        [TcpHandler(NetCmd.EnterWorld, MinState = SessionState.Authenticated)]
        public static async Task<NetResult> OnEnterWorld(NetRequest req)
        {
            return NetResult.Ok(await CharacterService.EnterWorldAsync(req.Session));
        }
    }
}
