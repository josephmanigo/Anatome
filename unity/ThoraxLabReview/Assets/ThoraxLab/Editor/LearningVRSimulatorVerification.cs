#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using ThoraxLab.Learning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using Unity.Collections;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ThoraxLab.Editor
{
    /// <summary>
    /// Editor integration check through real Input System XR devices and the normal
    /// LabHandInteractor/XRI path. This does not use the runtime QA interaction bypass.
    /// Invoke without -quit: -executeMethod ThoraxLab.Editor.LearningVRSimulatorVerification.Run
    /// </summary>
    [InitializeOnLoad]
    public static class LearningVRSimulatorVerification
    {
        const string Pending = "ThoraxLab.XRVerification.Pending";
        const string Started = "ThoraxLab.XRVerification.Started";
        const string Exit = "ThoraxLab.XRVerification.Exit";
        const string EditorErrors = "ThoraxLab.XRVerification.EditorErrors";
        const string InitialIndex = "ThoraxLab.XRVerification.InitialIndex";
        const string IndexBeforePlay = "ThoraxLab.XRVerification.IndexBeforePlay";
        internal static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../qa/vr-xr-simulator-report.json"));

        [Serializable] internal sealed class Check
        {
            public string id, detail;
            public bool passed;
        }
        [Serializable] internal sealed class Report
        {
            public int schemaVersion = 1;
            public string utc, unityVersion, status = "running", method = "Official XRI devices with queued Input System XR state events", screenshot, firstPersonHandsScreenshot;
            public bool passed, runtimeQaBypass;
            public bool searchIndexExistedAtStart, searchIndexExistedBeforePlay;
            public string[] devices = Array.Empty<string>();
            public List<Check> checks = new List<Check>();
            public List<string> errors = new List<string>();
            public List<string> observations = new List<string>();
        }

        static LearningVRSimulatorVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Watchdog;
            Application.logMessageReceived += OnEditorLog;
        }

        [MenuItem("Anatome/Verify XR Device Simulator integration")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start the simulator verification outside Play mode.");
            if (Environment.GetCommandLineArgs().Contains("--learning-qa")) throw new InvalidOperationException("Do not combine the XR integration check with --learning-qa: tracking loss must exercise the real gate.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, JsonUtility.ToJson(new Report { utc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion }, true));
            SessionState.SetBool(Pending, true);
            SessionState.SetString(EditorErrors, JsonUtility.ToJson(new Report()));
            SessionState.SetBool(InitialIndex, File.Exists("UserSettings/Search.index"));
            SessionState.SetBool(IndexBeforePlay, false);
            SessionState.SetBool(Exit, Environment.GetCommandLineArgs().Contains("-executeMethod"));
            SessionState.SetString(Started, DateTime.UtcNow.ToString("o"));
            EditorPrefs.SetBool("ThoraxLab.XRDeviceSimulator", true);
            EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            LearningEditorPlayMode.Request();
        }

        // Capture the Editor-to-Play transition too. The runtime runner starts
        // after startup callbacks, so it cannot alone detect indexing failures.
        static void OnEditorLog(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Pending, false) ||
                (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)) return;
            var captured = JsonUtility.FromJson<Report>(SessionState.GetString(EditorErrors, "{}"));
            string error = message + "\n" + trace;
            if (!captured.errors.Contains(error)) captured.errors.Add(error);
            SessionState.SetString(EditorErrors, JsonUtility.ToJson(captured));
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && SessionState.GetBool(Pending, false))
                SessionState.SetBool(IndexBeforePlay, File.Exists("UserSettings/Search.index"));
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            LearningSimulatorSetup.Install();
            var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            new GameObject("Editor XR integration verification") { tag = "EditorOnly" }.AddComponent<LearningVRSimulatorVerificationRunner>();
        }

        static void Watchdog()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (!DateTime.TryParse(SessionState.GetString(Started, ""), out var start) || (DateTime.UtcNow - start.ToUniversalTime()).TotalSeconds < 600) return;
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion };
            report.errors.Add("Editor XR integration verification exceeded its 600 second limit; it did not complete.");
            Finish(report);
        }

        internal static void Finish(Report report)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            LearningEditorPlayMode.Cancel();
            SessionState.SetBool(Pending, false);
            var captured = JsonUtility.FromJson<Report>(SessionState.GetString(EditorErrors, "{}"));
            foreach (string error in captured.errors) if (!report.errors.Contains(error)) report.errors.Add(error);
            SessionState.EraseString(EditorErrors);
            report.searchIndexExistedAtStart = SessionState.GetBool(InitialIndex, false);
            report.searchIndexExistedBeforePlay = SessionState.GetBool(IndexBeforePlay, false);
            report.utc = DateTime.UtcNow.ToString("o");
            report.passed = report.checks.Count >= 12 && report.checks.All(c => c.passed) && report.errors.Count == 0;
            report.status = report.passed ? "passed" : "failed";
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, JsonUtility.ToJson(report, true));
            Debug.Log("Thorax XR simulator integration: " + report.status + " · " + Output);
            if (SessionState.GetBool(Exit, false)) EditorApplication.Exit(report.passed ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }

    // Kept in Editor: no fixture state setter or virtual device creation is shipped.
    public sealed partial class LearningVRSimulatorVerificationRunner : MonoBehaviour
    {
        readonly LearningVRSimulatorVerification.Report report = new LearningVRSimulatorVerification.Report();
        IEnumerator<int> steps;
        int remainingFrames, selectedEvents, releasedEvents;
        bool finished;
        LabRuntime app;
        LabHandInteractor hand;
        XRDeviceSimulator simulator;
        UnityEngine.InputSystem.InputDevice hmd, left, right;
        XRSimulatedHMDState headState;
        XRSimulatedControllerState leftState, rightState;

        void Start()
        {
            report.unityVersion = Application.unityVersion;
            report.runtimeQaBypass = LabRuntime.VerificationRunning;
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            steps = Verify().GetEnumerator();
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                report.errors.Add(message + "\n" + trace);
        }

        void Update()
        {
            if (finished || steps == null) return;
            if (remainingFrames-- > 0) return;
            try
            {
                if (steps.MoveNext()) remainingFrames = Math.Max(0, steps.Current - 1);
                else { finished = true; LearningVRSimulatorVerification.Finish(report); }
            }
            catch (Exception ex)
            {
                report.errors.Add(ex.ToString());
                try { if(app && app.UI && Camera.main) { ObserveRay("failure"); CaptureWorldInterface(); } } catch(Exception capture) { report.observations.Add("Failure capture: " + capture.Message); }
                finished = true;
                LearningVRSimulatorVerification.Finish(report);
            }
        }

        void Require(string id, bool passed, string detail)
        {
            report.checks.Add(new LearningVRSimulatorVerification.Check { id = id, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(id + ": " + detail);
        }

        IEnumerable<int> Verify()
        {
            for (int frame = 0; frame < 300 && (!LabRuntime.Instance || LabRuntime.Instance.UI == null); frame++) yield return 1;
            app = LabRuntime.Instance;
            Require("runtime_ready", app && app.UI && app.Presentation && app.Structures.ContainsKey("FMA7163"), "Runtime, world-space UI and original skin structure must initialize.");
            Require("no_tracking_bypass", !LabRuntime.VerificationRunning, "The test runs without --learning-qa and must depend on actual XR tracking.");
            simulator = FindFirstObjectByType<XRDeviceSimulator>();
            Require("official_simulator_installed", simulator && simulator.enabled && simulator.deviceSimulatorActionAsset && simulator.controllerActionAsset && simulator.handActionAsset,
                "LearningSimulatorSetup must install the official XRI XRDeviceSimulator with simulator, controller and required hand sample action assets.");
            yield return 12;
            hmd = InputSystem.devices.FirstOrDefault(d => d.layout == "XRSimulatedHMD");
            left = Controller("LeftHand"); right = Controller("RightHand");
            report.devices = InputSystem.devices.Where(d => d.layout.StartsWith("XRSimulated", StringComparison.Ordinal)).Select(d => d.layout + " #" + d.deviceId + " [" + string.Join(", ", d.usages) + "]").ToArray();
            Require("official_virtual_devices_created", hmd != null && left != null && right != null,
                "The installed simulator must create an HMD plus both handed controllers; this harness never calls AddDevice.");
            Require("sample_controller_actions_enabled", simulator.controllerActionAsset.enabled && simulator.deviceSimulatorActionAsset.enabled &&
                simulator.GetComponent<SimulatedDeviceLifecycleManager>() && !simulator.GetComponent<SimulatedDeviceLifecycleManager>().handTrackingCapability,
                "The official keyboard/mouse sample actions are enabled and the lifecycle manager is configured for controller interaction before deterministic fixture injection.");
            // XRI 3.6 delegates device ownership to SimulatedDeviceLifecycleManager.
            // Disable only the keyboard/mouse writer, retaining the manager and exact devices.
            int headId = hmd.deviceId, rightId = right.deviceId;
            var initialHead = LearningXRInput.Read(XRNode.Head);
            var initialLeft = LearningXRInput.Read(XRNode.LeftHand);
            var initialRight = LearningXRInput.Read(XRNode.RightHand);
            report.observations.Add("Official simulator initial head="+initialHead.position+"; world head="+Camera.main.transform.position+"; world panel="+app.UI.Root.position);
            simulator.enabled = false;
            yield return 3;
            Require("device_lifetime_retained", hmd.added && right.added && hmd.deviceId == headId && right.deviceId == rightId,
                "Disabling only the simulator input writer must retain its virtual devices.");

            headState.Reset(); leftState.Reset(); rightState.Reset();
            // Preserve the simulator's origin convention after comfort has measured it.
            // A small physical head movement proves routing without teleporting away from the console.
            SetHead(initialHead.position + new Vector3(.025f,.03f,0), initialHead.rotation * Quaternion.Euler(0,2,0), true);
            SetController(ref leftState, initialLeft.position + new Vector3(-.02f,.01f,.03f), initialLeft.rotation, true, false, false);
            SetController(ref rightState, initialRight.position + new Vector3(.02f,.01f,.03f), initialRight.rotation, true, false, false);
            QueueAll(); yield return 12;
            hand = FindObjectsByType<LabHandInteractor>(FindObjectsSortMode.None).FirstOrDefault(h => h.hand == XRNode.RightHand);
            Require("head_and_hand_pose_follow_devices", hand && app.XRSimulation && app.XRReady &&
                Vector3.Distance(Camera.main.transform.localPosition, headState.devicePosition) < .005f &&
                Quaternion.Angle(Camera.main.transform.localRotation, headState.deviceRotation) < .5f &&
                Vector3.Distance(hand.transform.localPosition, rightState.devicePosition) < .005f,
                "Camera and LabHandInteractor transforms follow queued simulator local poses through LearningXRInput; focused=" + app.Focused);

            foreach (int frames in VerifyStartupHeight()) yield return frames;
            foreach (int frames in VerifyWidgetInput()) yield return frames;
            VerifyNarrationStartup();
            VerifyAnatomyInventory();
            if(Environment.GetCommandLineArgs().Contains("--ui-widgets-only"))yield break;
            foreach (int frames in VerifyButtonJitter()) yield return frames;
            foreach (int frames in VerifyFocusAndMonitor()) yield return frames;
            foreach (int frames in VerifyOpenXRController()) yield return frames;
            foreach (int frames in VerifyUnobstructedInput()) yield return frames;
            foreach (int frames in VerifyBareHandInput()) yield return frames;

            // Click the real guest button and then the orientation button through a controller ray.
            Require("sign_in_screen", app.UI.ScreenName == "Sign in", "Initial screen=" + app.UI.ScreenName);
            AimAtButton("Try your first VR lesson"); yield return 8;
            ObserveRay("before_guest_trigger"); Trigger(true); yield return 5;
            Require("queued_trigger_reaches_hand_adapter", LearningXRInput.Read(XRNode.RightHand).trigger, "Actual adapter trigger=" + LearningXRInput.Read(XRNode.RightHand).trigger);
            ObserveRay("held_guest_trigger"); Trigger(false); yield return 12;
            Require("controller_trigger_activates_guest_button", app.Preview && app.UI.ScreenName == "Career",
                "Queued trigger down/up must activate the actual world-space guest button. Screen=" + app.UI.ScreenName);
            AimAtButton("Learn the VR controls"); yield return 8;
            Trigger(true); yield return 5; Trigger(false); yield return 12;
            Require("controller_trigger_starts_orientation", app.OrientationActive && app.OrientationStep == 0 && !app.Progress.orientationCompleted,
                "The actual orientation UI button opens its first guarded step.");
            var skin = app.Structures["FMA7163"];
            Require("orientation_skin_grab_available", skin.Grab != null && skin.GetComponent<MeshCollider>() && skin.gameObject.activeInHierarchy,
                "Original skin must be an active mesh collider and XRI grab interactable.");
            skin.Grab.selectEntered.AddListener(_ => { selectedEvents++; report.observations.Add("XRI selectEntered #"+selectedEvents+" frame="+Time.frameCount+" tracked="+app.XRReady+" rawGrip="+LearningXRInput.Read(XRNode.RightHand).grip); });
            skin.Grab.selectExited.AddListener(_ => { releasedEvents++; report.observations.Add("XRI selectExited #"+releasedEvents+" frame="+Time.frameCount+" tracked="+app.XRReady+" rawGrip="+LearningXRInput.Read(XRNode.RightHand).grip); });
            Physics.SyncTransforms();
            Require("visible_skin_surface", app.Presentation.TryGetExposedSurface(skin, Camera.main.transform.position, out var surface),
                "The actual outer skin must be exposed from the simulated learner's view.");
            var toward = (surface - Camera.main.transform.position).normalized;
            var startPosition = surface - toward * .07f;
            SetHandWorld(startPosition, Quaternion.LookRotation(toward, Vector3.up)); yield return 8;
            Physics.SyncTransforms();
            bool actualSkinHit = Physics.Raycast(hand.transform.position, hand.transform.forward, out var rayHit, .3f) && rayHit.collider.GetComponentInParent<AnatomyStructure>() == skin;
            Require("controller_ray_hits_original_skin", actualSkinHit, "First physics hit=" + (rayHit.collider ? rayHit.collider.name : "none"));
            Trigger(true); yield return 5; Trigger(false); yield return 8;
            Require("controller_trigger_selects_skin", app.OrientationStep == 1 && app.Session.selected == skin && !app.Progress.orientationCompleted,
                "A real trigger selects the skin, advances only to the grip instruction, and does not complete orientation.");

            var beforePause=skin.transform.position;
            Menu(true); yield return 12;
            Require("controller_menu_preserves_orientation", LearningXRInput.Read(XRNode.RightHand).menu && app.UI.ScreenName=="Menu" && !app.CanInteract &&
                app.OrientationActive && app.OrientationStep==1 && !app.Progress.orientationCompleted && app.Session.selected==skin,
                "Holding the actual controller Menu button opens one pause menu without interrupting orientation or clearing its current step.");
            Menu(false); yield return 8;
            Grip(true); yield return 8;
            SetHandWorld(startPosition+Vector3.up*.06f,Quaternion.LookRotation(toward,Vector3.up)); yield return 8;
            Require("pause_menu_blocks_body_interaction", selectedEvents==0 && releasedEvents==0 && hand.interactablesSelected.Count==0 &&
                Vector3.Distance(skin.transform.position,beforePause)<.005f && app.OrientationStep==1 && !app.Progress.orientationCompleted,
                "A real grip and hand movement while the menu is open cannot acquire or move anatomy or grant orientation credit.");
            Grip(false); SetHandWorld(startPosition,Quaternion.LookRotation(toward,Vector3.up)); yield return 8;
            Menu(true); yield return 8; Menu(false); yield return 8;
            Require("controller_menu_resumes_same_orientation", app.UI.ScreenName=="Workspace" && app.CanInteract && app.OrientationActive &&
                app.OrientationStep==1 && !app.Progress.orientationCompleted && app.Session.selected==skin && selectedEvents==0,
                "The next controller Menu press returns to the same workspace, selected skin and orientation step without restarting or completing it.");

            SetHead(headState.devicePosition, headState.deviceRotation, false); QueueAll(); yield return 8;
            var rest = skin.transform.position;
            Grip(true); yield return 10;
            SetHandWorld(startPosition + Vector3.up * .12f, Quaternion.LookRotation(toward, Vector3.up)); yield return 10;
            Require("tracking_loss_blocks_body_grip", !app.XRReady && !app.CanInteract && selectedEvents == 0 && hand.interactablesSelected.Count == 0 &&
                Vector3.Distance(skin.transform.position, rest) < .005f && !app.Progress.orientationCompleted,
                "With HMD tracking false, grip cannot acquire or move anatomy and cannot complete orientation.");
            Grip(false); SetHandWorld(startPosition, Quaternion.LookRotation(toward, Vector3.up));
            SetHead(headState.devicePosition, headState.deviceRotation, true); QueueAll(); yield return 12;
            Require("tracking_recovers", app.XRReady && app.CanInteract, "Restoring the real tracked device state restores the normal interaction gate; focused=" + app.Focused);
            Require("tracking_recovery_does_not_replay_grip", selectedEvents==0 && releasedEvents==0, "A released controller must not reacquire anatomy from a stale grip when HMD tracking returns. Acquires="+selectedEvents+" releases="+releasedEvents);
            Grip(true); yield return 16;
            Require("controller_grip_acquires_skin", selectedEvents == 1 && hand.interactablesSelected.Any(x => ReferenceEquals(x, skin.Grab)) && LearningXRInput.Read(XRNode.RightHand).grip,
                "XRI selectEntered count=" + selectedEvents + "; interactor selected=" + hand.interactablesSelected.Count);
            float pickupDisplacement=Vector3.Distance(skin.transform.position,rest);
            Require("grip_preserves_anatomical_pose", pickupDisplacement<.005f && !app.Progress.orientationCompleted,
                "Stationary grip must not snap the skin pivot to the controller. Displacement="+pickupDisplacement.ToString("F4")+" m.");
            Grip(false); yield return 16;
            Require("stationary_release_does_not_complete_orientation", app.OrientationActive && app.OrientationStep==1 && !app.Progress.orientationCompleted && releasedEvents==1,
                "A real grab/release with no deliberate hand displacement leaves orientation on its lift step.");
            Grip(true); yield return 16;
            Require("controller_regrip_acquires_skin", selectedEvents==2 && hand.interactablesSelected.Any(x=>ReferenceEquals(x,skin.Grab)),
                "The learner can regrip after a stationary release; XRI acquires="+selectedEvents);
            var restRotation = skin.transform.rotation;
            SetHandWorld(startPosition, Quaternion.AngleAxis(80, Vector3.up) * Quaternion.LookRotation(toward, Vector3.up)); yield return 16;
            CaptureWorldInterface("vr-orientation-wrist-turn.png");
            Require("orientation_wrist_rotation_keeps_torso_on_table", Quaternion.Angle(skin.transform.rotation,restRotation)<1 && Vector3.Distance(skin.transform.position,rest)<.005f,
                "Rotating the hand without lifting cannot rotate the entire chest or swing it toward the learner. Rotation="+Quaternion.Angle(skin.transform.rotation,restRotation).ToString("F1")+" degrees; displacement="+Vector3.Distance(skin.transform.position,rest).ToString("F3")+" m.");
            SetHandWorld(startPosition, Quaternion.LookRotation(toward, Vector3.up)); yield return 12;
            for (int move = 1; move <= 10; move++)
            {
                SetHandWorld(startPosition + Vector3.up * (.012f * move), Quaternion.LookRotation(toward, Vector3.up)); yield return 2;
            }
            yield return 8;
            float lifted = Vector3.Distance(skin.transform.position, rest);
            Require("held_skin_follows_controller_without_credit", lifted >= .1f && lifted <= .14f && app.OrientationActive && !app.Progress.orientationCompleted && releasedEvents == 1,
                "Commanded hand lift=0.1200 m; measured skin displacement=" + lifted.ToString("F4") + " m. Lifting alone must not complete orientation.");
            Grip(false); yield return 16;
            Require("controller_release_completes_orientation", releasedEvents == 2 && app.Progress.orientationCompleted && !app.OrientationActive && app.UI.ScreenName == "Career",
                "Only the actual XRI selectExited release completes orientation. Releases=" + releasedEvents);
            Require("orientation_restores_intact_skin", Vector3.Distance(skin.transform.position, skin.transform.parent.TransformPoint(skin.RestPosition)) < .005f,
                "The real skin returns to its anatomical rest pose after the introductory lift.");
            Require("world_space_interface", app.UI.Root.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace,
                "Captured interface is the actual world-space Unity canvas.");
            CaptureWorldInterface();
            foreach(int frames in VerifyInterruptedOrientation())yield return frames;
            foreach(int frames in VerifyProcedureGrip())yield return frames;
            foreach(int frames in VerifyAssemblyGrip())yield return frames;
            foreach(int frames in VerifyGuide())yield return frames;
            foreach(int frames in VerifyUIAudit())yield return frames;
        }

        IEnumerable<int> VerifyButtonJitter()
        {
            AimAtButton("Try your first VR lesson"); yield return 8;
            Trigger(true); yield return 4;
            // Ordinary hand movement must not turn a button press
            // into a ScrollRect drag and suppress its release click.
            var position=hand.transform.position;
            var rotation=hand.transform.rotation;
            SetHandWorld(position+Camera.main.transform.right*.002f,rotation); yield return 4;
            Trigger(false); yield return 8;
            Require("controller_button_tolerates_hand_jitter",app.Preview&&app.UI.ScreenName=="Career",
                "A 2 mm controller movement during trigger press must still open the guest lesson. Screen="+app.UI.ScreenName);
            app.Account.SignOut(); yield return 8;
        }

        IEnumerable<int> VerifyAssemblyGrip()
        {
            var variant=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.AssembleAnatomy&&v.difficulty==Difficulty.Easy);
            app.StartActivity(variant,SessionPhase.Practice);yield return 16;
            var objective=app.Engine.Current;var target=app.Structures[objective.targetId];var parent=target.transform.parent;
            Physics.SyncTransforms();
            Require("assembly_piece_is_reachable",app.Presentation.TryGetExposedSurface(target,Camera.main.transform.position,out var surface),"The real staged Easy part has an exposed surface.");
            var toward=(surface-Camera.main.transform.position).normalized;var start=surface-toward*.06f;var rotation=Quaternion.LookRotation(toward,Vector3.up);
            SetHandWorld(start,rotation);yield return 8;
            var initial=target.transform.position;var initialRotation=target.transform.rotation;
            Grip(true);yield return 16;
            Require("assembly_grip_keeps_specimen_reference",hand.interactablesSelected.Any(x=>ReferenceEquals(x,target.Grab))&&target.transform.parent==parent,
                "Real XRI pickup keeps the specimen parent used by placement guides, avoiding a null parent during held-frame updates.");
            SetHandWorld(start,Quaternion.AngleAxis(25,Vector3.up)*rotation);yield return 16;
            Require("assembly_allows_deliberate_rotation",Quaternion.Angle(target.transform.rotation,initialRotation)>20&&!app.Engine.IsComplete(objective.id)&&!string.IsNullOrEmpty(app.AssemblyPlacementFeedback),
                "The orientation-only constraint does not restrict assembly rotation, and the live guide remains available while holding the part.");
            SetHandWorld(start,rotation);yield return 12;
            var translation=parent.TransformPoint(target.RestPosition)-initial;
            SetHandWorld(start+translation,rotation);yield return 16;
            Require("assembly_guide_recognizes_held_alignment",Vector3.Distance(target.transform.position,parent.TransformPoint(target.RestPosition))<.005f&&app.AssemblyPlacementFeedback=="Release your grip to place it.",
                "The guide recognizes anatomical alignment through actual held-controller movement.");
            Grip(false);yield return 16;
            Require("assembly_grip_release_places_current_piece",app.Engine.IsComplete(objective.id)&&app.Engine.Attempt.mistakes==0&&Vector3.Distance(target.transform.position,parent.TransformPoint(target.RestPosition))<.005f,
                "The real XRI release places the Easy part and advances to the next piece without errors.");
            CaptureWorldInterface("vr-assembly-controller-verified.png");
        }

        IEnumerable<int> VerifyInterruptedOrientation()
        {
            app.EnterPreview();app.StartOrientation();yield return 12;
            var skin=app.Structures["FMA7163"];Physics.SyncTransforms();
            Require("interrupted_orientation_visible_target",app.Presentation.TryGetExposedSurface(skin,Camera.main.transform.position,out var surface),"Fresh guest orientation has a visible skin surface.");
            var toward=(surface-Camera.main.transform.position).normalized;var start=surface-toward*.07f;var rotation=Quaternion.LookRotation(toward,Vector3.up);
            SetHandWorld(start,rotation);yield return 8;Trigger(true);yield return 5;Trigger(false);yield return 8;
            Grip(true);yield return 12;SetHandWorld(start+Vector3.up*.08f,rotation);yield return 12;
            SetHead(headState.devicePosition,headState.deviceRotation,false);QueueAll();yield return 12;
            Require("tracking_loss_restores_held_orientation_skin",Vector3.Distance(skin.transform.position,skin.transform.parent.TransformPoint(skin.RestPosition))<.005f&&!app.Progress.orientationCompleted&&hand.interactablesSelected.Count==0,
                "Loss of tracking releases and returns the lifted skin without granting credit.");
            SetHead(headState.devicePosition,headState.deviceRotation,true);QueueAll();yield return 12;
            Require("held_grip_does_not_reacquire_on_tracking_recovery",rightState.grip>.95f&&!LearningXRInput.Read(XRNode.RightHand).grip&&hand.interactablesSelected.Count==0&&!app.Progress.orientationCompleted,
                "A grip held through tracking recovery remains disarmed until a release.");
            Grip(false);SetHandWorld(start,rotation);yield return 8;Grip(true);yield return 12;
            SetHandWorld(start+Vector3.up*.08f,rotation);yield return 12;Menu(true);yield return 12;Menu(false);yield return 8;
            Require("pause_restores_held_orientation_skin",app.UI.ScreenName=="Menu"&&Vector3.Distance(skin.transform.position,skin.transform.parent.TransformPoint(skin.RestPosition))<.005f&&!app.Progress.orientationCompleted,
                "Opening Pause while holding the lifted skin restores its pose and keeps the lesson unfinished.");
            Menu(true);yield return 8;Menu(false);yield return 8;
            Require("held_grip_does_not_reacquire_after_pause",app.CanInteract&&hand.interactablesSelected.Count==0&&!app.Progress.orientationCompleted,
                "Closing Pause while still holding grip does not pick the chest up automatically.");
            Grip(false);SetHandWorld(start,rotation);yield return 8;Grip(true);yield return 12;
            SetHandWorld(start+Vector3.up*.35f,rotation);yield return 12;
            Require("orientation_lift_stays_within_comfort_bound",Vector3.Distance(skin.transform.position,skin.transform.parent.TransformPoint(skin.RestPosition))<=.121f,
                "An excessive 35 cm hand movement produces at most the declared 12 cm introductory lift.");
            Grip(false);yield return 12;
            Require("orientation_can_finish_after_interruption",app.Progress.orientationCompleted&&!app.OrientationActive&&app.UI.ScreenName=="Career",
                "A fresh deliberate grip/lift/release completes the interrupted orientation normally.");
        }

        IEnumerable<int> VerifyProcedureGrip()
        {
            // Use the same guarded activity entry point as the menu. No objective, tool,
            // device or assessment state is injected: the first finding and every grab
            // below flow through the normal controller ray and LabHandInteractor.
            var variant=app.Catalog.units.First(u=>u.id=="pneumothorax").variants.First(v=>v.mode==LearningMode.AnatomyCases&&v.difficulty==Difficulty.Easy);
            app.StartActivity(variant,SessionPhase.Practice);yield return 16;
            Require("procedure_practice_uses_normal_tracking_gate",app.XRReady&&app.CanInteract&&app.Engine!=null&&app.Variant==variant&&!LabRuntime.VerificationRunning,
                "The ordinary StartActivity entry point opens unlocked Easy pneumothorax practice with actual tracked simulator devices.");
            var finding=app.Engine.Current;
            Require("procedure_initial_finding_available",finding!=null&&finding.action==ActionKind.InspectFinding&&app.Structures.ContainsKey(finding.targetId),
                "The real case begins at its anatomical finding; no objective completion has been injected.");
            var anatomy=app.Structures[finding.targetId];Physics.SyncTransforms();
            Require("procedure_finding_physically_visible",app.Presentation.TryGetExposedSurface(anatomy,Camera.main.transform.position,out var surface),
                "Initial case target "+finding.targetId+" has a physically exposed surface.");
            var toward=(surface-Camera.main.transform.position).normalized;
            SetHandWorld(surface-toward*.07f,Quaternion.LookRotation(toward,Vector3.up));yield return 8;
            bool findingHit=Physics.Raycast(hand.transform.position,hand.transform.forward,out var hit,.3f)&&hit.collider.GetComponentInParent<AnatomyStructure>()==anatomy;
            Require("procedure_controller_ray_hits_finding",findingHit,"First actual case ray hit="+(hit.collider?hit.collider.name:"none"));
            Trigger(true);yield return 5;Trigger(false);yield return 12;
            foreach(int frames in VerifyClinicalReasoningGrab())yield return frames;
            Require("procedure_trigger_reveals_current_instrument",app.Engine.IsComplete(finding.id)&&app.Engine.Current?.toolId=="probe"&&app.Procedure.Tools.ContainsKey("probe")&&app.Procedure.Tools["probe"].gameObject.activeInHierarchy,
                "The real trigger records the finding and reveals the current assessment pointer on its tray.");

            var tool=app.Procedure.Tools["probe"];int toolSelected=0,toolReleased=0;
            tool.grab.selectEntered.AddListener(_=>toolSelected++);tool.grab.selectExited.AddListener(_=>toolReleased++);
            var collider=tool.GetComponent<BoxCollider>();Physics.SyncTransforms();
            var bounds=collider.bounds;bool foundRay=false;Vector3 handStart=default;Quaternion handRotation=default;
            // Find a clear physical approach to the retained grab box. The controller is
            // moved into that pose; the tool and collider remain on the real tray.
            foreach(var outward in new[]{(Camera.main.transform.position-bounds.center).normalized,Vector3.up,Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                var start=bounds.center+outward*(bounds.extents.magnitude+.055f);var ray=new Ray(start,-outward);
                if(!Physics.Raycast(ray,out var candidate,.5f)||candidate.collider.GetComponentInParent<ProcedureTool>()!=tool)continue;
                handStart=start;handRotation=Quaternion.LookRotation(-outward,Mathf.Abs(outward.y)>.9f?Vector3.forward:Vector3.up);foundRay=true;break;
            }
            Require("instrument_grab_box_reachable",foundRay&&tool.grab.useDynamicAttach&&tool.grab.matchAttachPosition&&tool.grab.matchAttachRotation&&!tool.grab.snapToColliderVolume,
                "The unchanged grab collider is physically reachable and dynamic attachment preserves the held instrument pose.");
            SetHandWorld(handStart,handRotation);yield return 10;
            bool toolHit=Physics.Raycast(hand.transform.position,hand.transform.forward,out var toolRay,.5f)&&toolRay.collider.GetComponentInParent<ProcedureTool>()==tool;
            Require("controller_ray_hits_instrument",toolHit,"First instrument ray hit="+(toolRay.collider?toolRay.collider.name:"none"));
            var restPosition=tool.transform.position;var restRotation=tool.transform.rotation;var restTip=tool.Tip;
            string objectiveId=app.Engine.Current.id;int actionCount=app.Engine.Attempt.actions.Count;
            Grip(true);yield return 16;
            Require("controller_grip_acquires_instrument",toolSelected==1&&hand.interactablesSelected.Any(x=>ReferenceEquals(x,tool.grab))&&tool.Held,
                "The actual queued controller grip causes one XRI selectEntered on the current procedure instrument.");
            float pickup=Vector3.Distance(tool.transform.position,restPosition),tipPickup=Vector3.Distance(tool.Tip,restTip),pickupAngle=Quaternion.Angle(tool.transform.rotation,restRotation);
            Require("instrument_pickup_preserves_pose_and_working_end",pickup<.003f&&tipPickup<.003f&&pickupAngle<.8f,
                "Stationary instrument pickup: body displacement="+pickup.ToString("F4")+" m, working-end displacement="+tipPickup.ToString("F4")+" m, rotation="+pickupAngle.ToString("F2")+" degrees.");
            var translation=new Vector3(.035f,.070f,-.025f);
            for(int i=1;i<=10;i++){SetHandWorld(handStart+translation*(i/10f),handRotation);yield return 2;}
            yield return 8;
            float movedBody=Vector3.Distance(tool.transform.position,restPosition+translation),movedTip=Vector3.Distance(tool.Tip,restTip+translation);
            Require("held_instrument_and_tip_follow_translation",movedBody<.003f&&movedTip<.003f&&toolReleased==0,
                "Commanded translation="+translation+"; body error="+movedBody.ToString("F4")+" m and working-end error="+movedTip.ToString("F4")+" m.");
            var rotationDelta=Quaternion.AngleAxis(22,Vector3.up);var handEnd=handStart+translation;
            SetHandWorld(handEnd,rotationDelta*handRotation);yield return 16;
            var expectedPosition=handEnd+rotationDelta*(restPosition-handStart);var expectedTip=handEnd+rotationDelta*(restTip-handStart);
            float rotationBody=Vector3.Distance(tool.transform.position,expectedPosition),rotationTip=Vector3.Distance(tool.Tip,expectedTip),rotationAngle=Quaternion.Angle(tool.transform.rotation,rotationDelta*restRotation);
            Require("held_instrument_rotates_around_grip",rotationBody<.003f&&rotationTip<.003f&&rotationAngle<.8f,
                "A 22 degree hand turn preserves the dynamic grip offset: body error="+rotationBody.ToString("F4")+" m, working-end error="+rotationTip.ToString("F4")+" m, angle error="+rotationAngle.ToString("F2")+" degrees.");
            var releasePosition=tool.transform.position;var releaseRotation=tool.transform.rotation;var releaseTip=tool.Tip;
            Grip(false);yield return 16;
            SetHandWorld(handEnd+Vector3.up*.08f,rotationDelta*handRotation);yield return 12;
            Require("controller_release_leaves_instrument_at_release_pose",toolReleased==1&&!tool.Held&&!hand.interactablesSelected.Any(x=>ReferenceEquals(x,tool.grab))&&
                Vector3.Distance(tool.transform.position,releasePosition)<.003f&&Vector3.Distance(tool.Tip,releaseTip)<.003f&&Quaternion.Angle(tool.transform.rotation,releaseRotation)<.8f,
                "Real grip release causes one XRI selectExited. The kinematic instrument stays at the released pose when the controller moves away.");
            Require("instrument_handling_alone_cannot_complete_procedure",app.Engine.Current?.id==objectiveId&&app.Engine.Attempt.actions.Count==actionCount&&!tool.activating,
                "Pickup, translation, rotation and release without treatment activation cannot grant procedure credit or append clinical actions.");
        }

        IEnumerable<int> VerifyUnobstructedInput()
        {
            var controllers = FindObjectsByType<LabHandInteractor>(FindObjectsSortMode.None);
            var leftController = controllers.SingleOrDefault(v => v.hand == XRNode.LeftHand);
            var rightController = controllers.SingleOrDefault(v => v.hand == XRNode.RightHand);
            Require("two_visible_tracked_controllers", controllers.Length == 2 && leftController && rightController &&
                LearningXRInput.Read(XRNode.LeftHand).tracked && LearningXRInput.Read(XRNode.RightHand).tracked,
                "Both controller inputs remain tracked with visible feedback.");
            foreach (var controller in controllers)
            {
                var visibleGeometry = controller.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is LineRenderer)).ToArray();
                Require(controller.hand + "_visible_controller_geometry", visibleGeometry.Length >= 3&&controller.GetComponentsInChildren<Collider>().All(c=>!c.enabled),
                    "Each tracked controller has visible geometry without colliders that block pointing or grabbing.");
            }
            var savedLeft = leftState; var savedRight = rightState;
            var camera = Camera.main;
            var cameraParent = camera.transform.parent;
            var cameraPose = camera.transform.localPosition;
            var cameraRotation = camera.transform.localRotation;
            SetHandWorld(XRNode.LeftHand, camera.transform.TransformPoint(new Vector3(-.15f, -.04f, .48f)), camera.transform.rotation * Quaternion.Euler(52, -12, -8));
            SetHandWorld(XRNode.RightHand, camera.transform.TransformPoint(new Vector3(.15f, -.04f, .48f)), camera.transform.rotation * Quaternion.Euler(52, 12, 8));
            yield return 24;
            Require("invisible_inputs_follow_device_poses", Vector3.Distance(leftController.transform.localPosition, leftState.devicePosition) < .005f &&
                Quaternion.Angle(leftController.transform.localRotation, leftState.deviceRotation) < .5f &&
                Vector3.Distance(rightController.transform.localPosition, rightState.devicePosition) < .005f &&
                Quaternion.Angle(rightController.transform.localRotation, rightState.deviceRotation) < .5f,
                "Each input follows its own tracked device pose without a visual avatar.");
            Require("first_person_camera_remains_head_tracked", Camera.main == camera && camera.transform.parent == cameraParent &&
                Vector3.Distance(camera.transform.localPosition, cameraPose) < .005f && Quaternion.Angle(camera.transform.localRotation, cameraRotation) < .5f,
                "Moving the input devices does not replace, offset, or detach the learner's HMD camera.");
            report.firstPersonHandsScreenshot = CaptureWorldInterface("vr-natural-first-person.png");
            SetAnalog(ref rightState, 1, 0); QueueAll(); yield return 12;
            Require("controller_trigger_routes_without_avatar", LearningXRInput.Read(XRNode.RightHand).trigger &&
                LearningXRInput.Read(XRNode.RightHand).triggerValue > .95f && !LearningXRInput.Read(XRNode.LeftHand).trigger,
                "Right trigger events remain independent of the left controller without rendered fingers.");
            SetAnalog(ref rightState, 0, 0); QueueAll(); yield return 12;
            SetAnalog(ref rightState, 0, .45f); QueueAll(); yield return 12;
            Require("controller_partial_grip_preserved", !LearningXRInput.Read(XRNode.RightHand).grip &&
                Mathf.Abs(LearningXRInput.Read(XRNode.RightHand).gripValue - .45f) < .02f,
                "The simulator analog grip value is preserved without falsely starting a grab.");
            SetAnalog(ref rightState, 0, 1); QueueAll(); yield return 12;
            Require("controller_grip_routes_without_avatar", LearningXRInput.Read(XRNode.RightHand).grip && !LearningXRInput.Read(XRNode.LeftHand).grip,
                "Full right grip still reaches the interaction system independently.");
            SetAnalog(ref rightState, 0, 0); QueueAll(); yield return 12;
            Require("controller_release_clears_input", !LearningXRInput.Read(XRNode.RightHand).trigger && !LearningXRInput.Read(XRNode.RightHand).grip,
                "Releasing both controls clears select and grab.");
            SetAnalog(ref leftState, 1, 1); QueueAll(); yield return 12;
            Require("left_controller_remains_independent", LearningXRInput.Read(XRNode.LeftHand).trigger && LearningXRInput.Read(XRNode.LeftHand).grip &&
                !LearningXRInput.Read(XRNode.RightHand).trigger && !LearningXRInput.Read(XRNode.RightHand).grip,
                "The left controls work without driving the right input.");
            SetAnalog(ref leftState, 0, 0); QueueAll(); yield return 12;
            leftState.isTracked = false; leftState.trackingState = 0; QueueAll(); yield return 12;
            Require("lost_controller_tracking_hides_pointer", !LearningXRInput.Read(XRNode.LeftHand).tracked &&
                !leftController.GetComponent<LineRenderer>().enabled && LearningXRInput.Read(XRNode.RightHand).tracked,
                "A lost device cannot leave its pointer frozen in view; the other input stays available.");
            leftState.isTracked = true; leftState.trackingState = (int)(InputTrackingState.Position | InputTrackingState.Rotation); QueueAll(); yield return 12;
            AimAtButton("Try your first VR lesson"); yield return 8;
            var guestButton = app.UI.Root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "Try your first VR lesson");
            var pointerProbe = guestButton.gameObject.AddComponent<LearningVRPointerEventProbe>();
            SetHead(headState.devicePosition, headState.deviceRotation, false); QueueAll(); yield return 12;
            Require("lost_hmd_tracking_hides_both_pointers", !leftController.GetComponent<LineRenderer>().enabled && !rightController.GetComponent<LineRenderer>().enabled && !app.CanInteract,
                "Loss of HMD tracking hides both pointers and retains the interaction pause.");
            Trigger(true); yield return 6; Trigger(false); yield return 8;
            Require("lost_hmd_tracking_blocks_ui_press", LearningXRInput.Read(XRNode.RightHand).tracked && pointerProbe.pointerDowns == 0 && pointerProbe.clicks == 0 && app.UI.ScreenName == "Sign in" && !app.Preview,
                "A still-tracked controller aimed at the actual guest button cannot send pointer-down or click events while HMD tracking is lost.");
            Destroy(pointerProbe);
            var menuButton = app.UI.Root.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TMP_Text>()?.text == "Menu");
            var menuRect = (RectTransform)menuButton.transform;
            var menuTarget = menuRect.TransformPoint(menuRect.rect.center);
            var dragOrigin = Camera.main.transform.position + Camera.main.transform.right * .08f - Camera.main.transform.up * .08f;
            var dragRotation = Quaternion.LookRotation(menuTarget - dragOrigin, Vector3.up);
            var panelPosition = app.UI.Root.position;
            SetHandWorld(dragOrigin, dragRotation); yield return 8;
            Grip(true); yield return 8;
            SetHandWorld(dragOrigin + Camera.main.transform.right * .04f, dragRotation); yield return 8;
            Grip(false); yield return 8;
            Require("lost_hmd_tracking_blocks_panel_drag", LearningXRInput.Read(XRNode.RightHand).tracked && !LearningXRInput.HeadTracked &&
                Vector3.Distance(app.UI.Root.position, panelPosition) < .001f && app.UI.ScreenName == "Sign in",
                "Actual grip and controller movement across the console header cannot drag the panel while the learner's HMD tracking is unavailable.");
            // A trigger held through tracking recovery must not become a fresh UI
            // press. Its later release also must not click the targeted Menu button.
            SetHandWorld(dragOrigin, dragRotation); Trigger(true); yield return 8;
            SetHead(headState.devicePosition, headState.deviceRotation, true); QueueAll(); yield return 8;
            bool heldTriggerOnRecovery = LearningXRInput.Read(XRNode.RightHand).trigger;
            Trigger(false); yield return 8;
            leftState = savedLeft; rightState = savedRight; QueueAll(); yield return 24;
            Require("tracking_restores_inputs_without_replay", LearningXRInput.Read(XRNode.LeftHand).tracked && LearningXRInput.Read(XRNode.RightHand).tracked &&
                !heldTriggerOnRecovery && !LearningXRInput.Read(XRNode.RightHand).grip && !LearningXRInput.Read(XRNode.RightHand).trigger && app.UI.ScreenName == "Sign in",
                "Tracking recovery restores both inputs without replaying a held trigger as a new Menu press or later click.");
        }

        IEnumerable<int> VerifyBareHandInput()
        {
            // Exercise the shipping reader with the real XR Hands subsystem and
            // Meta aim devices. Only the joint samples are synthetic; this cannot
            // validate the headset cameras or the learner's physical gestures.
            const string id="Anatome.Editor.HandInputVerification";
            var descriptors=new List<XRHandSubsystemDescriptor>();SubsystemManager.GetSubsystemDescriptors(descriptors);
            if(!descriptors.Any(d=>d.id==id))XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo{id=id,providerType=typeof(LearningVerificationHandProvider)});
            descriptors.Clear();SubsystemManager.GetSubsystemDescriptors(descriptors);
            var subsystem=descriptors.Single(d=>d.id==id).Create();
            var oldLeftAim=MetaAimHand.left;var oldRightAim=MetaAimHand.right;
            var savedLeft=leftState;var savedRight=rightState;
            var leftAim=MetaAimHand.CreateHand(InputDeviceCharacteristics.Left);
            var rightAim=MetaAimHand.CreateHand(InputDeviceCharacteristics.Right);
            MetaAimHand.left=leftAim;MetaAimHand.right=rightAim;
            var aimPose=new Pose(new Vector3(.2f,1.1f,-.5f),Quaternion.Euler(0,180,0));
            var valid=MetaAimFlags.Computed|MetaAimFlags.Valid;
            void Sample(bool closed,float pinch,MetaAimFlags flags)
            {
                LearningVerificationHandProvider.closed=closed;
                subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
                leftAim.UpdateHand(true,flags,aimPose,0,0,0,0);
                rightAim.UpdateHand(true,flags,aimPose,pinch,0,0,0);
            }
            try
            {
                subsystem.Start();Sample(false,0,valid);yield return 6;
                Require("tracked_controllers_take_priority_over_bare_hands",LearningXRInput.Read(XRNode.RightHand).source==XRInteractionSource.Simulator,
                    "A tracked controller retains ownership when hand samples are also available.");
                leftState.isTracked=rightState.isTracked=false;leftState.trackingState=rightState.trackingState=0;QueueAll();yield return 6;
                var state=LearningXRInput.Read(XRNode.RightHand);
                Require("xr_hand_subsystem_routes_open_hand",state.tracked&&state.source==XRInteractionSource.BareHand&&!state.trigger&&!state.grip,
                    "Real SDK joint/aim samples reach the common input path after controllers lose tracking; open hands are neutral. Curl="+state.gripValue);
                Require("bare_hand_palm_and_aim_are_separate",state.separateAim&&Vector3.Distance(state.position,LearningVerificationHandProvider.RightPalm.position)<.001f&&
                    Quaternion.Angle(state.rotation,LearningVerificationHandProvider.RightPalm.rotation)<.1f&&Vector3.Distance(state.rayPosition,aimPose.position)<.001f&&Quaternion.Angle(state.rayRotation,aimPose.rotation)<.1f,
                    "Manipulation uses the palm pose while remote UI pointing uses the SDK aim pose.");
                Sample(false,1,valid);yield return 6;
                Require("bare_index_pinch_selects_without_grabbing",LearningXRInput.Read(XRNode.RightHand).trigger&&!LearningXRInput.Read(XRNode.RightHand).grip,
                    "An index pinch selects without a grasp.");
                Sample(false,0,valid);yield return 6;Sample(true,0,valid);yield return 6;
                Require("bare_three_finger_curl_grabs_without_use",LearningXRInput.Read(XRNode.RightHand).grip&&!LearningXRInput.Read(XRNode.RightHand).trigger,
                    "Joint-derived finger curl grabs without activating the tool. Curl="+LearningXRInput.Read(XRNode.RightHand).gripValue);
                Sample(true,1,valid);yield return 6;
                Require("bare_pinch_can_use_while_holding",LearningXRInput.Read(XRNode.RightHand).grip&&LearningXRInput.Read(XRNode.RightHand).trigger,
                    "Index pinch remains available while the other fingers hold.");
                Sample(true,1,valid|MetaAimFlags.SystemGesture);yield return 6;
                Require("system_hand_gesture_cancels_game_input",!LearningXRInput.Read(XRNode.RightHand).tracked&&!LearningXRInput.Read(XRNode.RightHand).trigger&&!LearningXRInput.Read(XRNode.RightHand).grip,
                    "The system gesture cannot click, cut or keep an app grab active.");
                Sample(true,1,valid);yield return 6;
                Require("bare_hand_recovery_requires_release",LearningXRInput.Read(XRNode.RightHand).tracked&&!LearningXRInput.Read(XRNode.RightHand).trigger&&!LearningXRInput.Read(XRNode.RightHand).grip,
                    "A closed or pinching hand after interruption is blocked until release.");
                Sample(false,0,valid);yield return 6;
                aimPose.position=new Vector3(float.NaN,1,0);Sample(false,0,valid);yield return 6;
                Require("invalid_bare_hand_aim_is_rejected",!LearningXRInput.Read(XRNode.RightHand).tracked,
                    "A nonfinite SDK aim pose is rejected before it reaches transforms or physics.");
                aimPose.position=new Vector3(.2f,1.1f,-.5f);Sample(false,0,valid);yield return 6;
                rightState=savedRight;SetAnalog(ref rightState,1,1);QueueAll();yield return 6;
                Require("returning_held_controller_requires_release",LearningXRInput.Read(XRNode.RightHand).source==XRInteractionSource.Simulator&&!LearningXRInput.Read(XRNode.RightHand).trigger&&!LearningXRInput.Read(XRNode.RightHand).grip,
                    "Returning to a controller with held buttons cannot replay a pinch or grab.");
            }
            finally
            {
                subsystem.Stop();subsystem.Destroy();InputSystem.RemoveDevice(leftAim);InputSystem.RemoveDevice(rightAim);
                MetaAimHand.left=oldLeftAim;MetaAimHand.right=oldRightAim;leftState=savedLeft;rightState=savedRight;QueueAll();
            }
            yield return 12;
            Require("bare_hand_probe_does_not_activate_sign_in",app.UI.ScreenName=="Sign in"&&!app.Preview,
                "Synthetic gesture verification leaves the real sign-in state unchanged.");
        }

        static void SetAnalog(ref XRSimulatedControllerState state, float trigger, float grip)
        {
            state.trigger = trigger; state.grip = grip;
            state = state.WithButton(ControllerButton.TriggerButton, trigger >= .55f).WithButton(ControllerButton.GripButton, grip >= .55f);
        }

        UnityEngine.InputSystem.InputDevice Controller(string usage) => InputSystem.devices.FirstOrDefault(d => d.layout == "XRSimulatedController" && d.usages.Any(u => u == usage));
        void SetHead(Vector3 position, Quaternion rotation, bool tracked)
        {
            headState.devicePosition = headState.centerEyePosition = headState.leftEyePosition = headState.rightEyePosition = position;
            headState.deviceRotation = headState.centerEyeRotation = headState.leftEyeRotation = headState.rightEyeRotation = rotation;
            headState.isTracked = tracked;
            headState.trackingState = tracked ? (int)(InputTrackingState.Position | InputTrackingState.Rotation) : 0;
        }
        static void SetController(ref XRSimulatedControllerState state, Vector3 position, Quaternion rotation, bool tracked, bool trigger, bool grip)
        {
            state.devicePosition = position; state.deviceRotation = rotation; state.isTracked = tracked;
            state.trackingState = tracked ? (int)(InputTrackingState.Position | InputTrackingState.Rotation) : 0;
            state.trigger = trigger ? 1 : 0; state.grip = grip ? 1 : 0;
            state = state.WithButton(ControllerButton.TriggerButton, trigger).WithButton(ControllerButton.GripButton, grip);
        }
        IEnumerable<int> VerifyStartupHeight()
        {
            var controls=FindFirstObjectByType<LearningControls>();
            bool seated=app.Settings.Current.seated;
            var savedHead=headState;
            app.Settings.Current.seated=true;
            typeof(LearningControls).GetField("tracked",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(controls,false);
            SetHead(new Vector3(0,1.65f,0),Quaternion.identity,true);QueueAll();yield return 3;
            controls.ApplyComfort();yield return 3;
            Require("measured_floor_eye_height_preserved",Mathf.Abs(Camera.main.transform.position.y-1.65f)<.05f,"Seated layout preserves measured Floor eye height="+Camera.main.transform.position.y);
            // Replay the old Quest startup: Floor pose is available when settings
            // apply, then an eye-relative Device pose arrives in a later frame.
            SetHead(Vector3.zero,Quaternion.identity,true);QueueAll();yield return 8;
            Require("startup_origin_change_keeps_head_above_floor",Mathf.Abs(Camera.main.transform.position.y-1.25f)<.05f,
                "A delayed 1.65m -> 0m origin change must not put the headset under the floor. World eye height="+Camera.main.transform.position.y);
            app.Settings.Current.seated=seated;headState=savedHead;QueueAll();yield return 3;controls.ApplyComfort();yield return 3;
        }
        void QueueAll()
        {
            if (hmd == null || left == null || right == null || !hmd.added || !left.added || !right.added) throw new InvalidOperationException("Official simulator devices disappeared during verification.");
            InputSystem.QueueStateEvent(hmd, headState);
            InputSystem.QueueStateEvent(left, leftState);
            InputSystem.QueueStateEvent(right, rightState);
        }
        void Trigger(bool down) { rightState.trigger = down ? 1 : 0; rightState = rightState.WithButton(ControllerButton.TriggerButton, down); QueueAll(); }
        void Grip(bool down) { rightState.grip = down ? 1 : 0; rightState = rightState.WithButton(ControllerButton.GripButton, down); QueueAll(); }
        void Menu(bool down) { rightState=rightState.WithButton(ControllerButton.MenuButton,down); QueueAll(); }
        void SetHandWorld(Vector3 position, Quaternion rotation)
        {
            rightState.devicePosition = hand.transform.parent.InverseTransformPoint(position);
            rightState.deviceRotation = Quaternion.Inverse(hand.transform.parent.rotation) * rotation;
            QueueAll();
        }
        void SetHandWorld(XRNode node, Vector3 position, Quaternion rotation)
        {
            var controller = FindObjectsByType<LabHandInteractor>(FindObjectsSortMode.None).Single(h => h.hand == node);
            var localPosition = controller.transform.parent.InverseTransformPoint(position);
            var localRotation = Quaternion.Inverse(controller.transform.parent.rotation) * rotation;
            if (node == XRNode.LeftHand) SetController(ref leftState, localPosition, localRotation, true, false, false);
            else SetController(ref rightState, localPosition, localRotation, true, false, false);
            QueueAll();
        }
        void AimAtButton(string label)
        {
            Canvas.ForceUpdateCanvases();
            var button = app.UI.Root.GetComponentsInChildren<Button>().SingleOrDefault(b => b.GetComponentInChildren<TMP_Text>()?.text == label);
            if (!button || !button.interactable) throw new InvalidOperationException("Visible interactive button missing: " + label);
            var rect = (RectTransform)button.transform;
            var target = rect.TransformPoint(rect.rect.center);
            // A learner turns toward the console. This also keeps the actual
            // graphic raycast inside a narrow Editor Game view without changing FOV or UI placement.
            var headRotation = Quaternion.LookRotation(app.UI.Root.position - Camera.main.transform.position, Vector3.up);
            SetHead(headState.devicePosition, Quaternion.Inverse(Camera.main.transform.parent.rotation) * headRotation, true);
            var origin = Camera.main.transform.position + Camera.main.transform.right * .08f - Camera.main.transform.up * .08f;
            SetHandWorld(origin, Quaternion.LookRotation(target - origin, Vector3.up));
            report.observations.Add("Aimed " + label + " world=" + target + "; screen=" + Camera.main.WorldToScreenPoint(target) + "; button rect=" + rect.rect + "; camera pixel=" + Camera.main.pixelRect);
        }
        void ObserveRay(string moment)
        {
            if(!hand) return;
            var ray=new Ray(hand.transform.position,hand.transform.forward);
            var plane=new Plane(app.UI.Root.forward,app.UI.Root.position);
            bool intersection=plane.Raycast(ray,out float distance);
            var screenPoint=Camera.main.WorldToScreenPoint(ray.GetPoint(distance));
            var data=new TrackedDeviceEventData(EventSystem.current){position=screenPoint,rayPoints=new List<Vector3>{ray.origin,ray.GetPoint(3)},layerMask=~0};
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
            report.observations.Add(moment+": head="+Camera.main.transform.position+" hand="+hand.transform.position+" direction="+hand.transform.forward+" plane="+intersection+" distance="+distance+" screen="+screenPoint+" hits="+string.Join(", ",hits.Select(h=>h.gameObject.name+"/"+h.gameObject.GetComponentInParent<Button>()?.GetComponentInChildren<TMP_Text>()?.text)));
        }
        string CaptureWorldInterface(string filename = "vr-xr-simulator-world-ui.png")
        {
            Canvas.ForceUpdateCanvases();
            var camera = Camera.main;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var texture = new RenderTexture(1440, 1000, 24);
            var pixels = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0); pixels.Apply();
                var path = Path.Combine(Path.GetDirectoryName(LearningVRSimulatorVerification.Output), filename);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                if (filename == "vr-xr-simulator-world-ui.png") report.screenshot = path;
                return path;
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Destroy(texture); Destroy(pixels);
            }
        }
    }

    // Editor-only synthetic joint provider; never part of a player build.
    public sealed class LearningVerificationHandProvider : XRHandSubsystemProvider
    {
        public static bool closed;
        public static Pose RightPalm=>new Pose(new Vector3(.2f,1.1f,-.4f),Quaternion.identity);
        public override void Start(){}public override void Stop(){}public override void Destroy(){}
        public override void GetHandLayout(NativeArray<bool> layout){for(int i=0;i<layout.Length;i++)layout[i]=true;}
        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType updateType,ref Pose leftRoot,NativeArray<XRHandJoint> leftJoints,ref Pose rightRoot,NativeArray<XRHandJoint> rightJoints)
        {
            leftRoot=new Pose(new Vector3(-.2f,1.1f,-.4f),Quaternion.identity);rightRoot=RightPalm;
            Fill(leftJoints,Handedness.Left,leftRoot);Fill(rightJoints,Handedness.Right,rightRoot);
            return XRHandSubsystem.UpdateSuccessFlags.All;
        }
        static void Fill(NativeArray<XRHandJoint> joints,Handedness side,Pose palm)
        {
            for(int i=0;i<joints.Length;i++)joints[i]=XRHandProviderUtility.CreateJoint(side,XRHandJointTrackingState.Pose,XRHandJointIDUtility.FromIndex(i),palm);
            foreach(var front in new[]{XRHandJointID.IndexMetacarpal,XRHandJointID.MiddleMetacarpal,XRHandJointID.RingMetacarpal,XRHandJointID.LittleMetacarpal})
            {
                var position=palm.position;
                for(int i=0;i<5;i++)
                {
                    var id=front+i;joints[id.ToIndex()]=XRHandProviderUtility.CreateJoint(side,XRHandJointTrackingState.Pose,id,new Pose(position,Quaternion.identity));
                    position+=(closed?Quaternion.Euler(i*90,0,0)*Vector3.forward:Vector3.forward)*.02f;
                }
            }
        }
    }

    // Observation only: this Editor fixture does not implement drag interfaces or
    // alter the button's normal event route while counting attempted UI activation.
    public sealed class LearningVRPointerEventProbe : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public int pointerDowns, clicks;
        public void OnPointerDown(PointerEventData eventData) { pointerDowns++; }
        public void OnPointerClick(PointerEventData eventData) { clicks++; }
    }
}
#endif
