using System;
using System.Collections.Generic;
using System.Threading;

namespace SkylinesAgentBridge
{
    public sealed class CommandQueue
    {
        private readonly object gate = new object();
        private readonly Queue<QueuedCommand> commands = new Queue<QueuedCommand>();

        public CommandResult RunSync(Func<CommandResult> work, int timeoutMs)
        {
            QueuedCommand command = new QueuedCommand(work);

            lock (gate)
            {
                commands.Enqueue(command);
            }

            try
            {
                if (!command.Wait(timeoutMs))
                {
                    // Abandon rather than leave it queued: running a command whose caller has
                    // already given up is how a retry gets applied twice.
                    command.Abandon();
                    return CommandResult.Fail("Timed out waiting for the game thread.");
                }

                return command.Result;
            }
            finally
            {
                command.Release();
            }
        }

        public void Process(int maxCount)
        {
            int processed = 0;

            while (processed < maxCount)
            {
                QueuedCommand command = null;

                lock (gate)
                {
                    if (commands.Count == 0)
                    {
                        return;
                    }

                    command = commands.Dequeue();
                }

                try
                {
                    if (!command.IsAbandoned)
                    {
                        command.Execute();
                    }
                }
                finally
                {
                    command.Release();
                }

                processed++;
            }
        }

        public void Clear()
        {
            List<QueuedCommand> drained = new List<QueuedCommand>();

            lock (gate)
            {
                while (commands.Count > 0)
                {
                    drained.Add(commands.Dequeue());
                }
            }

            for (int i = 0; i < drained.Count; i++)
            {
                drained[i].Cancel("Level is unloading.");
                drained[i].Release();
            }
        }

        private sealed class QueuedCommand
        {
            private readonly Func<CommandResult> work;
            private readonly ManualResetEvent done = new ManualResetEvent(false);
            private CommandResult result;
            private volatile bool abandoned;

            // Each wait handle is an OS file descriptor and macOS ships a far lower default
            // ulimit than Windows, so a long agent session must not leak one per request.
            // Both the waiter and the game thread call Release(); the second one disposes,
            // which is what makes disposal safe without another lock.
            private int releases;

            public QueuedCommand(Func<CommandResult> work)
            {
                this.work = work;
            }

            public CommandResult Result
            {
                get { return result; }
            }

            public bool IsAbandoned
            {
                get { return abandoned; }
            }

            public void Abandon()
            {
                abandoned = true;
            }

            public bool Wait(int timeoutMs)
            {
                return done.WaitOne(timeoutMs, false);
            }

            public void Execute()
            {
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    result = CommandResult.Fail(ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    done.Set();
                }
            }

            public void Cancel(string message)
            {
                result = CommandResult.Fail(message);
                done.Set();
            }

            public void Release()
            {
                if (Interlocked.Increment(ref releases) == 2)
                {
                    ((IDisposable)done).Dispose();
                }
            }
        }
    }
}
