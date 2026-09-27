using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyShapez.Buildings
{
    public sealed class FactoryIssue<T> where T : class
    {
        public T Machine { get; internal set; }
        public MachineFeedback Feedback { get; internal set; }
        public float Since { get; internal set; }
    }

    public sealed class FactoryIssueTracker<T> where T : class
    {
        private readonly Dictionary<T, FactoryIssue<T>> candidates = new();
        private readonly HashSet<T> seen = new();
        public float NeedsPropertyDelay { get; set; } = 1.5f;
        public float InvalidRecipeDelay { get; set; } = 0.75f;
        public float OutputBlockedDelay { get; set; } = 0.75f;
        public IReadOnlyList<FactoryIssue<T>> Issues { get; private set; } =
            Array.Empty<FactoryIssue<T>>();

        public void Observe(T machine, MachineFeedback feedback, float now)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            seen.Add(machine);
            if (!Eligible(feedback.State))
            {
                candidates.Remove(machine);
                return;
            }
            if (!candidates.TryGetValue(machine, out FactoryIssue<T> issue) ||
                issue.Feedback.State != feedback.State ||
                issue.Feedback.Ports != feedback.Ports)
            {
                candidates[machine] = new FactoryIssue<T>
                {
                    Machine = machine, Feedback = feedback, Since = now
                };
            }
            else issue.Feedback = feedback;
        }

        public void EndFrame(float now)
        {
            foreach (T key in candidates.Keys.Where(key => !seen.Contains(key)).ToArray())
                candidates.Remove(key);
            seen.Clear();
            Issues = candidates.Values.Where(issue =>
                now - issue.Since >= Delay(issue.Feedback.State))
                .OrderBy(issue => Priority(issue.Feedback.State))
                .ThenBy(issue => issue.Since).ToArray();
        }

        public void Clear()
        {
            candidates.Clear();
            seen.Clear();
            Issues = Array.Empty<FactoryIssue<T>>();
        }

        private static bool Eligible(MachineFeedbackState state) => state is
            MachineFeedbackState.NeedsProperty or MachineFeedbackState.InvalidRecipe or
            MachineFeedbackState.OutputBlocked;

        private float Delay(MachineFeedbackState state) => state switch
        {
            MachineFeedbackState.OutputBlocked => OutputBlockedDelay,
            MachineFeedbackState.InvalidRecipe => InvalidRecipeDelay,
            MachineFeedbackState.NeedsProperty => NeedsPropertyDelay,
            _ => float.PositiveInfinity
        };

        private static int Priority(MachineFeedbackState state) => state switch
        {
            MachineFeedbackState.OutputBlocked => 0,
            MachineFeedbackState.InvalidRecipe => 1,
            MachineFeedbackState.NeedsProperty => 2,
            _ => 3
        };
    }
}
