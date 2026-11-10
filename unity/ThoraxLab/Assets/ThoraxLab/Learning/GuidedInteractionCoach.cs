using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    // Teaching aids share the actual source mesh and saved anatomical pose. They do
    // not contain colliders and never submit assessment actions.
    public sealed partial class GuidedInteractionCoach : MonoBehaviour
    {
        LabRuntime app;
        GameObject aids;
        Material ghostMaterial, lineMaterial;
        string currentId;
        bool currentHints;
        readonly HashSet<string> staged = new HashSet<string>();
        public string PlacementFeedback { get; private set; }
        public void Initialize(LabRuntime runtime) { app = runtime; }
        public void Prepare()
        {
            Clear();
            if (app.Variant == null || app.Engine == null) return;
            if (app.Variant.mode == LearningMode.AssembleAnatomy)
            {
                foreach (var objective in app.Variant.objectives)
                {
                    var target = app.Structures[objective.targetId];
                    if (app.Engine.IsComplete(objective.id)) { target.RestorePose(); continue; }
                    target.gameObject.SetActive(app.Variant.difficulty != Difficulty.Easy);
                    if (app.Variant.difficulty != Difficulty.Easy) Stage(target, staged.Count);
                }
            }
            RefreshCurrent();
        }
        public void RefreshCurrent()
        {
            var objective = app.Engine?.Current;
            if (objective?.id == currentId && currentHints==app.Settings.Current.hints) return;
            currentHints=app.Settings.Current.hints;
            currentId = objective?.id;
            ClearAids();
            foreach (var anatomy in app.Structures.Values) if(anatomy)anatomy.Highlight(false);
            if (objective == null || !app.Structures.TryGetValue(objective.targetId, out var target)) return;
            bool guided = currentHints && (app.Variant.difficulty == Difficulty.Easy || app.Engine.Attempt.phase != SessionPhase.Assessment);
            if (objective.action == ActionKind.Place)
            {
                target.gameObject.SetActive(true);
                if (!staged.Contains(target.record.id)) Stage(target, 0);
                if (guided) CreateSocket(target);
            }
            if (guided && (objective.action == ActionKind.Place || objective.action == ActionKind.Expose ||
                (objective.action == ActionKind.Select && app.Engine.Attempt.phase != SessionPhase.Assessment))) target.Highlight(true);
        }
        void Stage(AnatomyStructure target, int index)
        {
            target.RestorePose();
            var view = Camera.main;
            var right = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
            var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            float spacing = Mathf.Clamp(target.Renderer.bounds.extents.magnitude * 1.8f, .12f, .24f);
            target.transform.position += right * (.24f + (index % 3) * spacing) - forward * (.14f + (index / 3) * spacing) + Vector3.up * .04f;
            if (app.Variant.difficulty != Difficulty.Easy)
                target.transform.RotateAround(target.Renderer.bounds.center, Vector3.up, app.Variant.difficulty == Difficulty.Medium ? 25 + index * 10 : 45 + index * 20);
            staged.Add(target.record.id);
        }
        void CreateSocket(AnatomyStructure target)
        {
            aids = new GameObject("Matching anatomy socket - teaching aid without collision");
            aids.transform.SetParent(app.Session.specimen,true);
            var ghost = new GameObject("Correct anatomical shape and orientation");
            ghost.transform.SetParent(aids.transform, false);
            ghost.transform.SetParent(target.transform.parent, false);
            ghost.transform.SetLocalPositionAndRotation(target.RestPosition, target.RestRotation);
            ghost.transform.localScale = target.transform.localScale;
            ghost.transform.SetParent(aids.transform, true);
            ghost.AddComponent<MeshFilter>().sharedMesh = target.Filter.sharedMesh;
            ghostMaterial = new Material(Resources.Load<Material>("Learning/GuidanceSocket"));
            ghostMaterial.SetFloat("_Surface", 1); ghostMaterial.SetFloat("_Blend", 0);
            ghostMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); ghostMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            ghostMaterial.SetFloat("_ZWrite", 0); ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ghostMaterial.SetColor("_BaseColor", new Color(.2f,.85f,.77f,.16f)); ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
            var renderer = ghost.AddComponent<MeshRenderer>(); renderer.sharedMaterial=ghostMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var bounds=renderer.bounds; var min=bounds.min;var max=bounds.max;
            lineMaterial=new Material(Resources.Load<Material>("Learning/GuidanceLine"));
            // Dotted corners provide a shape-independent cue in addition to colour.
            for(int corner=0;corner<8;corner++)
            {
                var p=new Vector3((corner&1)==0?min.x:max.x,(corner&2)==0?min.y:max.y,(corner&4)==0?min.z:max.z);
                for(int axis=0;axis<3;axis++)
                {
                    var end=p;end[axis]+=(((corner>>axis)&1)==0?1:-1)*Mathf.Min(.025f,bounds.size[axis]*.25f);
                    var line=new GameObject("Socket corner").AddComponent<LineRenderer>();line.transform.SetParent(aids.transform,false);line.useWorldSpace=false;line.sharedMaterial=lineMaterial;line.positionCount=2;line.startWidth=line.endWidth=.0015f;line.SetPosition(0,p);line.SetPosition(1,end);line.shadowCastingMode=ShadowCastingMode.Off;
                }
            }
        }
        void Update()
        {
            if(app?.Settings!=null)RefreshCurrent();
            UpdateDissectionGuide();
            if(app?.Engine?.Current==null||app.Engine.Current.action!=ActionKind.Place){PlacementFeedback=null;return;}
            var objective=app.Engine.Current;var target=app.Structures[objective.targetId];
            float distance=Vector3.Distance(target.transform.position,target.transform.parent.TransformPoint(target.RestPosition));
            float angle=Quaternion.Angle(target.transform.localRotation,target.RestRotation);
            PlacementFeedback=distance<=objective.maxDistance&&angle<=objective.maxAngle?"Release your grip to place it.":distance>objective.maxDistance?"Bring the piece into its matching outline.":"Turn the piece to match the outline.";
        }
        void ClearAids(){if(aids)Destroy(aids);if(ghostMaterial)Destroy(ghostMaterial);if(lineMaterial)Destroy(lineMaterial);aids=null;}
        public void Clear(){ClearAids();ClearDissectionGuide();staged.Clear();currentId=null;PlacementFeedback=null;}
        void OnDestroy()=>Clear();
    }
}
