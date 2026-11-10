using System;
using System.Collections.Generic;

namespace ThoraxLab.Learning
{
    public static class LearningContent { public const string CurrentVersion = "2026.10.1"; }
    public enum LearningMode { AnatomyCases, IdentifyEnumerate, AssembleAnatomy, GuidedDissection }
    // Keep ordinal zero stable for existing saves and server rubrics.
    public enum Difficulty { Easy = 0, Medium = 1, Hard = 2 }
    public enum SessionPhase { Lesson, Practice, Assessment }
    public enum ActionKind { Select, Place, Expose, InspectFinding, EquipmentConnect, ToolContact, ToolActivate, Trace, Reassess, ProtectedStructureViolation, ClinicalDecision }
    public enum AttemptStatus { Active, Completed, Interrupted }

    [Serializable] public sealed class LearningCatalog
    {
        public int schemaVersion = 1;
        public string version = LearningContent.CurrentVersion;
        public LearningUnit[] units = Array.Empty<LearningUnit>();
        public SourceReference[] sources = Array.Empty<SourceReference>();
    }

    [Serializable] public sealed class SourceReference
    {
        public string id, title, url, accessed, usage;
    }

    [Serializable] public sealed class LearningUnit
    {
        public string id, title, summary, lesson, narration, system, disease, pathway, response, limitations;
        public bool isCase;
        public string[] sourceIds = Array.Empty<string>();
        public string[] anatomyIds = Array.Empty<string>();
        public string[] protectedIds = Array.Empty<string>();
        public ScenarioVariant[] variants = Array.Empty<ScenarioVariant>();
    }

    [Serializable] public sealed class ScenarioVariant
    {
        public string id, unitId, version, briefing, debrief;
        public LearningMode mode;
        public Difficulty difficulty;
        public bool ordered;
        public string[] protectedIds = Array.Empty<string>();
        public ObjectiveDefinition[] objectives = Array.Empty<ObjectiveDefinition>();
    }

    [Serializable] public sealed class ObjectiveDefinition
    {
        public string id, title, instruction, explanation, targetId, toolId, equipmentState, narration;
        public ActionKind action;
        public bool essential = true;
        public float weight = 1, maxDistance = 0.06f, maxAngle = 35, minContactSeconds, minPathFraction;
        public string[] prerequisites = Array.Empty<string>();
        public DecisionOption[] choices = Array.Empty<DecisionOption>();
        public bool criticalOnWrong;
    }

    [Serializable] public sealed class DecisionOption
    {
        public string id, label, model, rationale;
        public float response;
    }

    // Evidence is emitted by verified scene interactions, never by UI completion controls.
    [Serializable] public sealed class InteractionAction
    {
        public string id = Guid.NewGuid().ToString("N");
        public string targetId, toolId, equipmentState;
        public ActionKind kind;
        public int sequence;
        public double elapsedSeconds;
        public bool contactVerified;
        public float distance, angle, contactSeconds, pathFraction;
        public float[] localPosition = Array.Empty<float>();
        public float[] localRotation = Array.Empty<float>();
    }

    [Serializable] public sealed class ObjectiveResult
    {
        public string objectiveId, actionId;
        public bool completed;
    }

    [Serializable] public sealed class AttemptRecord
    {
        public int schemaVersion = 1;
        public string id = Guid.NewGuid().ToString("N"), userId, scenarioId, unitId, contentVersion;
        public string startedUtc = DateTime.UtcNow.ToString("o"), completedUtc, deviceId;
        public LearningMode mode;
        public Difficulty difficulty;
        public SessionPhase phase;
        public AttemptStatus status;
        public int resetGeneration, mistakes;
        public bool criticalFailure, assisted, passed, synced;
        public float score;
        public List<InteractionAction> actions = new List<InteractionAction>();
        public List<ObjectiveResult> objectives = new List<ObjectiveResult>();
    }

    [Serializable] public sealed class ActivityCheckpoint
    {
        public string attemptId, scenarioId, savedUtc;
        public SessionPhase phase;
        public int resetGeneration;
        public string[] completedObjectiveIds = Array.Empty<string>();
        public AttemptRecord attempt;
    }

    [Serializable] public sealed class RewardRecord
    {
        public string key;
        public int xp;
    }

    [Serializable] public sealed class ProgressSnapshot
    {
        public int schemaVersion = 1, resetGeneration;
        public string userId, savedUtc, lastSyncedUtc;
        public bool orientationCompleted;
        public bool pendingSync;
        public long localRevision;
        public List<AttemptRecord> attempts = new List<AttemptRecord>();
        public List<RewardRecord> rewards = new List<RewardRecord>();
        public List<ActivityCheckpoint> checkpoints = new List<ActivityCheckpoint>();
    }
}
