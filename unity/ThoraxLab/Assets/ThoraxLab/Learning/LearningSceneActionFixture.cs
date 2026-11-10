using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThoraxLab.Learning
{
    // Scene-level regression fixture. It is callable only in Editor/development
    // verification and cannot submit actions in a normal VR player.
    public static class LearningSceneActionFixture
    {
        public static IEnumerator CompleteCurrent(LabRuntime app, List<string> errors)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(app?.Engine?.Current==null){errors.Add("No current scene objective.");yield break;}
            var objective=app.Engine.Current;
            if(objective.action==ActionKind.ClinicalDecision)
            {
                var reasoning=app.Procedure.Reasoning;var token=reasoning.Tokens[objective.toolId];
                token.desktopHeld=true;yield return null;token.transform.position=reasoning.TargetPoint;
                yield return null;token.desktopHeld=false;yield return null;yield return null;
            }
            else if(objective.action==ActionKind.Select||objective.action==ActionKind.InspectFinding)
            {
                var anatomy=app.Structures[objective.targetId];
                if(!FindObserver(app,anatomy,out _)){errors.Add("Covered selection target: "+objective.id+" / "+app.Presentation.LastSurfaceCheck);yield break;}
                app.Select(anatomy);
            }
            else if(objective.action==ActionKind.Place)
            {
                var anatomy=app.Structures[objective.targetId];anatomy.RestorePose();Physics.SyncTransforms();anatomy.TrySnap();
            }
            else if(objective.action==ActionKind.Expose)
            {
                var anatomy=app.Structures[objective.targetId];
                for(int attempt=0;attempt<24&&!app.Engine.IsComplete(objective.id);attempt++){
                    if(!FindObserver(app,anatomy,out _)){errors.Add("Covered dissection target: "+objective.id);yield break;}
                    if(!TryCutSurface(app,anatomy,objective.region,out var hit)){errors.Add("No reachable cut surface in region: "+objective.id);yield break;}
                    app.Select(anatomy);if(app.Session.selected!=anatomy){errors.Add("Cannot select "+objective.id);yield break;}
                    if(app.Session.saw!=(objective.toolId=="dissection-saw")){errors.Add("Required tool was not equipped: "+objective.id);yield break;}
                    var before=anatomy.Filter.sharedMesh;var normal=hit.normal;
                    var tangent=Vector3.Cross(normal,Vector3.right).normalized;if(tangent.sqrMagnitude<.1f)tangent=Vector3.Cross(normal,Vector3.up).normalized;
                    var rotation=Quaternion.LookRotation(-normal,tangent);var stroke=new List<float>();
                    NativeCutting.AddPose(stroke,anatomy.transform,hit.point-tangent*.008f,rotation,.008f,app.Session.saw?.012f:.008f,app.Session.saw?.025f:.018f);
                    NativeCutting.AddPose(stroke,anatomy.transform,hit.point+tangent*.008f,rotation,.008f,app.Session.saw?.012f:.008f,app.Session.saw?.025f:.018f);
                    app.Session.Cut(stroke);float timeout=Time.realtimeSinceStartup+30;while(app.Session.busy&&Time.realtimeSinceStartup<timeout)yield return null;
                    if(app.Session.busy||anatomy.Filter.sharedMesh==before){errors.Add("Chunk cut failed: "+objective.id+" / "+app.Session.message);yield break;}
                    if(app.Engine.IsComplete(objective.id)){errors.Add("Cut alone completes objective: "+objective.id);yield break;}
                    var chunk=UnityEngine.Object.FindObjectsByType<AnatomyStructure>(FindObjectsSortMode.None).LastOrDefault(x=>x.fragment&&x.cutObjectiveId==objective.id);
                    if(!chunk||!app.CanManipulate(chunk)){errors.Add("No grabbable chunk: "+objective.id);yield break;}
                    chunk.TrySnap();if(app.Engine.IsComplete(objective.id)){errors.Add("Unmoved chunk grants credit");yield break;}
                    chunk.transform.position+=Vector3.up*.2f;Physics.SyncTransforms();chunk.TrySnap();yield return null;
                }

            }
            else
            {
                if(!app.Procedure.Tools.TryGetValue(objective.toolId,out var tool)){errors.Add("Missing tool: "+objective.id);yield break;}
                Quaternion rotation=Quaternion.LookRotation(app.Procedure.Approach);
                var path=app.Procedure.TrainingRoute.ToArray();
                if(objective.targetId.EndsWith("withdrawal",StringComparison.Ordinal))app.Procedure.ReturnLooseTools();
                if(objective.targetId.EndsWith("withdrawal",StringComparison.Ordinal)&&Vector3.Distance(tool.Tip,path[0])>.005f){errors.Add("Withdrawal tool lost its retained insertion pose: "+objective.id);yield break;}
                // This scene fixture stages at the approach before gripping. Teleporting
                // a held needle from its tray across the patient is an unsafe swept contact,
                // not a valid pickup path. Actual pickup motion has separate XRI coverage.
                Vector3 start=objective.action==ActionKind.Trace?path[0]:app.Procedure.TargetPoint-app.Procedure.Approach*.1f;
                tool.transform.SetPositionAndRotation(start-rotation*tool.tip.localPosition,rotation);
                tool.desktopHeld=true;tool.activating=false;yield return null;
                if(objective.action==ActionKind.Trace)
                    foreach(var point in path){tool.transform.SetPositionAndRotation(point-rotation*tool.tip.localPosition,rotation);yield return null;}
                Vector3 target=app.Procedure.TargetPoint;tool.transform.SetPositionAndRotation(target-rotation*tool.tip.localPosition,rotation);
                if(objective.equipmentState=="inspiration")
                {
                    float phaseTimeout=Time.realtimeSinceStartup+8;
                    while(app.Procedure.Inspiration&&Time.realtimeSinceStartup<phaseTimeout)yield return null;
                    while(!app.Procedure.Inspiration&&Time.realtimeSinceStartup<phaseTimeout)yield return null;
                }
                tool.activating=objective.action==ActionKind.ToolActivate;float timeout=Time.realtimeSinceStartup+objective.minContactSeconds+3;
                while(!app.Engine.IsComplete(objective.id)&&Time.realtimeSinceStartup<timeout)yield return null;
                if(!app.Engine.IsComplete(objective.id)){
                    var fields=typeof(ProcedureWorkbench).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    string state=string.Join(", ",fields.Where(f=>new[]{"contactDuration","consumedGesture","lastActivation","objectiveId"}.Contains(f.Name)).Select(f=>f.Name+"="+f.GetValue(app.Procedure)));
                    errors.Add("Tool contact diagnostics: "+objective.id+"; canInteract="+app.CanInteract+"; held="+tool.Held+"; active="+tool.activating+"; visible="+tool.gameObject.activeInHierarchy+"; distance="+Vector3.Distance(tool.Tip,app.Procedure.TargetPoint)+"; angle="+Vector3.Angle(tool.transform.forward,app.Procedure.Approach)+"; "+state);
                }
                tool.activating=false;tool.desktopHeld=false;yield return null;
            }
            if(!app.Engine.IsComplete(objective.id))errors.Add("Scene action failed: "+app.Variant.id+"/"+objective.id+" / "+app.Engine.Feedback);
#else
            errors.Add("Scene verification is unavailable in this VR release build.");
            yield break;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool FindObserver(LabRuntime app,AnatomyStructure target,out Vector3 surface)
        {
            Physics.SyncTransforms();var camera=Camera.main;
            if(app.Presentation.TryGetExposedSurface(target,camera.transform.position,out surface))return true;
            if(app.XRReady)return false; // Never teleport the headset or simulated HMD.
            var bound=target.Renderer.bounds;
            var whole=app.Structures.Values.Where(s=>s&&s.gameObject.activeInHierarchy).Select(s=>s.Renderer.bounds).ToArray();
            var specimen=whole.Length>0?whole[0]:bound;foreach(var box in whole)specimen.Encapsulate(box);
            float radius=Mathf.Max(.65f,specimen.extents.magnitude+.3f);
            var directions=new[]{Vector3.up,Vector3.forward,Vector3.back,Vector3.left,Vector3.right,new Vector3(1,1,1),new Vector3(-1,1,1),new Vector3(1,1,-1),new Vector3(-1,1,-1)};
            foreach(var direction in directions)
            {
                var observer=specimen.center+direction.normalized*radius;
                if(!app.Presentation.TryGetExposedSurface(target,observer,out surface))continue;
                camera.transform.parent.position+=observer-camera.transform.position;camera.transform.LookAt(surface);return true;
            }
            surface=default;return false;
        }
        public static bool TryCutSurface(LabRuntime app,AnatomyStructure target,string region,out RaycastHit hit){
            Physics.SyncTransforms();var mesh=target.Filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;var observer=Camera.main.transform.position;
            var collider=target.GetComponent<MeshCollider>();float margin=.04f/Mathf.Abs(target.transform.lossyScale.x);
            foreach(int i in Enumerable.Range(0,indices.Length/3).OrderByDescending(i=>Vector3.Cross(vertices[indices[i*3+1]]-vertices[indices[i*3]],vertices[indices[i*3+2]]-vertices[indices[i*3]]).sqrMagnitude)){
                var center=(vertices[indices[i*3]]+vertices[indices[i*3+1]]+vertices[indices[i*3+2]])/3;
                if(region=="left"&&center.x>target.SourceBounds.center.x-margin||region=="right"&&center.x<target.SourceBounds.center.x+margin)continue;
                var world=target.transform.TransformPoint(center);var ray=new Ray(observer,(world-observer).normalized);
                if(!collider.Raycast(ray,out hit,Vector3.Distance(world,observer)+.02f))continue;
                var local=target.transform.InverseTransformPoint(hit.point);
                if(region=="left"&&local.x>target.SourceBounds.center.x-margin||region=="right"&&local.x<target.SourceBounds.center.x+margin)continue;
                if(Physics.Raycast(ray,out var first,hit.distance+.002f,~0,QueryTriggerInteraction.Ignore)&&first.collider==collider)return true;
            }
            hit=default;return false;
        }

#endif
    }
}
