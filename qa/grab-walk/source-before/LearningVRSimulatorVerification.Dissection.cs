#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ThoraxLab.Learning;
using UnityEngine;
namespace ThoraxLab.Editor {
 public sealed partial class LearningVRSimulatorVerificationRunner {
  IEnumerable<int> VerifyDissectionLayerCopies(){
   var chest=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
   // Isolate each real authored objective to exercise the input/geometry seam.
   // Separate content and full-player tests replay every complete lesson in order.
   foreach(string id in new[]{"FMA7163","REC_fat","REC_fascia","FMA79979","MAL_RUL","FMA7957","FMA8148"}){
    var source=app.Catalog.units.SelectMany(u=>u.variants).First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy&&v.objectives.Any(o=>o.targetId==id&&o.action==ActionKind.Expose));
    var variant=JsonUtility.FromJson<ScenarioVariant>(JsonUtility.ToJson(source));
    var objective=variant.objectives.First(o=>o.targetId==id&&o.action==ActionKind.Expose);objective.prerequisites=Array.Empty<string>();variant.objectives=new[]{objective};
    app.StartActivity(variant,SessionPhase.Practice);yield return 12;
    var target=app.Structures[id];var originalPose=target.transform.position;var before=target.Filter.sharedMesh;
    Require("auto_tool/"+id,app.Session.saw==(target.record.tissue=="bone"),"Bone objectives equip the saw before any input.");
    Require("cut_surface/"+id,LearningSceneActionFixture.TryCutSurface(app,target,objective.region,out var surface),"A real unobstructed surface exists in the required region.");
    var forward=(surface.point-Camera.main.transform.position).normalized;var origin=surface.point-forward*.7f;
    var tangent=Vector3.Cross(forward,Vector3.right).normalized;if(tangent.sqrMagnitude<.1f)tangent=Vector3.up;
    var rotation=Quaternion.LookRotation(forward,Vector3.up);
    for(int attempt=0;attempt<3&&target.Filter.sharedMesh==before;attempt++){
     var direction=Quaternion.AngleAxis(attempt*120,forward)*tangent;
     SetHandWorld(origin,rotation);yield return 6;Trigger(true);yield return 4;
     for(int i=1;i<=6;i++){SetHandWorld(origin,Quaternion.LookRotation(surface.point+direction*(.0015f*i)-origin,Vector3.up));yield return 3;}
     report.observations.Add(id+" attempt="+attempt+" canCut="+app.CanCut+" ray="+hand.HasRayCutTarget+" notice="+app.Notice+" strokes="+((List<float>)typeof(LabHandInteractor).GetField("stroke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hand)).Count);
     Trigger(false);yield return 5;for(int frame=0;frame<900&&app.Session.busy;frame++)yield return 2;
    }
    Require("ray_cut_separates_chunk/"+id,!app.Session.busy&&target.Filter.sharedMesh!=before,app.Session.message);
    var chunks=FindObjectsByType<AnatomyStructure>(FindObjectsSortMode.None).Where(s=>s.fragment&&s.record.id==id).ToArray();
    Require("one_bounded_chunk/"+id,chunks.Length==1&&chunks[0].Renderer.bounds.size.magnitude<target.Renderer.bounds.size.magnitude,"Moving tissue is confined to the swept cut, not a complete layer.");
    Require("uncut_remainder_anchored/"+id,!app.CanManipulate(target)&&target.transform.position==originalPose,"A small cut cannot unlock the entire remaining structure.");
    Require("cut_alone_no_credit/"+id,!app.Engine.IsComplete(objective.id),"A committed cut is not a verified lift.");
    var chunk=chunks[0];Require("chunk_exposed/"+id,app.Presentation.TryGetExposedSurface(chunk,Camera.main.transform.position,out var point),app.Presentation.LastSurfaceCheck);
    forward=(point-Camera.main.transform.position).normalized;origin=point-forward*.45f;rotation=Quaternion.LookRotation(forward,Vector3.up);
    SetHandWorld(origin,rotation);yield return 8;Grip(true);yield return 12;
    Require("xr_grips_chunk_only/"+id,chunk.Grab.isSelected&&!target.Grab.isSelected,"Real XRI selection holds only the cut piece.");
    Grip(false);yield return 12;
    Require("release_without_lift_no_credit/"+id,!app.Engine.IsComplete(objective.id)&&chunk.gameObject.activeInHierarchy,"Releasing in place cannot remove tissue or advance.");
    Grip(true);yield return 12;SetHandWorld(origin+Vector3.up*.2f+Vector3.right*.25f,rotation);yield return 18;
    CaptureWorldInterface("dissection-overhaul/grab-"+id+".png");Grip(false);yield return 18;
    Require("collected_chunk_not_duplicated/"+id,!chunk&&target.transform.position==originalPose,"chunkAlive="+(bool)chunk+" remainderMove="+Vector3.Distance(target.transform.position,originalPose)+" notice="+app.Notice+" chunkRegion="+(chunk?chunk.Filter.sharedMesh.bounds.ToString():"collected")+" center="+target.SourceBounds.center);
    Require("single_remaining_source/"+id,FindObjectsByType<AnatomyStructure>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(s=>!s.fragment&&s.record.id==id)==1,"Only one original anatomy object exists after cutting/grabbing.");
    Require("one_visible_cut_renderer/"+id,target.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled)==1,"No intact LOD copy remains enabled.");
    app.Session.Undo();yield return 4;
    Require("credited_chunk_cannot_return_on_undo/"+id,target.Filter.sharedMesh!=before,"Undo cannot recreate already collected tissue.");
   }
   app.StartActivity(chest,SessionPhase.Practice);yield return 10;
   var errors=new List<string>();var left=app.Engine.Current;
   var complete=LearningSceneActionFixture.CompleteCurrent(app,errors);while(complete.MoveNext())yield return 1;
   Require("left_region_complete",errors.Count==0&&app.Engine.IsComplete(left.id),string.Join("; ",errors));
   Require("left_only_not_finished",app.Engine.Current?.region=="right"&&!app.Engine.Attempt.passed,"The left chest does not satisfy the right chest or finish the activity.");
   var skin=app.Structures["FMA7163"];double cutVolume=CutMeshData.Capture(skin.Filter.sharedMesh).Volume();
   app.Save();app.Interrupt();app.ResumeLast();yield return 8;for(int frame=0;frame<900&&app.Session.busy;frame++)yield return 2;
   Require("resume_preserves_actual_opening",Math.Abs(CutMeshData.Capture(skin.Filter.sharedMesh).Volume()-cutVolume)<skin.SourceVolume*.00001,"Stored cut strokes rebuild completed regional openings without restoring removed tissue.");
   app.RestoreActivity();app.RestoreActivity();yield return 8;for(int frame=0;frame<900&&app.Session.busy;frame++)yield return 2;
   Require("repeated_restore_keeps_activity_usable",!app.Session.busy&&!app.Paused&&app.CanInteract,"A second Restore press must not cancel the in-flight reconstruction and pause the activity.");
   Require("repeated_restore_preserves_opening",Math.Abs(CutMeshData.Capture(skin.Filter.sharedMesh).Volume()-cutVolume)<skin.SourceVolume*.00001,"Repeated Restore keeps one reconstructed remainder with the accepted opening.");
   app.Interrupt();app.UI.ShowHub();yield return 5;
  }
  IEnumerable<int> VerifyChestCutEntry(){
   var chest=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
   app.StartActivity(chest,SessionPhase.Practice);yield return 12;
   var skin=app.Structures["FMA7163"];var before=skin.Filter.sharedMesh;
   Require("chest_entry_surface",LearningSceneActionFixture.TryCutSurface(app,skin,"left",out var surface),"The real skin is reachable on the current side.");
   var forward=(surface.point-Camera.main.transform.position).normalized;var origin=surface.point-forward*.7f;
   var tangent=Vector3.Cross(surface.normal,Vector3.right).normalized;if(tangent.sqrMagnitude<.1f)tangent=Vector3.Cross(surface.normal,Vector3.up).normalized;
   // Keep this trigger-entry regression on one continuous skin surface.
   bool pathFound=false;
   for(int angle=0;angle<360&&!pathFound;angle+=30){
    var candidate=Quaternion.AngleAxis(angle,surface.normal)*tangent;var previous=surface.point;bool clear=true;
    for(int i=1;i<=6;i++){
     if(!Physics.Raycast(new Ray(origin,(surface.point+candidate*(.0015f*i)-origin).normalized),out var hit,3,~0,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<AnatomyStructure>()!=skin||Vector3.Distance(previous,hit.point)>.04f){clear=false;break;}
     previous=hit.point;
    }
    if(clear){tangent=candidate;pathFound=true;}
   }
   Require("chest_entry_continuous_path",pathFound,"The regression stroke stays on one unobstructed skin surface.");
   SetHandWorld(origin,Quaternion.LookRotation(Vector3.up,Vector3.forward));yield return 6;
   Trigger(true);yield return 4;
   Require("chest_entry_trigger_starts_in_air",!hand.HasRayCutTarget&&!app.Session.selected,"Press trigger before bringing the scalpel to skin.");
   SetHandWorld(origin,Quaternion.LookRotation(forward,Vector3.up));yield return 4;
   for(int i=1;i<=6;i++){SetHandWorld(origin,Quaternion.LookRotation(surface.point+tangent*(.0015f*i)-origin,Vector3.up));yield return 3;}
   var strokes=(List<float>)typeof(LabHandInteractor).GetField("stroke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hand);
   Require("held_trigger_enters_skin_and_draws",strokes.Count>=48&&app.Session.selected==skin,"A held trigger must begin a stroke when it first reaches the current tissue; samples="+strokes.Count+" notice="+app.Notice);
   app.Session.saw=true;Trigger(false);yield return 5;
   Require("wrong_cut_tool_feedback_visible",skin.Filter.sharedMesh==before&&app.Notice=="Select the scalpel for soft tissue.","Wrong tool preserves the skin and displays the reason: "+app.Notice);
   app.Session.saw=false;SetHandWorld(origin,Quaternion.LookRotation(Vector3.up,Vector3.forward));yield return 5;Trigger(true);yield return 4;
   SetHandWorld(origin,Quaternion.LookRotation(forward,Vector3.up));yield return 4;
   for(int i=1;i<=6;i++){SetHandWorld(origin,Quaternion.LookRotation(surface.point+tangent*(.0015f*i)-origin,Vector3.up));yield return 3;}
   Trigger(false);yield return 5;for(int frame=0;frame<900&&app.Session.busy;frame++)yield return 2;
   Require("held_trigger_skin_cut_commits",!app.Session.busy&&skin.Filter.sharedMesh!=before,app.Session.message);
   Require("cut_feedback_reaches_learner",app.Notice.StartsWith("Chunk separated."),app.Notice);
   app.Interrupt();app.UI.ShowHub();yield return 5;
  }
 }
}
#endif
