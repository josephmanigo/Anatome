using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

namespace ThoraxLab.Learning
{
    public sealed class LabRuntime : MonoBehaviour
    {
        public static LabRuntime Instance { get; private set; }
        public DissectionSession Session { get; private set; }
        public LearningCatalog Catalog { get; private set; }
        public AccountService Account { get; private set; }
        public SettingsService Settings { get; private set; }
        public ProgressSnapshot Progress { get; private set; }
        public AssessmentEngine Engine { get; private set; }
        public LearningUnit Unit { get; private set; }
        public ScenarioVariant Variant { get; private set; }
        public LabUI UI { get; private set; }
        public ProcedureWorkbench Procedure { get; private set; }
        public LessonNarrator Narrator { get; private set; }
        public LessonDemonstrator Demonstrator {get;private set;}
        public VRSpecimenPresentation Presentation { get; private set; }
        public GuidedInteractionCoach Coach { get; private set; }
        public EnumerationMarkers Markers { get; private set; }
        LabAudio audioFeedback;
        public bool Preview { get; private set; }
        public bool Sandbox { get; private set; }
        public bool Paused { get; set; }
        bool applicationFocused=true,applicationPaused;
        public bool Focused => applicationFocused&&!applicationPaused;
        public bool XRWasActive { get; private set; }
        public bool XRReady => LearningXRInput.HeadTracked;
        public bool XRSimulation => LearningXRInput.IsSimulated;
        public string XRStatus => XRReady ? (XRSimulation ? "XR Device Simulator · Editor testing" : "VR headset connected") : "Connect a VR headset, or press Play with the XR Device Simulator in Unity.";
        public bool OrientationActive { get; private set; }
        public int OrientationStep { get; private set; }
        public string CurrentTargetName => OrientationActive ? "Skin of the chest" : Engine?.Current != null && structures.TryGetValue(Engine.Current.targetId, out var target) ? target.record.name : Engine?.Current?.title ?? "Explore the specimen";
        public string AssemblyPlacementFeedback => Coach ? Coach.PlacementFeedback : null;
        public string TeachingWindowLabel => Presentation ? Presentation.CurrentWindowLabel : null;
        public string CurrentInstruction
        {
            get
            {
                if (OrientationActive) return OrientationStep == 0 ? "Point at the skin, then press trigger or pinch thumb and index once." : "Hold grip, or curl your middle, ring and little fingers. Lift slightly, then release or open your hand. The chest returns to the table.";
                var current = Engine?.Current;
                if (current == null) return "All steps complete. Finish to see your feedback.";
                if (current.action == ActionKind.Place) return AssemblyPlacementFeedback ?? "Grip the nearby piece. Bring it into the matching outline and release.";
                if (current.action == ActionKind.Expose && structures.TryGetValue(current.targetId, out var layer))
                    return (Session.saw?"BONE SAW":"SCALPEL")+" equipped. "+(current.region=="all"?"Aim at the named tissue.":"Work on the specimen's "+current.region+" side.")+"\n"+(Coach?.DissectionFeedback??"Hold trigger and sweep a short cut; release. Grip the detached chunk, lift clear and release.")+"\nRemoved: "+Mathf.Min(100,Mathf.RoundToInt(RemovedFraction(current.id)/current.minPathFraction*100))+"%. Whole layers stay anchored; only detached chunks can be grabbed.";
                return current.instruction;
            }
        }
        public string Notice { get; private set; } = "Welcome to " + LearningBrand.Name + ".";
        public string SyncStatus { get; private set; } = "Sign in to save your learning.";
        public bool CanInteract => UI && UI.WorkspaceVisible && !Paused && Focused && (XRReady || VerificationRunning);
        public bool CanUseCuttingTool => CanInteract && !Session.busy && (Sandbox || (Variant?.mode == LearningMode.GuidedDissection && Engine?.Current?.action == ActionKind.Expose));
        public bool CanCut => CanUseCuttingTool && (Sandbox || (Session.selected && !Session.selected.fragment && Session.selected.record.id == Engine.Current.targetId));
        public static bool VerificationRunning
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Environment.GetCommandLineArgs().Contains("--learning-qa");
#else
                return false;
#endif
            }
        }
        public event Action Changed;
        AtomicFileStore<ProgressSnapshot> progressStore;
        string root;
        readonly Dictionary<string, AnatomyStructure> structures = new Dictionary<string, AnatomyStructure>();
        readonly Dictionary<string,float> removedChunks = new Dictionary<string,float>();
        float RemovedFraction(string id)=>removedChunks.TryGetValue(id,out var value)?value:0;
        readonly Dictionary<string,List<CutStroke>> removedStrokes = new Dictionary<string,List<CutStroke>>();
        bool saveBlocked, syncing;
        float syncClock;
        double attemptStart;
        public IReadOnlyDictionary<string, AnatomyStructure> Structures => structures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(Environment.GetCommandLineArgs().Contains("-selftest"))return;
            if (Instance || !FindFirstObjectByType<DissectionSession>()) return;
            new GameObject("Thorax learning application").AddComponent<LabRuntime>();
        }
        IEnumerator Start()
        {
            Instance = this; yield return null;
            Session = FindFirstObjectByType<DissectionSession>();
            if (!Session) yield break;
            root = LearningBrand.DataRoot(Application.persistentDataPath, Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor, Application.companyName, Application.productName);
            var commandLine=Environment.GetCommandLineArgs();int qaIndex=Array.IndexOf(commandLine,"--qa-output");
            if(commandLine.Contains("--learning-qa")||commandLine.Contains("--learning-benchmark")){root=Path.Combine(qaIndex>=0&&qaIndex+1<commandLine.Length?commandLine[qaIndex+1]:Application.temporaryCachePath,"LearningQA-data");Directory.CreateDirectory(root);}
            Directory.CreateDirectory(root);
            try
            {
                Catalog = JsonUtility.FromJson<LearningCatalog>(Resources.Load<TextAsset>("Learning/catalog").text);
                foreach (var s in Session.originals) if (s && !s.fragment && !s.record.reference_region) structures[s.record.id] = s;
                var intercostalRepair=Resources.Load<Mesh>("Learning/Specimen/FMA9757-validated-working-mesh");
                if(intercostalRepair&&structures.TryGetValue("FMA9757",out var intercostal))intercostal.UseValidatedWorkingMesh(intercostalRepair);
                ValidateCatalog();
                Settings = new SettingsService(root); Account = new AccountService(root); Account.Changed += AccountChanged;
                Progress = NewProgress("preview");
                Narrator = gameObject.AddComponent<LessonNarrator>(); Procedure = gameObject.AddComponent<ProcedureWorkbench>(); Procedure.Initialize(this);
                Demonstrator=gameObject.AddComponent<LessonDemonstrator>();
                audioFeedback=gameObject.AddComponent<LabAudio>();
                UI = gameObject.AddComponent<LabUI>(); UI.Initialize(this);
                gameObject.AddComponent<LearningControls>().Initialize(this);
                gameObject.AddComponent<LearningPerformanceMonitor>();
                var oldBoard = GameObject.Find("Dissection controls"); if (oldBoard) oldBoard.SetActive(false);
                RenderSettings.ambientLight = new Color(.14f, .17f, .20f);
                var environment=GameObject.Find("Laboratory");if(environment)foreach(var renderer in environment.GetComponentsInChildren<Renderer>())
                {if(renderer.name.Contains("Light")||renderer.name.Contains("MainTable"))continue;var tint=new MaterialPropertyBlock();renderer.GetPropertyBlock(tint);tint.SetColor("_BaseColor",new Color(.13f,.18f,.23f));renderer.SetPropertyBlock(tint);}
                var light = GameObject.Find("Specimen examination light")?.GetComponent<Light>(); if (light) { light.intensity = 2f; light.color = new Color(1f,.96f,.9f); }
                Presentation = gameObject.AddComponent<VRSpecimenPresentation>(); Presentation.Initialize(Session); Presentation.ShowIntact();
                Settings.ApplySceneSettings();
                if (!string.IsNullOrEmpty(Settings.RecoveryNotice)) Message(Settings.RecoveryNotice);
                Coach = gameObject.AddComponent<GuidedInteractionCoach>(); Coach.Initialize(this);
                Markers = gameObject.AddComponent<EnumerationMarkers>(); Markers.Initialize(this);
                string[] args = Environment.GetCommandLineArgs();
                if (args.Contains("--learning-preview") || args.Contains("--learning-qa")) EnterPreview();
                if (args.Contains("--learning-qa")&&VerificationRunning) gameObject.AddComponent<LearningRuntimeVerification>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(args.Contains("--learning-benchmark")){EnterPreview();gameObject.AddComponent<LearningBenchmark>();}
#endif
            }
            catch (Exception ex) { Debug.LogException(ex); Notice = "The learning content could not be initialized: " + ex.Message; }
        }
        void ValidateCatalog()
        {
            if (Catalog == null || Catalog.schemaVersion != 1 || Catalog.units.Length != 12) throw new InvalidDataException("Learning catalog is incomplete.");
            foreach (var unit in Catalog.units)
            {
                if (unit.anatomyIds.Any(id => !structures.ContainsKey(id))) throw new InvalidDataException("Unresolved anatomy in " + unit.id);
                foreach (var variant in unit.variants)
                    foreach (var objective in variant.objectives)
                        if (!structures.ContainsKey(objective.targetId) && !objective.targetId.StartsWith("equipment:", StringComparison.Ordinal)) throw new InvalidDataException("Unresolved objective target.");
            }
        }
        ProgressSnapshot NewProgress(string id) => new ProgressSnapshot { userId = id };
        public void EnterPreview()
        {
            Save(); Interrupt(); Preview = true; Progress = NewProgress("preview"); progressStore = null;
            Notice = "Guest practice · start with the short VR orientation. Progress lasts for this session."; SyncStatus = "Guest practice · this session only"; if(Presentation)Presentation.ShowIntact(); UI.ShowHub();
        }
        public void LogOut()
        {
            if(Account.UserId==null)return;
            if(Session.busy||Account.Busy){Message("Wait for the current operation to finish, then log out.");return;}
            Save();
            if(saveBlocked){Message("Your progress could not be saved. Resolve the save error before logging out.");return;}
            Account.SignOut();Message("Logged out. Your progress stays saved on this device.");
        }
        void AccountChanged()
        {
            Save(); Interrupt(); Preview = false; progressStore = null; saveBlocked = false;
            if (Account.UserId == null) { Progress = NewProgress("preview"); UI.ShowSignIn(); return; }
            string id = Account.UserId;
            progressStore = new AtomicFileStore<ProgressSnapshot>(Path.Combine(root, "accounts", id, "progress.save"), p => JsonUtility.ToJson(p), JsonUtility.FromJson<ProgressSnapshot>, p =>
            { if (p == null || p.schemaVersion != 1 || p.userId != id || p.attempts == null || p.rewards == null || p.checkpoints == null) throw new InvalidDataException("Progress identity could not be verified."); });
            try { Progress = progressStore.Load(() => NewProgress(id)); ProgressRules.RebuildRewards(Progress); SyncStatus = progressStore.RecoveredBackup ? "Recovered verified backup" : "Saved locally"; }
            catch (Exception ex) { saveBlocked = true; Progress = NewProgress(id); Notice = ex.Message; SyncStatus = "Save recovery needed · original files preserved"; }
            if(!saveBlocked)Notice=Account.Offline?"Signed in offline. Your progress is saved on this device.":"Welcome back. Your learning progress is ready.";
            UI.ShowHub(); if (!Account.Offline && !saveBlocked) _ = Synchronize();
        }
        public void StartOrientation()
        {
            if(!XRReady&&!VerificationRunning){Message(XRStatus);return;}
            Interrupt(); Unit=null; Variant=null; OrientationActive=true; OrientationStep=0;
            Session.ResetSpecimen();Presentation.ShowIntact();UI.ShowWorkspace();
            if(structures.TryGetValue("FMA7163",out var skin))
            {
                if(!skin.GetComponent<OrientationGrabConstraint>())skin.gameObject.AddComponent<OrientationGrabConstraint>();
                skin.Highlight(true);
            }
            Message(CurrentInstruction);
        }
        public void CompleteOrientation()
        {
            if(!OrientationActive||OrientationStep<2)return;
            OrientationActive=false;Progress.orientationCompleted=true;Session.ResetSpecimen();Presentation.ShowIntact();Save();
            Notice="Ready to begin. Choose a mode and try its Easy lesson.";UI.ShowHub();
        }
        public bool Unlocked(ScenarioVariant variant) => variant != null && ProgressRules.IsUnlocked(Progress, variant.mode, variant.unitId, variant.difficulty, variant.version);
        public void StartActivity(ScenarioVariant variant, SessionPhase phase) => BeginActivity(variant,phase,null);
        void BeginActivity(ScenarioVariant variant, SessionPhase phase, AttemptRecord resumed)
        {
            if (!XRReady&&!VerificationRunning) { Message(XRStatus); return; }
            if (resumed != null && (!ProgressRules.CanResume(resumed,variant) || resumed.userId != Progress.userId || resumed.resetGeneration != Progress.resetGeneration))
            { Message("This saved activity belongs to an earlier curriculum or another account. Start this unit's Easy activity; previous results are preserved."); return; }
            if (!Unlocked(variant)) { Message("Complete orientation, then pass this unit's Easy assessment before Medium, and Medium before Hard."); return; }
            Interrupt(); Notice=""; Unit = Catalog.units.First(u => u.id == variant.unitId); Variant = variant; Sandbox = false;
            Session.SetMode(variant.mode == LearningMode.GuidedDissection ? LabMode.Free : LabMode.Explore);
            var attempt = new AttemptRecord { userId = Progress.userId, scenarioId = variant.id, unitId = Unit.id, contentVersion = variant.version, mode = variant.mode, difficulty = variant.difficulty, phase = phase, resetGeneration = Progress.resetGeneration, deviceId = SystemInfo.deviceUniqueIdentifier };
            Engine = new AssessmentEngine(variant, resumed ?? attempt); attemptStart = Time.realtimeSinceStartupAsDouble;
            PrepareSpecimen(); Physics.SyncTransforms();Procedure.Configure(Unit, variant); UI.ShowWorkspace();
            if(phase!=SessionPhase.Assessment)Narrator.Play("intro-"+Variant.id, Variant.briefing);if(phase==SessionPhase.Lesson)Demonstrator.Play(); Save(); Changed?.Invoke();
            if(Unit.isCase&&phase==SessionPhase.Lesson&&resumed==null)UI.ShowCaseIntroduction();
            FrameActivity();if(resumed!=null)_=RestoreCheckpointGeometry();
        }
        void PrepareSpecimen()
        {
            var selected = new HashSet<string>(Unit.anatomyIds.Concat(Unit.protectedIds));
            foreach (var pair in structures) { pair.Value.RestoreOriginal(); pair.Value.gameObject.SetActive(selected.Contains(pair.Key)); pair.Value.Highlight(false); }
            removedStrokes.Clear();removedChunks.Clear();
            Presentation.Prepare(Variant.mode, Variant.objectives.Select(o=>o.targetId), Unit.protectedIds);
            UpdateTeachingWindow();
            RestoreCompletedLayers();Coach.Prepare();Markers.Rebuild();
        }
        void UpdateTeachingWindow()
        {
            if(Engine?.Current!=null&&structures.TryGetValue(Engine.Current.targetId,out var target)){
                if(Variant.mode==LearningMode.GuidedDissection){
                    Session.saw=Engine.Current.toolId=="dissection-saw";
                    if(Engine.Current.action==ActionKind.Expose)Presentation.PrepareDissectionWindow(target,Unit.protectedIds);
                    else Presentation.PrepareTeachingWindow(target);
                }else Presentation.PrepareTeachingWindow(target);
            }
            RestoreCompletedLayers();
        }
        public void StartSandbox()
        {
            if(!XRReady&&!VerificationRunning){Message(XRStatus);return;}
            Interrupt(); Unit = null; Variant = null; Sandbox = true; Session.SetMode(LabMode.Free); Presentation.ShowIntact(); UI.ShowWorkspace();
            Notice = "Dissection sandbox · cuts are temporary. Use Undo or Restore specimen.";
        }
        public void Select(AnatomyStructure structure, Vector3? contact = null)
        {
            if (!CanInteract || !structure || !structure.gameObject.activeInHierarchy || structure.record.reference_region) return;
            Physics.SyncTransforms();
            Vector3 surface=structure.Renderer.bounds.center;
            if(Presentation&&!Presentation.TryGetExposedSurface(structure,Camera.main.transform.position,out surface))
            {Message("That structure is covered by another layer. Work from the visible outer surface first.");return;}
            Session.Select(structure);
            if(OrientationActive){if(structure.record.id=="FMA7163"&&OrientationStep==0){OrientationStep=1;Message(CurrentInstruction);}return;}
            if (Engine == null) return;
            if (Variant.mode == LearningMode.IdentifyEnumerate || Variant.mode == LearningMode.AnatomyCases ||
                (Variant.mode == LearningMode.GuidedDissection && Engine.Current?.action == ActionKind.Select))
            {
                if(Variant.mode==LearningMode.AnatomyCases&&Engine.Current?.action!=ActionKind.Select&&Engine.Current?.action!=ActionKind.InspectFinding)return;
                var kind = Engine.Current?.action == ActionKind.InspectFinding ? ActionKind.InspectFinding : ActionKind.Select;
                var objective=Variant.objectives.FirstOrDefault(o=>o.targetId==structure.record.id&&!Engine.IsComplete(o.id));
                var action=new InteractionAction { kind = kind, targetId = structure.record.id, contactVerified = true,
                    localPosition=Vec(structure.transform.InverseTransformPoint(contact??surface)) };
                if(Emit(action)&&Variant.mode==LearningMode.IdentifyEnumerate&&objective!=null)Markers.Add(objective,action);
            }
        }
        public void Released(AnatomyStructure structure, float distance, float angle)
        {
            if(OrientationActive)
            {
                if(OrientationStep==1&&structure.record.id=="FMA7163")
                {
                    float lift=structure.transform.position.y-structure.transform.parent.TransformPoint(structure.RestPosition).y;
                    structure.RestorePose();
                    if(lift>=.04f&&CanInteract){OrientationStep=2;CompleteOrientation();}
                    else Message("Try a small upward lift while holding grip, then release. You can try again as often as you like.");
                }
                return;
            }
            if(!CanInteract)return;
            if (Engine == null) return;
            if (Variant.mode == LearningMode.AssembleAnatomy)
            {
                if(structure.fragment)return;
                var current=Engine.Current;
                if(current==null||current.targetId!=structure.record.id)return;
                if((distance>current.maxDistance||angle>current.maxAngle)&&Engine.Attempt.phase!=SessionPhase.Assessment){Message(AssemblyPlacementFeedback??"Bring the piece into its outline, then release.");return;}
                bool accepted = Emit(new InteractionAction { kind = ActionKind.Place, targetId = structure.record.id, contactVerified = true, distance = distance, angle = angle, localPosition = Vec(structure.transform.localPosition), localRotation = Quat(structure.transform.localRotation) });
                if (accepted) structure.RestorePose();
            }
            else if (Variant.mode == LearningMode.GuidedDissection && Engine.Current?.action==ActionKind.Expose)
            {
                var objective=Engine.Current;
                if(!structure.fragment||structure.cutObjectiveId!=objective.id||structure.record.id!=objective.targetId)return;
                if(distance<.06f){structure.RestorePose();Message("Lift the separated chunk clear of the specimen, then release.");return;}
                var original=structures[objective.targetId];var bounds=structure.Filter.sharedMesh.bounds;
                // Source coordinates: anatomical left is negative X, right is positive X.
                bool inRegion=objective.region=="left"?bounds.max.x<=original.SourceBounds.center.x:
                    objective.region=="right"?bounds.min.x>=original.SourceBounds.center.x:true;
                if(!inRegion){structure.RestorePose();Message("Cut a chunk entirely on the named side. Crossing the midline cannot complete this region.");return;}
                float fraction=RemovedFraction(objective.id)+structure.chunkFraction;
                if(!removedStrokes.TryGetValue(objective.id,out var strokes))removedStrokes[objective.id]=strokes=new List<CutStroke>();
                strokes.Add(new CutStroke{poses=structure.chunkCutPoses});
                string tool=structure.chunkToolId;
                removedChunks[objective.id]=fraction;Session.CommitRemovedChunk(structure);
                if(fraction<objective.minPathFraction){Message("Chunk removed. Continue cutting this region until its removal meter is full.");return;}
                Emit(new InteractionAction{kind=ActionKind.Expose,targetId=objective.targetId,toolId=tool,equipmentState="chunk-lifted:"+objective.region,contactVerified=true,pathFraction=fraction,cutStrokes=strokes.ToArray()});
            }
        }

        public bool CanManipulate(AnatomyStructure structure)
        {
            if(!CanInteract||Session.busy||!structure||!structure.gameObject.activeInHierarchy||structure.record.reference_region)return false;
            if(Sandbox)return structure.fragment;
            if(OrientationActive)return OrientationStep==1&&structure.record.id=="FMA7163";
            if(Engine==null)return false;
            if(!structure.fragment)return Variant.mode!=LearningMode.GuidedDissection||Engine.Current?.action!=ActionKind.Expose;
            // The interactor already supplies the first physical ray/proximity hit.
            // Do not repeat mesh-ray access scans on every XR target-query frame.
            if(Variant.mode==LearningMode.GuidedDissection)return Engine.Current?.action==ActionKind.Expose&&structure.cutObjectiveId==Engine.Current.id&&structure.record.id==Engine.Current.targetId;
            return false;
        }
        void RestoreCompletedLayers()
        {
            if(Engine==null)return;
            foreach(var objective in Variant.objectives.Where(o=>o.action==ActionKind.Expose&&Engine.IsComplete(o.id)))
                if(!Variant.objectives.Any(o=>o.targetId==objective.targetId&&o.action==ActionKind.Expose&&!Engine.IsComplete(o.id))&&structures.TryGetValue(objective.targetId,out var layer))Session.RetireCompletedLayer(layer);
        }
        public bool Emit(InteractionAction action) => RecordAction(action,false);
        bool RecordAction(InteractionAction action,bool committed)
        {
            if (Engine == null || (!committed&&(!CanInteract||Session.busy))) return false;
            action.elapsedSeconds = Time.realtimeSinceStartupAsDouble - attemptStart;
            var completed=Engine.Current;
            bool accepted = Engine.Apply(action); Notice = Engine.Feedback;
            if(Variant.mode==LearningMode.AnatomyCases&&completed!=null){
                if(action.kind==ActionKind.ClinicalDecision){var choice=completed.choices.FirstOrDefault(c=>c.id==action.toolId);if(choice!=null)Notice=(accepted?"Correct: ":"Review your choice: ")+choice.label+". "+Engine.Feedback;}
                else if(accepted&&completed.explanation=="Step complete. Follow the next single instruction.")Notice=completed.title+" complete.";
            }
            if(audioFeedback)audioFeedback.Feedback(accepted);
            if (accepted)
            {
                UpdateTeachingWindow();
                Procedure.ObjectiveCompleted();
                Coach.RefreshCurrent();
                if (Engine.Current != null && Engine.Attempt.phase != SessionPhase.Assessment && !string.IsNullOrEmpty(Engine.Current.narration)) Narrator.Play(Engine.Current.narration, Engine.Current.instruction);
            }
            Save(); Changed?.Invoke(); return accepted;
        }
        public void OnCutCommitted(AnatomyStructure target)
        {
            if (Engine == null) return;
            if (Unit.protectedIds.Contains(target.record.id)) RecordAction(new InteractionAction { kind = ActionKind.ProtectedStructureViolation, targetId = target.record.id, contactVerified = true },true);
            else if (Variant.mode == LearningMode.GuidedDissection && Engine.Current?.targetId==target.record.id)
            {Message("Chunk separated. Grip the small detached piece, lift it clear and release. Whole layers stay anchored.");}
        }
        public void RevealHint()
        {
            if (Engine == null) return;
            if (Engine.Attempt.phase == SessionPhase.Assessment) { Message("Answer-revealing hints are available in practice. Finish this attempt or switch to practice."); return; }
            Procedure.ShowHint(); if (Engine.Current != null && structures.TryGetValue(Engine.Current.targetId, out var s)) s.Highlight(true);
        }
        public void Finish()
        {
            if (Engine == null || Session.busy) return; Engine.Finish(); Save(); Narrator.Stop(); UI.ShowResults(); _ = Synchronize();
        }
        public void Interrupt()
        {
            if(Markers)Markers.Clear();
            if (Engine != null && Engine.Attempt.status == AttemptStatus.Active) { Save(); Engine.Attempt.status = AttemptStatus.Interrupted; }
            Engine = null; Sandbox = false; OrientationActive=false; if(Coach)Coach.Clear();removedStrokes.Clear();removedChunks.Clear();if(Demonstrator)Demonstrator.Stop();if (Procedure) Procedure.Clear(); if (Narrator) Narrator.Stop();
        }
        public void ResumeLast()
        {
            var attempt = Progress.attempts.LastOrDefault(a => a.status == AttemptStatus.Active && a.resetGeneration == Progress.resetGeneration);
            if (attempt == null) { Message("There is no unfinished activity to resume."); return; }
            var variant = Catalog.units.SelectMany(u => u.variants).FirstOrDefault(v => v.id == attempt.scenarioId);
            if (!ProgressRules.CanResume(attempt,variant)) { Message("This save uses an earlier curriculum. Start this unit's Easy activity to learn the updated sequence. Your previous results are preserved."); return; }
            var snapshot = JsonUtility.FromJson<AttemptRecord>(JsonUtility.ToJson(attempt));
            if(snapshot.deviceId!=SystemInfo.deviceUniqueIdentifier){snapshot.id=Guid.NewGuid().ToString("N");snapshot.deviceId=SystemInfo.deviceUniqueIdentifier;snapshot.startedUtc=DateTime.UtcNow.ToString("o");snapshot.synced=false;}
            BeginActivity(variant, snapshot.phase, snapshot);
            if(Engine==null||Engine.Attempt.id!=snapshot.id)return;
            foreach (var objective in Variant.objectives.Where(o => Engine.IsComplete(o.id)))
            {
                if (objective.action == ActionKind.Place && structures.TryGetValue(objective.targetId, out var s)) s.RestorePose();
            }
            Procedure.RestoreCheckpoint(); Message("Resumed the last completed step. Partial tool movements have been reset."); Save();
        }
        public void Save()
        {
            if (Progress == null || saveBlocked) return;
            try
            {
                if (Engine != null)
                {
                    var copy = JsonUtility.FromJson<AttemptRecord>(JsonUtility.ToJson(Engine.Attempt)); ProgressRules.AddAttempt(Progress, copy);
                    Progress.checkpoints.RemoveAll(c => c.attemptId == copy.id);
                    if (copy.status == AttemptStatus.Active) Progress.checkpoints.Add(new ActivityCheckpoint { attemptId = copy.id, scenarioId = copy.scenarioId, phase = copy.phase, resetGeneration = copy.resetGeneration, savedUtc = DateTime.UtcNow.ToString("o"), attempt = copy, completedObjectiveIds = copy.objectives.Where(o => o.completed).Select(o => o.objectiveId).ToArray() });
                }
                if(Preview||progressStore==null)return;
                Progress.pendingSync=true;Progress.localRevision++;progressStore.Save(Progress); SyncStatus = Account.Offline ? "Saved locally · offline" : "Saved locally · sync pending";
            }
            catch (Exception ex) { saveBlocked = true; SyncStatus = "Save needs attention"; Notice = ex.Message; }
        }
        public async void RestoreActivity()
        {
            if(Session.busy){Message("Wait for the current cut or saved opening to finish.");return;}
            Session.ResetSpecimen();if(Engine!=null){PrepareSpecimen();Procedure.Configure(Unit,Variant);Procedure.RestoreCheckpoint();}
            else if(Presentation)Presentation.ShowIntact();
            if(!await RestoreCheckpointGeometry())return;
            Message("Specimen restored. Recorded actions and errors remain in this attempt.");
        }
        async Task<bool> RestoreCheckpointGeometry()
        {
            var engine=Engine;var current=engine?.Current;
            if(current?.action!=ActionKind.Expose)return true;
            var strokes=engine.Attempt.actions.Where(a=>a.kind==ActionKind.Expose&&a.targetId==current.targetId&&Variant.objectives.Any(o=>o.targetId==a.targetId&&o.equipmentState==a.equipmentState&&engine.IsComplete(o.id))).SelectMany(a=>a.cutStrokes??Array.Empty<CutStroke>()).ToArray();
            if(strokes.Length==0)return true;
            bool restored=await Session.RestoreRemovedChunks(structures[current.targetId],strokes);
            if(Engine!=engine)return false;
            if(!restored){Paused=true;Message("The saved opening could not be rebuilt. Start a fresh attempt; previous results remain saved.");return false;}
            Physics.SyncTransforms();return true;
        }
        public void FrameActivity()
        {
            FrameRenderers(structures.Values.Where(s=>s&&s.gameObject.activeInHierarchy).Select(s=>(Renderer)s.Renderer).Concat(Procedure.VisibleRenderers));
        }
        public void FocusSpecimen()=>FrameRenderers(structures.Values.Where(s=>s&&s.gameObject.activeInHierarchy).Select(s=>(Renderer)s.Renderer));
        public void FocusTools()=>FrameRenderers(Procedure.Tools.Values.Where(t=>t).SelectMany(t=>t.GetComponentsInChildren<Renderer>()));
        void FrameRenderers(IEnumerable<Renderer> source)
        {
            if(!VerificationRunning || XRReady)return;
            var renderers=source.Where(r=>r&&r.gameObject.activeInHierarchy).ToArray();
            if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var view=Camera.main;float vertical=view.fieldOfView*Mathf.Deg2Rad;float horizontal=2*Mathf.Atan(Mathf.Tan(vertical/2)*view.aspect);float distance=bounds.extents.magnitude/Mathf.Sin(Mathf.Min(vertical,horizontal)/2)*1.12f;
            var position=bounds.center-view.transform.forward*distance;view.transform.parent.position+=position-view.transform.position;view.transform.LookAt(bounds.center);
        }
        public async Task Synchronize()
        {
            if (Preview || Account?.UserId == null || saveBlocked || syncing) return;
            syncing = true; var accountProgress = Progress;
            try
            {
                await Account.Sync(accountProgress, Engine?.Attempt.id);
                if (ReferenceEquals(Progress, accountProgress))
                {
                    if(Engine!=null&&Engine.Attempt.resetGeneration!=Progress.resetGeneration){Engine=null;Session.ResetSpecimen();Procedure.Clear();UI.ShowHub();Notice="Progress was reset on another device. This old attempt has been closed.";}
                    progressStore.Save(Progress); SyncStatus = Progress.pendingSync||Progress.attempts.Any(a=>!a.synced&&a.status==AttemptStatus.Completed)?"Saved locally · sync pending":"Synced"; Changed?.Invoke();
                }
            }
            catch (Exception ex) { if (ReferenceEquals(Progress, accountProgress)) { Notice = ex.Message; SyncStatus = "Saved locally · sync pending"; } }
            finally { syncing = false; }
        }
        public async Task ResetProgress()
        {
            if (Preview) { Interrupt();Progress = NewProgress("preview");Presentation.ShowIntact(); UI.ShowHub(); return; }
            Interrupt(); await Account.ResetProgress(Progress); saveBlocked = false; Save(); UI.ShowHub();
        }
        public string ExportProgress()
        {
            string file = Path.Combine(root, "thorax-progress-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json"); File.WriteAllText(file, JsonUtility.ToJson(Progress, true)); return file;
        }
        public void Message(string message) { Notice = message; Changed?.Invoke(); }
        void Update() { if (XRReady) XRWasActive = true; syncClock += Time.unscaledDeltaTime; if (syncClock > 60) { syncClock = 0; if (Account != null && !Account.Offline) _ = Synchronize(); } }
        void OnApplicationFocus(bool focus) { applicationFocused=focus;RefreshApplicationAvailability(); }
        void OnApplicationPause(bool paused) { applicationPaused=paused;RefreshApplicationAvailability(); }
        void RefreshApplicationAvailability()
        {
            if(!Focused)Save();
            if(UI)UI.ApplicationAvailabilityChanged(Focused);
        }
        void OnApplicationQuit() => Save();
        void OnDestroy() { Save(); Settings?.Dispose(); if (Instance == this) Instance = null; }
        static float[] Vec(Vector3 p) => new[] { p.x, p.y, p.z };
        static float[] Quat(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
    }
}
