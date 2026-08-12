using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ErenshorDeepSims
{
    internal sealed class OllamaTimingMetrics
    {
        internal double TotalMs;
        internal double LoadMs;
        internal double PromptEvalMs;
        internal double EvalMs;
        internal int PromptEvalCount;
        internal int EvalCount;
        internal int Attempts;

        internal OllamaTimingMetrics Clone()
        {
            return new OllamaTimingMetrics
            {
                TotalMs = TotalMs, LoadMs = LoadMs, PromptEvalMs = PromptEvalMs, EvalMs = EvalMs,
                PromptEvalCount = PromptEvalCount, EvalCount = EvalCount, Attempts = Attempts
            };
        }
    }

    internal class OllamaClient
    {
        private readonly ManualLogSource _log;
        private readonly object _timingLock = new object();
        private OllamaTimingMetrics _lastTiming = new OllamaTimingMetrics();

        internal OllamaClient(ManualLogSource log)
        {
            _log = log;
        }

        internal OllamaTimingMetrics GetLastTiming()
        {
            lock (_timingLock) return _lastTiming == null ? new OllamaTimingMetrics() : _lastTiming.Clone();
        }

        internal Task<string> ChatAsync(string endpoint, string model, List<ChatMessage> messages, int timeoutSeconds, int numCtx, string keepAlive, string inferenceMode, int cpuThreads)
        {
            return Task.Run(delegate
            {
                OllamaTimingMetrics aggregate = new OllamaTimingMetrics();
                // Short MMO replies do not need a large generation budget. Thinking is
                // explicitly disabled in the request for models that support it.
                ChatAttempt first = SendChat(endpoint, model, messages, timeoutSeconds, numCtx, keepAlive, 72, inferenceMode, cpuThreads);
                AccumulateTiming(aggregate, first);
                if (!string.IsNullOrWhiteSpace(first.Content)) { SetLastTiming(aggregate); return first.Content; }

                LogEmptyResponse("primary", first);

                // done_reason=load means Ollama loaded a model but did not generate text.
                // This has appeared in Ollama API bug reports as well as load-only calls.
                // Wait briefly and resend the exact same generation request once.
                if (string.Equals(first.DoneReason, "load", StringComparison.OrdinalIgnoreCase))
                {
                    System.Threading.Thread.Sleep(900);
                    ChatAttempt afterLoad = SendChat(endpoint, model, messages, timeoutSeconds, numCtx, keepAlive, 96, inferenceMode, cpuThreads);
                    AccumulateTiming(aggregate, afterLoad);
                    if (!string.IsNullOrWhiteSpace(afterLoad.Content)) { SetLastTiming(aggregate); return afterLoad.Content; }
                    LogEmptyResponse("post-load retry", afterLoad);
                    first = afterLoad;
                }

                // Retry with a little more room in case a model ended before final content.
                ChatAttempt second = SendChat(endpoint, model, messages, timeoutSeconds, numCtx, keepAlive, 112, inferenceMode, cpuThreads);
                AccumulateTiming(aggregate, second);
                if (!string.IsNullOrWhiteSpace(second.Content)) { SetLastTiming(aggregate); return second.Content; }

                LogEmptyResponse("expanded-budget retry", second);

                // Final compatibility fallback: flatten system/context messages into one
                // ordinary user prompt. This avoids model-template edge cases while keeping
                // Erenshor authoritative for actual gameplay actions.
                List<ChatMessage> flattened = new List<ChatMessage>();
                flattened.Add(new ChatMessage("user", FlattenMessages(messages)));
                ChatAttempt third = SendChat(endpoint, model, flattened, timeoutSeconds, numCtx, keepAlive, 96, inferenceMode, cpuThreads);
                AccumulateTiming(aggregate, third);
                if (!string.IsNullOrWhiteSpace(third.Content)) { SetLastTiming(aggregate); return third.Content; }

                LogEmptyResponse("flattened-prompt retry", third);
                SetLastTiming(aggregate);
                throw new InvalidDataException(BuildEmptyResponseError(third));
            });
        }

        private ChatAttempt SendChat(string endpoint, string model, List<ChatMessage> messages, int timeoutSeconds, int numCtx, string keepAlive, int numPredict, string inferenceMode, int cpuThreads)
        {
            OllamaOptions options = new OllamaOptions();
            options.num_ctx = numCtx;
            options.temperature = 0.60f;
            options.num_predict = numPredict;
            options.num_gpu = int.MinValue; // sentinel: omit and let Ollama choose
            options.num_thread = Math.Max(0, cpuThreads);
            string mode = string.IsNullOrWhiteSpace(inferenceMode) ? "Auto" : inferenceMode.Trim();
            if (string.Equals(mode, "CPU", StringComparison.OrdinalIgnoreCase)) options.num_gpu = 0;
            else if (string.Equals(mode, "GPU", StringComparison.OrdinalIgnoreCase)) options.num_gpu = -1;

            OllamaChatRequest body = new OllamaChatRequest();
            body.model = model;
            body.messages = messages;
            body.stream = false;
            body.think = false;
            body.keep_alive = keepAlive;
            body.options = options;

            // Build the request JSON manually. Unity JsonUtility is convenient for
            // responses/memory, but relying on it for an API request containing a generic
            // List<T> proved too opaque: an omitted/empty messages array makes Ollama treat
            // the call like a model-load request and return done_reason=load.
            string requestJson = BuildChatRequestJson(body);
            if (_log != null) _log.LogDebug("Ollama chat request: utc=" + DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + TrimForLog(requestJson, 1400));
            string responseJson = PostJson(endpoint, requestJson, timeoutSeconds);

            // Do not use Unity JsonUtility for Ollama's response envelope. In Unity 2021
            // it successfully populated primitive top-level fields but left the nested
            // `message` object null, even though the raw JSON contained message.content.
            // Parse the few fields we need directly from the response instead.
            ChatAttempt result = new ChatAttempt();
            result.Raw = responseJson;
            result.Done = ExtractJsonBoolean(responseJson, "done", false);
            result.DoneReason = ExtractJsonString(responseJson, "done_reason", 0);
            result.EvalCount = ExtractJsonInteger(responseJson, "eval_count", 0);
            result.PromptEvalCount = ExtractJsonInteger(responseJson, "prompt_eval_count", 0);
            result.TotalDurationNs = ExtractJsonLong(responseJson, "total_duration", 0L);
            result.LoadDurationNs = ExtractJsonLong(responseJson, "load_duration", 0L);
            result.PromptEvalDurationNs = ExtractJsonLong(responseJson, "prompt_eval_duration", 0L);
            result.EvalDurationNs = ExtractJsonLong(responseJson, "eval_duration", 0L);

            int messageStart = FindJsonObjectPropertyStart(responseJson, "message");
            if (messageStart >= 0)
            {
                result.Content = ExtractJsonString(responseJson, "content", messageStart);
                result.Thinking = ExtractJsonString(responseJson, "thinking", messageStart);
            }

            if (_log != null && !string.IsNullOrWhiteSpace(result.Content))
                _log.LogDebug("Ollama reply parsed successfully: utc=" + DateTime.UtcNow.ToString("HH:mm:ss.fff") + " " + TrimForLog(result.Content, 300));

            return result;
        }

        private void LogEmptyResponse(string phase, ChatAttempt attempt)
        {
            if (_log == null) return;
            string diagnostic = "Ollama returned empty message.content during " + phase +
                ". done=" + attempt.Done +
                " done_reason=" + Safe(attempt.DoneReason) +
                " eval_count=" + attempt.EvalCount +
                " thinking_chars=" + (attempt.Thinking == null ? 0 : attempt.Thinking.Length) +
                " raw=" + TrimForLog(attempt.Raw, 1200);
            _log.LogWarning(diagnostic);
        }

        private static string BuildEmptyResponseError(ChatAttempt attempt)
        {
            string detail = "Ollama returned no final chat text";
            if (!string.IsNullOrWhiteSpace(attempt.DoneReason)) detail += " (done_reason=" + attempt.DoneReason + ")";
            if (!string.IsNullOrWhiteSpace(attempt.Thinking)) detail += "; thinking was returned but no final answer";
            detail += ". Check BepInEx LogOutput.log for the raw Ollama response.";
            return detail;
        }

        private static string FlattenMessages(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Follow the character/context instructions below and return only the final short in-game chat reply. Do not explain your reasoning.");
            sb.AppendLine();
            if (messages != null)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    ChatMessage message = messages[i];
                    if (message == null || string.IsNullOrWhiteSpace(message.content)) continue;
                    string role = string.IsNullOrWhiteSpace(message.role) ? "message" : message.role.ToUpperInvariant();
                    sb.AppendLine("[" + role + "]");
                    sb.AppendLine(message.content);
                    sb.AppendLine();
                }
            }
            sb.AppendLine("[RESPONSE]");
            return sb.ToString();
        }

        private static string BuildChatRequestJson(OllamaChatRequest body)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"model\":\"").Append(EscapeJson(body.model)).Append("\"");
            sb.Append(",\"messages\":[");
            if (body.messages != null)
            {
                bool first = true;
                for (int i = 0; i < body.messages.Count; i++)
                {
                    ChatMessage message = body.messages[i];
                    if (message == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"role\":\"").Append(EscapeJson(message.role)).Append("\",\"content\":\"")
                      .Append(EscapeJson(message.content)).Append("\"}");
                }
            }
            sb.Append(']');
            sb.Append(",\"stream\":false");
            sb.Append(",\"think\":false");
            if (!string.IsNullOrWhiteSpace(body.keep_alive))
                sb.Append(",\"keep_alive\":\"").Append(EscapeJson(body.keep_alive)).Append("\"");
            if (body.options != null)
            {
                sb.Append(",\"options\":{");
                sb.Append("\"num_ctx\":").Append(body.options.num_ctx);
                sb.Append(",\"temperature\":").Append(body.options.temperature.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"num_predict\":").Append(body.options.num_predict);
                if (body.options.num_gpu != int.MinValue) sb.Append(",\"num_gpu\":").Append(body.options.num_gpu);
                if (body.options.num_thread > 0) sb.Append(",\"num_thread\":").Append(body.options.num_thread);
                sb.Append('}');
            }
            sb.Append('}');
            return sb.ToString();
        }

        internal Task<string> GetStatusAsync(string chatEndpoint, string model, int timeoutSeconds)
        {
            return Task.Run(delegate
            {
                string baseEndpoint = GetApiBase(chatEndpoint);

                Get(baseEndpoint + "/version", timeoutSeconds);

                string requestJson = "{\"model\":\"" + EscapeJson(model) + "\",\"verbose\":false}";
                try
                {
                    PostJson(baseEndpoint + "/show", requestJson, timeoutSeconds);
                    return "Ollama is running and '" + model + "' is installed.";
                }
                catch (Exception ex)
                {
                    string detail = ex.Message == null ? "model lookup failed" : ex.Message;
                    if (detail.Length > 180) detail = detail.Substring(0, 180) + "...";
                    return "Ollama is running, but '" + model + "' could not be opened. Run: ollama pull " + model + " (" + detail + ")";
                }
            });
        }

        private static string GetApiBase(string chatEndpoint)
        {
            string endpoint = string.IsNullOrWhiteSpace(chatEndpoint) ? "http://localhost:11434/api/chat" : chatEndpoint.Trim();
            int apiIndex = endpoint.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            if (apiIndex >= 0) return endpoint.Substring(0, apiIndex).TrimEnd('/') + "/api";
            return endpoint.TrimEnd('/') + "/api";
        }

        private static string EscapeJson(string value)
        {
            if (value == null) return string.Empty;
            StringBuilder sb = new StringBuilder(value.Length + 16);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        // Any other control character (including ones decoded from a \uXXXX escape in
                        // fetched wiki/news text) produces invalid JSON Ollama's server rejects with
                        // HTTP 400, which blocks all Deep Sims generation for a cooldown period.
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private string PostJson(string url, string json, int timeoutSeconds)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = timeoutSeconds * 1000;
            request.ReadWriteTimeout = timeoutSeconds * 1000;
            DisableProxyForLoopback(request, url);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            request.ContentLength = bytes.Length;
            try
            {
                // GetRequestStream is where "Ollama is not running" surfaces, so it belongs inside
                // the same handler as the response read; otherwise the common failure escapes raw.
                using (Stream requestStream = request.GetRequestStream()) requestStream.Write(bytes, 0, bytes.Length);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                string detail = ReadWebException(ex);
                if (_log != null) _log.LogWarning("Ollama request failed: " + detail);
                throw new InvalidOperationException(detail, ex);
            }
        }

        private string Get(string url, int timeoutSeconds)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = timeoutSeconds * 1000;
            request.ReadWriteTimeout = timeoutSeconds * 1000;
            DisableProxyForLoopback(request, url);
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException ex) { throw new InvalidOperationException(ReadWebException(ex), ex); }
        }

        private static string ReadWebException(WebException ex)
        {
            if (ex.Response != null)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        string body = reader.ReadToEnd();
                        if (!string.IsNullOrWhiteSpace(body)) return body;
                    }
                }
                catch { }
            }
            return ex.Message;
        }

        private static void DisableProxyForLoopback(HttpWebRequest request, string url)
        {
            Uri endpoint;
            if (request != null && Uri.TryCreate(url, UriKind.Absolute, out endpoint) && endpoint.IsLoopback)
                request.Proxy = null;
        }

        private static int FindJsonObjectPropertyStart(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName)) return -1;
            int key = FindJsonProperty(json, propertyName, 0);
            if (key < 0) return -1;
            int colon = json.IndexOf(':', key + propertyName.Length + 2);
            if (colon < 0) return -1;
            int pos = colon + 1;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
            return pos < json.Length && json[pos] == '{' ? pos : -1;
        }

        private static int FindJsonProperty(string json, string propertyName, int startIndex)
        {
            string needle = "\"" + propertyName + "\"";
            int pos = Math.Max(0, startIndex);
            while (pos < json.Length)
            {
                int found = json.IndexOf(needle, pos, StringComparison.Ordinal);
                if (found < 0) return -1;

                // Ensure this quoted token is functioning as an object property name.
                int after = found + needle.Length;
                while (after < json.Length && char.IsWhiteSpace(json[after])) after++;
                if (after < json.Length && json[after] == ':') return found;
                pos = found + needle.Length;
            }
            return -1;
        }

        private static string ExtractJsonString(string json, string propertyName, int startIndex)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int key = FindJsonProperty(json, propertyName, startIndex);
            if (key < 0) return null;
            int colon = json.IndexOf(':', key + propertyName.Length + 2);
            if (colon < 0) return null;
            int pos = colon + 1;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
            if (pos >= json.Length) return null;
            if (json.IndexOf("null", pos, StringComparison.Ordinal) == pos) return null;
            if (json[pos] != '"') return null;
            pos++;

            StringBuilder sb = new StringBuilder();
            while (pos < json.Length)
            {
                char c = json[pos++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (pos >= json.Length) break;
                char esc = json[pos++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 <= json.Length)
                        {
                            int code;
                            if (int.TryParse(json.Substring(pos, 4), System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out code))
                            {
                                sb.Append((char)code);
                                pos += 4;
                            }
                        }
                        break;
                    default:
                        sb.Append(esc);
                        break;
                }
            }
            return null;
        }

        private static bool ExtractJsonBoolean(string json, string propertyName, bool fallback)
        {
            int key = FindJsonProperty(json, propertyName, 0);
            if (key < 0) return fallback;
            int colon = json.IndexOf(':', key + propertyName.Length + 2);
            if (colon < 0) return fallback;
            int pos = colon + 1;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
            if (pos + 4 <= json.Length && string.Compare(json, pos, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0) return true;
            if (pos + 5 <= json.Length && string.Compare(json, pos, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0) return false;
            return fallback;
        }

        private static int ExtractJsonInteger(string json, string propertyName, int fallback)
        {
            int key = FindJsonProperty(json, propertyName, 0);
            if (key < 0) return fallback;
            int colon = json.IndexOf(':', key + propertyName.Length + 2);
            if (colon < 0) return fallback;
            int pos = colon + 1;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
            int end = pos;
            if (end < json.Length && (json[end] == '-' || json[end] == '+')) end++;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            int value;
            if (end > pos && int.TryParse(json.Substring(pos, end - pos), out value)) return value;
            return fallback;
        }

        private static long ExtractJsonLong(string json, string propertyName, long fallback)
        {
            int key = FindJsonProperty(json, propertyName, 0);
            if (key < 0) return fallback;
            int colon = json.IndexOf(':', key + propertyName.Length + 2);
            if (colon < 0) return fallback;
            int pos = colon + 1;
            while (pos < json.Length && char.IsWhiteSpace(json[pos])) pos++;
            int end = pos;
            if (end < json.Length && (json[end] == '-' || json[end] == '+')) end++;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            long value;
            if (end > pos && long.TryParse(json.Substring(pos, end - pos), out value)) return value;
            return fallback;
        }

        private void SetLastTiming(OllamaTimingMetrics metrics)
        {
            lock (_timingLock) _lastTiming = metrics == null ? new OllamaTimingMetrics() : metrics.Clone();
        }

        private static void AccumulateTiming(OllamaTimingMetrics metrics, ChatAttempt attempt)
        {
            if (metrics == null || attempt == null) return;
            metrics.Attempts++;
            metrics.TotalMs += attempt.TotalDurationNs / 1000000.0;
            metrics.LoadMs += attempt.LoadDurationNs / 1000000.0;
            metrics.PromptEvalMs += attempt.PromptEvalDurationNs / 1000000.0;
            metrics.EvalMs += attempt.EvalDurationNs / 1000000.0;
            metrics.PromptEvalCount += attempt.PromptEvalCount;
            metrics.EvalCount += attempt.EvalCount;
        }

        private static string TrimForLog(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "<empty>";
            string clean = value.Replace("\r", " ").Replace("\n", " ");
            if (clean.Length <= max) return clean;
            return clean.Substring(0, max) + "...";
        }

        private static string Safe(string value) { return string.IsNullOrWhiteSpace(value) ? "<none>" : value; }

        private class ChatAttempt
        {
            public string Content;
            public string Thinking;
            public bool Done;
            public string DoneReason;
            public int EvalCount;
            public int PromptEvalCount;
            public long TotalDurationNs;
            public long LoadDurationNs;
            public long PromptEvalDurationNs;
            public long EvalDurationNs;
            public string Raw;
        }

        [Serializable]
        private class OllamaOptions
        {
            public int num_ctx;
            public float temperature;
            public int num_predict;
            public int num_gpu;
            public int num_thread;
        }

        [Serializable]
        private class OllamaChatRequest
        {
            public string model;
            public List<ChatMessage> messages;
            public bool stream;
            public bool think;
            public string keep_alive;
            public OllamaOptions options;
        }

    }
}
