using UnityEngine;

namespace SkylinesAgentBridge
{
    public sealed class AgentBridge
    {
        public const int DefaultPort = 32123;

        private static readonly AgentBridge singleton = new AgentBridge();
        private readonly CommandQueue queue = new CommandQueue();
        private ApiServer server;
        private bool levelLoaded;

        public static AgentBridge Instance
        {
            get { return singleton; }
        }

        public bool LevelLoaded
        {
            get { return levelLoaded; }
        }

        public CommandQueue Queue
        {
            get { return queue; }
        }

        /// <summary>
        /// Called when the mod is enabled, before any city exists. Starting the listener here
        /// rather than at level load means /health answers from the main menu, so an agent can
        /// wait for the bridge instead of retrying against a refused connection and guessing
        /// whether the mod is even installed.
        /// </summary>
        public void OnEnabled()
        {
            EnsureServer();
        }

        public void OnDisabled()
        {
            levelLoaded = false;
            CaptureCommands.Cancel();
            queue.Clear();
            OpCache.Clear();
            ChatPanel.Destroy();

            // Release any /chat/inbox long-polls so their worker threads end with the server.
            ChatStore.Instance.WakeAll();

            if (server != null)
            {
                server.Stop();
                server = null;
            }
        }

        public void OnLevelLoaded()
        {
            levelLoaded = true;
            EnsureServer();
            AgentBridgeNotifier.Notify("API ready: Skylines Agent Bridge");
            ChatPanel.Create();
            Debug.Log("[SkylinesAgentBridge] Level loaded. API bridge is ready.");
        }

        public void OnLevelUnloading()
        {
            levelLoaded = false;
            CaptureCommands.Cancel();
            queue.Clear();

            // Entity ids are only meaningful within one city, so a cached manifest from the
            // previous save must never be replayed against the next one.
            OpCache.Clear();

            AgentBridgeNotifier.Destroy();
            ChatPanel.Destroy();
            Debug.Log("[SkylinesAgentBridge] Level unloading. Pending API commands cleared.");
        }

        public void ProcessGameThreadQueue(float realTimeDelta)
        {
            queue.Process(4);
            CaptureCommands.Update();
            BridgeLog.Drain();
            AgentBridgeNotifier.Update(realTimeDelta);
            ChatPanel.Update(realTimeDelta);
        }

        private void EnsureServer()
        {
            if (server != null && server.IsRunning)
            {
                return;
            }

            try
            {
                server = new ApiServer(this, DefaultPort);
                server.Start();
            }
            catch (System.Exception ex)
            {
                server = null;
                Debug.Log("[SkylinesAgentBridge] Could not start the API server: " + ex.Message);
            }
        }
    }
}
