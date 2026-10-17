using UnityEngine;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// A main-thread pump that also runs at the main menu.
    ///
    /// AgentBridgeThreading (an ICities ThreadingExtension) is only ticked by
    /// SimulationManager.Update through m_ThreadingWrapper, and the wrapper and its extensions are
    /// created by SimulationManager.CreateRelay when a level loads. At the main menu nothing drains
    /// AgentBridge.Queue, so a menu command queued there would just time out. This MonoBehaviour
    /// lives on its own DontDestroyOnLoad GameObject, created in IUserMod.OnEnabled (which the
    /// PluginManager calls on the main thread), so its Update runs on Unity's main thread in every
    /// scene: intro, main menu, loading, and in game.
    ///
    /// It drains a separate queue (MenuQueue) used only by the lifecycle routes, so the
    /// in-game queue's "cleared on unload" semantics are untouched.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly CommandQueue menuQueue = new CommandQueue();
        private static GameObject host;

        public static CommandQueue MenuQueue
        {
            get { return menuQueue; }
        }

        public static bool IsRunning
        {
            get { return host != null; }
        }

        /// <summary>Must be called on the main thread (IUserMod.OnEnabled is).</summary>
        public static void Ensure()
        {
            if (host != null)
            {
                return;
            }

            host = new GameObject("SkylinesAgentBridge.MainThreadDispatcher");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<MainThreadDispatcher>();
        }

        /// <summary>Must be called on the main thread (IUserMod.OnDisabled is).</summary>
        public static void Shutdown()
        {
            menuQueue.Clear();
            if (host != null)
            {
                Object.Destroy(host);
                host = null;
            }
        }

        private void Update()
        {
            menuQueue.Process(2);
        }
    }
}
