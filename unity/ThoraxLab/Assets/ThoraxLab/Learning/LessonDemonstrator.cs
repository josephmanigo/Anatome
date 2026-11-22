using System.Collections;
using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class LessonDemonstrator : MonoBehaviour
    {
        GameObject visual;Material material;Coroutine sequence;
        public bool Playing {get;private set;}
        public void Play()
        {
            Stop();var app=LabRuntime.Instance;
            if(app?.Engine==null||app.Engine.Attempt.phase==SessionPhase.Assessment)return;
            sequence=StartCoroutine(Demonstrate(app));
        }
        IEnumerator Demonstrate(LabRuntime app)
        {
            var objective=app.Engine.Current;if(objective==null)yield break;
            Playing=true;
            if(app.Structures.TryGetValue(objective.targetId,out var anatomy))
            {
                anatomy.Highlight(true);
                if(objective.action==ActionKind.Place)
                {
                    visual = new GameObject("Placement guide line");
                    var destination = visual.transform;
                    destination.SetParent(anatomy.transform.parent, false);
                    destination.SetLocalPositionAndRotation(anatomy.RestPosition, anatomy.RestRotation);
                    destination.localScale = anatomy.transform.localScale;
                    var line = visual.AddComponent<LineRenderer>();
                    line.sharedMaterial = Resources.Load<Material>("Learning/GuidanceLine");
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.startWidth = .004f;
                    line.endWidth = .002f;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    var meshCenter = anatomy.SourceBounds.center;
                    float time = 0;
                    while (time < 3 && app.Engine?.Current == objective)
                    {
                        if (app.CanInteract && !app.Narrator.Paused) time += Time.deltaTime;
                        float t = app.Settings.Current.reducedMotion ? 1 : Mathf.SmoothStep(0, 1, time / 3);
                        var start = anatomy.transform.TransformPoint(meshCenter);
                        line.SetPosition(0, start);
                        line.SetPosition(1, Vector3.Lerp(start, destination.TransformPoint(meshCenter), t));
                        yield return null;
                    }
                }
                else{app.Procedure.ShowHint();yield return new WaitForSeconds(2);}
                if(anatomy)anatomy.Highlight(false);
            }
            else if(app.Procedure.Tools.TryGetValue(objective.toolId,out var tool))
            {
                app.Procedure.ShowHint();
                visual=new GameObject("Tool demonstration - no collision or scoring");
                var model=InstrumentGeometry.Create(visual.transform,objective.toolId);
                material=new Material(Resources.Load<Material>("Learning/GuidanceSocket"));
                material.SetColor("_BaseColor",new Color(.2f,.85f,.77f,.35f));
                var renderer=model.GetComponent<MeshRenderer>();var finishes=renderer.sharedMaterials;for(int i=0;i<finishes.Length;i++)finishes[i]=material;renderer.sharedMaterials=finishes;
                var rotation=objective.toolId=="scalpel"&&app.Procedure.Incision?app.Procedure.Incision.WorkingRotation:Quaternion.LookRotation(app.Procedure.Approach);var points=app.Procedure.TrainingRoute;
                float time=0;while(time<4&&app.Engine!=null&&app.Engine.Current==objective){
                    if(app.CanInteract&&!app.Narrator.Paused)time+=Time.deltaTime;
                    float t=app.Settings.Current.reducedMotion?1:Mathf.Clamp01(time/3);
                    Vector3 end=app.Procedure.TargetPoint;
                    if(objective.action==ActionKind.Trace&&points.Count>1){float index=t*(points.Count-1);int from=Mathf.Min(points.Count-2,Mathf.FloorToInt(index));end=Vector3.Lerp(points[from],points[from+1],index-from);}
                    else end=Vector3.Lerp(end-app.Procedure.Approach*.09f,end,t);
                    visual.transform.SetPositionAndRotation(end-rotation*tool.tip.localPosition,rotation);yield return null;
                }
            }
            else app.Procedure.ShowHint();
            app.Message("Your turn. Follow the current step; grip holds the tool and the panel explains its action.");Stop();
        }
        public void Stop()
        {
            if(sequence!=null)StopCoroutine(sequence);
            sequence=null;
            if(visual){visual.SetActive(false);Destroy(visual);visual=null;}
            if(material){Destroy(material);material=null;}
            Playing=false;
        }
        void OnDestroy()=>Stop();
    }
}
