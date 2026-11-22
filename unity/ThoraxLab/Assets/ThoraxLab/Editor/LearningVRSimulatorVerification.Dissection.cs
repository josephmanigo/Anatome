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
     if(attempt==0){
      bool saw=app.Session.saw;
      var mesh=hand.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m=>m.sharedMesh&&m.sharedMesh.name=="Detailed dissection-"+(saw?"saw":"scalpel"));
      float tipGap=mesh?Vector3.Distance(mesh.transform.TransformPoint(Vector3.forward*(saw?InstrumentGeometry.SawTipZ:InstrumentGeometry.ScalpelTipZ)),hand.RayCutPoint):float.PositiveInfinity;
      Require("visible_edge_meets_native_contact/"+id,mesh&&hand.DrivingBlade&&tipGap<.0002f,
       "The tool's nominal working end meets this tracked surface. Mesh="+(mesh?mesh.name:"missing")+", driving="+hand.DrivingBlade+", ray="+hand.HasRayCutTarget+", tip gap (m)="+tipGap.ToString("F6")+", can cut="+app.CanCut+", notice="+app.Notice);
      if(saw){
       var blade=mesh.transform.Find(InstrumentGeometry.SawBladeName);float excursion=0;
       bool reduced=app.Settings.Current.reducedMotion;app.Settings.Current.reducedMotion=false;
       for(int frame=0;frame<8;frame++){yield return 1;excursion=Mathf.Max(excursion,Quaternion.Angle(Quaternion.identity,blade.localRotation));}
       app.Settings.Current.reducedMotion=true;yield return 2;
       Require("saw_contact_and_reduced_motion/"+id,excursion>.1f&&excursion<=1.6f&&Quaternion.Angle(Quaternion.identity,blade.localRotation)<.01f,"Only the blade moves under contact. Reduced motion retains the resting pose.");
       app.Settings.Current.reducedMotion=reduced;
      }
     }
     for(int i=1;i<=6;i++){SetHandWorld(origin,Quaternion.LookRotation(surface.point+direction*(.0015f*i)-origin,Vector3.up));yield return 3;}
     report.observations.Add(id+" attempt="+attempt+" canCut="+app.CanCut+" ray="+hand.HasRayCutTarget+" notice="+app.Notice+" strokes="+((List<float>)typeof(LabHandInteractor).GetField("stroke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hand)).Count);
     Trigger(false);yield return 5;for(int frame=0;frame<900&&app.Session.busy;frame++)yield return 2;
    }
    Require("ray_cut_separates_chunk/"+id,!app.Session.busy&&target.Filter.sharedMesh!=before,app.Session.message);
    var chunks=FindObjectsByType<AnatomyStructure>(FindObjectsSortMode.None).Where(s=>s.fragment&&s.record.id==id).ToArray();
    Require("one_bounded_chunk/"+id,chunks.Length==1&&chunks[0].Renderer.bounds.size.magnitude<target.Renderer.bounds.size.magnitude,"Moving tissue is confined to the swept cut, not a complete layer.");
    Require("uncut_remainder_preserved/"+id,target.transform.position==originalPose,"A small cut preserves the remaining structure; inspection never earns removal credit.");
    Require("cut_alone_no_credit/"+id,!app.Engine.IsComplete(objective.id),"A committed cut is not a verified lift.");
    var chunk=chunks[0];Require("chunk_exposed/"+id,app.Presentation.TryGetExposedSurface(chunk,Camera.main.transform.position,out var point),app.Presentation.LastSurfaceCheck);
    forward=(point-Camera.main.transform.position).normalized;origin=point-forward*.45f;rotation=Quaternion.LookRotation(forward,Vector3.up);
    SetHandWorld(origin,rotation);yield return 8;Grip(true);yield return 12;
    Require("xr_grips_chunk_only/"+id,chunk.Grab.isSelected&&!target.Grab.isSelected,"Real XRI selection holds only the cut piece.");
    SetHandWorld(origin,Quaternion.AngleAxis(15,Vector3.up)*rotation);yield return 8;
    Require("chunk_grip_does_not_collect_other_targets/"+id,hand.interactablesSelected.Count==1&&chunk.Grab.isSelected,"Moving a held ray cannot select additional tissue.");
    SetHandWorld(origin,rotation);yield return 8;
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
  IEnumerable<int> VerifyChestCutEntry(bool zoom=false){
   var chest=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
   app.StartActivity(chest,SessionPhase.Practice);yield return 12;
   if(zoom){app.Inspector.Open();app.Inspector.ZoomIn();yield return 4;}
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
   // Exercise a tool change on the release frame, while the samples still belong to the scalpel.
   app.Session.saw=true;Trigger(false);yield return 5;
   Require("wrong_cut_tool_feedback_visible",skin.Filter.sharedMesh==before&&app.Notice=="Select the scalpel for soft tissue.","Wrong tool preserves the skin and displays the reason: "+app.Notice);
   app.UI.RefreshLive();Canvas.ForceUpdateCanvases();yield return 2;
   var feedback=app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.text==app.Notice);
   var feedbackScroll=feedback.GetComponentInParent<UnityEngine.UI.ScrollRect>();
   var feedbackBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(feedbackScroll.viewport,feedback.rectTransform);
   Require("cut_rejection_visible_without_scrolling",feedbackBounds.min.y>=feedbackScroll.viewport.rect.yMin&&feedbackBounds.max.y<=feedbackScroll.viewport.rect.yMax,"Cut feedback must fit inside the visible panel after a stroke; bounds="+feedbackBounds+" viewport="+feedbackScroll.viewport.rect);
   foreach(int frames in GuideClick("Bone saw"))yield return frames;
   SetHandWorld(origin,Quaternion.LookRotation(forward,Vector3.up));yield return 5;Trigger(true);yield return 4;
   Require("saw_on_skin_rejects_at_contact",app.Session.saw&&hand.HasRayCutTarget&&!hand.DrivingBlade&&strokes.Count==0&&app.Notice=="Select the scalpel for soft tissue.","A new trigger press with the saw touching skin gives the tool-specific guidance without driving the blade.");
   Trigger(false);yield return 4;
   Require("saw_on_skin_release_preserves_mesh",skin.Filter.sharedMesh==before&&!app.Session.busy&&app.Notice=="Select the scalpel for soft tissue.","Releasing the rejected saw leaves the original tissue and the feedback intact.");
   foreach(int frames in GuideClick("Scalpel"))yield return frames;
   SetHandWorld(origin,Quaternion.LookRotation(Vector3.up,Vector3.forward));yield return 5;Trigger(true);yield return 4;
   SetHandWorld(origin,Quaternion.LookRotation(forward,Vector3.up));yield return 4;
   if(zoom){
    float size=app.Inspector.Magnification;var point=hand.RayCutPoint;
    rightState.primary2DAxis=Vector2.up;QueueAll();yield return 10;
    rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 3;
    Require("inspection/zoom_during_stroke",app.Inspector.Magnification>size&&hand.HasRayCutTarget&&Vector3.Distance(hand.RayCutPoint,point)<.002f,
        "Zoom can change mid-stroke without moving the blade contact on the skin.");
   }
   for(int i=1;i<=6;i++){SetHandWorld(origin,Quaternion.LookRotation(surface.point+tangent*(.0015f*i)-origin,Vector3.up));yield return 3;}
   Trigger(false);yield return 5;for(int frame=0;frame<900&&app.Session.busy;frame++)yield return 2;
   Require("held_trigger_skin_cut_commits",!app.Session.busy&&skin.Filter.sharedMesh!=before,app.Session.message);
   if(zoom)Require("inspection/cut",app.Inspector.IsOpen&&app.Inspector.Magnification>1&&app.CanUseCuttingTool,"The native cut completes while the enlarged workspace stays interactive.");
   Require("cut_feedback_reaches_learner",app.Notice.StartsWith("Chunk separated."),app.Notice);
   Require("cut_feedback_matches_chunk_only_grab",app.Notice.Contains("Whole layers stay anchored.")&&!app.Notice.Contains("Whole tissue can be inspected"),app.Notice);
   app.Interrupt();app.UI.ShowHub();yield return 5;
  }
 }
}
#endif
