using System.Collections.Generic;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Core
{
    /// <summary>In-memory cache of users seen in lists, used when the profile API returns sparse data.</summary>
    public static class UserCache
    {
        private static readonly Dictionary<long, UserInfo> Map = new();
        private static readonly object Gate = new();

        public static void Put(UserInfo user)
        {
            if (user == null || user.Id <= 0) return;
            lock (Gate)
            {
                Map[user.Id] = user;
            }
        }

        public static UserInfo Get(long uid)
        {
            lock (Gate)
            {
                return Map.TryGetValue(uid, out var user) ? user : null;
            }
        }
    }
}
