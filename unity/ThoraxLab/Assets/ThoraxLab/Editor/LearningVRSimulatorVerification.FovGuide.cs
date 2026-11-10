#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyFovAndDissectionGuide()
        {
            var original=ThoraxLab.Learning.SettingsService.Clone(app.Settings.Current);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                var legacy=JsonUtility.FromJson<SettingsProfile>("{\"name\":\"Old profile\",\"platform\":\"Windows\"}");legacy.Validate();
                Require("fov/legacy_unity_json",legacy.simulatorFov==75&&legacy.comfortFov==55,"Actual Unity JSON imports old profiles with usable FOV defaults.");
                app.UI.ShowSettings();typeof(LabUI).GetField("settingCategory",flags).SetValue(app.UI,"VR comfort");
                ((SettingsProfile)typeof(LabUI).GetField("draft",flags).GetValue(app.UI)).vignette=true;
                InvokeUI("RenderSettingsPage");yield return 4;
                var sliders=app.UI.Root.GetComponentsInChildren<Slider>();
                var fov=sliders.Single(s=>s.minValue==50&&s.maxValue==110);var comfort=sliders.Single(s=>s.minValue==40&&s.maxValue==100);
                fov.value=95;comfort.value=80;
                var draft=(SettingsProfile)typeof(LabUI).GetField("draft",flags).GetValue(app.UI);
                Require("fov/draft_only",draft.simulatorFov==95&&draft.comfortFov==80&&app.Settings.Current.simulatorFov==original.simulatorFov,"Sliders edit a draft until Apply.");
                CaptureWorldInterface("fov-guide/settings.png");
                app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Apply").onClick.Invoke();yield return 5;
                Require("fov/apply_camera",Mathf.Abs(Camera.main.fieldOfView-95)<.01f&&app.Settings.Current.comfortFov==80,"Apply persists both controls and updates the simulator lens.");
                var copy=ThoraxLab.Learning.SettingsService.Clone(app.Settings.Current);
                Require("fov/round_trip",copy.simulatorFov==95&&copy.comfortFov==80,"Unity serialization retains both controls.");
                var vignette=Camera.main.GetComponent<ComfortVignette>();yield return 2;
                var ring=vignette.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="VR comfort periphery");
                float wide=ring.transform.localScale.x;app.Settings.Current.comfortFov=40;yield return 2;
                Require("fov/comfort_aperture",ring.transform.localScale.x<wide&&Mathf.Abs(Camera.main.fieldOfView-95)<.01f,"Narrowing the comfort aperture leaves camera projection unchanged.");
                app.Settings.Current.vignette=false;vignette.SetMoving(true);yield return 20;
                Require("fov/vignette_off",!ring.enabled,"Disabling vignette removes the mask.");
                app.UI.ShowSettings();typeof(LabUI).GetField("settingCategory",flags).SetValue(app.UI,"VR comfort");InvokeUI("ResetCategory");
                draft=(SettingsProfile)typeof(LabUI).GetField("draft",flags).GetValue(app.UI);
                Require("fov/category_reset",draft.simulatorFov==75&&draft.comfortFov==55,"Reset category includes both FOV controls.");
                app.Settings.Apply(original);app.Settings.Current.hints=true;app.UI.ShowHub();
                var variant=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
                app.StartActivity(variant,SessionPhase.Practice);yield return 20;
                var guide=app.Coach.transform.Find("Dissection visual guide - no collision or scoring");
                Require("guide/practice_visible",guide&&guide.gameObject.activeInHierarchy,app.Coach.DissectionFeedback??"No guide feedback.");
                Require("guide/no_colliders",guide.GetComponentsInChildren<Collider>(true).Length==0,"Teaching guides cannot intercept controller hits.");
                var target=app.Structures[app.Engine.Current.targetId];var path=new Vector3[7];
                Require("guide/left_surface",app.Presentation.TryGetCutGuide(target,"left",Camera.main.transform.position,path,out var normal),"A continuous real left surface exists.");
                Require("guide/left_side",path.All(p=>target.transform.InverseTransformPoint(p).x<target.SourceBounds.center.x),"Every guide point remains on anatomical left.");
                var rightPath=new Vector3[7];
                Require("guide/right_surface",app.Presentation.TryGetCutGuide(target,"right",Camera.main.transform.position,rightPath,out _),"A continuous real right surface exists.");
                Require("guide/right_side",rightPath.All(p=>target.transform.InverseTransformPoint(p).x>target.SourceBounds.center.x),"Every guide point remains on anatomical right.");
                var before=target.Filter.sharedMesh;var count=app.Engine.Attempt.actions.Count;
                CaptureWorldInterface("fov-guide/sweep.png");
                app.Settings.Current.hints=false;yield return 3;
                Require("guide/hints_off",!guide.gameObject.activeInHierarchy&&app.Engine.Attempt.actions.Count==count,"Turning hints off hides the guide without submitting actions.");
                app.Settings.Current.hints=true;yield return 4;
                var observer=Camera.main.transform.position;
                SetHandWorld(observer,Quaternion.LookRotation(path[0]-observer));yield return 5;Trigger(true);yield return 4;
                foreach(var point in path){SetHandWorld(observer,Quaternion.LookRotation(point-observer));yield return 3;}
                Trigger(false);yield return 5;for(int i=0;i<900&&app.Session.busy;i++)yield return 2;
                Require("guide/route_cuts_real_chunk",target.Filter.sharedMesh!=before&&app.Session.Fragments.Count>0,app.Session.message);
                yield return 4;
                Require("guide/lift_stage",app.Coach.DissectionFeedback?.Contains("chunk")==true&&!app.Engine.IsComplete(app.Engine.Current.id),"A cut shows lift guidance but earns no removal credit.");
                CaptureWorldInterface("fov-guide/lift.png");
                app.Session.Undo();yield return 5;
                Require("guide/undo_restores_sweep",app.Coach.DissectionFeedback?.StartsWith("Start at the ring")==true,"Undo replaces stale lift guidance with the current surface route.");
                app.StartActivity(variant,SessionPhase.Assessment);yield return 10;
                Require("guide/assessment_hidden",app.Coach.DissectionFeedback==null&&app.Coach.GetComponentsInChildren<LineRenderer>().All(l=>l.name!="Start ring"),"No added route or lift answers appear in Easy assessments.");
                app.StartSandbox();yield return 4;
                Require("guide/sandbox_cleanup",app.Coach.DissectionFeedback==null&&!app.Coach.transform.Find("Dissection visual guide - no collision or scoring"),"Leaving an activity destroys its guide.");
            }
            finally{app.Settings.Apply(original);app.Interrupt();app.UI.ShowHub();}
        }
    }
}
#endif
