using System;
using System.Collections.Generic;

namespace ErenshorDeepSims
{
    // Plain-data typing queue. Every eventual party-chat line retains the conversation generation
    // and diagnostic scope that owned it so the Unity-thread display boundary can revalidate it.
    internal class ScheduledGroupMessage
    {
        internal DateTime DueUtc;
        internal DateTime EnqueuedUtc;
        internal string Speaker;
        internal string Text;
        internal bool Autonomous;
        internal int ConversationGeneration = -1;
        internal string DiagnosticContext = string.Empty;
    }

    internal class GroupMessageQueue
    {
        private readonly object _lock = new object();
        private readonly List<ScheduledGroupMessage> _items = new List<ScheduledGroupMessage>();

        internal void Enqueue(DateTime dueUtc, string speaker, string text, bool autonomous = false,
            int conversationGeneration = -1, string diagnosticContext = null)
        {
            if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(text)) return;
            lock (_lock)
            {
                ScheduledGroupMessage item = new ScheduledGroupMessage();
                item.DueUtc = dueUtc;
                item.EnqueuedUtc = DateTime.UtcNow;
                item.Speaker = speaker;
                item.Text = text;
                item.Autonomous = autonomous;
                item.ConversationGeneration = conversationGeneration;
                item.DiagnosticContext = diagnosticContext ?? string.Empty;
                _items.Add(item);
                _items.Sort(delegate(ScheduledGroupMessage a, ScheduledGroupMessage b) { return a.DueUtc.CompareTo(b.DueUtc); });
            }
        }

        internal List<ScheduledGroupMessage> TakeDue(DateTime now)
        {
            List<ScheduledGroupMessage> due = new List<ScheduledGroupMessage>();
            lock (_lock)
            {
                int count = 0;
                while (count < _items.Count && _items[count].DueUtc <= now) count++;
                if (count > 0)
                {
                    due.AddRange(_items.GetRange(0, count));
                    _items.RemoveRange(0, count);
                }
            }
            return due;
        }

        internal List<ScheduledGroupMessage> Clear()
        {
            lock (_lock)
            {
                List<ScheduledGroupMessage> removed = new List<ScheduledGroupMessage>(_items);
                _items.Clear();
                return removed;
            }
        }

        internal List<ScheduledGroupMessage> ClearAutonomous()
        {
            lock (_lock)
            {
                List<ScheduledGroupMessage> removed = new List<ScheduledGroupMessage>();
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (!_items[i].Autonomous) continue;
                    removed.Add(_items[i]);
                    _items.RemoveAt(i);
                }
                return removed;
            }
        }

        internal int Count
        {
            get { lock (_lock) return _items.Count; }
        }
    }
}
