#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Learning
{
    // Opt-in development fixture. LabRuntime directs all storage to --qa-output.
    public static class LearningGlassUIVerification
    {
        [Serializable] sealed class Report
        {
            public string version,utc;
            public bool physicalHeadsetTested=false;
            public List<string> checks=new(),errors=new(),screenshots=new();
        }
        public static IEnumerator Run(LabRuntime app,string output)
        {
            if(!LabRuntime.VerificationRunning)yield break;
            var report=new Report{version=Application.version,utc=DateTime.UtcNow.ToString("o")};
            void Check(bool ok,string detail){(ok?report.checks:report.errors).Add(detail);}
            void Log(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)report.errors.Add(message);}
            Application.logMessageReceived+=Log;
            yield return new WaitForSecondsRealtime(2);
            var settings=app.Settings.Draft();settings.textScale=1;settings.captionScale=1;settings.highContrast=false;settings.showFps=false;app.Settings.Apply(settings);
            app.UI.ShowSignIn();yield return Shot("signin");
            Check(!app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Log out"),"Logout hidden while signed out");
            app.EnterPreview();app.Progress.orientationCompleted=true;app.UI.ShowHub();yield return Shot("guest-hub");
            Check(!app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Log out"),"Logout hidden in guest session");
            app.UI.SendMessage("ShowModes");yield return Shot("activities");
            app.UI.ShowUnits(LearningMode.AnatomyCases);yield return Shot("cases");
            app.UI.ShowSettings();yield return Shot("settings");
            yield return Click("Choose a settings category");yield return Click("Audio");
            Check(app.UI.Root.GetComponentsInChildren<Slider>().Any(s=>s.name=="Master volume"),"Navigation activates through XR ray input");
            var slider=app.UI.Root.GetComponentsInChildren<Slider>().First();var r=(RectTransform)slider.transform;var origin=Camera.main.transform.position;
            Vector3 Point(float fraction)=>r.TransformPoint(new Vector3(Mathf.Lerp(r.rect.xMin,r.rect.xMax,fraction),0,0));
            app.UI.RayPointer(new Ray(origin,Point(.2f)-origin),true,held:true,pointerId:91);float before=slider.value;
            app.UI.RayPointer(new Ray(origin,Point(.8f)-origin),false,held:true,pointerId:91);app.UI.RayPointer(new Ray(origin,Point(.8f)-origin),false,held:false,pointerId:91);
            Check(slider.value-before>(slider.maxValue-slider.minValue)*.3f,"Glass controls preserve slider dragging");

            const string id="2e0cb470-3708-437f-9a2c-8a4a14fb0123",pin="583921";
            app.Account.Trusted.Enroll(new AuthSession{user=new AuthUser{id=id,email="glass-ui@example.invalid"},access_token="local-fixture",refresh_token="local-fixture",expires_at=long.MaxValue},pin);
            app.Account.UnlockOffline(id,pin);app.Progress.orientationCompleted=true;app.Save();app.UI.ShowHub();yield return Shot("signed-in-hub");
            Check(app.UI.Root.GetComponentsInChildren<Button>().Count(b=>b.name=="Log out")==1,"Signed-in header has one visible Logout button");
            var first=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy);
            app.StartActivity(first,SessionPhase.Practice);yield return Shot("activity");
            string attemptId=app.Engine?.Attempt.id;Check(attemptId!=null,"Signed-in learner can start an activity");
            app.Session.busy=true;yield return Click("Log out");Check(app.Account.UserId==id,"Logout waits for an active cut");app.Session.busy=false;
            var blocked=typeof(LabRuntime).GetField("saveBlocked",BindingFlags.Instance|BindingFlags.NonPublic);blocked.SetValue(app,true);
            yield return Click("Log out");Check(app.Account.UserId==id,"Logout preserves session when saving is blocked");blocked.SetValue(app,false);
            yield return Click("Log out");
            Check(app.Account.Session==null&&!app.Account.Offline&&app.Engine==null&&!app.Preview&&app.UI.ScreenName=="Sign in","Logout clears account and activity and returns to sign-in");
            Check(app.Progress.userId!=id&&!app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Log out"),"Logged-out page exposes no active account progress");
            app.Account.UnlockOffline(id,pin);
            Check(app.Progress.orientationCompleted&&app.Progress.checkpoints.Any(c=>c.attemptId==attemptId),"Sign-in restores progress and the saved activity checkpoint");
            app.ResumeLast();Check(app.Engine?.Attempt.id==attemptId,"Saved activity resumes after logout and sign-in");app.Interrupt();

            settings=app.Settings.Draft();settings.textScale=1.5f;settings.captionScale=1.7f;settings.name="Clinical anatomy evening study profile review";app.Settings.Apply(settings);
            app.UI.ShowHub();yield return Shot("large-signed-in-hub");app.UI.SendMessage("ShowModes");yield return Shot("large-activities");
            app.UI.ShowUnits(LearningMode.AnatomyCases);yield return Shot("large-cases");
            app.UI.ShowSettings();yield return Shot("large-settings");yield return Click("Choose a settings category");yield return Click("Learning & accessibility");yield return Shot("large-accessibility");
            var scroll=app.UI.Root.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return Shot("large-settings-bottom");
            app.StartActivity(first,SessionPhase.Practice);yield return Shot("large-activity");app.UI.ShowGuide();yield return Shot("large-guide");app.UI.CloseGuide();
            app.UI.ShowSignIn();yield return null;app.UI.Root.GetComponentInChildren<TMP_InputField>().Select();yield return Shot("large-keyboard");
            settings=app.Settings.Draft();settings.highContrast=true;app.Settings.Apply(settings);app.UI.ShowSettings();yield return Shot("high-contrast");
            app.LogOut();app.Account.Trusted.Forget(id);
            Application.logMessageReceived-=Log;
            File.WriteAllText(Path.Combine(output,"glass-ui-report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.errors.Count==0?0:1);

            IEnumerator Click(string name)
            {
                yield return null;Canvas.ForceUpdateCanvases();yield return null;
                app.SendMessage("OnApplicationFocus",true);
                Camera.main.transform.LookAt(app.UI.Root.position);
                var button=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name==name);var rect=(RectTransform)button.transform;
                var point=rect.TransformPoint(rect.rect.center);var eye=Camera.main.transform.position;
                app.UI.RayPointer(new Ray(eye,point-eye),true,held:true,pointerId:91);app.UI.RayPointer(new Ray(eye,point-eye),false,held:false,pointerId:91);
                yield return null;yield return null;
            }
            IEnumerator Shot(string name)
            {
                app.SendMessage("OnApplicationFocus",true);
                yield return null;Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.15f);
                var root=app.UI.Root;var buttons=root.GetComponentsInChildren<Button>();
                foreach(var button in buttons)
                {
                    foreach(var label in button.GetComponentsInChildren<TMP_Text>())Check(!label.isTextOverflowing,name+": label fits "+button.name);
                    var siblings=buttons.Where(b=>b.transform.parent==button.transform.parent&&b.GetComponent<LabButtonHeight>()).ToArray();
                    if(siblings.Length>1&&button.GetComponent<LabButtonHeight>())
                    {
                        var rect=(RectTransform)button.transform;
                        Check(siblings.All(b=>Mathf.Abs(((RectTransform)b.transform).rect.height-rect.rect.height)<1),name+": equal button heights "+button.name);
                        Check(siblings.All(b=>Mathf.Abs(((RectTransform)b.transform).rect.width-rect.rect.width)<1),name+": equal button widths "+button.name);
                    }
                }
                foreach(var group in root.GetComponentsInChildren<LayoutGroup>())
                {
                    var children=group.transform.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy&&!(t.GetComponent<LayoutElement>()?.ignoreLayout??false)).OfType<RectTransform>().ToArray();
                    for(int i=1;i<children.Length;i++)
                    {
                        var a=RectTransformUtility.CalculateRelativeRectTransformBounds(group.transform,children[i-1]);var b=RectTransformUtility.CalculateRelativeRectTransformBounds(group.transform,children[i]);
                        bool separated=group is HorizontalLayoutGroup?a.max.x<=b.min.x+1:a.min.y>=b.max.y-1;
                        Check(separated,name+": no overlap in "+group.name+" / "+children[i].name);
                    }
                }
                var camera=new GameObject("Glass review camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
                camera.transform.rotation=root.rotation;camera.transform.position=root.position-root.forward*1.15f;camera.fieldOfView=40;camera.aspect=1;
                var target=new RenderTexture(1200,1200,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                var texture=new Texture2D(1200,1200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1200),0,0);texture.Apply();
                string path=Path.Combine(output,name+".png");File.WriteAllBytes(path,texture.EncodeToPNG());report.screenshots.Add(path);
                RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(camera.gameObject);
            }
        }
    }
}
#endif
