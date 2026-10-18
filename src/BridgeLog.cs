using System.Collections.Generic;
using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Unity's Debug.Log is not safe to call from a background thread on the Unity 5 runtime
    /// Cities: Skylines ships. The HTTP accept and worker threads therefore buffer their
    /// messages here and the game thread drains them once per frame.
    /// </summary>
    public static class BridgeLog
    {
        private const int MaxBuffered = 200;

        private static readonly object gate = new object();
        private static readonly List<string> pending = new List<string>();

        /// <summary>Safe from any thread.</summary>
        public static void Queue(string message)
        {
            lock (gate)
            {
                if (pending.Count >= MaxBuffered)
                {
                    // Drop the oldest rather than grow without bound if the game thread stalls.
                    pending.RemoveAt(0);
                }
                pending.Add(message);
            }
        }

        /// <summary>Game thread only.</summary>
        public static void Drain()
        {
            string[] messages = null;

            lock (gate)
            {
                if (pending.Count == 0)
                {
                    return;
                }
                messages = pending.ToArray();
                pending.Clear();
            }

            for (int i = 0; i < messages.Length; i++)
            {
                Debug.Log(messages[i]);
            }
        }
    }
}
