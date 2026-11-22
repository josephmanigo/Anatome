#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyCaseClarity()
        {
            void Check(string id,bool passed,string detail)=>report.checks.Add(new LearningVRSimulatorVerification.Check{id=id,passed=passed,detail=detail});
            app.SendMessage("OnApplicationFocus",false);app.SendMessage("OnApplicationPause",false);yield return 3;
            Check("focus_unpause_cannot_steal_browser_input",!app.Focused,"Unpausing does not override a still-unfocused browser session.");
            app.SendMessage("OnApplicationPause",true);app.SendMessage("OnApplicationFocus",true);yield return 3;
            Check("focus_gain_cannot_override_pause",!app.Focused,"Input remains blocked until both OS pause and focus recover.");
            app.SendMessage("OnApplicationPause",false);yield return 3;
            Check("focus_returns_after_both_events",app.Focused,"Both callback orders recover focus.");
            var variant=app.Catalog.units.First(u=>u.id=="pneumothorax").variants.First(v=>v.difficulty==Difficulty.Easy);
            app.StartActivity(variant,SessionPhase.Practice);yield return 10;
            var attempt=app.Engine.Attempt.id;var objective=app.Engine.Current.id;
            Check("case_has_visible_controls",app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Case and controls"),"The current case and its controls must be available from the main step.");
            Check("case_has_live_action_feedback",app.UI.Root.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Tool handling feedback"),"Pickup, alignment and action progress must be visible on the main panel.");
            InvokeUI("ShowReferences",app.Unit);yield return 4;
            var back=app.UI.Root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Return to activity");
            Check("references_return_to_activity",back,"References must have a direct return to the retained activity.");
            if(back){foreach(int frames in GuideClick("Return to activity"))yield return frames;}
            else app.UI.ShowWorkspace();
            Check("references_preserve_attempt",app.Engine.Attempt.id==attempt&&app.Engine.Current.id==objective&&app.CanInteract,"Viewing references never restarts the current attempt.");
            InvokeUI("PrepareBrowserReturn",app.Unit);yield return 3;
            Check("browser_has_visible_return",app.UI.ScreenName=="Browser"&&app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Return to activity"),"Opening a website leaves a saved-place screen with a return action.");
            app.SendMessage("OnApplicationFocus",false);app.SendMessage("OnApplicationPause",true);yield return 3;
            app.SendMessage("OnApplicationPause",false);yield return 3;
            Check("browser_waits_for_real_focus",app.UI.ScreenName=="Browser"&&!app.CanInteract,"Unpause alone cannot resume the activity behind the browser.");
            app.SendMessage("OnApplicationFocus",true);yield return 3;
            Check("browser_resumes_same_step",app.UI.ScreenName=="Workspace"&&app.CanInteract&&app.Engine.Attempt.id==attempt&&app.Engine.Current.id==objective,"Focus recovery returns to the same step, without rebuilding the case or losing progress.");
            var errors=new List<string>();
            for(int i=0;i<2;i++){
                var action=LearningSceneActionFixture.CompleteCurrent(app,errors);while(action.MoveNext())yield return 1;
            }
            Check("case_reaches_patient_plan",errors.Count==0&&app.Engine.Current?.targetId=="equipment:chart",string.Join("; ",errors));
            var current=app.Engine.Current;var tool=app.Procedure.Tools["probe"];
            tool.desktopHeld=true;tool.activating=false;yield return 2;
            var rotation=Quaternion.LookRotation(app.Procedure.Approach);
            tool.transform.SetPositionAndRotation(app.Procedure.TargetPoint-rotation*tool.tip.localPosition,rotation);
            float until=Time.realtimeSinceStartup+current.minContactSeconds+.3f;
            while(Time.realtimeSinceStartup<until)yield return 1;
            Check("pointer_contact_needs_no_trigger",app.Engine.IsComplete(current.id),"A held pointer at the chart completes contact without an unexplained trigger hold.");
            tool.desktopHeld=false;yield return 4;
            foreach(float scale in new[]{1f,1.5f}){
                app.Settings.Current.textScale=scale;LabWidgets.TextScale=scale;app.UI.ShowWorkspace();yield return 4;
                foreach(int frames in AuditButtons("case-clarity/"+scale))yield return frames;
            }
            app.Settings.Current.textScale=1;LabWidgets.TextScale=1;app.UI.ShowWorkspace();yield return 4;
            RestHandsForCaseCapture();yield return 4;
            CaptureWorldInterface("case-clarity/workspace.png");
            foreach(int frames in GuideClick("Case and controls"))yield return frames;
            Check("case_overview_available_without_restart",app.UI.ScreenName=="Case and controls"&&app.Engine.Attempt.id==attempt,"Opening the case overview retains progress.");
            RestHandsForCaseCapture();yield return 4;
            CaptureWorldInterface("case-clarity/case-controls.png");
            foreach(int frames in GuideClick("Return to activity"))yield return frames;
            foreach(int frames in VerifyCaseIncision())yield return frames;
            app.StartActivity(variant,SessionPhase.Assessment);yield return 6;
            for(int i=0;i<2;i++){
                var step=LearningSceneActionFixture.CompleteCurrent(app,errors);while(step.MoveNext())yield return 1;
            }
            var distractor=app.Procedure.Tools["swab"];
            Require("case_protected_contact_fixture",errors.Count==0&&app.Engine.Current?.targetId=="equipment:chart"&&distractor.gameObject.activeInHierarchy,"A grip-only assessment step offers a second instrument on the tray.");
            var protectedRegion=FindObjectsByType<ProcedureProtectedRegion>(FindObjectsSortMode.None).Single(r=>r.anatomyId=="REC_Right_IC4_Nerve");
            int mistakes=app.Engine.Attempt.mistakes;
            distractor.transform.position+=protectedRegion.transform.position-distractor.Tip;distractor.desktopHeld=true;yield return 12;
            int violations=app.Engine.Attempt.actions.Count(a=>a.kind==ActionKind.ProtectedStructureViolation&&a.toolId==distractor.toolId);
            Require("case_stationary_protected_contact_once",violations==1&&app.Engine.Attempt.mistakes==mistakes+1&&app.Engine.Attempt.criticalFailure&&app.Engine.Current.targetId=="equipment:chart","One stationary contact recorded "+violations+" preservation events without completing the step.");
            distractor.desktopHeld=false;yield return 3;
            app.StartActivity(variant,SessionPhase.Lesson);yield return 4;
            Check("case_lesson_starts_with_briefing",app.UI.ScreenName=="Case and controls"&&app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Begin this case"),"New case lessons explain the patient and controls before interaction.");
            foreach(int frames in GuideClick("Begin this case"))yield return frames;
            Check("case_begins_from_briefing",app.CanInteract&&app.Engine.Current==variant.objectives[0],"Begin enters the first step without awarding credit.");
            app.Interrupt();app.UI.ShowHub();yield return 3;
        }
        IEnumerable<int> VerifyCaseIncision()
        {
            var errors=new List<string>();
            while(app.Engine.Current!=null&&app.Engine.Current.toolId!="scalpel"&&errors.Count==0){
                var step=LearningSceneActionFixture.CompleteCurrent(app,errors);while(step.MoveNext())yield return 1;
            }
            Require("case_reaches_skin_insert",errors.Count==0&&app.Engine.Current?.toolId=="scalpel",string.Join("; ",errors));
            var objective=app.Engine.Current;var skin=app.Procedure.Incision;var tool=app.Procedure.Tools["scalpel"];
            var start=app.Procedure.TrainingRoute.First();var end=app.Procedure.TrainingRoute.Last();
            var rotation=skin.WorkingRotation;var away=-skin.transform.forward*.014f;
            var gripBox=tool.GetComponent<BoxCollider>();
            SetHandWorld(tool.transform.TransformPoint(gripBox.center),Quaternion.LookRotation(Vector3.up,Vector3.forward));yield return 5;
            var gripRay=new Ray(hand.transform.position,hand.transform.forward);
            Require("case_touch_pickup_ray_misses",!gripBox.Raycast(gripRay,out _,3),"The learner's hand is inside the tool and the pointing ray misses its grab box.");
            Grip(true);yield return 8;
            Require("case_touch_picks_up_tool",tool.Held&&hand.interactablesSelected.Any(x=>ReferenceEquals(x,tool.grab)),"A grip around the case instrument picks it up without aiming a ray.");
            Grip(false);yield return 5;
            Require("case_scalpel_tray_reachable",TryAimAtTrayTool(tool,out var handStart,out var handRotation),"The simulated hand can reach the instrument on the real tray.");
            SetHandWorld(handStart,handRotation);yield return 6;
            Grip(true);yield return 10;
            Require("case_scalpel_xr_grip",tool.grab.isSelected&&tool.Held,"Picked up the case scalpel from its tray through XRI.");
            var localHand=tool.transform.InverseTransformPoint(hand.transform.position);
            var wrist=Quaternion.Inverse(tool.transform.rotation)*hand.transform.rotation;
            void Pose(Vector3 tip,Quaternion blade){SetHandWorld(tip+blade*(localHand-tool.tip.localPosition),blade*wrist);}
            Pose(start+away,rotation);yield return 4;
            Pose((start+end)*.5f+away,rotation);yield return 4;Pose(end+away,rotation);
            float until=Time.realtimeSinceStartup+objective.minContactSeconds+.15f;
            while(Time.realtimeSinceStartup<until)yield return 1;
            Require("case_hover_cannot_cut",skin.Progress==0&&!app.Engine.IsComplete(objective.id),"Held the working tip above every former checkpoint without marking the skin.");
            var inverted=rotation*Quaternion.AngleAxis(180,Vector3.forward);
            for(int i=0;i<=4;i++){Pose(Vector3.Lerp(start,end,i*.25f),inverted);yield return 3;}
            Require("case_blunt_side_cannot_cut",skin.Progress==0&&!skin.Touching,"Rolling the sharp edge away from the skin gives no incision credit.");
            Pose(start,rotation);yield return 4;Pose(end,rotation);yield return 4;
            Require("case_tip_jump_cannot_cut",skin.Progress==0&&!skin.Touching,"Moving immediately between endpoints cannot cut the intervening surface.");
            for(int i=0;i<=6;i++){Pose(Vector3.Lerp(start,end,i/12f),rotation);yield return 3;}
            float partial=skin.Progress;
            Require("case_surface_opens_under_held_blade",partial>.44f&&partial<.56f&&skin.Touching&&!app.Engine.IsComplete(objective.id),"The held instrument opened "+partial.ToString("P0")+" of the insert.");
            string directory=Path.Combine(Path.GetDirectoryName(LearningVRSimulatorVerification.Output),"case-instruments");Directory.CreateDirectory(directory);
            var camera=new GameObject("Incision review camera").AddComponent<Camera>();
            try{
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.055f;camera.nearClipPlane=.005f;camera.farClipPlane=5;
                camera.transform.position=skin.transform.TransformPoint(new Vector3(-.048f,.041f,-.20f));
                camera.transform.LookAt(skin.transform.TransformPoint(new Vector3(-.024f,0,0)),skin.transform.up);
                CaseVisualVerification.Render(camera,Path.Combine(directory,"live-incision.png"));
            }finally{Destroy(camera.gameObject);}
            Pose((start+end)*.5f+away,rotation);yield return 4;Pose(end,rotation);yield return 4;
            Require("case_lift_requires_recontact_at_cut",Mathf.Abs(skin.Progress-partial)<.002f&&!skin.Touching,"Moving off the surface preserves the existing mark and rejects a restart across the gap.");
            for(int i=6;i<=12;i++){Pose(Vector3.Lerp(start,end,i/12f),rotation);yield return 3;}
            until=Time.realtimeSinceStartup+objective.minContactSeconds+.6f;
            while(!app.Engine.IsComplete(objective.id)&&Time.realtimeSinceStartup<until)yield return 1;
            var evidence=app.Engine.Attempt.actions.LastOrDefault(a=>a.toolId=="scalpel"&&a.kind==ActionKind.Trace);
            Require("case_swept_cut_records_evidence",app.Engine.IsComplete(objective.id)&&skin.Progress==1&&evidence!=null&&evidence.contactVerified&&evidence.pathFraction==1&&evidence.contactSeconds>=objective.minContactSeconds,"One continuous surface opening was recorded using the working edge of the XR-held scalpel.");
            Grip(false);yield return 6;RestHandsForCaseCapture();yield return 4;
            while(app.Engine.Current!=null&&errors.Count==0){
                var step=LearningSceneActionFixture.CompleteCurrent(app,errors);while(step.MoveNext())yield return 1;
            }
            Require("pneumothorax_full_case_with_surface_cut",errors.Count==0&&app.Engine.AllComplete&&!app.Engine.Attempt.criticalFailure,errors.Count==0?app.Variant.objectives.Length+" objectives complete, patient reassessed.":string.Join("; ",errors));
        }
        static bool TryAimAtTrayTool(ProcedureTool tool,out Vector3 origin,out Quaternion rotation)
        {
            Physics.SyncTransforms();var bounds=tool.GetComponent<BoxCollider>().bounds;
            foreach(var outward in new[]{(Camera.main.transform.position-bounds.center).normalized,Vector3.up,Vector3.right,Vector3.left,Vector3.forward,Vector3.back}){
                origin=bounds.center+outward*(bounds.extents.magnitude+.055f);
                if(!Physics.Raycast(origin,-outward,out var hit,.5f)||hit.collider.GetComponentInParent<ProcedureTool>()!=tool)continue;
                rotation=Quaternion.LookRotation(-outward,Mathf.Abs(outward.y)>.9f?Vector3.forward:Vector3.up);return true;
            }
            origin=default;rotation=default;return false;
        }
        void RestHandsForCaseCapture()
        {
            var camera=Camera.main.transform;
            foreach(var side in new[]{UnityEngine.XR.XRNode.LeftHand,UnityEngine.XR.XRNode.RightHand})
                SetHandWorld(side,camera.TransformPoint(new Vector3(side==UnityEngine.XR.XRNode.LeftHand?-.25f:.25f,-.3f,.4f)),camera.rotation*Quaternion.Euler(60,0,0));
        }
    }
}
#endif
