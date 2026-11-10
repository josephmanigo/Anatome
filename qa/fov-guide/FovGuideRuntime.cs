using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ThoraxLab;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;

// Test-only source compiled into the isolated existing Mono player, never a release.
public static class FovGuideRuntime
{
    [Serializable] class Report {public string method="Fresh managed code in copied development player with cached scene/assets; QA tracking bypass",utc;public bool passed,physicalHeadsetTested=false;public List<string> checks=new(),errors=new(),screenshots=new();}
    public static IEnumerator Run(LabRuntime app,string output)
    {
        var report=new Report{utc=DateTime.UtcNow.ToString("o")};
        void Check(bool ok,string detail){(ok?report.checks:report.errors).Add(detail);}
        void Log(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)report.errors.Add(message);}
        Application.logMessageReceived+=Log;
        yield return new WaitForSecondsRealtime(2);
        app.SendMessage("OnApplicationFocus",true);app.EnterPreview();app.Progress.orientationCompleted=true;
        var settings=app.Settings.Draft();settings.hints=true;settings.textScale=1;settings.vignette=true;app.Settings.Apply(settings);
        app.UI.ShowSettings();const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(LabUI).GetField("settingCategory",flags).SetValue(app.UI,"VR comfort");app.UI.SendMessage("RenderSettingsPage");yield return Shot("settings",true);
        var draft=(SettingsProfile)typeof(LabUI).GetField("draft",flags).GetValue(app.UI);
        var slider=app.UI.Root.GetComponentsInChildren<Slider>().Single(s=>s.name=="Comfort view (degrees)");slider.value=85;
        Check(draft.comfortFov==85&&app.Settings.Current.comfortFov!=85,"Comfort slider changes draft only");
        app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Apply").onClick.Invoke();yield return null;
        Check(app.Settings.Current.comfortFov==85,"Apply saves comfort width");
        var vignette=Camera.main.GetComponent<ComfortVignette>();yield return null;
        var ring=vignette.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="VR comfort periphery");float wide=ring.transform.localScale.x;
        app.Settings.Current.comfortFov=40;yield return null;yield return null;
        Check(ring.transform.localScale.x<wide,"Comfort aperture narrows independently of camera projection");
        app.Settings.Current.vignette=false;vignette.SetMoving(true);yield return new WaitForSecondsRealtime(.3f);Check(!ring.enabled,"Disabled vignette stays hidden");
        foreach(float scale in new[]{1.5f}){
            settings=app.Settings.Draft();settings.textScale=scale;settings.highContrast=true;settings.vignette=true;app.Settings.Apply(settings);
            app.UI.ShowSettings();app.UI.SendMessage("RenderSettingsPage");yield return null;Canvas.ForceUpdateCanvases();
            var largeSlider=app.UI.Root.GetComponentsInChildren<Slider>().Single(s=>s.name=="Comfort view (degrees)");
            var scroll=largeSlider.GetComponentInParent<ScrollRect>();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,(RectTransform)largeSlider.transform);
            scroll.StopMovement();scroll.content.anchoredPosition+=Vector2.up*(scroll.viewport.rect.center.y-bounds.center.y);
            yield return Shot("settings-large-contrast",true);
            Check(app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing),"Large settings labels fit their rectangles");
        }
        settings=app.Settings.Draft();settings.textScale=1;settings.highContrast=false;app.Settings.Apply(settings);
        var variant=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
        app.StartActivity(variant,SessionPhase.Practice);app.SendMessage("OnApplicationFocus",true);yield return new WaitForSecondsRealtime(.2f);
        var target=app.Structures[app.Engine.Current.targetId];var objective=app.Engine.Current;
        var path=new Vector3[7];bool found=app.Presentation.TryGetCutGuide(target,objective.region,Camera.main.transform.position,path,out var normal);
        Check(found,"Default practice viewpoint has a continuous surface route");
        var guide=app.Coach.transform.Find("Dissection visual guide - no collision or scoring");
        Check(guide&&guide.gameObject.activeInHierarchy,"Practice guide is visible");
        Check(guide&&guide.GetComponentsInChildren<Collider>(true).Length==0,"Guide has no colliders");
        yield return Shot("sweep",false);
        if(found){
            Check(path.All(p=>target.transform.InverseTransformPoint(p).x<target.SourceBounds.center.x),"Left route stays on anatomical left");
            var right=new Vector3[7];bool rightFound=app.Presentation.TryGetCutGuide(target,"right",Camera.main.transform.position,right,out _);
            Check(rightFound&&right.All(p=>target.transform.InverseTransformPoint(p).x>target.SourceBounds.center.x),"Right route stays on anatomical right");
            int count=app.Engine.Attempt.actions.Count;app.Settings.Current.hints=false;yield return null;yield return null;
            Check(!guide.gameObject.activeInHierarchy&&count==app.Engine.Attempt.actions.Count,"Hints off hides guide without scoring");
            app.Settings.Current.hints=true;yield return null;yield return null;
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=(Camera.main.transform.position+target.Renderer.bounds.center)*.5f;blocker.transform.rotation=Quaternion.LookRotation(target.Renderer.bounds.center-Camera.main.transform.position);blocker.transform.localScale=new Vector3(2,2,.02f);
            Physics.SyncTransforms();Check(!app.Presentation.TryGetCutGuide(target,"left",Camera.main.transform.position,new Vector3[7],out _),"Blocking geometry prevents guide route");UnityEngine.Object.Destroy(blocker);yield return null;Physics.SyncTransforms();
            app.Select(target);var before=target.Filter.sharedMesh;var stroke=new List<float>();
            foreach(var point in path)NativeCutting.AddPose(stroke,target.transform,point,Quaternion.LookRotation(-normal,(path[path.Length-1]-path[0]).normalized),.008f,.008f,.018f);
            app.Session.Cut(stroke);float deadline=Time.realtimeSinceStartup+30;while(app.Session.busy&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Check(target.Filter.sharedMesh!=before&&app.Session.FragmentCount>0,"Guide route creates a real native-cut chunk: "+app.Session.message);
            Check(!app.Engine.IsComplete(objective.id),"Cut and guide alone earn no removal credit");
            Check(app.Coach.DissectionFeedback?.Contains("chunk")==true,"Guide switches to chunk lift stage");yield return Shot("lift",false);
            app.Session.Undo();yield return null;yield return null;
            Check(app.Coach.DissectionFeedback?.StartsWith("Start at the ring")==true,"Undo restores current sweep guide");
        }
        app.Paused=true;yield return null;yield return null;Check(!guide.gameObject.activeInHierarchy,"Pause hides guide");app.Paused=false;
        app.StartActivity(variant,SessionPhase.Assessment);yield return null;yield return null;
        Check(app.Coach.DissectionFeedback==null&&!app.Coach.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="Start ring"),"Assessment has no added answer guide");
        app.StartSandbox();yield return null;yield return null;
        Check(!app.Coach.transform.Find("Dissection visual guide - no collision or scoring"),"Sandbox clears old guide objects");
        foreach(string id in new[]{"REC_fat","REC_fascia","FMA79979","MAL_RUL","FMA7957","FMA8148"}){
            var source=app.Catalog.units.SelectMany(u=>u.variants).First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy&&v.objectives.Any(o=>o.targetId==id&&o.action==ActionKind.Expose));
            var isolated=JsonUtility.FromJson<ScenarioVariant>(JsonUtility.ToJson(source));var step=isolated.objectives.First(o=>o.targetId==id&&o.action==ActionKind.Expose);step.prerequisites=Array.Empty<string>();isolated.objectives=new[]{step};
            app.StartActivity(isolated,SessionPhase.Practice);yield return null;Physics.SyncTransforms();
            var watch=System.Diagnostics.Stopwatch.StartNew();bool reachable=app.Presentation.TryGetCutGuide(app.Structures[id],step.region,Camera.main.transform.position,new Vector3[7],out _);watch.Stop();
            Check(reachable,"Guide surface "+id+" from prepared QA view; probe "+watch.Elapsed.TotalMilliseconds.ToString("0.00")+" ms on this PC");
        }
        report.passed=report.errors.Count==0;Application.logMessageReceived-=Log;
        File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));Application.Quit(report.passed?0:1);

        IEnumerator Shot(string name,bool panel)
        {
            yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.15f);
            var camera=new GameObject("FOV guide review camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.aspect=1;
            if(panel){camera.transform.rotation=app.UI.Root.rotation;camera.transform.position=app.UI.Root.position-app.UI.Root.forward*1.15f;camera.fieldOfView=40;}
            else {var bounds=app.Structures[app.Engine.Current.targetId].Renderer.bounds;camera.transform.position=Camera.main.transform.position;camera.transform.LookAt(bounds.center);camera.fieldOfView=42;}
            var render=new RenderTexture(1200,1200,24);camera.targetTexture=render;camera.Render();var previous=RenderTexture.active;RenderTexture.active=render;
            var image=new Texture2D(1200,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,1200),0,0);image.Apply();
            var path=Path.Combine(output,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());report.screenshots.Add(path);
            RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(camera.gameObject);
        }
    }
}
