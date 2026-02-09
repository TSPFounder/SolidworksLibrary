using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolidworksLibrary
{
    public static class SessionManager
    {
        private static readonly ConcurrentDictionary<string, SwSession> _sessions
            = new ConcurrentDictionary<string, SwSession>();

        public static string Create(bool visible)
        {
            var id = Guid.NewGuid().ToString("N");
            var s = new SwSession();
            s.Connect(visible);
            _sessions[id] = s;
            return id;
        }

        public static string InsertBossExtrudeBlind(string sessionId, double depth)
        {
            return _sessions[sessionId].InsertBossExtrudeBlind(depth);
        }

        public static void Close(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var s))
                s.Dispose();
        }
    }

}
