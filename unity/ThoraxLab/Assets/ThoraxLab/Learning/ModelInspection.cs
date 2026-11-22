using System.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace ThoraxLab.Learning
{
    [DefaultExecutionOrder(-50)]
    public sealed class ModelInspection : MonoBehaviour
    {
        LabRuntime app;
        Transform rig, head, viewSpace;
        LabHandInteractor[] hands;
        Vector3 focus;
        readonly InspectionZoom zoom = new InspectionZoom();
        readonly InspectionButton button = new InspectionButton();
        readonly VRStickInput stick = new VRStickInput();
        bool leftHanded;
        XRInteractionSource activeSource;

        public bool IsOpen => viewSpace;
        public float Magnification => zoom.Value;
        public XRNode ControlHand => app.Settings.Current.leftHanded ? XRNode.LeftHand : XRNode.RightHand;
        bool Available => app && app.UI && app.UI.WorkspaceVisible && app.Focused && !app.Paused &&
            app.XRReady && app.Session && !app.Session.busy;
        public bool CanOpen => Available && !app.OrientationActive &&
            (!app.MuseumActive || (!app.Museum.ChangingGallery && app.Museum.Selected)) &&
            (app.Engine != null || app.Sandbox || app.MuseumActive);

        public void Initialize(LabRuntime runtime)
        {
            app = runtime;
            head = Camera.main.transform;
            rig = head.parent;
            hands = rig.GetComponentsInChildren<LabHandInteractor>();
            leftHanded = app.Settings.Current.leftHanded;
        }

        void Update()
        {
            if (!app || app.Settings == null) return;
            var input = LearningXRInput.Read(ControlHand);
            bool tracked = app.Focused && app.XRReady && input.tracked;
            if (leftHanded != app.Settings.Current.leftHanded || !tracked || (IsOpen && activeSource != input.source))
            {
                leftHanded = app.Settings.Current.leftHanded;
                Close(); button.Reset(); stick.Reset(); return;
            }
            if (IsOpen && (app.Paused || app.UI.ScreenName != "Workspace")) { Close(); return; }
            if (button.Press(input.source, tracked && CanOpen, input.inspect)) Toggle();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (IsOpen || !CanOpen) return;
            var renderers = app.MuseumActive ? app.Museum.InspectionRenderers :
                app.Procedure.InspectionRenderers.Where(r => r && r.enabled && !r.forceRenderingOff).ToArray();
            if (!renderers.Any())
                renderers = app.Session.originals.Concat(app.Session.Fragments)
                    .Where(s => s && s.gameObject.activeInHierarchy && !s.record.reference_region)
                    .Select(s => (Renderer)s.Renderer);
            var visible = renderers.Where(r => r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff).ToArray();
            if (visible.Length == 0) { app.Message("Choose a visible model to zoom."); return; }
            var bounds = visible[0].bounds;
            foreach (var renderer in visible) bounds.Encapsulate(renderer.bounds);
            focus = bounds.center;

            // Transform the tracked eyes and hands together. All tissue, cut meshes,
            // sockets and instrument paths stay in their original metric space.
            viewSpace = new GameObject("Live model zoom").transform;
            viewSpace.SetParent(rig, false);
            head.SetParent(viewSpace, false);
            foreach (var hand in hands) hand.transform.SetParent(viewSpace, false);
            activeSource = LearningXRInput.Read(ControlHand).source;
            zoom.Reset(); stick.Reset();
            app.UI.RefreshInspectionZoom();
        }

        // The hand ray gets first use of its stick for scrolling.
        public void HandleControllerAxis(LearningXRInput.State input, bool overUI)
        {
            if (!IsOpen || !Available || overUI || app.UI.XRPanelCaptured ||
                !input.tracked || input.source != activeSource)
            { stick.Reset(); return; }
            float x = input.axis.x, y = input.axis.y;
            stick.Filter(input.source, true, app.Settings.Current.stickDeadzone, ref x, ref y);
            if (x == 0 && y == 0) return;
            bool rotate = Mathf.Abs(x) > Mathf.Abs(y);
            if (!TryGetPivot(rotate, out var pivot)) return;
            if (!rotate) zoom.Move(y, Time.unscaledDeltaTime);
            ApplyView(rotate ? -x * 65 * Mathf.Min(Time.unscaledDeltaTime, .1f) : 0, pivot);
        }

        // An active hand stays on its grip point or cutting ray. Zoom alone must
        // never move a held piece into a socket or draw a stroke across tissue.
        bool TryGetPivot(bool rotate, out Vector3 pivot)
        {
            pivot = focus;
            bool anchored = false;
            foreach (var hand in hands)
            {
                var input = LearningXRInput.Read(hand.hand);
                if (hand.interactablesSelected.Count > 0)
                    pivot = hand.GetAttachTransform(hand.interactablesSelected[0]).position;
                else if (app.CanUseCuttingTool && input.trigger && !hand.PointerOverUI)
                {
                    var ray = hand.InputRay(input);
                    pivot = Physics.Raycast(ray, out var hit, 3, ~0, QueryTriggerInteraction.Ignore) ? hit.point : ray.origin;
                }
                else continue;
                if (rotate || anchored) return false;
                anchored = true;
            }
            return true;
        }

        public void ZoomIn() => Step(true);
        public void ZoomOut() => Step(false);
        void Step(bool enlarge)
        {
            if (!IsOpen || !Available || !TryGetPivot(false, out var pivot)) return;
            zoom.Step(enlarge); ApplyView(0, pivot);
        }

        void ApplyView(float yaw, Vector3 pivot)
        {
            float scale = 1f / zoom.Value;
            float ratio = scale / viewSpace.localScale.x;
            if (yaw == 0 && Mathf.Approximately(ratio, 1)) return;
            var turn = Quaternion.AngleAxis(yaw, Vector3.up);
            var position = pivot + turn * (viewSpace.position - pivot) * ratio;
            SetView(position, turn * viewSpace.rotation, scale);
            app.UI.RefreshInspectionZoom();
        }

        void SetView(Vector3 position, Quaternion rotation, float scale)
        {
            // Keep the workspace buttons at the same apparent distance and size during zoom.
            var panel = app.UI.Root;
            var panelPosition = viewSpace.InverseTransformPoint(panel.position);
            var panelRotation = Quaternion.Inverse(viewSpace.rotation) * panel.rotation;
            float ratio = scale / viewSpace.localScale.x;
            viewSpace.SetPositionAndRotation(position, rotation);
            viewSpace.localScale = Vector3.one * scale;
            panel.SetPositionAndRotation(viewSpace.TransformPoint(panelPosition), viewSpace.rotation * panelRotation);
            panel.localScale *= ratio;
        }

        public void ResetView()
        {
            if (!IsOpen) return;
            zoom.Reset(); stick.Reset();
            SetView(rig.position, rig.rotation, 1);
            app.UI.RefreshInspectionZoom();
        }

        public void Close()
        {
            if (!viewSpace) return;
            if (app && app.UI && app.UI.Root && rig) ResetView();
            if (head) head.SetParent(rig, false);
            foreach (var hand in hands) if (hand) hand.transform.SetParent(rig, false);
            Destroy(viewSpace.gameObject);
            viewSpace = null; button.Reset(); stick.Reset(); zoom.Reset();
            if (app && app.UI) app.UI.RefreshInspectionZoom();
        }
        void OnDisable() => Close();
    }
}
