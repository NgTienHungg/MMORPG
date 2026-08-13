using System.Collections.Concurrent;

namespace MMORPG.GameServer
{
    public static class SessionRegistry
    {
        private static readonly ConcurrentDictionary<int, ClientSession> _sessions = new();

        public static int Count => _sessions.Count;

        public static void Add(ClientSession session)
        {
            _sessions[session.Id] = session;
        }

        public static void Remove(ClientSession session)
        {
            _sessions.TryRemove(session.Id, out _);
        }

        public static IReadOnlyCollection<ClientSession> All => _sessions.Values.ToList();
    }
}
