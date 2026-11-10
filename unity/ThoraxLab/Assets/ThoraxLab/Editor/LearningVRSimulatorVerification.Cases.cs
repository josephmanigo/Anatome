#if UNITY_EDITOR
using System.Collections.Generic;
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
            app.StartActivity(variant,SessionPhase.Lesson);yield return 4;
            Check("case_lesson_starts_with_briefing",app.UI.ScreenName=="Case and controls"&&app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Begin this case"),"New case lessons explain the patient and controls before interaction.");
            foreach(int frames in GuideClick("Begin this case"))yield return frames;
            Check("case_begins_from_briefing",app.CanInteract&&app.Engine.Current==variant.objectives[0],"Begin enters the first step without awarding credit.");
            app.Interrupt();app.UI.ShowHub();yield return 3;
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
