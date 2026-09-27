using System;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Raised for failures the agent can act on: a missing prefab, an exhausted pool, an
    /// out-of-range argument. The message goes back in the JSON error field verbatim, so
    /// write it for the agent, not for a log file.
    /// </summary>
    public sealed class BridgeException : Exception
    {
        public BridgeException(string message)
            : base(message)
        {
        }
    }
}
