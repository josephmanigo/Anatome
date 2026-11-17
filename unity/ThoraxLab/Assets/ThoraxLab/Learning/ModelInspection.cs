using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace ThoraxLab.Learning
{
    // Observe A/X before hand tools and locomotion can consume this frame's input.
    [DefaultExecutionOrder(-50)]
    public sealed class ModelInspection : MonoBehaviour
    {
        LabRuntime app;
        readonly InspectionZoom zoom = new InspectionZoom();
        readonly InspectionButton button = new InspectionButton();
        readonly VRStickInput stick = new VRStickInput();
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        GameObject visual;
        Coroutine opening;
        float fitScale;
        bool leftHanded;
        bool resumeNarration;
        XRInteractionSource activeSource;

        public bool IsOpen { get; private set; }
        public float Magnification => zoom.Value;
        public Transform Visual => visual ? visual.transform : null;
        public XRNode ControlHand => app.Settings.Current.leftHanded ? XRNode.LeftHand : XRNode.RightHand;
        bool Available => app && app.UI && app.Focused && !app.Paused && app.XRReady &&
            !app.UI.HasModal && app.Session && !app.Session.busy;
        public bool CanOpen => Available && app.UI.WorkspaceVisible && !app.OrientationActive &&
            (!app.MuseumActive || (!app.Museum.ChangingGallery && app.Museum.Selected)) &&
            (app.Engine != null || app.Sandbox || app.MuseumActive);

        public void Initialize(LabRuntime runtime) { app = runtime; leftHanded = app.Settings.Current.leftHanded; }

        void Update()
        {
            if (!app || app.Settings == null) return;
            bool changedHand = leftHanded != app.Settings.Current.leftHanded;
            leftHanded = app.Settings.Current.leftHanded;
            var input = LearningXRInput.Read(ControlHand);
            bool tracked = app.Focused && app.XRReady && input.tracked;
            if (changedHand) { button.Reset(); stick.Reset(); }
            if (!tracked || (IsOpen && input.source != activeSource))
            {
                button.Reset(); stick.Reset();
                if (IsOpen) ReturnToActivity();
                return;
            }
            if (IsOpen && (app.Paused || app.Session.busy || (visual && app.Engine == null && !app.Sandbox && !app.MuseumActive)))
            { ReturnToActivity(); return; }
            if (IsOpen && !Available) stick.Reset();
            if (button.Press(input.source, tracked && Available && (IsOpen || CanOpen), input.inspect))
            {
                if (IsOpen) ReturnToActivity(); else Open();
            }
        }

        public void Open()
        {
            if (IsOpen || !CanOpen) return;
            IsOpen = true;
            activeSource = LearningXRInput.Read(ControlHand).source;
            stick.Reset(); zoom.Reset();
            resumeNarration = app.Narrator.IsPlaying;
            if (resumeNarration) app.Narrator.TogglePause();
            opening = StartCoroutine(CaptureAfterRelease());
        }

        IEnumerator CaptureAfterRelease()
        {
            // XRI releases held parts through CanInteract before copying the scene.
            // Keep the workspace visible until case anatomy has been captured.
            yield return null;
            yield return null;
            opening = null;
            if (!Available || !app.UI.WorkspaceVisible) { Close(); yield break; }
            var anatomy = (app.MuseumActive ? app.Museum.InspectionRenderers : app.Session.originals.Concat(app.Session.Fragments)
                .Where(s => s && !s.record.reference_region).Select(s => (Renderer)s.Renderer)).Where(VisibleMesh).ToArray();
            var cases = (app.MuseumActive ? System.Array.Empty<Renderer>() : app.Procedure.InspectionRenderers).Where(VisibleMesh).ToArray();
            var source = cases.Length > 0 ? cases : anatomy;
            if (source.Length == 0)
            { Close(); app.Message("No visible model to inspect. Return to your activity and choose a visible layer."); yield break; }

            var bounds = source[0].bounds;
            foreach (var renderer in source) bounds.Encapsulate(renderer.bounds);
            visual = new GameObject("Model inspection · visual only");
            visual.transform.position = bounds.center;
            var block = new MaterialPropertyBlock();
            foreach (var renderer in source)
            {
                var copy = new GameObject(renderer.name + " · inspection");
                copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                copy.transform.localScale = renderer.transform.lossyScale;
                copy.transform.SetParent(visual.transform, true);
                copy.AddComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var display = copy.AddComponent<MeshRenderer>();
                display.sharedMaterials = renderer.sharedMaterials;
                renderer.GetPropertyBlock(block); display.SetPropertyBlock(block);
                for (int i = 0; i < display.sharedMaterials.Length; i++)
                { renderer.GetPropertyBlock(block, i); display.SetPropertyBlock(block, i); }
                display.shadowCastingMode = ShadowCastingMode.Off; display.receiveShadows = false;
            }
            foreach (var renderer in anatomy.Concat(cases).Distinct())
            { hidden[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
            fitScale = .24f / Mathf.Max(.001f, bounds.extents.magnitude);
            ResetView();
            app.UI.ShowModelInspection();
        }

        static bool VisibleMesh(Renderer renderer) => renderer && renderer.enabled && !renderer.forceRenderingOff &&
            renderer.gameObject.activeInHierarchy && renderer is MeshRenderer &&
            renderer.GetComponent<MeshFilter>() && renderer.GetComponent<MeshFilter>().sharedMesh &&
            !renderer.GetComponent<TMPro.TMP_Text>();

        // Called after the ray/UI decision so one stick cannot scroll and zoom together.
        public void HandleControllerAxis(LearningXRInput.State input, bool overUI)
        {
            if (!IsOpen || !visual || !Available || overUI || app.UI.XRPanelCaptured ||
                !input.tracked || input.source != activeSource)
            { stick.Reset(); return; }
            float x = input.axis.x, y = input.axis.y;
            stick.Filter(input.source, true, app.Settings.Current.stickDeadzone, ref x, ref y);
            // Use the dominant axis to avoid rotating while enlarging the model.
            if (Mathf.Abs(y) >= Mathf.Abs(x)) zoom.Move(y, Time.unscaledDeltaTime);
            else visual.transform.Rotate(Vector3.up, x * 65 * Mathf.Min(Time.unscaledDeltaTime, .1f), Space.World);
            ApplyZoom();
        }

        public void ZoomIn() { if (IsOpen && Available) { zoom.Step(true); ApplyZoom(); } }
        public void ZoomOut() { if (IsOpen && Available) { zoom.Step(false); ApplyZoom(); } }
        void ApplyZoom()
        {
            if (visual) visual.transform.localScale = Vector3.one * (fitScale * zoom.Value);
            if (app && app.UI) app.UI.RefreshInspectionZoom();
        }
        public void ResetView()
        {
            if (!visual) return;
            zoom.Reset(); stick.Reset();
            var head = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            visual.transform.SetPositionAndRotation(head.position + forward * 1.05f + right * .3f - Vector3.up * .2f, Quaternion.identity);
            ApplyZoom();
        }
        public void ReturnToActivity()
        {
            bool returnToWorkspace = IsOpen && app && app.UI && app.UI.ScreenName == "Model inspection";
            Close();
            if (returnToWorkspace) app.UI.ShowWorkspace();
        }
        public void Close()
        {
            if (opening != null) StopCoroutine(opening);
            opening = null;
            foreach (var pair in hidden) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
            hidden.Clear();
            if (visual) { visual.SetActive(false); Destroy(visual); }
            visual = null; IsOpen = false; stick.Reset(); button.Reset();
            if (resumeNarration && app && app.Narrator && app.Narrator.Paused) app.Narrator.TogglePause();
            resumeNarration = false;
        }
        void OnDisable() => Close();
        void OnDestroy() => Close();
    }
}
