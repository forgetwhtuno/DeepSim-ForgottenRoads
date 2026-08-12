using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    [Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage() { }
        public ChatMessage(string roleValue, string contentValue)
        {
            role = roleValue;
            content = contentValue;
        }
    }

    [Serializable]
    public class MemoryEvent
    {
        public string utc;
        public string type;
        public string text;
        public int importance;
    }

    [Serializable]
    public class SimRelationshipMemory
    {
        public string OtherSimKey;
        public string OtherName;
        public int SharedOutings;
        public int SharedMinutes;
        public int SharedConversationThreads;
        public int PositiveExchanges;
        public int CompetitiveExchanges;
        public int VerifiedPracticeDuels;
        public float Familiarity;
        public float Rapport;
        public float Rivalry;
        public string LastSharedUtc;
    }

    [Serializable]
    public class SimPreferenceMemory
    {
        public string TopicKey;
        public string Statement;
        public int TimesExpressed;
        public string UpdatedUtc;
    }

    [Serializable]
    public class SimMemory
    {
        public string SimKey;
        public string Name;
        public string FirstSeenUtc;
        public string LastSeenUtc;
        public string LastKnownScene;
        public string LastKnownClass;
        public string LastKnownPersonality;
        public int LastKnownLevel;
        public int GroupSessions;
        public int CompletedOutings;
        public float Familiarity;
        public float Rapport;
        public float Rivalry;
        public int ConversationExchanges;
        public int PositivePlayerExchanges;
        public int CompetitivePlayerExchanges;
        public int VerifiedPracticeDuels;
        public int RelationshipDataVersion;
        public List<MemoryEvent> RecentEvents;
        public List<string> ImportantMemories;
        public List<string> RecentGroupChat;
        public List<ChatMessage> Conversation;
        public List<string> OutingSummaries;
        public string LastOutingUtc;
        public int TotalGroupedMinutes;
        public List<string> ConversationSummaries;
        public List<SimRelationshipMemory> SimRelationships;
        // Flavor continuity only. These are the Sim's own previously emitted opinions, never
        // authoritative game facts and never part of the verified event corpus.
        public List<SimPreferenceMemory> Preferences;

        public void Normalize()
        {
            if (RecentEvents == null) RecentEvents = new List<MemoryEvent>();
            if (ImportantMemories == null) ImportantMemories = new List<string>();
            if (RecentGroupChat == null) RecentGroupChat = new List<string>();
            if (Conversation == null) Conversation = new List<ChatMessage>();
            if (OutingSummaries == null) OutingSummaries = new List<string>();
            if (ConversationSummaries == null) ConversationSummaries = new List<string>();
            if (SimRelationships == null) SimRelationships = new List<SimRelationshipMemory>();
            if (Preferences == null) Preferences = new List<SimPreferenceMemory>();
            if (Preferences.Count > 8) Preferences.RemoveRange(0, Preferences.Count - 8);
            if (LastOutingUtc == null) LastOutingUtc = string.Empty;
            if (Name == null) Name = string.Empty;
            if (SimKey == null) SimKey = string.Empty;
            if (FirstSeenUtc == null) FirstSeenUtc = string.Empty;
            if (LastSeenUtc == null) LastSeenUtc = string.Empty;
            if (LastKnownScene == null) LastKnownScene = string.Empty;
            if (LastKnownClass == null) LastKnownClass = string.Empty;
            if (LastKnownPersonality == null) LastKnownPersonality = string.Empty;
            RelationshipModel.Normalize(this);
        }
    }

    public class SimSnapshot
    {
        public string Key;
        public string Name;
        public string ClassName;
        public string Scene;
        public string Personality;
        public string PersonalityRaw;
        public int PersonalityCode;
        public string Bio;
        public string RefersToSelfAs;
        public string SignOff;
        public int Level;
        public int SkillLevel;
        public int TypoRate;
        public int Greed;
        public int Patience;
        public int GearChase;
        public bool TypesInAllCaps;
        public bool TypesInAllLowers;
        public bool TypesInThirdPerson;
        public bool LovesEmojis;
        public bool Abbreviates;
        public bool Rival;
        public string TiedToSlot;
        public int GuildId;
        public string GuildName;
        public string CombatRole;
        // Exact, read-only Erenshor Manage Roles assignments. Empty + known means no role is
        // assigned to this Sim; unknown means the native grouping state could not be read safely.
        public bool RoleAssignmentsKnown;
        public List<string> AssignedRoles;
        public string CurrentAction;
        public string CurrentTarget;
        public float CurrentHp;
        public float MaxHp;
        public float HpPercent;
        public bool IsDead;
        public List<string> DialogueExamples;
        public SimPlayer RuntimeSim;

        public string SummaryLine()
        {
            string shortPersonality = Personality;
            if (!Rival && PersonalityCode > 3) shortPersonality = "P" + PersonalityCode;
            if (string.IsNullOrWhiteSpace(shortPersonality)) shortPersonality = "?";
            string guild = string.IsNullOrWhiteSpace(GuildName) ? string.Empty : ", guild: " + GuildName;
            string role = string.IsNullOrWhiteSpace(CombatRole) ? string.Empty : ", class role: " + CombatRole;
            if (RoleAssignmentsKnown && AssignedRoles != null && AssignedRoles.Count > 0)
                role += ", assigned: " + string.Join("/", AssignedRoles.ToArray());
            string action = string.IsNullOrWhiteSpace(CurrentAction) ? string.Empty : ", now: " + CurrentAction;
            return Name + " (L" + Level + " " + ClassName + ", " + shortPersonality + guild + role + action + ")";
        }
    }

    public class PlayerSnapshot
    {
        public string Name;
        public string ClassName;
        public int Level;
        public float CurrentHp;
        public float MaxHp;
        public float HpPercent;
        public bool IsDead;
    }

    public class WorldSnapshot
    {
        public string Scene;
        public PlayerSnapshot Player;
        public List<SimSnapshot> Party;
        public OutingSnapshot Outing;
        public CampContextFacts Camp;
    }

    // Verified facts read from the optional Erenshor Campmaster mod via
    // CampmasterBridge. Every field mirrors a key CampmasterApi only sets when
    // the underlying native fact was actually verified; an unset field must
    // never be rendered as a claim (see AGENTS.md trust hierarchy: OBSERVED_NOW).
    public class CampContextFacts
    {
        public bool Active;
        public string State;
        public string Recognition;
        public string Activity;
        public string Zone;
        public string Party;
        public int? ElapsedMinutes;
        public string Puller;
        public string MainTank;
        public string MainAssist;
        public bool AutoPullEnabledKnown;
        public bool AutoPullEnabled;
        public bool HoldManaPercentKnown;
        public int HoldManaPercent;
        public int CompletedEncounters;
    }

    public class CampEventFact
    {
        public long Sequence;
        public string Type;
        public string Zone;
        public string Detail;
    }

    public class EncounterSnapshot
    {
        public int Id;
        public string StartedUtc;
        public string EndedUtc;
        public string PrimaryEnemy;
        public List<string> EnemyTypes;
        public List<string> NotableSimActions;
        public int TotalKills;
        public int CloseCalls;
        public int Deaths;
        public int DurationSeconds;
        public string Zone;
        public string Result;
        public string Summary;
    }

    public class OutingSnapshot
    {
        public bool Active;
        public int Minutes;
        public string CurrentZone;
        public string Activity;
        public string Mood;
        public List<string> Facts;
        public int TotalKills;
        public int TotalLootItems;
        public int UniqueEnemies;
        public int UniqueLoot;
        public int Gold;
        public int Experience;
        public string ZoneHistory;
        public string CurrentCombatTarget;
        public string CurrentEncounter;
        public string LastEncounter;
        public List<string> RecentEncounters;
        public EncounterSnapshot LastCompletedEncounter;
        public List<EncounterSnapshot> RecentCompletedEncounters;
    }

    public class ConversationLine
    {
        public string Speaker;
        public string Text;

        public ConversationLine() { }
        public ConversationLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    public class WikiResult
    {
        public string Query;
        public string Title;
        public string Extract;
        public string Url;
        public string SourceLabel;
        public bool Found;
    }
}
