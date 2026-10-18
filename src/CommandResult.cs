namespace SkylinesAgentBridge
{
    public sealed class CommandResult
    {
        public bool Ok;
        public string Json;
        public string Error;

        /// <summary>
        /// Set when the command queued simulation-thread work; the API server awaits it off the
        /// main thread and returns its result instead of this one.
        /// </summary>
        public SimulationJob Deferred;

        public static CommandResult FromJson(string json)
        {
            CommandResult result = new CommandResult();
            result.Ok = true;
            result.Json = json;
            return result;
        }

        public static CommandResult Fail(string error)
        {
            CommandResult result = new CommandResult();
            result.Ok = false;
            result.Error = error;
            result.Json = "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(error) + "\"}";
            return result;
        }
    }
}
