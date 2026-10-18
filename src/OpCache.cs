using System.Collections.Generic;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Idempotency for composite commands. An agent that retries after a client-side timeout
    /// must not end up with two overlapping grids, so a completed operation is remembered by
    /// its opId for the rest of the session and replayed instead of re-executed.
    /// </summary>
    public static class OpCache
    {
        private const int MaxEntries = 200;

        private static readonly object gate = new object();
        private static readonly Dictionary<string, string> results = new Dictionary<string, string>();
        private static readonly List<string> order = new List<string>();

        public static bool TryGet(string opId, out string json)
        {
            json = null;
            if (opId == null || opId.Length == 0)
            {
                return false;
            }

            lock (gate)
            {
                return results.TryGetValue(opId, out json);
            }
        }

        public static void Store(string opId, string json)
        {
            if (opId == null || opId.Length == 0)
            {
                return;
            }

            lock (gate)
            {
                if (!results.ContainsKey(opId))
                {
                    order.Add(opId);
                }
                results[opId] = json;

                while (order.Count > MaxEntries)
                {
                    results.Remove(order[0]);
                    order.RemoveAt(0);
                }
            }
        }

        public static void Clear()
        {
            lock (gate)
            {
                results.Clear();
                order.Clear();
            }
        }
    }
}
