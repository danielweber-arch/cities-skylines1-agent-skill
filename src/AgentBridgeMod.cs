using ICities;

namespace SkylinesAgentBridge
{
    public sealed class AgentBridgeMod : IUserMod
    {
        public string Name
        {
            get { return "Skylines Agent Bridge"; }
        }

        public string Description
        {
            get { return "Localhost API bridge for AI agents to inspect and build in Cities: Skylines."; }
        }

        public void OnEnabled()
        {
            AgentBridge.Instance.OnEnabled();
        }

        public void OnDisabled()
        {
            AgentBridge.Instance.OnDisabled();
        }
    }
}
