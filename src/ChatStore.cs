using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace SkylinesAgentBridge
{
    /// <summary>What the player had selected when a message was sent. Plain data, no Unity types.</summary>
    public sealed class ChatSelection
    {
        /// <summary>building | segment | node | citizen | vehicle | district | none</summary>
        public string Type = "none";
        public long Id;
        public string Name;
        public string Prefab;
        public bool HasPosition;
        public float X;
        public float Z;

        public static ChatSelection None()
        {
            return new ChatSelection();
        }

        public ChatSelection Clone()
        {
            return (ChatSelection)MemberwiseClone();
        }

        public void AppendJson(StringBuilder json)
        {
            json.Append("{\"type\":\"").Append(JsonUtil.Escape(Type)).Append("\"");
            if (Type != "none")
            {
                json.Append(",\"id\":").Append(Id.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(Name))
                {
                    json.Append(",").Append(JsonUtil.StringField("name", Name));
                }
                if (!string.IsNullOrEmpty(Prefab))
                {
                    json.Append(",").Append(JsonUtil.StringField("prefab", Prefab));
                }
                if (HasPosition)
                {
                    json.Append(",\"x\":").Append(JsonUtil.Number(X));
                    json.Append(",\"z\":").Append(JsonUtil.Number(Z));
                }
            }
            json.Append("}");
        }
    }

    /// <summary>
    /// Where the player was looking when a message was sent. Captured on the game thread by
    /// ChatPanel; the store only carries it.
    /// </summary>
    public sealed class ChatContext
    {
        public string GameTime;
        public bool HasCamera;
        public float CameraX;
        public float CameraZ;
        public ChatSelection Selected = ChatSelection.None();

        /// <summary>live = captured at send time; cached = the panel's last periodic snapshot; none = nothing known.</summary>
        public string Source = "none";
        public DateTime CapturedUtc;

        public ChatContext Clone()
        {
            ChatContext copy = (ChatContext)MemberwiseClone();
            copy.Selected = Selected == null ? ChatSelection.None() : Selected.Clone();
            return copy;
        }
    }

    public sealed class ChatInboxMessage
    {
        public long Id;
        public string Text;
        public DateTime Utc;
        public ChatContext Context;

        /// <summary>new | seen | answered</summary>
        public string Status = "new";

        /// <summary>panel = typed in game; api = POST /chat/send (terminal, tests, or any local process).</summary>
        public string Source = "panel";
    }

    public sealed class ChatEntry
    {
        public long Id;

        /// <summary>player | claude</summary>
        public string From;

        /// <summary>message | reply | update | status</summary>
        public string Kind;

        public string Text;
        public DateTime Utc;
        public long InReplyTo;
        public string Source;
    }

    public sealed class ChatClaudeStatus
    {
        /// <summary>idle | thinking | working | offline (offline is derived, never stored)</summary>
        public string State;
        public string Text;
        public DateTime LastSeenUtc;
        public bool EverSeen;
    }

    /// <summary>
    /// The chat's single source of truth. Thread-safe and Unity-free: HTTP worker threads
    /// write and long-poll here, and the game thread reads it once per frame with a
    /// non-blocking TryEnter so a slow HTTP caller can never stall a frame.
    /// </summary>
    public sealed class ChatStore
    {
        public const int MaxHistory = 500;
        public const int MaxInbox = 500;
        public const int MaxClaudeTextLength = 4000;
        public const int MaxPlayerTextLength = 1000;
        public const int MaxWaitSeconds = 30;
        public static readonly TimeSpan OfflineAfter = TimeSpan.FromSeconds(120);

        private static readonly ChatStore instance = new ChatStore(null);

        private readonly object gate = new object();
        private readonly Func<DateTime> clock;
        private readonly List<ChatInboxMessage> inbox = new List<ChatInboxMessage>();
        private readonly List<ChatEntry> history = new List<ChatEntry>();
        private readonly ChatClaudeStatus claude = new ChatClaudeStatus();
        private ChatContext cachedContext;
        private long nextId = 1;

        // Read without the lock via Interlocked.Read, so the panel can tell cheaply whether
        // anything changed before it tries to take the lock at all.
        private long latestHistoryId;
        private long statusVersion;

        public ChatStore(Func<DateTime> clock)
        {
            this.clock = clock != null ? clock : new Func<DateTime>(delegate { return DateTime.UtcNow; });
            claude.State = "idle";
            claude.Text = "";
        }

        public static ChatStore Instance
        {
            get { return instance; }
        }

        public DateTime Now
        {
            get { return clock(); }
        }

        public long LatestHistoryId
        {
            get { return Interlocked.Read(ref latestHistoryId); }
        }

        public long StatusVersion
        {
            get { return Interlocked.Read(ref statusVersion); }
        }

        // --- Player -> Claude ----------------------------------------------------------

        /// <summary>Adds a player message. Returns its id. Wakes every long-polling reader.</summary>
        public long AddPlayerMessage(string text, ChatContext context, string source)
        {
            text = NormalizeText(text, MaxPlayerTextLength);
            if (text.Length == 0)
            {
                throw new ArgumentException("Message text is empty.");
            }

            lock (gate)
            {
                DateTime now = clock();
                long id = nextId++;

                ChatInboxMessage message = new ChatInboxMessage();
                message.Id = id;
                message.Text = text;
                message.Utc = now;
                message.Context = context == null ? new ChatContext() : context.Clone();
                message.Source = string.IsNullOrEmpty(source) ? "panel" : source;
                inbox.Add(message);
                while (inbox.Count > MaxInbox)
                {
                    inbox.RemoveAt(0);
                }

                AppendHistory(id, "player", "message", text, now, 0, message.Source);
                Monitor.PulseAll(gate);
                return id;
            }
        }

        /// <summary>
        /// Player messages with id &gt; after, oldest first. If there are none and waitSeconds
        /// is positive, blocks up to that long for one to arrive. Returned "new" messages
        /// become "seen". Counts as a Claude heartbeat.
        /// </summary>
        public List<ChatInboxMessage> PollInbox(long after, int waitSeconds, bool unansweredOnly, int limit)
        {
            if (waitSeconds < 0) waitSeconds = 0;
            if (waitSeconds > MaxWaitSeconds) waitSeconds = MaxWaitSeconds;
            if (limit <= 0 || limit > MaxInbox) limit = MaxInbox;

            lock (gate)
            {
                TouchLocked();

                DateTime deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
                List<ChatInboxMessage> result = CollectLocked(after, unansweredOnly, limit);

                // Wall clock (not the injectable one) bounds the real wait.
                while (result.Count == 0 && waitSeconds > 0)
                {
                    int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                    if (remaining <= 0)
                    {
                        break;
                    }
                    Monitor.Wait(gate, remaining);
                    result = CollectLocked(after, unansweredOnly, limit);
                }

                bool changed = false;
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i].Status == "new")
                    {
                        result[i].Status = "seen";
                        changed = true;
                    }
                }

                // A long poll can end much later than it began; the caller is plainly alive.
                TouchLocked();
                if (changed)
                {
                    Interlocked.Increment(ref statusVersion);
                }

                // Hand out copies so callers never observe later status changes mid-serialize.
                List<ChatInboxMessage> copies = new List<ChatInboxMessage>(result.Count);
                for (int i = 0; i < result.Count; i++)
                {
                    copies.Add(CopyMessage(result[i]));
                }
                return copies;
            }
        }

        /// <summary>Wakes every long-poll so worker threads do not outlive a mod disable.</summary>
        public void WakeAll()
        {
            lock (gate)
            {
                Monitor.PulseAll(gate);
            }
        }

        // --- Claude -> player ----------------------------------------------------------

        /// <summary>Posts a Claude message. kind is reply | update | status. Returns its id.</summary>
        public long AddClaudeMessage(string text, string kind, long inReplyTo)
        {
            text = NormalizeText(text, int.MaxValue);
            if (text.Length == 0)
            {
                throw new ArgumentException("text must not be empty.");
            }
            if (text.Length > MaxClaudeTextLength)
            {
                throw new ArgumentException("text is " + text.Length + " characters; the limit is " + MaxClaudeTextLength + ".");
            }
            if (kind != "reply" && kind != "update" && kind != "status")
            {
                throw new ArgumentException("kind must be reply, update or status.");
            }

            lock (gate)
            {
                TouchLocked();
                DateTime now = clock();
                long id = nextId++;

                if (inReplyTo > 0 && kind == "reply")
                {
                    for (int i = inbox.Count - 1; i >= 0; i--)
                    {
                        if (inbox[i].Id == inReplyTo)
                        {
                            inbox[i].Status = "answered";
                            break;
                        }
                    }
                }

                AppendHistory(id, "claude", kind, text, now, inReplyTo, "claude");
                Interlocked.Increment(ref statusVersion);
                return id;
            }
        }

        /// <summary>state is idle | thinking | working. offline is derived from silence and cannot be set.</summary>
        public void SetClaudeStatus(string state, string text)
        {
            if (state != "idle" && state != "thinking" && state != "working" && state != "offline")
            {
                throw new ArgumentException("state must be idle, thinking, working or offline.");
            }

            lock (gate)
            {
                if (state == "offline")
                {
                    // An explicit sign-off (chat-bridge.sh on Ctrl-C). Backdate lastSeen so the
                    // derived state flips immediately instead of 120 s later.
                    claude.State = "idle";
                    claude.Text = NormalizeText(text, 200);
                    claude.LastSeenUtc = clock() - OfflineAfter - TimeSpan.FromSeconds(1);
                    claude.EverSeen = true;
                }
                else
                {
                    TouchLocked();
                    claude.State = state;
                    claude.Text = NormalizeText(text, 200);
                }
                Interlocked.Increment(ref statusVersion);
            }
        }

        /// <summary>A bare heartbeat: Claude is still there, state unchanged.</summary>
        public void Touch()
        {
            lock (gate)
            {
                TouchLocked();
            }
        }

        public ChatClaudeStatus GetClaudeStatus()
        {
            lock (gate)
            {
                return EffectiveStatusLocked();
            }
        }

        /// <summary>Game thread: non-blocking. Returns null if the lock is busy right now.</summary>
        public ChatClaudeStatus TryGetClaudeStatus()
        {
            if (!Monitor.TryEnter(gate))
            {
                return null;
            }
            try
            {
                return EffectiveStatusLocked();
            }
            finally
            {
                Monitor.Exit(gate);
            }
        }

        // --- History -------------------------------------------------------------------

        public List<ChatEntry> GetHistory(long after, int limit)
        {
            lock (gate)
            {
                return CollectHistoryLocked(after, limit);
            }
        }

        /// <summary>Game thread: non-blocking. Returns null if the lock is busy right now.</summary>
        public List<ChatEntry> TryGetHistory(long after, int limit)
        {
            if (!Monitor.TryEnter(gate))
            {
                return null;
            }
            try
            {
                return CollectHistoryLocked(after, limit);
            }
            finally
            {
                Monitor.Exit(gate);
            }
        }

        public string GetInboxStatus(long id)
        {
            lock (gate)
            {
                for (int i = inbox.Count - 1; i >= 0; i--)
                {
                    if (inbox[i].Id == id)
                    {
                        return inbox[i].Status;
                    }
                }
                return null;
            }
        }

        public int CountUnanswered()
        {
            lock (gate)
            {
                int count = 0;
                for (int i = 0; i < inbox.Count; i++)
                {
                    if (inbox[i].Status != "answered") count++;
                }
                return count;
            }
        }

        // --- Context cache -------------------------------------------------------------

        /// <summary>Game thread, a few times a second: the last known camera and selection.</summary>
        public bool TrySetCachedContext(ChatContext context)
        {
            if (!Monitor.TryEnter(gate))
            {
                return false;
            }
            try
            {
                cachedContext = context == null ? null : context.Clone();
                return true;
            }
            finally
            {
                Monitor.Exit(gate);
            }
        }

        /// <summary>The panel's last snapshot, marked cached, or an empty context.</summary>
        public ChatContext GetCachedContext()
        {
            lock (gate)
            {
                if (cachedContext == null)
                {
                    return new ChatContext();
                }
                ChatContext copy = cachedContext.Clone();
                copy.Source = "cached";
                return copy;
            }
        }

        public void ClearCachedContext()
        {
            lock (gate)
            {
                cachedContext = null;
            }
        }

        // --- Internals -----------------------------------------------------------------

        private void TouchLocked()
        {
            claude.LastSeenUtc = clock();
            claude.EverSeen = true;
        }

        private ChatClaudeStatus EffectiveStatusLocked()
        {
            ChatClaudeStatus copy = new ChatClaudeStatus();
            copy.LastSeenUtc = claude.LastSeenUtc;
            copy.EverSeen = claude.EverSeen;
            copy.Text = claude.Text;

            if (!claude.EverSeen || clock() - claude.LastSeenUtc > OfflineAfter)
            {
                copy.State = "offline";
            }
            else
            {
                copy.State = claude.State;
            }
            return copy;
        }

        private List<ChatInboxMessage> CollectLocked(long after, bool unansweredOnly, int limit)
        {
            List<ChatInboxMessage> result = new List<ChatInboxMessage>();
            for (int i = 0; i < inbox.Count && result.Count < limit; i++)
            {
                ChatInboxMessage message = inbox[i];
                if (message.Id <= after)
                {
                    continue;
                }
                if (unansweredOnly && message.Status == "answered")
                {
                    continue;
                }
                result.Add(message);
            }
            return result;
        }

        private List<ChatEntry> CollectHistoryLocked(long after, int limit)
        {
            if (limit <= 0 || limit > MaxHistory) limit = MaxHistory;

            // The newest `limit` entries after `after`, oldest first: a reader that fell far
            // behind wants the end of the conversation, not its beginning.
            int start = history.Count;
            int count = 0;
            while (start > 0 && count < limit && history[start - 1].Id > after)
            {
                start--;
                count++;
            }

            List<ChatEntry> result = new List<ChatEntry>(count);
            for (int i = start; i < history.Count; i++)
            {
                result.Add(CopyEntry(history[i]));
            }
            return result;
        }

        private void AppendHistory(long id, string from, string kind, string text, DateTime utc, long inReplyTo, string source)
        {
            ChatEntry entry = new ChatEntry();
            entry.Id = id;
            entry.From = from;
            entry.Kind = kind;
            entry.Text = text;
            entry.Utc = utc;
            entry.InReplyTo = inReplyTo;
            entry.Source = source;
            history.Add(entry);
            while (history.Count > MaxHistory)
            {
                history.RemoveAt(0);
            }
            Interlocked.Exchange(ref latestHistoryId, id);
        }

        private static ChatInboxMessage CopyMessage(ChatInboxMessage source)
        {
            ChatInboxMessage copy = new ChatInboxMessage();
            copy.Id = source.Id;
            copy.Text = source.Text;
            copy.Utc = source.Utc;
            copy.Context = source.Context == null ? new ChatContext() : source.Context.Clone();
            copy.Status = source.Status;
            copy.Source = source.Source;
            return copy;
        }

        private static ChatEntry CopyEntry(ChatEntry source)
        {
            ChatEntry copy = new ChatEntry();
            copy.Id = source.Id;
            copy.From = source.From;
            copy.Kind = source.Kind;
            copy.Text = source.Text;
            copy.Utc = source.Utc;
            copy.InReplyTo = source.InReplyTo;
            copy.Source = source.Source;
            return copy;
        }

        /// <summary>Trims, drops control characters other than newline and tab, and caps length.</summary>
        public static string NormalizeText(string text, int maxLength)
        {
            if (text == null)
            {
                return "";
            }

            StringBuilder clean = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    continue;
                }
                if (char.IsControl(c) && c != '\n' && c != '\t')
                {
                    continue;
                }
                clean.Append(c);
            }

            string result = clean.ToString().Trim();
            if (result.Length > maxLength)
            {
                result = result.Substring(0, maxLength);
            }
            return result;
        }

        // --- JSON ----------------------------------------------------------------------

        public static string Iso(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        public static void AppendMessageJson(StringBuilder json, ChatInboxMessage message)
        {
            ChatContext context = message.Context == null ? new ChatContext() : message.Context;

            json.Append("{\"id\":").Append(message.Id.ToString(CultureInfo.InvariantCulture));
            json.Append(",").Append(JsonUtil.StringField("text", message.Text));
            json.Append(",").Append(JsonUtil.StringField("time", Iso(message.Utc)));
            json.Append(",\"gameTime\":");
            if (string.IsNullOrEmpty(context.GameTime)) json.Append("null");
            else json.Append("\"").Append(JsonUtil.Escape(context.GameTime)).Append("\"");
            json.Append(",\"camera\":");
            if (context.HasCamera)
            {
                json.Append("{\"x\":").Append(JsonUtil.Number(context.CameraX))
                    .Append(",\"z\":").Append(JsonUtil.Number(context.CameraZ)).Append("}");
            }
            else
            {
                json.Append("null");
            }
            json.Append(",\"selected\":");
            (context.Selected == null ? ChatSelection.None() : context.Selected).AppendJson(json);
            json.Append(",").Append(JsonUtil.StringField("context", context.Source));
            json.Append(",").Append(JsonUtil.StringField("status", message.Status));
            json.Append(",").Append(JsonUtil.StringField("source", message.Source));
            json.Append("}");
        }

        public static void AppendEntryJson(StringBuilder json, ChatEntry entry)
        {
            json.Append("{\"id\":").Append(entry.Id.ToString(CultureInfo.InvariantCulture));
            json.Append(",").Append(JsonUtil.StringField("from", entry.From));
            json.Append(",").Append(JsonUtil.StringField("kind", entry.Kind));
            json.Append(",").Append(JsonUtil.StringField("text", entry.Text));
            json.Append(",").Append(JsonUtil.StringField("time", Iso(entry.Utc)));
            if (entry.InReplyTo > 0)
            {
                json.Append(",\"inReplyTo\":").Append(entry.InReplyTo.ToString(CultureInfo.InvariantCulture));
            }
            if (entry.From == "player" && !string.IsNullOrEmpty(entry.Source))
            {
                json.Append(",").Append(JsonUtil.StringField("source", entry.Source));
            }
            json.Append("}");
        }

        public static void AppendStatusJson(StringBuilder json, ChatClaudeStatus status)
        {
            json.Append("{").Append(JsonUtil.StringField("state", status.State));
            json.Append(",").Append(JsonUtil.StringField("text", status.Text));
            json.Append(",\"lastSeen\":");
            if (status.EverSeen) json.Append("\"").Append(Iso(status.LastSeenUtc)).Append("\"");
            else json.Append("null");
            json.Append("}");
        }
    }
}
