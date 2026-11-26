using System;
using System.Collections.Generic;
using System.Linq;

namespace ThoraxLab.Learning
{
    public sealed class AssessmentEngine
    {
        readonly ScenarioVariant definition;
        readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        public AttemptRecord Attempt { get; }
        public string Feedback { get; private set; } = "Follow the objective in the specimen.";
        public ObjectiveDefinition Current => definition.objectives.FirstOrDefault(o => !IsComplete(o.id));
        public bool AllComplete => definition.objectives.Length > 0 && definition.objectives.All(o => IsComplete(o.id));

        public AssessmentEngine(ScenarioVariant definition, AttemptRecord attempt)
        {
            if (definition == null || definition.objectives == null || definition.objectives.Length == 0)
                throw new ArgumentException("An assessment needs authored objectives.");
            if (definition.objectives.Any(o => string.IsNullOrWhiteSpace(o.id)) || definition.objectives.Select(o => o.id).Distinct().Count() != definition.objectives.Length)
                throw new ArgumentException("Objective identifiers must be unique.");
            this.definition = definition;
            Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
            if (!string.IsNullOrEmpty(definition.version) && Attempt.contentVersion != definition.version)
                throw new ArgumentException("This attempt belongs to a different content version. Start a new activity; its history is preserved.");
            Attempt.objectives = definition.objectives.Select(o => new ObjectiveResult { objectiveId = o.id }).ToList();
            Attempt.mistakes = 0;
            Attempt.criticalFailure = false;
            var history = Attempt.actions.ToArray();
            Attempt.actions.Clear();
            foreach (var action in history) Apply(action, true);
        }

        public bool IsComplete(string id) => Attempt.objectives.Any(o => o.objectiveId == id && o.completed);
        public bool Apply(InteractionAction action) => Apply(action, false);

        bool Apply(InteractionAction action, bool replay)
        {
            if ((!replay && Attempt.status != AttemptStatus.Active) || action == null || string.IsNullOrEmpty(action.id) || !seen.Add(action.id)) return false;
            if (Attempt.actions.Count >= 4096) { Feedback = "This attempt reached its event limit. Finish or restart the activity."; return false; }
            if (!Finite(action.distance) || !Finite(action.angle) || !Finite(action.contactSeconds) || !Finite(action.pathFraction) || action.distance < 0 || action.angle < 0 || action.contactSeconds < 0 || action.pathFraction < 0 || action.pathFraction > 1) return false;
            action.sequence = Attempt.actions.Count;
            Attempt.actions.Add(action);
            if (action.kind == ActionKind.ProtectedStructureViolation)
            {
                if(!action.contactVerified || (definition.protectedIds.Length>0 && !definition.protectedIds.Contains(action.targetId)))return false;
                Attempt.criticalFailure = true;
                Attempt.mistakes++;
                Feedback = "A protected structure was affected. Review its relationship and retry the assessment.";
                Recalculate();
                return false;
            }
            var candidates = definition.objectives.Where(o => !IsComplete(o.id) && o.action == action.kind && o.targetId == action.targetId);
            if (definition.ordered || definition.difficulty == Difficulty.Easy || definition.mode == LearningMode.GuidedDissection)
                candidates = candidates.Where(o => o == Current);
            var objective = candidates.FirstOrDefault(o => o.prerequisites.All(IsComplete));
            if (objective == null)
            {
                // Repeated observations of already-completed objectives are harmless.
                if (!definition.objectives.Any(o => IsComplete(o.id) && o.action == action.kind && o.targetId == action.targetId))
                {
                    Attempt.mistakes++;
                    Feedback = "That action does not complete the current objective. Recheck the structure and sequence.";
                }
                Recalculate();
                return false;
            }
            if (!Matches(objective, action))
            {
                Attempt.mistakes++;
                var choice = objective.action == ActionKind.ClinicalDecision ? objective.choices.FirstOrDefault(c => c.id == action.toolId) : null;
                if (choice != null && objective.criticalOnWrong && action.contactVerified && action.distance <= objective.maxDistance && action.equipmentState == objective.equipmentState && action.toolId != objective.toolId) Attempt.criticalFailure = true;
                Feedback = choice?.rationale ?? "Check the tool, alignment, contact and equipment state before continuing.";
                Recalculate();
                return false;
            }
            var result = Attempt.objectives.First(o => o.objectiveId == objective.id);
            result.completed = true;
            result.actionId = action.id;
            Feedback = objective.explanation;
            Recalculate();
            return true;
        }

        public static bool Matches(ObjectiveDefinition objective, InteractionAction action)
        {
            if (!action.contactVerified || objective.action != action.kind || objective.targetId != action.targetId) return false;
            if (!string.IsNullOrEmpty(objective.toolId) && objective.toolId != action.toolId) return false;
            if (!string.IsNullOrEmpty(objective.equipmentState) && objective.equipmentState != action.equipmentState) return false;
            return action.distance <= objective.maxDistance && action.angle <= objective.maxAngle && action.contactSeconds >= objective.minContactSeconds && action.pathFraction >= objective.minPathFraction;
        }

        public void MarkAssisted() { Attempt.assisted = true; Recalculate(); }
        public void Finish()
        {
            Recalculate();
            Attempt.status = AttemptStatus.Completed;
            Attempt.completedUtc = DateTime.UtcNow.ToString("o");
        }
        void Recalculate()
        {
            float total = definition.objectives.Sum(o => Math.Max(0.01f, o.weight));
            float completed = definition.objectives.Where(o => IsComplete(o.id)).Sum(o => Math.Max(0.01f, o.weight));
            Attempt.score = Math.Max(0, 100 * completed / total - Math.Min(50, Attempt.mistakes * 5));
            Attempt.passed = Attempt.score >= 80 && !Attempt.criticalFailure && !Attempt.assisted && definition.objectives.Where(o => o.essential).All(o => IsComplete(o.id));
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class ProgressRules
    {
        public static bool HasCompletedActivity(ProgressSnapshot progress, ScenarioVariant variant, SessionPhase? phase = null)
        {
            return variant.objectives.Length > 0 && progress.attempts.Any(a => a != null &&
                a.userId == progress.userId && a.resetGeneration == progress.resetGeneration &&
                a.scenarioId == variant.id && a.contentVersion == variant.version && a.status == AttemptStatus.Completed &&
                (!phase.HasValue || a.phase == phase.Value) && a.objectives != null &&
                variant.objectives.All(step => a.objectives.Any(result => result != null && result.objectiveId == step.id && result.completed)));
        }

        public static bool MergeCheckpoint(ProgressSnapshot progress, ActivityCheckpoint incoming, string activeAttemptId = null)
        {
            if (incoming == null || incoming.resetGeneration != progress.resetGeneration) return false;
            var attempt = incoming.attempt;
            if (attempt == null || attempt.userId != progress.userId || attempt.id != incoming.attemptId ||
                attempt.resetGeneration != progress.resetGeneration || attempt.status != AttemptStatus.Active ||
                attempt.scenarioId != incoming.scenarioId || attempt.phase != incoming.phase || attempt.actions == null)
                throw new InvalidOperationException("Checkpoint identity mismatch.");
            var existing = progress.attempts.FirstOrDefault(a => a.id == attempt.id);
            // An in-flight response cannot replace work being performed on this device,
            // nor resurrect an attempt whose completed record has already arrived.
            if (existing != null && (existing.status != AttemptStatus.Active || existing.id == activeAttemptId)) return false;
            var previous = progress.checkpoints.FirstOrDefault(c => c.attemptId == attempt.id);
            if (!DateTimeOffset.TryParse(incoming.savedUtc, out var incomingTime)) return false;
            if (previous != null && DateTimeOffset.TryParse(previous.savedUtc, out var previousTime) && incomingTime <= previousTime) return false;
            if (existing != null && (attempt.actions.Count < existing.actions.Count ||
                !existing.actions.Select(a => a.id).SequenceEqual(attempt.actions.Take(existing.actions.Count).Select(a => a.id))))
                return false;
            AddAttempt(progress, attempt);
            progress.checkpoints.RemoveAll(c => c.attemptId == attempt.id);
            progress.checkpoints.Add(incoming);
            return true;
        }

        public static bool IsUnlocked(ProgressSnapshot progress, LearningMode mode, string unitId, Difficulty difficulty, string contentVersion = LearningContent.CurrentVersion)
        {
            if (progress == null || !progress.orientationCompleted || !Enum.IsDefined(typeof(Difficulty), difficulty)) return false;
            for (int prerequisite = 0; prerequisite < (int)difficulty; prerequisite++)
                if (!progress.attempts.Any(a => a.userId == progress.userId && a.resetGeneration == progress.resetGeneration &&
                    a.mode == mode && a.unitId == unitId && a.contentVersion == contentVersion && (int)a.difficulty == prerequisite &&
                    a.phase == SessionPhase.Assessment && a.status == AttemptStatus.Completed && a.passed &&
                    !a.assisted && !a.criticalFailure && !float.IsNaN(a.score) && !float.IsInfinity(a.score) && a.score >= 80 &&
                    a.objectives != null && a.objectives.Any(o => o.completed))) return false;
            return true;
        }

        public static bool CanResume(AttemptRecord attempt, ScenarioVariant definition)
        {
            return attempt != null && definition != null && !string.IsNullOrEmpty(definition.version) &&
                !string.IsNullOrEmpty(definition.id) && attempt.status == AttemptStatus.Active &&
                attempt.contentVersion == definition.version && attempt.scenarioId == definition.id &&
                attempt.unitId == definition.unitId && attempt.mode == definition.mode && attempt.difficulty == definition.difficulty;
        }

        public static bool AddAttempt(ProgressSnapshot progress, AttemptRecord attempt)
        {
            if (attempt.userId != progress.userId || attempt.resetGeneration != progress.resetGeneration) throw new InvalidOperationException("Account or reset generation mismatch.");
            int index = progress.attempts.FindIndex(a => a.id == attempt.id);
            if (index >= 0 && progress.attempts[index].status == AttemptStatus.Completed) return false;
            if (index >= 0) progress.attempts[index] = attempt; else progress.attempts.Add(attempt);
            AwardMasteredObjectives(progress, attempt);
            progress.savedUtc = DateTime.UtcNow.ToString("o");
            return true;
        }

        // Rewards are derived from mastery evidence, never from attempt IDs or phase completions.
        // Rebuild on load and cloud reconciliation so older saves cannot retain duplicate rewards.
        public static void RebuildRewards(ProgressSnapshot progress)
        {
            progress.rewards.Clear();
            foreach (var attempt in progress.attempts) AwardMasteredObjectives(progress, attempt);
        }

        static void AwardMasteredObjectives(ProgressSnapshot progress, AttemptRecord attempt)
        {
            if (attempt.userId != progress.userId || attempt.resetGeneration != progress.resetGeneration ||
                attempt.phase != SessionPhase.Assessment || attempt.status != AttemptStatus.Completed ||
                !attempt.passed || attempt.assisted || attempt.criticalFailure || attempt.score < 80 ||
                float.IsNaN(attempt.score) || float.IsInfinity(attempt.score) ||
                !Enum.IsDefined(typeof(Difficulty), attempt.difficulty)) return;
            foreach (var objective in attempt.objectives.Where(o => o.completed && !string.IsNullOrWhiteSpace(o.objectiveId)))
            {
                string key = "objective:" + attempt.mode + ":" + attempt.unitId + ":" + attempt.difficulty + ":" + objective.objectiveId;
                if (!progress.rewards.Any(r => r.key == key))
                    progress.rewards.Add(new RewardRecord { key = key, xp = new[] { 10, 15, 25 }[(int)attempt.difficulty] });
            }
        }

        public static void ApplyReset(ProgressSnapshot progress, int generation)
        {
            if (generation <= progress.resetGeneration) return;
            progress.resetGeneration = generation;
            progress.attempts.Clear(); progress.rewards.Clear(); progress.checkpoints.Clear();
            progress.orientationCompleted = false;
        }

        public static string CareerChapter(ProgressSnapshot progress)
        {
            if (!progress.orientationCompleted) return "Foundations";
            var passes = progress.attempts.Where(a => a.contentVersion == LearningContent.CurrentVersion && a.phase == SessionPhase.Assessment && a.status == AttemptStatus.Completed && a.passed && a.resetGeneration == progress.resetGeneration).ToArray();
            if (passes.Where(a => a.mode == LearningMode.AnatomyCases && a.difficulty == Difficulty.Hard).Select(a=>a.unitId).Distinct().Count() >= 6) return "Advanced Cases";
            if (passes.Where(a => a.mode == LearningMode.AnatomyCases && a.difficulty == Difficulty.Easy).Select(a => a.unitId).Distinct().Count() >= 6) return "Treatment Practice";
            if (passes.Any(a => a.mode == LearningMode.AnatomyCases)) return "Disease Investigation";
            return "Anatomy Lab";
        }
    }
}
