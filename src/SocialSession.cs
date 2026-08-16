using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ErenshorDeepSims
{
    internal enum SemanticTurnType { DirectQuestion, Statement, Opinion, PersonalPreference, SocialQuestion, Reaction, Humor, Joke, Greeting, CommandLike, Other }
    internal enum KnowledgeNeed { None, GameWiki, ExternalNews, BothAmbiguous }
    internal enum SessionEventProvenance { VerifiedWorld, PlayerSaid, SimSaid, SoftPersona, ExternalKnowledge, InferenceOnly }

    internal sealed class SemanticTurnRoute
    {
        internal SemanticTurnType TurnType;
        internal KnowledgeNeed KnowledgeNeed;
        internal string Topic = "general";
        internal string Subject = string.Empty;
        internal string SearchQuery = string.Empty;
        internal double Confidence;
        internal bool DirectAnswerRequired;
        internal string SocialIntent = "respond";
    }

    internal static class SemanticTurnRouter
    {
        internal static List<ChatMessage> BuildClassificationPrompt(string message, string recentTopic)
        {
            List<ChatMessage> result = new List<ChatMessage>();
            result.Add(new ChatMessage("system", "Classify one player party-chat turn. Return exactly seven lines: TurnType=DirectQuestion|Statement|Opinion|PersonalPreference|SocialQuestion|Reaction|Humor|Joke|Greeting|CommandLike|Other; KnowledgeNeed=None|GameWiki|ExternalNews|BothAmbiguous; Topic=short topic; Subject=resolved subject; SearchQuery=useful query or blank; Confidence=0..1; DirectAnswerRequired=true|false. Opinions, preferences, feelings, humor, greetings, casual social questions, and questions about a Sim's own tastes use KnowledgeNeed=None unless a separate specific factual claim is required. Game mechanics/items/zones use GameWiki. Current real-world events use ExternalNews. Do not answer the player."));
            if (!string.IsNullOrWhiteSpace(recentTopic)) result.Add(new ChatMessage("system", "Recent thread topic: " + Bound(recentTopic, 80)));
            result.Add(new ChatMessage("user", Bound(message, 500)));
            return result;
        }

        internal static bool TryParse(string raw, string original, out SemanticTurnRoute route)
        {
            route = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = raw.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int equals = lines[i].IndexOf('=');
                if (equals <= 0) continue;
                fields[lines[i].Substring(0, equals).Trim()] = lines[i].Substring(equals + 1).Trim();
            }
            string turnText, needText;
            SemanticTurnType turn;
            KnowledgeNeed need;
            if (!fields.TryGetValue("TurnType", out turnText) || !Enum.TryParse<SemanticTurnType>(turnText, true, out turn)) return false;
            if (!fields.TryGetValue("KnowledgeNeed", out needText) || !Enum.TryParse<KnowledgeNeed>(needText, true, out need)) return false;
            SemanticTurnRoute parsed = new SemanticTurnRoute();
            parsed.TurnType = turn;
            parsed.KnowledgeNeed = need;
            string value;
            if (fields.TryGetValue("Topic", out value)) parsed.Topic = Bound(value, 80);
            if (fields.TryGetValue("Subject", out value)) parsed.Subject = Bound(value, 100);
            if (fields.TryGetValue("SearchQuery", out value)) parsed.SearchQuery = NormalizeSearchQuery(value, need);
            double confidence;
            parsed.Confidence = fields.TryGetValue("Confidence", out value) && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out confidence)
                ? Math.Max(0.0, Math.Min(1.0, confidence)) : 0.5;
            bool required;
            parsed.DirectAnswerRequired = fields.TryGetValue("DirectAnswerRequired", out value) && bool.TryParse(value, out required)
                ? required : turn != SemanticTurnType.CommandLike;
            parsed.SocialIntent = turn == SemanticTurnType.DirectQuestion ? "answer" : turn == SemanticTurnType.Statement || turn == SemanticTurnType.Opinion ? "acknowledge_and_react" : "respond";
            ApplyMeaningOverride(parsed, original);
            ApplyNoRetrievalRule(parsed);
            if (parsed.KnowledgeNeed != KnowledgeNeed.None && string.IsNullOrWhiteSpace(parsed.SearchQuery)) parsed.SearchQuery = BuildUsefulSearchQuery(original, parsed.KnowledgeNeed);
            route = parsed;
            return true;
        }

        // A deterministic fail-safe, not the primary route. The live path asks the small model first.
        internal static SemanticTurnRoute Fallback(string message)
        {
            string text = (message ?? string.Empty).Trim();
            string lower = text.ToLowerInvariant();
            SemanticTurnRoute route = new SemanticTurnRoute();
            route.DirectAnswerRequired = text.Length > 0;
            route.TurnType = text.IndexOf('?') >= 0 || Regex.IsMatch(lower, @"^(what|where|who|when|why|how|did|does|do|is|are|can|could|would)\b")
                ? SemanticTurnType.DirectQuestion : SemanticTurnType.Statement;
            if (Regex.IsMatch(lower, @"\b(?:do you like|what do you like|do you think|being a|are .* fun|what are you reading|how do you feel|i (?:like|love|hate|prefer|think|feel)|imo|my favorite|my favourite)\b"))
                route.TurnType = lower.IndexOf("reading", StringComparison.OrdinalIgnoreCase) >= 0 ? SemanticTurnType.SocialQuestion : SemanticTurnType.PersonalPreference;
            if (Regex.IsMatch(lower, @"^(hi|hey|hello|yo|sup)\b")) route.TurnType = SemanticTurnType.Greeting;
            if (Regex.IsMatch(lower, @"\b(today|tonight|latest|current|recent|news)\b") && Regex.IsMatch(lower, @"\b(spacex|nasa|openai|world|news|ukraine|election|market|company)\b")) route.KnowledgeNeed = KnowledgeNeed.ExternalNews;
            else if (KnowledgeQueryClassifier.ShouldLookup(text) ||
                Regex.IsMatch(lower, @"\b(?:what abilities|what skills|what spells)\b") ||
                Regex.IsMatch(lower, @"\b(?:latest|newest|recent)\s+(?:erenshor\s+)?(?:patch|update)\b|patch notes\b")) route.KnowledgeNeed = KnowledgeNeed.GameWiki;
            else route.KnowledgeNeed = KnowledgeNeed.None;
            ApplyMeaningOverride(route, text);
            ApplyNoRetrievalRule(route);
            route.Topic = PromptBuilder.ClassifyThreadTopic(text);
            route.Subject = ExtractSubject(text);
            route.SearchQuery = route.KnowledgeNeed == KnowledgeNeed.None ? string.Empty : BuildUsefulSearchQuery(text, route.KnowledgeNeed);
            route.Confidence = 0.45;
            route.SocialIntent = route.TurnType == SemanticTurnType.DirectQuestion ? "answer" : "acknowledge_and_react";
            return route;
        }

        internal static string BuildUsefulSearchQuery(string message, KnowledgeNeed need)
        {
            string text = Regex.Replace(message ?? string.Empty, @"[/<>\r\n]", " ");
            text = Regex.Replace(text, @"\b(?:hey|guys|anyone|does anyone know|do you know|can you tell me|please|pls|in erenshor|erenshor)\b", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(?:where does|where do|what does|what do|how do i|how can i|did anything happen with|what happened with|what's going on with|whats going on with)\b", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"[^A-Za-z0-9'\- ]", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length > 100) text = text.Substring(0, 100).TrimEnd();
            if (need == KnowledgeNeed.GameWiki)
            {
                text = Regex.Replace(text, @"\b(drop|drops|dropped|level|location|located|found|get|obtain)$", string.Empty, RegexOptions.IgnoreCase).Trim();
                if (text.Length == 0) text = "Erenshor game information";
                return TitleWords(text);
            }
            if (text.Length == 0) text = "current world news";
            if (!Regex.IsMatch(text, @"\b(today|latest|recent|current)\b", RegexOptions.IgnoreCase)) text += " latest news";
            return text;
        }

        internal static string LookupAcknowledgement(SimSnapshot speaker, SemanticTurnRoute route)
        {
            string seedText = (speaker == null ? string.Empty : speaker.Name) + "|" + (route == null ? string.Empty : route.Topic);
            int seed = StableHash(seedText);
            string[] lines = new string[] { "give me a sec, i'll check", "not sure, let me look", "hang on, i'll check", "let me look that up" };
            return lines[Math.Abs(seed == int.MinValue ? 0 : seed) % lines.Length];
        }

        // Deterministic meaning override applied after the small-model classifier as well as in the
        // fallback classifier. The model is allowed to resolve ambiguous turns, but it may not turn
        // an explicit personal-taste question into a wiki request merely because the subject happens
        // to be an Erenshor class/item noun. Conversely, current-event and mechanics wording remain
        // factual even when they contain conversational words such as "think".
        internal static void ApplyMeaningOverride(SemanticTurnRoute route, string original)
        {
            if (route == null || string.IsNullOrWhiteSpace(original)) return;
            string lower = original.Trim().ToLowerInvariant();

            bool currentExternalFact = Regex.IsMatch(lower, @"\b(today|latest|current|recent|news|happened|happening)\b") &&
                Regex.IsMatch(lower, @"\b(nasa|spacex|openai|world|ukraine|election|market|company|news)\b");
            bool explicitGameFact = Regex.IsMatch(lower, @"\b(?:where does|where do|how do i|how can i|what abilities|what skills|what spells|what drops|what does .* drop|what is the drop rate|where is|where are)\b") ||
                Regex.IsMatch(lower, @"\bwhat do you think (?:is|are|happened|caused|drops|drop)\b");
            if (currentExternalFact || explicitGameFact) return;

            bool personalTaste = Regex.IsMatch(lower, @"\b(?:do you (?:like|love|enjoy|prefer)|would you (?:rather|prefer)|what do you (?:like|prefer)|what(?:'s| is) your (?:favorite|favourite)|your opinion|how do you feel about|what do you think (?:about|of)|what are you (?:reading|watching|listening to|playing))\b") ||
                Regex.IsMatch(lower, @"\b(?:like|love|enjoy|prefer) being (?:a|an|the)?\s*\w+");
            if (!personalTaste) return;

            route.TurnType = lower.IndexOf("what are you reading", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("what are you watching", StringComparison.Ordinal) >= 0 ||
                lower.IndexOf("what are you listening", StringComparison.Ordinal) >= 0
                ? SemanticTurnType.SocialQuestion
                : SemanticTurnType.PersonalPreference;
            route.KnowledgeNeed = KnowledgeNeed.None;
            route.SearchQuery = string.Empty;
            route.DirectAnswerRequired = true;
            route.SocialIntent = "answer_personal_opinion";
        }

        // A class name or other topical noun must not reopen lookup after the semantic route has
        // recognized a normal social turn.
        internal static void ApplyNoRetrievalRule(SemanticTurnRoute route)
        {
            if (route == null) return;
            switch (route.TurnType)
            {
                case SemanticTurnType.Opinion:
                case SemanticTurnType.PersonalPreference:
                case SemanticTurnType.SocialQuestion:
                case SemanticTurnType.Reaction:
                case SemanticTurnType.Humor:
                case SemanticTurnType.Joke:
                case SemanticTurnType.Greeting:
                    route.KnowledgeNeed = KnowledgeNeed.None;
                    route.SearchQuery = string.Empty;
                    break;
            }
        }

        private static string NormalizeSearchQuery(string value, KnowledgeNeed need)
        {
            string clean = Bound(Regex.Replace(value ?? string.Empty, @"[\r\n<>]", " ").Trim(), 120);
            if (clean.Length < 3) return string.Empty;
            return need == KnowledgeNeed.GameWiki ? TitleWords(clean) : clean;
        }

        private static string ExtractSubject(string message)
        {
            string clean = Regex.Replace(message ?? string.Empty, @"[^A-Za-z0-9'\- ]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            return Bound(clean, 100);
        }

        private static string TitleWords(string value)
        {
            string[] words = (value ?? string.Empty).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++) if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        private static string Bound(string value, int max) { string v = (value ?? string.Empty).Trim(); return v.Length <= max ? v : v.Substring(0, max).TrimEnd(); }
        private static int StableHash(string value) { unchecked { int h = 17; string v = value ?? string.Empty; for (int i = 0; i < v.Length; i++) h = h * 31 + v[i]; return h; } }
    }

    internal sealed class SessionSocialEvent
    {
        internal long Id;
        internal DateTime Utc;
        internal SessionEventProvenance Provenance;
        internal string Type;
        internal string Text;
        internal string Topic;
        internal List<string> Participants = new List<string>();
        internal List<string> Witnesses = new List<string>();
        internal string Zone = string.Empty;
        internal int Importance;
        internal double Novelty;
        internal long ThreadId;
    }

    internal sealed class SessionChatLine
    {
        internal string Speaker;
        internal string Text;
        internal DateTime Utc;
        internal SessionEventProvenance Provenance;
        internal long ThreadId;
    }

    internal sealed class SessionConversationSeed
    {
        internal string SeedId;
        internal string TopicKey;
        internal SessionEventProvenance Provenance;
        internal string Context;
        internal DateTime CreatedUtc;
        internal DateTime ExpiresUtc;
        internal int Importance;
        internal double Novelty;
        internal double Fatigue;
        internal double ConversationPotential;
        internal List<string> EligibleSpeakers = new List<string>();
    }

    internal sealed class SocialSessionState
    {
        private const int MaxEvents = 64;
        private const int MaxChat = 12;
        private const int MaxSummaryChars = 1200;
        private readonly object _lock = new object();
        private readonly List<SessionSocialEvent> _events = new List<SessionSocialEvent>();
        private readonly List<SessionChatLine> _chat = new List<SessionChatLine>();
        private long _nextEventId = 1;
        private long _threadId;
        private string _threadTopic = string.Empty;
        private DateTime _threadExpiresUtc = DateTime.MinValue;
        private int _threadVisibleReplies;
        private long _lastReflectedEventId;
        private string _summary = string.Empty;
        private string _characterKey = string.Empty;

        internal void ResetForCharacter(string characterKey)
        {
            lock (_lock)
            {
                if (string.Equals(_characterKey, characterKey ?? string.Empty, StringComparison.Ordinal)) return;
                _characterKey = characterKey ?? string.Empty;
                _events.Clear(); _chat.Clear(); _summary = string.Empty; _threadId = 0; _threadTopic = string.Empty;
                _threadVisibleReplies = 0; _threadExpiresUtc = DateTime.MinValue; _lastReflectedEventId = 0; _nextEventId = 1;
            }
        }

        internal long BeginPlayerTurn(string player, string text, string topic, DateTime now)
        {
            lock (_lock)
            {
                bool continueThread = _threadId > 0 && now <= _threadExpiresUtc &&
                    (string.Equals(_threadTopic, topic, StringComparison.OrdinalIgnoreCase) || string.Equals(topic, "general party chat", StringComparison.OrdinalIgnoreCase));
                if (!continueThread) { _threadId++; _threadTopic = topic ?? "general"; _threadVisibleReplies = 0; }
                _threadExpiresUtc = now.AddSeconds(120.0);
                AddChatLocked(player, text, SessionEventProvenance.PlayerSaid, now, _threadId);
                AddEventLocked("player_message", text, _threadTopic, SessionEventProvenance.PlayerSaid, new string[] { player }, 35, 1.0, _threadId, now);
                return _threadId;
            }
        }

        internal void RecordVisibleSim(string speaker, string text, DateTime now)
        {
            lock (_lock)
            {
                AddChatLocked(speaker, text, SessionEventProvenance.SimSaid, now, _threadId);
                AddEventLocked("sim_message", text, _threadTopic, SessionEventProvenance.SimSaid, new string[] { speaker }, 20, 0.6, _threadId, now);
                _threadVisibleReplies++;
                _threadExpiresUtc = now.AddSeconds(120.0);
            }
        }

        internal void RecordEvent(string type, string text, string topic, SessionEventProvenance provenance, IList<string> participants, int importance, DateTime now)
        {
            lock (_lock) AddEventLocked(type, text, topic, provenance, participants, importance, 1.0, _threadId, now);
        }

        internal bool CanAddThreadReply(int hardCap) { lock (_lock) return _threadVisibleReplies < Math.Max(1, hardCap); }
        internal int PendingReflectionCount { get { lock (_lock) { int n = 0; for (int i = 0; i < _events.Count; i++) if (_events[i].Id > _lastReflectedEventId) n++; return n; } } }

        internal List<SessionSocialEvent> ReflectionDelta()
        {
            lock (_lock)
            {
                List<SessionSocialEvent> result = new List<SessionSocialEvent>();
                for (int i = 0; i < _events.Count; i++) if (_events[i].Id > _lastReflectedEventId) result.Add(_events[i]);
                return result;
            }
        }

        internal void ApplyReflection(string updatedSummary, long throughEventId)
        {
            if (string.IsNullOrWhiteSpace(updatedSummary)) return;
            lock (_lock)
            {
                _summary = Bound(updatedSummary, MaxSummaryChars);
                if (throughEventId > _lastReflectedEventId) _lastReflectedEventId = throughEventId;
            }
        }

        internal string Summary()
        {
            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(_summary)) return _summary;
                StringBuilder sb = new StringBuilder();
                int start = Math.Max(0, _events.Count - 10);
                for (int i = start; i < _events.Count; i++)
                {
                    SessionSocialEvent evt = _events[i];
                    if (evt.Provenance == SessionEventProvenance.InferenceOnly) continue;
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(evt.Type).Append(": ").Append(Bound(evt.Text, 120)).Append(".");
                    if (sb.Length >= MaxSummaryChars) break;
                }
                return Bound(sb.ToString(), MaxSummaryChars);
            }
        }

        internal List<SessionChatLine> RecentChat() { lock (_lock) return new List<SessionChatLine>(_chat); }

        internal List<SessionConversationSeed> BuildSeeds(DateTime now)
        {
            List<SessionConversationSeed> seeds = new List<SessionConversationSeed>();
            lock (_lock)
            {
                for (int i = _events.Count - 1; i >= 0 && seeds.Count < 8; i--)
                {
                    SessionSocialEvent evt = _events[i];
                    if (evt.Importance < 25 || now - evt.Utc > TimeSpan.FromMinutes(20)) continue;
                    SessionConversationSeed seed = new SessionConversationSeed();
                    seed.SeedId = "session:" + evt.Id;
                    seed.TopicKey = evt.Topic;
                    seed.Provenance = evt.Provenance;
                    seed.Context = evt.Text;
                    seed.CreatedUtc = evt.Utc;
                    seed.ExpiresUtc = evt.Utc.AddMinutes(evt.Provenance == SessionEventProvenance.VerifiedWorld ? 30 : 15);
                    seed.Importance = evt.Importance;
                    seed.Novelty = evt.Novelty;
                    seed.Fatigue = 0.0;
                    seed.ConversationPotential = Math.Min(1.0, (evt.Importance / 100.0) + (evt.Novelty * 0.5));
                    seed.EligibleSpeakers.AddRange(evt.Witnesses);
                    if (seed.ExpiresUtc >= now) seeds.Add(seed);
                }
            }
            return seeds;
        }

        internal static bool CanSpeakFirstPerson(SessionConversationSeed seed, string simName)
        {
            if (seed == null || string.IsNullOrWhiteSpace(simName)) return false;
            for (int i = 0; i < seed.EligibleSpeakers.Count; i++)
                if (string.Equals(seed.EligibleSpeakers[i], simName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal string DescribeThread()
        {
            lock (_lock) return "thread=" + _threadId + " topic=" + (_threadTopic.Length == 0 ? "none" : _threadTopic) + " visibleReplies=" + _threadVisibleReplies + " expires=" + (_threadExpiresUtc == DateTime.MinValue ? "none" : _threadExpiresUtc.ToString("HH:mm:ss"));
        }

        private void AddChatLocked(string speaker, string text, SessionEventProvenance provenance, DateTime now, long thread)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _chat.Add(new SessionChatLine { Speaker = speaker ?? string.Empty, Text = Bound(text, 300), Provenance = provenance, Utc = now, ThreadId = thread });
            while (_chat.Count > MaxChat) _chat.RemoveAt(0);
        }

        private void AddEventLocked(string type, string text, string topic, SessionEventProvenance provenance, IList<string> participants, int importance, double novelty, long thread, DateTime now)
        {
            SessionSocialEvent evt = new SessionSocialEvent { Id = _nextEventId++, Utc = now, Type = type ?? "event", Text = Bound(text, 400), Topic = Bound(topic, 80), Provenance = provenance, Importance = Math.Max(0, Math.Min(100, importance)), Novelty = Math.Max(0.0, Math.Min(1.0, novelty)), ThreadId = thread };
            if (participants != null) for (int i = 0; i < participants.Count && i < 6; i++) if (!string.IsNullOrWhiteSpace(participants[i])) evt.Participants.Add(Bound(participants[i], 40));
            // Capture witnesses at event time. A Sim joining later cannot use the event as its own memory.
            evt.Witnesses.AddRange(evt.Participants);
            _events.Add(evt);
            while (_events.Count > MaxEvents) _events.RemoveAt(0);
        }

        private static string Bound(string value, int max) { string v = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim(); return v.Length <= max ? v : v.Substring(0, max).TrimEnd(); }
    }

    internal static class DirectPreferenceTopicPolicy
    {
        // Maps an explicitly subjective player turn onto one of the existing bounded SoftPersona
        // topic buckets. This never creates a world fact; it only allows an accepted, actually visible
        // first-person opinion to become future tone continuity after the output boundary confirms it.
        internal static string Resolve(string message, PartyReplyIntent intent)
        {
            if (!PartyReplyIntentClassifier.IsSubjective(intent) || string.IsNullOrWhiteSpace(message)) return string.Empty;
            string m = message.ToLowerInvariant();
            if (Regex.IsMatch(m, @"\b(?:class|tank|tanking|heal|healing|healer|dps|reroll|arcanist|druid|paladin|reaver|stormcaller|windblade|duelist)\b")) return "class_opinion";
            if (Regex.IsMatch(m, @"\b(?:zone|place|area|vibe|atmosphere|scenery)\b")) return "zone_preference";
            if (Regex.IsMatch(m, @"\b(?:pace|pull|pulls|fast|slow|careful)\b")) return "pace_preference";
            if (Regex.IsMatch(m, @"\b(?:gear|armor|armour|weapon|looks|style|fashion)\b")) return "gear_aesthetics";
            if (Regex.IsMatch(m, @"\b(?:enemy|enemies|mob|mobs|monster|monsters|encounter design)\b")) return "enemy_design";
            if (Regex.IsMatch(m, @"\b(?:dungeon|dungeons|grind|grinding|camp|explore|exploring|adventure)\b")) return "future_activity";
            if (Regex.IsMatch(m, @"\b(?:music|listen|listening|food|snack|weather|reading|book|watching)\b")) return "ordinary_downtime";
            return string.Empty;
        }

        // A direct topic label is only permission to remember a preference if the accepted visible
        // answer actually expresses one. Generic uncertainty/acknowledgement fallbacks remain useful
        // conversation history, but must not harden into SoftPersona.
        internal static bool CanEstablishFromVisible(string topicKey, string visibleText)
        {
            if (!PreferenceMemoryPolicy.IsEligible(topicKey, visibleText)) return false;
            string t = visibleText.Trim().ToLowerInvariant();
            if (Regex.IsMatch(t, @"\b(?:not sure|don't know|do not know|can't confirm|cannot confirm|couldn't confirm|no idea|i hear you|what part|can't say|cannot say)\b")) return false;
            return Regex.IsMatch(t, @"\b(?:like|love|enjoy|prefer|rather|favorite|favourite|fun|good|great|nice|boring|stressful|hate|into it|not for me|i'd|i would|i'm|i am)\b");
        }
    }

    internal static class ConnectedBanterThreadPolicy
    {
        internal const int ManualTailReplies = 1; // A -> B. A third turn remains optional, not automatic.

        // The only legal seed for B is chat that has already become visible. The caller records A at
        // the final output boundary before invoking this method. If the exact shown opener is not the
        // newest visible line, the thread is stale or was preempted and must stop.
        internal static bool TryBuildFromVisible(IList<ConversationLine> visible, string openerSpeaker, string openerText, out List<ConversationLine> thread)
        {
            thread = new List<ConversationLine>();
            if (visible == null || visible.Count == 0 || string.IsNullOrWhiteSpace(openerSpeaker) || string.IsNullOrWhiteSpace(openerText)) return false;
            ConversationLine newest = visible[visible.Count - 1];
            if (newest == null || !string.Equals(newest.Speaker, openerSpeaker, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(newest.Text, openerText, StringComparison.Ordinal)) return false;
            int start = Math.Max(0, visible.Count - 5);
            for (int i = start; i < visible.Count; i++)
            {
                ConversationLine line = visible[i];
                if (line != null && !string.IsNullOrWhiteSpace(line.Text)) thread.Add(new ConversationLine(line.Speaker, line.Text));
            }
            return thread.Count > 0;
        }
    }

    internal static class DirectResponseFallback
    {
        internal static string ClassifyRejectionReason(string rejectionReason)
        {
            string reason = (rejectionReason ?? string.Empty).ToLowerInvariant();
            if (reason.IndexOf("topic mismatch", StringComparison.Ordinal) >= 0) return "topic_mismatch";
            if (reason.IndexOf("loot/acquisition", StringComparison.Ordinal) >= 0) return "loot_acquisition";
            if (reason.IndexOf("relationship/entities", StringComparison.Ordinal) >= 0 ||
                reason.IndexOf("retrieved game facts", StringComparison.Ordinal) >= 0) return "retrieved_relationship";
            if (reason.IndexOf("kill/clear", StringComparison.Ordinal) >= 0) return "kill_clear";
            return "other";
        }

        internal static string RenderAfterGroundingRejection(string message, string rejectionReason, SimSnapshot speaker)
        {
            string reason = (rejectionReason ?? string.Empty).ToLowerInvariant();
            if (reason.IndexOf("loot/acquisition", StringComparison.Ordinal) >= 0) return "i can't confirm that item claim";
            if (reason.IndexOf("kill/clear", StringComparison.Ordinal) >= 0) return "i can't confirm that happened";
            if (reason.IndexOf("relationship/entities", StringComparison.Ordinal) >= 0 || reason.IndexOf("retrieved game facts", StringComparison.Ordinal) >= 0) return "i couldn't confirm that cleanly from what i found";
            SemanticTurnRoute route = FallbackForRejectedDirect(message);
            return Render(message, route, speaker, false);
        }

        private static SemanticTurnRoute FallbackForRejectedDirect(string message)
        {
            SemanticTurnRoute route = SemanticTurnRouter.Fallback(message);
            if (route == null) route = new SemanticTurnRoute { TurnType = SemanticTurnType.Statement, KnowledgeNeed = KnowledgeNeed.None };
            return route;
        }

        internal static string Render(string message, SemanticTurnRoute route, SimSnapshot speaker, bool lookupFailed)
        {
            if (lookupFailed) return "couldn't find anything useful on that";
            string text = (message ?? string.Empty).Trim();
            string lower = text.ToLowerInvariant();
            if (Regex.IsMatch(lower, @"\b(i (?:like|love|prefer).{0,30}\btank|being the tank)\b")) return "yeah, tanking fits if you like setting the pace";
            if (Regex.IsMatch(lower, @"\b(i (?:like|love|prefer).{0,30}\bheal|healing)\b")) return "healing's stressful, but keeping everyone up feels good";
            if (route != null && route.TurnType == SemanticTurnType.Greeting) return "hey";
            if (route != null && route.TurnType == SemanticTurnType.DirectQuestion) return "not sure on that oneâ€”what part did you mean?";
            if (route != null && (route.TurnType == SemanticTurnType.Statement || route.TurnType == SemanticTurnType.Opinion))
            {
                string topic = string.IsNullOrWhiteSpace(route.Topic) ? "that" : route.Topic.Replace('_', ' ');
                return "yeah, i can see why you feel that way about " + topic;
            }
            return "yeah, i hear you";
        }
    }

    internal static class SocialOverhaulDeterministicTests
    {
        internal static List<string> Run()
        {
            List<string> lines = new List<string>();
            SemanticTurnRoute social = SemanticTurnRouter.Fallback("what are you guys reading tonight");
            lines.Add("[DeepSims Social] social question does not retrieve: " + Pass(social.KnowledgeNeed == KnowledgeNeed.None));
            SemanticTurnRoute windbladeOpinion = new SemanticTurnRoute { TurnType = SemanticTurnType.Opinion, KnowledgeNeed = KnowledgeNeed.GameWiki, SearchQuery = "Windblade" };
            SemanticTurnRouter.ApplyNoRetrievalRule(windbladeOpinion);
            lines.Add("[DeepSims Social] Windblade opinion overrides class lookup: " + Pass(windbladeOpinion.KnowledgeNeed == KnowledgeNeed.None && windbladeOpinion.SearchQuery.Length == 0));
            SemanticTurnRoute preference = SemanticTurnRouter.Fallback("dancer do you like being a windblade?");
            lines.Add("[DeepSims Social] personal preference does not retrieve: " + Pass(preference.TurnType == SemanticTurnType.PersonalPreference && preference.KnowledgeNeed == KnowledgeNeed.None));
            SemanticTurnRoute misroutedOpinion;
            bool misroutedParsed = SemanticTurnRouter.TryParse("TurnType=DirectQuestion\nKnowledgeNeed=GameWiki\nTopic=Windblade\nSubject=Dancer class\nSearchQuery=Windblade\nConfidence=0.91\nDirectAnswerRequired=true",
                "Dancer, do you like being a Windblade?", out misroutedOpinion);
            lines.Add("[DeepSims Social] model-misrouted class opinion is forced back to social: " + Pass(misroutedParsed && misroutedOpinion.KnowledgeNeed == KnowledgeNeed.None && misroutedOpinion.SearchQuery.Length == 0));
            SemanticTurnRoute enjoyTank = SemanticTurnRouter.Fallback("do you enjoy tanking?");
            lines.Add("[DeepSims Social] enjoy-role opinion stays social: " + Pass(enjoyTank.KnowledgeNeed == KnowledgeNeed.None));
            SemanticTurnRoute druidTaste = SemanticTurnRouter.Fallback("what do you think about being a Druid?");
            lines.Add("[DeepSims Social] class-identity opinion stays social: " + Pass(druidTaste.KnowledgeNeed == KnowledgeNeed.None));
            SemanticTurnRoute game = SemanticTurnRouter.Fallback("where does wolf meat drop?");
            lines.Add("[DeepSims Social] game question uses wiki: " + Pass(game.KnowledgeNeed == KnowledgeNeed.GameWiki));
            lines.Add("[DeepSims Social] useful exact entity query: " + Pass(game.SearchQuery.IndexOf("Wolf Meat", StringComparison.OrdinalIgnoreCase) >= 0 && game.SearchQuery.Length < 60));
            SemanticTurnRoute abilityFacts = SemanticTurnRouter.Fallback("what abilities do Windblades get?");
            lines.Add("[DeepSims Social] factual class ability contrast still uses wiki: " + Pass(abilityFacts.KnowledgeNeed == KnowledgeNeed.GameWiki));
            SemanticTurnRoute news = SemanticTurnRouter.Fallback("did anything happen with SpaceX today?");
            lines.Add("[DeepSims Social] current outside-world question uses news: " + Pass(news.KnowledgeNeed == KnowledgeNeed.ExternalNews));
            lines.Add("[DeepSims Social] lookup acknowledgement is visible: " + Pass(SemanticTurnRouter.LookupAcknowledgement(new SimSnapshot { Name = "Fiora" }, news).Length > 5));
            lines.Add("[DeepSims Social] lookup failure still replies: " + Pass(DirectResponseFallback.Render("where?", game, null, true).Length > 0));
            lines.Add("[DeepSims Social] direct opinion fallback stays relevant: " + Pass(DirectResponseFallback.Render("i like being the tank", social, null, false).IndexOf("tank", StringComparison.OrdinalIgnoreCase) >= 0));
            lines.Add("[DeepSims Social] topic-mismatch direct rejection has fallback: " + Pass(DirectResponseFallback.RenderAfterGroundingRejection("do you like being a Windblade?", "topic mismatch for selected class_opinion", null).Length > 0));
            lines.Add("[DeepSims Social] loot/acquisition direct rejection has fallback: " + Pass(DirectResponseFallback.RenderAfterGroundingRejection("did we get that item?", "unsupported loot/acquisition assertion", null).Length > 0));
            lines.Add("[DeepSims Social] retrieved-relationship direct rejection has fallback: " + Pass(DirectResponseFallback.RenderAfterGroundingRejection("where does it come from?", "answer relationship/entities are not supported by the retrieved game facts", null).Length > 0));
            lines.Add("[DeepSims Social] kill/clear direct rejection has fallback: " + Pass(DirectResponseFallback.RenderAfterGroundingRejection("did we clear that?", "unsupported kill/clear assertion", null).Length > 0));
            lines.Add("[DeepSims Social] rejection categories are privacy-safe and stable: " + Pass(
                DirectResponseFallback.ClassifyRejectionReason("topic mismatch for selected other_sim_preference") == "topic_mismatch" &&
                DirectResponseFallback.ClassifyRejectionReason("unsupported loot/acquisition assertion") == "loot_acquisition" &&
                DirectResponseFallback.ClassifyRejectionReason("answer relationship/entities are not supported by the retrieved game facts") == "retrieved_relationship" &&
                DirectResponseFallback.ClassifyRejectionReason("unsupported kill/clear assertion") == "kill_clear"));
            lines.Add("[DeepSims Social] direct class opinion gets SoftPersona topic: " + Pass(DirectPreferenceTopicPolicy.Resolve("Dancer, do you like being a Windblade?", PartyReplyIntent.Opinion) == "class_opinion"));
            lines.Add("[DeepSims Social] direct reading opinion gets downtime SoftPersona topic: " + Pass(DirectPreferenceTopicPolicy.Resolve("what are you reading tonight?", PartyReplyIntent.SocialBanter) == "ordinary_downtime"));
            lines.Add("[DeepSims Social] factual turn never becomes SoftPersona: " + Pass(DirectPreferenceTopicPolicy.Resolve("what abilities do Windblades get?", PartyReplyIntent.FactualGameQuestion).Length == 0));
            lines.Add("[DeepSims Social] actual visible opinion may establish SoftPersona: " + Pass(DirectPreferenceTopicPolicy.CanEstablishFromVisible("class_opinion", "i like being a windblade")));
            lines.Add("[DeepSims Social] generic direct fallback does not become SoftPersona: " + Pass(!DirectPreferenceTopicPolicy.CanEstablishFromVisible("class_opinion", "not sure on that one")));

            List<SimPreferenceMemory> rememberedPrefs = new List<SimPreferenceMemory>();
            rememberedPrefs.Add(new SimPreferenceMemory { TopicKey = "class_opinion", Statement = "i like being a windblade", TimesExpressed = 1 });
            lines.Add("[DeepSims Social] class-name question retrieves prior SoftPersona: " + Pass(PreferenceMemoryPolicy.Select(rememberedPrefs, "do you like being a Windblade?", 1).Count == 1));
            rememberedPrefs.Add(new SimPreferenceMemory { TopicKey = "ordinary_downtime", Statement = "i am reading an old travel journal", TimesExpressed = 1 });
            lines.Add("[DeepSims Social] reading question retrieves prior downtime SoftPersona: " + Pass(PreferenceMemoryPolicy.Select(rememberedPrefs, "what are you reading tonight?", 1).Count == 1));

            SocialSessionState state = new SocialSessionState();
            state.ResetForCharacter("slot-1-a");
            long thread = state.BeginPlayerTurn("Player", "i like being the tank", "class role preferences", DateTime.UtcNow);
            state.RecordVisibleSim("Fiora", "yeah, tanking fits if you like setting the pace", DateTime.UtcNow);
            state.RecordVisibleSim("Phanty", "healing has its own kind of stress too", DateTime.UtcNow);
            List<SessionChatLine> recent = state.RecentChat();
            lines.Add("[DeepSims Social] Sim B receives Sim A visible history: " + Pass(recent.Count == 3 && recent[1].Speaker == "Fiora" && recent[2].Speaker == "Phanty"));
            List<ConversationLine> visibleBanter = new List<ConversationLine>();
            visibleBanter.Add(new ConversationLine("Astra", "windblade looks fun to me"));
            List<ConversationLine> banterThread;
            bool banterBuilt = ConnectedBanterThreadPolicy.TryBuildFromVisible(visibleBanter, "Astra", "windblade looks fun to me", out banterThread);
            lines.Add("[DeepSims Social] connected banter B receives exact accepted A text: " + Pass(banterBuilt && banterThread.Count == 1 && banterThread[0].Text == "windblade looks fun to me"));
            List<ConversationLine> rejectedCandidateNotVisible;
            bool rejectedSeeded = ConnectedBanterThreadPolicy.TryBuildFromVisible(visibleBanter, "Astra", "unshown rejected candidate", out rejectedCandidateNotVisible);
            lines.Add("[DeepSims Social] rejected unshown A cannot seed B: " + Pass(!rejectedSeeded));
            lines.Add("[DeepSims Social] manual connected banter hard maximum is A plus one tail: " + Pass(ConnectedBanterThreadPolicy.ManualTailReplies == 1));
            lines.Add("[DeepSims Social] player generation preempts autonomous tail: " + Pass(ConversationTurnGuard.IsStale(4, 5)));
            long continued = state.BeginPlayerTurn("Player", "yeah, but healing looks fun too", "class role preferences", DateTime.UtcNow.AddSeconds(4));
            lines.Add("[DeepSims Social] player continues active thread: " + Pass(continued == thread));
            lines.Add("[DeepSims Social] conversation budget stops tails: " + Pass(!state.CanAddThreadReply(2)));
            for (int i = 0; i < 80; i++) state.RecordEvent("event", "event " + i, "event", SessionEventProvenance.VerifiedWorld, null, 30, DateTime.UtcNow);
            lines.Add("[DeepSims Social] event journal bounded: " + Pass(state.ReflectionDelta().Count <= 64));
            string prior = state.Summary();
            state.ApplyReflection(string.Empty, 999);
            lines.Add("[DeepSims Social] reflection failure preserves summary: " + Pass(state.Summary() == prior));
            state.ApplyReflection(new string('x', 1600), 999);
            lines.Add("[DeepSims Social] rolling summary bounded: " + Pass(state.Summary().Length <= 1200));
            lines.Add("[DeepSims Social] reflection consumes delta only: " + Pass(state.ReflectionDelta().Count == 0));
            lines.Add("[DeepSims Social] actual events produce seeds: " + Pass(state.BuildSeeds(DateTime.UtcNow).Count > 0));
            SocialSessionState witnessState = new SocialSessionState();
            witnessState.ResetForCharacter("slot-witness");
            witnessState.RecordEvent("expedition", "Reached Bonepits.", "bonepits", SessionEventProvenance.VerifiedWorld,
                new string[] { "Brinon", "Fiora", "Phanty", "Dancer" }, 60, DateTime.UtcNow);
            List<SessionConversationSeed> witnessedSeeds = witnessState.BuildSeeds(DateTime.UtcNow);
            lines.Add("[DeepSims Social] new Sim cannot claim unwitnessed session event: " + Pass(witnessedSeeds.Count == 1 && !SocialSessionState.CanSpeakFirstPerson(witnessedSeeds[0], "Astra")));
            lines.Add("[DeepSims Social] witness can use shared event seed: " + Pass(witnessedSeeds.Count == 1 && SocialSessionState.CanSpeakFirstPerson(witnessedSeeds[0], "Dancer")));
            lines.Add("[DeepSims Social] stale seeds expire: " + Pass(state.BuildSeeds(DateTime.UtcNow.AddHours(2)).Count == 0));
            state.ResetForCharacter("slot-2-b");
            lines.Add("[DeepSims Social] no cross-character session leakage: " + Pass(state.RecentChat().Count == 0 && state.ReflectionDelta().Count == 0));
            lines.Add("[DeepSims Social] SoftPersona distinct from VerifiedWorld: " + Pass(SessionEventProvenance.SoftPersona != SessionEventProvenance.VerifiedWorld));

            SemanticTurnRoute parsed;
            bool parsedOk = SemanticTurnRouter.TryParse("TurnType=DirectQuestion\nKnowledgeNeed=None\nTopic=books\nSubject=party reading\nSearchQuery=\nConfidence=0.91\nDirectAnswerRequired=true", "what are you reading?", out parsed);
            lines.Add("[DeepSims Social] structured semantic route parses: " + Pass(parsedOk && parsed.DirectAnswerRequired && parsed.KnowledgeNeed == KnowledgeNeed.None));
            List<ConversationLine> compactThread = new List<ConversationLine>();
            compactThread.Add(new ConversationLine { Speaker = "Fiora", Text = "i usually like the quieter zones" });
            compactThread.Add(new ConversationLine { Speaker = "Player", Text = "what do you like about them?" });
            List<ChatMessage> compact = PromptBuilder.BuildCompactDirectPartyReply(
                new SimSnapshot { Name = "Phanty", ClassName = "Druid", Personality = "thoughtful" }, null,
                new WorldSnapshot { Scene = "Hidden Hills" }, compactThread, null, social, new string('s', 1200));
            bool currentPreserved = false;
            for (int i = 0; i < compact.Count; i++)
                if (compact[i] != null && compact[i].content != null && compact[i].content.IndexOf("what do you like about them?", StringComparison.Ordinal) >= 0) currentPreserved = true;
            lines.Add("[DeepSims Social] compact direct prompt fits numCtx=2048: " + Pass(PromptBuilder.EstimateTokenCount(compact) < 1500));
            lines.Add("[DeepSims Social] compact prompt preserves newest player turn: " + Pass(currentPreserved));
            GroupMessageQueue queue = new GroupMessageQueue();
            ConnectedBanterPlan plan = new ConnectedBanterPlan { RemainingReplies = 1, TopicKey = "class_opinion", ManualThread = true };
            queue.Enqueue(DateTime.UtcNow.AddSeconds(-1), "Astra", "visible opener", true, 5, "manual_banter", 1, 1, "sim:astra", "manual_banter", DateTime.UtcNow, 2, "class_opinion", plan);
            List<ScheduledGroupMessage> scheduled = queue.TakeDue(DateTime.UtcNow);
            lines.Add("[DeepSims Social] queue preserves visible-only continuation metadata: " + Pass(scheduled.Count == 1 && scheduled[0].ConnectedBanter == plan && scheduled[0].SoftPreferenceTopicKey == "class_opinion"));
            return lines;
        }

        private static string Pass(bool value) { return value ? "PASS" : "FAIL"; }
    }
}
