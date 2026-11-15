using System.Collections.Generic;
using TieLiteDesktop.Core.Models;

namespace TieLiteDesktop.Core
{
    /// <summary>Session cache of followed user ids, used to seed profile follow buttons.</summary>
    public static class FollowCache
    {
        private static readonly HashSet<long> Following = new();

        public static bool IsFollowing(long uid) => uid != 0 && Following.Contains(uid);

        public static void Set(long uid, bool following)
        {
            if (uid == 0) return;
            lock (Following)
            {
                if (following) Following.Add(uid);
                else Following.Remove(uid);
            }
        }

        public static void AddRange(IEnumerable<UserInfo> users)
        {
            if (users == null) return;
            lock (Following)
            {
                foreach (var user in users)
                {
                    if (user?.Id > 0) Following.Add(user.Id);
                }
            }
        }
    }
}
