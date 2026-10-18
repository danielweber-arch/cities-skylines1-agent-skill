using System;
using System.Threading;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// Work that must run on the simulation thread (SimulationManager.AddAction), awaited from
    /// the HTTP thread. Game code that the tools call from SimulationStep (BuildingAI.CheckBuildPosition,
    /// which activates tutorial guides) belongs there, not on the main thread, and the main thread
    /// must never block waiting for the simulation thread.
    /// </summary>
    public sealed class SimulationJob
    {
        private readonly object gate = new object();
        private readonly ManualResetEvent done = new ManualResetEvent(false);
        private readonly Func<CommandResult> work;
        private bool started;
        private bool committing;
        private bool abandoned;
        private CommandResult result;

        public SimulationJob(Func<CommandResult> work)
        {
            this.work = work;
        }

        /// <summary>Runs on the simulation thread. Does nothing if the waiter already gave up.</summary>
        public void Run()
        {
            lock (gate)
            {
                if (abandoned)
                {
                    return;
                }
                started = true;
            }

            CommandResult r;
            try
            {
                r = work();
            }
            catch (Exception ex)
            {
                r = CommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
            }

            lock (gate)
            {
                result = r;
            }
            done.Set();
        }

        /// <summary>
        /// Called by the work, on the simulation thread, right before it changes the city.
        /// Returns false if the waiter has already given up, in which case the work must stop
        /// without changing anything.
        /// </summary>
        public bool BeginCommit()
        {
            lock (gate)
            {
                if (abandoned)
                {
                    return false;
                }
                committing = true;
                return true;
            }
        }

        /// <summary>
        /// Waits for the job. If it has not started within timeoutMs it is abandoned, so a late
        /// simulation tick can never apply a change the caller was told failed.
        /// </summary>
        public CommandResult Await(int timeoutMs)
        {
            if (!done.WaitOne(timeoutMs, false))
            {
                lock (gate)
                {
                    if (!started)
                    {
                        abandoned = true;
                        return CommandResult.Fail("Timed out waiting for the simulation thread; nothing was changed.");
                    }
                }

                // Already running: it is short, so give it time to finish rather than report a
                // result that may still change the city.
                if (!done.WaitOne(30000, false))
                {
                    lock (gate)
                    {
                        if (!committing)
                        {
                            // BeginCommit will now refuse, so the late job cannot change anything.
                            abandoned = true;
                            return CommandResult.Fail("Simulation-thread job did not reach its commit point within 30 s; abandoned, nothing was changed.");
                        }
                    }

                    // Past the commit point the remaining work is a single CreateBuilding call.
                    done.WaitOne();
                }
            }

            lock (gate)
            {
                return result;
            }
        }
    }
}
