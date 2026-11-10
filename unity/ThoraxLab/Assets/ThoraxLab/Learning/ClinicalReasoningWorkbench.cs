using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ThoraxLab.Learning
{
    // Decisions require a deliberate grab and release into the patient-plan dock.
    // Neither a text button, an unheld token, nor tracking loss submits evidence.
    public sealed class ClinicalReasoningWorkbench : MonoBehaviour
    {
        LabRuntime app;
        ObjectiveDefinition objective;
        readonly Dictionary<string, ProcedureTool> tokens = new();
        readonly HashSet<ProcedureTool> armed = new();
        readonly HashSet<ProcedureTool> requireRelease = new();
        readonly Dictionary<ProcedureTool, Vector3> rests = new();
        readonly List<TextMeshPro> labels = new();
        readonly List<GameObject> hintMarkers = new();
        float labelScale;
        bool labelContrast, hints;
        Material surface, ink;
        TextMeshPro dockLabel;
        Renderer dockRenderer;
        MaterialPropertyBlock dockColor;
        public bool HintVisible => hintMarkers.Count > 0 && hintMarkers[0].activeSelf;
        public string PlacementFeedback => dockLabel?dockLabel.text:"Grip a choice, then place it on the answer dock.";
        public IReadOnlyDictionary<string, ProcedureTool> Tokens => tokens;
        public Vector3 TargetPoint => transform.TransformPoint(new Vector3(0, .19f, .02f));
        public void Configure(LabRuntime runtime, ObjectiveDefinition definition, Vector3 position)
        {
            ResetTokens(); app = runtime; objective = definition; transform.position = position; hints=app.Settings.Current.hints;
            surface ??= new Material(Resources.Load<Material>("Learning/ControllerFace"));
            ink ??= new Material(Resources.Load<Material>("Learning/ControllerBody"));
            Part(transform, "Patient-plan dock", PrimitiveType.Cylinder, new Vector3(0, .19f, .02f), new Vector3(.20f, .006f, .20f), surface);
            dockRenderer=transform.Find("Patient-plan dock").GetComponent<Renderer>();
            dockLabel=Label(transform, "Grip a choice, then place it here", new Vector3(0, .34f, .02f), .30f);
            // Stable per-attempt rotation prevents the correct answer always occupying one slot.
            int offset = (int)app.Variant.difficulty + app.Engine.Attempt.id[0] % definition.choices.Length;
            for (int slot = 0; slot < definition.choices.Length; slot++)
            {
                var choice = definition.choices[(slot + offset) % definition.choices.Length];
                var go = new GameObject(choice.label); go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((slot - 1) * .17f, .015f, -.12f);
                var token = go.AddComponent<ProcedureTool>(); token.toolId = choice.id; token.tip = go.transform;
                Part(go.transform, "Grab base", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.13f, .006f, .11f), surface);
                if (!string.IsNullOrEmpty(choice.model))
                {
                    var model = new GameObject("Original mechanism cutaway"); model.transform.SetParent(go.transform, false);
                    model.transform.localPosition = new Vector3(0, .07f, 0); model.transform.localScale = Vector3.one * .38f;
                    var teaching = model.AddComponent<DiseaseTeachingModel>(); teaching.Build(choice.model); teaching.SetResponse(choice.response, true);
                    foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
                else
                {
                    // Different solid symbols distinguish pause, continue and dismiss.
                    Part(go.transform, choice.label, choice.id == "pause-review" ? PrimitiveType.Cube : choice.id == "continue" ? PrimitiveType.Capsule : PrimitiveType.Sphere,
                        new Vector3(0, .05f, 0), new Vector3(.035f, .055f, .035f), ink);
                }
                Label(go.transform, choice.label, new Vector3(0, -.025f, -.10f), .16f);
                var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0, .04f, 0); box.size = new Vector3(.14f, .13f, .12f);
                var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                token.grab = go.AddComponent<XRGrabInteractable>(); token.grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                token.grab.throwOnDetach = false; token.grab.useDynamicAttach = true; token.grab.matchAttachPosition = true; token.grab.matchAttachRotation = true;
                token.grab.snapToColliderVolume=false;token.grab.unparentTransformOnGrab=false;
                tokens.Add(choice.id, token); rests.Add(token, go.transform.localPosition);
            }
            RefreshLabels();SetDockFeedback(false,false);
        }
        void Update()
        {
            if (app && (labelScale != app.Settings.Current.textScale || labelContrast != app.Settings.Current.highContrast)) RefreshLabels();
            if(app && hints!=app.Settings.Current.hints)
            {
                hints=app.Settings.Current.hints;
                if(!hints)foreach(var marker in hintMarkers)marker.SetActive(false);
            }
            if (!app || !app.CanInteract || app.Engine?.Current != objective || !app.UI.WorkspaceVisible)
            { foreach(var token in tokens.Values)if(token.Held||armed.Contains(token))requireRelease.Add(token);armed.Clear();SetDockFeedback(false,false); return; }
            bool holding=false,ready=false;
            foreach (var token in tokens.Values)
            {
                if(requireRelease.Contains(token)){if(!token.Held){Restore(token);requireRelease.Remove(token);}continue;}
                if (token.Held) { armed.Add(token);holding=true;ready|=DockDistance(token)<=objective.maxDistance;continue; }
                if (!armed.Remove(token)) continue;
                float distance = DockDistance(token);
                if (distance > objective.maxDistance) { Restore(token);app.Message("Move the choice to the patient-plan dock. Release grip when it says ‘Release grip to place’.");continue; }
                var action = new InteractionAction { kind = ActionKind.ClinicalDecision, targetId = objective.targetId, toolId = token.toolId,
                    equipmentState = "placed", contactVerified = true, distance = distance, angle = 0, localPosition = new[] { token.transform.position.x, token.transform.position.y, token.transform.position.z } };
                bool accepted = app.Emit(action);
                if (!accepted) Restore(token);
                // Accepted callbacks may replace this objective and rebuild the tray.
                return;
            }
            SetDockFeedback(holding,ready);
        }
        // Contact is measured from the visible choice's grab volume, not its hidden pivot.
        float DockDistance(ProcedureTool token)
        {
            // The base must be lowered to dock height; a tall model hovering above
            // the dock is not a placement. Its visible width still counts at an edge.
            if(Mathf.Abs(Vector3.Dot(token.transform.position-TargetPoint,transform.up))>objective.maxDistance)return float.PositiveInfinity;
            return Vector3.Distance(token.GetComponent<Collider>().ClosestPoint(TargetPoint),TargetPoint);
        }
        void Restore(ProcedureTool token){token.transform.SetParent(transform,false);token.transform.SetLocalPositionAndRotation(rests[token],Quaternion.identity);}
        void SetDockFeedback(bool holding,bool ready)
        {
            if(!dockLabel||!dockRenderer)return;
            string text=ready?"Release grip to place":holding?"Move your choice to this dock":"Hold GRIP on a choice. Place it here, then release.";
            if(dockLabel.text!=text)dockLabel.text=text;
            dockColor??=new MaterialPropertyBlock();dockColor.SetColor("_BaseColor",ready?LabWidgets.Accent:new Color(.25f,.36f,.40f));dockRenderer.SetPropertyBlock(dockColor);
        }
        void OnDisable(){foreach(var token in tokens.Values)if(token&&(token.Held||armed.Contains(token)))requireRelease.Add(token);armed.Clear();}
        void ResetTokens()
        {
            armed.Clear(); requireRelease.Clear(); tokens.Clear(); rests.Clear(); labels.Clear(); hintMarkers.Clear();
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        static void Part(Transform parent, string title, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = title; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        }
        TextMeshPro Label(Transform parent, string text, Vector3 position, float width)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.AddComponent<TextMeshPro>(); label.font = LabWidgets.Font; label.text = text;
            label.alignment = TextAlignmentOptions.Top; label.textWrappingMode = TextWrappingModes.Normal; label.richText = false;
            label.rectTransform.pivot = new Vector2(.5f, 1); label.rectTransform.sizeDelta = new Vector2(width, .09f);
            labels.Add(label);
            return label;
        }
        void RefreshLabels()
        {
            labelScale = app.Settings.Current.textScale; labelContrast = app.Settings.Current.highContrast;
            foreach (var label in labels)
            {
                label.fontSize = .16f * labelScale; label.color = labelContrast ? Color.white : LabWidgets.Ink;
                label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, .09f * labelScale);
            }
        }
        public void ShowHint()
        {
            if (!app || app.Engine?.Attempt.phase == SessionPhase.Assessment || !tokens.TryGetValue(objective.toolId, out var correct)) return;
            if (hintMarkers.Count == 0)
            {
                // Static, non-colliding cues teach one placement without creating answer evidence.
                foreach (var parent in new[] { correct.transform, transform })
                {
                    var cue = GameObject.CreatePrimitive(PrimitiveType.Sphere); cue.name = "Guided choice cue";
                    cue.transform.SetParent(parent, false); cue.transform.localPosition = parent == transform ? new Vector3(0, .21f, .02f) : new Vector3(0, .2f, 0);
                    cue.transform.localScale = Vector3.one * .025f; cue.GetComponent<Renderer>().sharedMaterial = surface;
                    var collider = cue.GetComponent<Collider>(); collider.enabled = false; Destroy(collider); hintMarkers.Add(cue);
                }
            }
            foreach (var marker in hintMarkers) marker.SetActive(true);
        }
        void OnDestroy() { if (surface) Destroy(surface); if (ink) Destroy(ink); }
    }
}
