using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SkylinesAgentBridge
{
    /// <summary>
    /// The /chat/* endpoints. Every route here only touches ChatStore, so none of them goes
    /// through the game-thread CommandQueue and all of them work with no city loaded.
    ///
    /// ApiServer's HttpRequest/HttpResponse are private nested types, so the hook takes the
    /// request's plain fields and hands back a status plus a JSON body:
    ///
    ///     int chatStatus;
    ///     string chatJson;
    ///     if (ChatRoutes.TryHandle(request.Method, request.Path, request.Query, request.Body, out chatStatus, out chatJson))
    ///     {
    ///         return HttpResponse.Json(chatStatus, chatJson);
    ///     }
    /// </summary>
    public static class ChatRoutes
    {
        public static bool TryHandle(string method, string path, string query, string body, out int status, out string json)
        {
            return TryHandle(ChatStore.Instance, method, path, query, body, out status, out json);
        }

        public static bool TryHandle(ChatStore store, string method, string path, string query, string body, out int status, out string json)
        {
            status = 0;
            json = null;

            if (path == null || !(path == "/chat" || path.StartsWith("/chat/", StringComparison.Ordinal)))
            {
                return false;
            }

            try
            {
                if (method == "GET" && path == "/chat/inbox")
                {
                    return Done(200, Inbox(store, query), out status, out json);
                }
                if (method == "POST" && path == "/chat/say")
                {
                    return Done(200, Say(store, body), out status, out json);
                }
                if (method == "POST" && path == "/chat/status")
                {
                    return Done(200, SetStatus(store, body), out status, out json);
                }
                if (method == "GET" && path == "/chat/status")
                {
                    return Done(200, GetStatus(store, query), out status, out json);
                }
                if (method == "GET" && path == "/chat/history")
                {
                    return Done(200, History(store, query), out status, out json);
                }
                if (method == "POST" && path == "/chat/send")
                {
                    return Done(200, Send(store, body), out status, out json);
                }

                return Done(404, "{\"ok\":false,\"error\":\"Not found. Chat endpoints: GET /chat/inbox, GET /chat/history, " +
                    "GET /chat/status, POST /chat/say, POST /chat/status, POST /chat/send.\"}", out status, out json);
            }
            catch (ArgumentException ex)
            {
                return Done(400, Error(ex.Message), out status, out json);
            }
            catch (FormatException ex)
            {
                return Done(400, Error("Invalid JSON body: " + ex.Message), out status, out json);
            }
            catch (Exception ex)
            {
                return Done(500, Error(ex.GetType().Name + ": " + ex.Message), out status, out json);
            }
        }

        // GET /chat/inbox?after=<id>&wait=<0..30>&unanswered=true&limit=N
        private static string Inbox(ChatStore store, string query)
        {
            long after = QueryLong(query, "after", 0);
            int wait = (int)QueryLong(query, "wait", 0);
            bool unanswered = QueryString(query, "unanswered", "false") == "true";
            int limit = (int)QueryLong(query, "limit", 50);

            List<ChatInboxMessage> messages = store.PollInbox(after, wait, unanswered, limit);

            long lastId = after;
            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true,\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) json.Append(",");
                ChatStore.AppendMessageJson(json, messages[i]);
                if (messages[i].Id > lastId) lastId = messages[i].Id;
            }
            json.Append("],\"lastId\":").Append(lastId.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"claude\":");
            ChatStore.AppendStatusJson(json, store.GetClaudeStatus());
            json.Append("}");
            return json.ToString();
        }

        // POST /chat/say {"text":"...","kind":"reply|update|status","inReplyTo":12}
        private static string Say(ChatStore store, string body)
        {
            Dictionary<string, object> fields = ChatJson.ParseObject(body);
            string text = ChatJson.GetString(fields, "text", "");
            string kind = ChatJson.GetString(fields, "kind", "reply");
            long inReplyTo = ChatJson.GetLong(fields, "inReplyTo", 0);

            long id = store.AddClaudeMessage(text, kind, inReplyTo);
            return "{\"ok\":true,\"id\":" + id.ToString(CultureInfo.InvariantCulture) + "}";
        }

        // POST /chat/status {"state":"idle|thinking|working|offline","text":"..."}
        private static string SetStatus(ChatStore store, string body)
        {
            Dictionary<string, object> fields = ChatJson.ParseObject(body);
            string state = ChatJson.GetString(fields, "state", "");
            string text = ChatJson.GetString(fields, "text", "");
            store.SetClaudeStatus(state, text);

            StringBuilder json = new StringBuilder("{\"ok\":true,\"claude\":");
            ChatStore.AppendStatusJson(json, store.GetClaudeStatus());
            json.Append("}");
            return json.ToString();
        }

        // GET /chat/status[?heartbeat=false] — also a cheap heartbeat while a long task runs.
        private static string GetStatus(ChatStore store, string query)
        {
            if (QueryString(query, "heartbeat", "true") != "false")
            {
                store.Touch();
            }

            StringBuilder json = new StringBuilder("{\"ok\":true,\"claude\":");
            ChatStore.AppendStatusJson(json, store.GetClaudeStatus());
            json.Append(",\"unanswered\":").Append(store.CountUnanswered().ToString(CultureInfo.InvariantCulture));
            json.Append(",\"latestId\":").Append(store.LatestHistoryId.ToString(CultureInfo.InvariantCulture));
            json.Append("}");
            return json.ToString();
        }

        // GET /chat/history?after=<id>&limit=N
        private static string History(ChatStore store, string query)
        {
            long after = QueryLong(query, "after", 0);
            int limit = (int)QueryLong(query, "limit", 100);
            store.Touch();

            List<ChatEntry> entries = store.GetHistory(after, limit);
            StringBuilder json = new StringBuilder();
            json.Append("{\"ok\":true,\"entries\":[");
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) json.Append(",");
                ChatStore.AppendEntryJson(json, entries[i]);
            }
            json.Append("],\"latestId\":").Append(store.LatestHistoryId.ToString(CultureInfo.InvariantCulture));
            json.Append("}");
            return json.ToString();
        }

        // POST /chat/send {"text":"..."} — as if typed in the panel, but marked source "api".
        // Camera and selection come from the panel's periodic snapshot (context:"cached"),
        // because capturing them live would mean a round trip through the game thread.
        private static string Send(ChatStore store, string body)
        {
            Dictionary<string, object> fields = ChatJson.ParseObject(body);
            string text = ChatJson.GetString(fields, "text", "");
            if (ChatStore.NormalizeText(text, ChatStore.MaxPlayerTextLength).Length == 0)
            {
                throw new ArgumentException("text must not be empty.");
            }

            long id = store.AddPlayerMessage(text, store.GetCachedContext(), "api");
            return "{\"ok\":true,\"id\":" + id.ToString(CultureInfo.InvariantCulture) + "}";
        }

        private static bool Done(int code, string body, out int status, out string json)
        {
            status = code;
            json = body;
            return true;
        }

        private static string Error(string message)
        {
            return "{\"ok\":false,\"error\":\"" + JsonUtil.Escape(message) + "\"}";
        }

        private static string QueryString(string query, string name, string defaultValue)
        {
            if (string.IsNullOrEmpty(query))
            {
                return defaultValue;
            }

            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq > 0 && pairs[i].Substring(0, eq) == name)
                {
                    return Uri.UnescapeDataString(pairs[i].Substring(eq + 1).Replace("+", " "));
                }
            }
            return defaultValue;
        }

        private static long QueryLong(string query, string name, long defaultValue)
        {
            string raw = QueryString(query, name, null);
            if (raw == null)
            {
                return defaultValue;
            }

            long value;
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            throw new ArgumentException("Query parameter '" + name + "' must be an integer, got '" + raw + "'.");
        }
    }

    /// <summary>
    /// A small strict JSON reader for chat bodies. JsonUtil's regex getters cannot read a
    /// string containing an escaped quote or newline, and chat text contains both.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// </summary>
    public static class ChatJson
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            if (json == null || json.Trim().Length == 0)
            {
                throw new FormatException("the body is empty; expected a JSON object.");
            }

            int index = 0;
            object value = ParseValue(json, ref index, 0);
            SkipWhitespace(json, ref index);
            if (index != json.Length)
            {
                throw new FormatException("unexpected text after the JSON value at offset " + index + ".");
            }

            Dictionary<string, object> result = value as Dictionary<string, object>;
            if (result == null)
            {
                throw new FormatException("expected a JSON object.");
            }
            return result;
        }

        public static string GetString(Dictionary<string, object> fields, string name, string defaultValue)
        {
            object value;
            if (!fields.TryGetValue(name, out value) || value == null)
            {
                return defaultValue;
            }
            string text = value as string;
            if (text == null)
            {
                throw new ArgumentException("'" + name + "' must be a string.");
            }
            return text;
        }

        public static long GetLong(Dictionary<string, object> fields, string name, long defaultValue)
        {
            object value;
            if (!fields.TryGetValue(name, out value) || value == null)
            {
                return defaultValue;
            }
            if (value is double)
            {
                double number = (double)value;
                if (number != Math.Floor(number) || number < long.MinValue || number > long.MaxValue)
                {
                    throw new ArgumentException("'" + name + "' must be a whole number.");
                }
                return (long)number;
            }
            string text = value as string;
            long parsed;
            if (text != null && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            throw new ArgumentException("'" + name + "' must be a number.");
        }

        private static object ParseValue(string s, ref int i, int depth)
        {
            if (depth > 32)
            {
                throw new FormatException("nesting is too deep.");
            }

            SkipWhitespace(s, ref i);
            if (i >= s.Length)
            {
                throw new FormatException("unexpected end of input.");
            }

            char c = s[i];
            if (c == '{') return ParseObjectBody(s, ref i, depth);
            if (c == '[') return ParseArray(s, ref i, depth);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
            throw new FormatException("unexpected character '" + c + "' at offset " + i + ".");
        }

        private static Dictionary<string, object> ParseObjectBody(string s, ref int i, int depth)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"')
                {
                    throw new FormatException("expected a property name at offset " + i + ".");
                }
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':')
                {
                    throw new FormatException("expected ':' at offset " + i + ".");
                }
                i++;
                result[key] = ParseValue(s, ref i, depth + 1);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return result;
                }
                throw new FormatException("expected ',' or '}' at offset " + i + ".");
            }
        }

        private static List<object> ParseArray(string s, ref int i, int depth)
        {
            List<object> result = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return result;
            }

            while (true)
            {
                result.Add(ParseValue(s, ref i, depth + 1));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return result;
                }
                throw new FormatException("expected ',' or ']' at offset " + i + ".");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            StringBuilder result = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                {
                    return result.ToString();
                }
                if (c != '\\')
                {
                    result.Append(c);
                    continue;
                }
                if (i >= s.Length)
                {
                    break;
                }
                char e = s[i++];
                switch (e)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                        {
                            throw new FormatException("truncated \\u escape.");
                        }
                        int code;
                        if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        {
                            throw new FormatException("invalid \\u escape at offset " + i + ".");
                        }
                        result.Append((char)code);
                        i += 4;
                        break;
                    default:
                        throw new FormatException("invalid escape '\\" + e + "'.");
                }
            }
            throw new FormatException("unterminated string.");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
            {
                i++;
            }
            double value;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new FormatException("invalid number at offset " + start + ".");
            }
            return value;
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
            {
                throw new FormatException("unexpected token at offset " + i + ".");
            }
            i += word.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
            {
                i++;
            }
        }
    }
}
