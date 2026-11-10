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
                if(!FindObserver(app,anatomy,out var exposedSurface)){errors.Add("Covered dissection target: "+objective.id+" / "+app.Presentation.LastSurfaceCheck);yield break;}
                app.Select(anatomy);
                if(app.Session.selected!=anatomy){errors.Add("Dissection target could not be selected: "+objective.id);yield break;}
                if(anatomy.record.cutting_ready)
                {
                    app.Session.saw=anatomy.record.tissue=="bone";
                    bool committed=false;
                    for(int direction=0;direction<3&&!committed;direction++)
                    {
                        var before=anatomy.Filter.sharedMesh;
                        var stroke=LocalizedStroke(anatomy,exposedSurface,direction);
                        if(stroke.Count<48)continue;
                        app.Session.Cut(stroke);
                        float timeout=Time.realtimeSinceStartup+30;
                        while(app.Session.busy&&Time.realtimeSinceStartup<timeout)yield return null;
                        if(app.Session.busy){errors.Add("Native cut timed out: "+objective.id);yield break;}
                        committed=anatomy.Filter.sharedMesh!=before;
                    }
                    if(!committed){errors.Add("Native cut preserved original: "+objective.id+" / "+app.Session.message+" / existing fragments="+app.Session.FragmentCount);yield break;}
                    if(app.Engine.IsComplete(objective.id)){errors.Add("Incision alone incorrectly completed exposure: "+objective.id);yield break;}
                }
                // Move actual committed mesh outputs out of the access corridor. No
                // visibility toggles, fake cut callbacks, or direct assessment events.
                var pieces=UnityEngine.Object.FindObjectsByType<AnatomyStructure>(FindObjectsSortMode.None).Where(s=>s.record.id==objective.targetId).ToArray();
                int pieceIndex=0;
                foreach(var piece in pieces)piece.transform.position+=Vector3.right*(.85f+pieceIndex++*.10f)+Vector3.up*.15f;
                Physics.SyncTransforms();
                var next=app.Variant.objectives.Skip(Array.IndexOf(app.Variant.objectives,objective)+1).FirstOrDefault();
                if(next!=null&&!FindObserver(app,app.Structures[next.targetId],out _)){errors.Add("Next layer remains covered after opening: "+objective.id+" -> "+next.targetId+" / "+app.Presentation.LastSurfaceCheck);yield break;}
                anatomy.TrySnap();
            }
            else
            {
                if(!app.Procedure.Tools.TryGetValue(objective.toolId,out var tool)){errors.Add("Missing tool: "+objective.id);yield break;}
                tool.desktopHeld=true;tool.activating=false;yield return null;
                Quaternion rotation=Quaternion.LookRotation(app.Procedure.Approach);
                var path=app.Procedure.TrainingRoute.ToArray();
                if(objective.action==ActionKind.Trace)
                    foreach(var point in path){tool.transform.SetPositionAndRotation(point-rotation*tool.tip.localPosition,rotation);tool.activating=true;yield return null;}
                Vector3 target=app.Procedure.TargetPoint;tool.transform.SetPositionAndRotation(target-rotation*tool.tip.localPosition,rotation);
                if(objective.equipmentState=="inspiration")
                {
                    float phaseTimeout=Time.realtimeSinceStartup+8;
                    while(app.Procedure.Inspiration&&Time.realtimeSinceStartup<phaseTimeout)yield return null;
                    while(!app.Procedure.Inspiration&&Time.realtimeSinceStartup<phaseTimeout)yield return null;
                }
                tool.activating=true;float timeout=Time.realtimeSinceStartup+objective.minContactSeconds+3;
                while(!app.Engine.IsComplete(objective.id)&&Time.realtimeSinceStartup<timeout)yield return null;
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
        static List<float> LocalizedStroke(AnatomyStructure anatomy,Vector3 exposedSurface,int direction)
        {
            // Trace a short incision on the actual visible surface instead of a
            // specimen-wide plane through every bilateral muscle component.
            var poses=new List<float>();var collider=anatomy.GetComponent<MeshCollider>();var observer=Camera.main.transform.position;
            var initialRay=new Ray(observer,(exposedSurface-observer).normalized);
            if(!collider.Raycast(initialRay,out var initial,Vector3.Distance(observer,exposedSurface)+.02f))return poses;
            var normal=initial.normal.normalized;
            var tangent=Vector3.ProjectOnPlane(Camera.main.transform.up,normal).normalized;
            if(tangent.sqrMagnitude<.5f)tangent=Vector3.ProjectOnPlane(Vector3.right,normal).normalized;
            tangent=Quaternion.AngleAxis(direction*60,normal)*tangent;
            float halfStroke=direction==2?.003f:.005f;
            for(int sample=0;sample<3;sample++)
            {
                var candidate=initial.point+tangent*((sample-1)*halfStroke);
                var ray=new Ray(observer,(candidate-observer).normalized);
                if(!collider.Raycast(ray,out var hit,Vector3.Distance(observer,candidate)+.025f))continue;
                var up=Vector3.ProjectOnPlane(tangent,hit.normal).normalized;
                if(up.sqrMagnitude<.5f)continue;
                var rotation=Quaternion.LookRotation(-hit.normal,up);
                NativeCutting.AddPose(poses,anatomy.transform,hit.point,rotation,anatomy.record.tissue=="bone"?.0015f:.0007f,.003f,.006f);
            }
            return poses;
        }
#endif
    }
}
