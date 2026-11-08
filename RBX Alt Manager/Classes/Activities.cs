using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RBX_Alt_Manager.Classes
{
    public enum ActivityKind { Login, LoginQueue, BanCheck, FriendRequests, Joining }

    /// <summary>One long-running job (a login window, the group login queue, the ban check …).</summary>
    public sealed class Activity
    {
        public ActivityKind Kind { get; internal set; }
        public string Text { get; internal set; }
        public DateTime Started { get; internal set; }
        public CancellationTokenSource Cancel { get; internal set; }
    }

    /// <summary>
    /// The background jobs as one explicit state: Idle when the list is empty, otherwise "these jobs are running".
    /// Before, every job had its own flag / token field (LoginRunning, LoginQueueCts, BanTokenSource, FriendTokenSource …)
    /// and nothing showed them when you were on another page. Now:
    ///   – the header shows what is running (also on the settings / server pages),
    ///   – code asks Activities.IsRunning(kind) instead of reading private flags,
    ///   – the log gets "[Activity] start / end" lines with the duration.
    /// Only the job itself calls Begin / Update / End (always End in a finally block).
    /// </summary>
    public static class Activities
    {
        private static readonly List<Activity> List = new List<Activity>();

        /// <summary>Raised on every start, update and end. May come from a background thread.</summary>
        public static event Action Changed;

        public static IReadOnlyList<Activity> Running { get { lock (List) return List.ToList(); } }

        public static bool Any { get { lock (List) return List.Count > 0; } }

        public static bool IsRunning(ActivityKind Kind) { lock (List) return List.Any(a => a.Kind == Kind); }

        public static Activity Begin(ActivityKind Kind, string Text, CancellationTokenSource Cancel = null)
        {
            var A = new Activity() { Kind = Kind, Text = Text, Started = DateTime.Now, Cancel = Cancel };

            lock (List) List.Add(A);

            Program.Logger?.Info($"[Activity] start {Kind}: {Text}");
            Raise();
            return A;
        }

        public static void Update(Activity A, string Text)
        {
            if (A == null || A.Text == Text) return;

            A.Text = Text;
            Raise();
        }

        public static void End(Activity A)
        {
            if (A == null) return;

            bool Removed;
            lock (List) Removed = List.Remove(A);

            if (!Removed) return;

            Program.Logger?.Info($"[Activity] end {A.Kind} after {(int)(DateTime.Now - A.Started).TotalSeconds} s");
            Raise();
        }

        private static void Raise()
        {
            try { Changed?.Invoke(); } catch { }
        }
    }
}
