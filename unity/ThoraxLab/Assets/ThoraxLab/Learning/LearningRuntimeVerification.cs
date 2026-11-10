using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Learning
{
    // Scene fixtures run only in an explicitly requested Editor/development test session.
    // Actual XR Device Simulator input has its own Editor integration verification.
    public sealed class LearningRuntimeVerification : MonoBehaviour
    {
        [Serializable] class Result
        {
            public string utc,gpu,contentVersion;
            public int units,variants,sceneAttemptsPassed;
            public bool orientationGuarded,easyInitiallyLocked,mediumInitiallyLocked,hardInitiallyLocked,easyAssemblyOnePiece,deviceEncryption,xrSliderRayInput,xrReleaseStops,xrButtonRayInput,xrScrollRayInput;
            public string[] screenshots,errors;
        }
        readonly List<string> shots=new List<string>();
        readonly List<string> errors=new List<string>();
        string output;
        Bounds originalSpecimenBounds;
        Quaternion reviewRotation;
        IEnumerator Start()
        {
            if(!LabRuntime.VerificationRunning){Destroy(this);yield break;}
            var app=LabRuntime.Instance;
            originalSpecimenBounds=app.Structures["FMA7163"].Renderer.bounds;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--qa-output");
            output=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"LearningVRQA");Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(args.Contains("--qa-glass-ui"))
            {
                yield return LearningGlassUIVerification.Run(app,output);
                Application.logMessageReceived-=Log;yield break;
            }
#endif
            var settings=app.Settings.Draft();settings.textScale=1;settings.captionScale=1;settings.width=1440;settings.height=1000;settings.highContrast=false;settings.SetPreset(GraphicsPreset.Medium);app.Settings.Apply(settings);
            yield return new WaitForSecondsRealtime(3);
            reviewRotation=Camera.main.transform.rotation;
            app.EnterPreview();var first=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy);
            bool easyLocked=!app.Unlocked(first),mediumLocked=!app.Unlocked(app.Catalog.units.First().variants.First(v=>v.difficulty==Difficulty.Medium)),hardLocked=!app.Unlocked(app.Catalog.units.First().variants.First(v=>v.difficulty==Difficulty.Hard));
            app.CompleteOrientation();bool guarded=!app.Progress.orientationCompleted;
            yield return Capture("vr-first-visit",true);
            app.UI.ShowSignIn();yield return Capture("vr-signin",true);
            app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Privacy and shared headsets").onClick.Invoke();yield return Capture("vr-privacy",true);
            var privacyScroll=app.UI.Root.GetComponentInChildren<ScrollRect>();privacyScroll.StopMovement();privacyScroll.verticalNormalizedPosition=0;yield return Capture("vr-privacy-bottom",true);app.UI.ShowHub();
            app.StartOrientation();yield return Capture("vr-orientation",true);
            // Background scene fixtures explicitly restore focus; the separate official-XR test checks tracking/menu pauses.
            Debug.Log("VR fixture orientation focus="+app.Focused+", paused="+app.Paused+", workspace="+app.UI.WorkspaceVisible);
            app.SendMessage("OnApplicationFocus",true);
            var skin=app.Structures["FMA7163"];app.Select(skin);skin.transform.position+=Vector3.up*.08f;Physics.SyncTransforms();skin.TrySnap();
            if(!app.Progress.orientationCompleted)errors.Add("Physical orientation fixture did not unlock Easy: "+app.Notice);
            if(!easyLocked||!mediumLocked||!hardLocked||!guarded)errors.Add("The initial orientation/difficulty gate was bypassed.");
            app.UI.ShowHub();yield return Capture("vr-career",true);
            app.UI.SendMessage("ShowModes");yield return Capture("vr-mode-cards",true);
            app.UI.ShowUnits(LearningMode.AnatomyCases);yield return Capture("vr-case-cards",true);
            app.UI.ShowUnits(LearningMode.IdentifyEnumerate);yield return Capture("vr-unit-cards",true);
            app.UI.ShowUnits(LearningMode.IdentifyEnumerate);app.UI.ShowUnit(app.Catalog.units.First());yield return Capture("vr-easy-selection",true);
            app.StartActivity(first,SessionPhase.Practice);yield return Capture("vr-one-step",true);
            app.Interrupt();app.Presentation.ShowLayer(SpecimenLayer.Skin);yield return CaptureSpecimen("vr-skin");
            app.Presentation.ShowLayer(SpecimenLayer.Subcutaneous);yield return CaptureSpecimen("vr-fat-fascia");
            app.Presentation.ShowLayer(SpecimenLayer.Muscles);yield return CaptureSpecimen("vr-muscles");
            app.Presentation.ShowLayer(SpecimenLayer.Organs);yield return CaptureSpecimen("vr-inner-anatomy");
            var assembly=app.Catalog.units.First().variants.First(v=>v.mode==LearningMode.AssembleAnatomy&&v.difficulty==Difficulty.Easy);
            app.StartActivity(assembly,SessionPhase.Practice);
            bool onePiece=assembly.objectives.Count(o=>app.Structures[o.targetId].gameObject.activeInHierarchy)==1;
            if(!onePiece)errors.Add("Easy assembly showed more than one unfinished piece.");
            yield return Capture("vr-assembly-guidance",true);
            var caseVariant=app.Catalog.units.First(u=>u.isCase).variants.First(v=>v.difficulty==Difficulty.Easy);app.StartActivity(caseVariant,SessionPhase.Practice);yield return Capture("vr-case-first-step",true);
            app.UI.ShowSettings();yield return Capture("vr-settings",true);
            var large=app.Settings.Draft();large.textScale=1.5f;large.captionScale=1.7f;large.name="Clinical anatomy evening study profile review";app.Settings.Apply(large);app.UI.ShowSettings();yield return Capture("vr-large-text-settings",true);
            app.UI.SendMessage("ShowModes");yield return Capture("vr-large-mode-cards",true);
            app.UI.ShowUnits(LearningMode.AnatomyCases);yield return Capture("vr-large-case-cards",true);
            app.StartActivity(first,SessionPhase.Practice);yield return Capture("vr-large-captions",true);
            app.UI.ShowGuide();yield return Capture("vr-large-guide",true);app.UI.CloseGuide();
            app.UI.ShowSignIn();yield return null;app.UI.Root.GetComponentInChildren<TMP_InputField>().Select();yield return Capture("vr-large-text-keyboard",true);
            if(!app.UI.HasModal)errors.Add("VR keyboard did not open for text entry.");
            var regular=app.Settings.Draft();regular.textScale=1;regular.captionScale=1;app.Settings.Apply(regular);
            bool encryption=false;
            try{var plain=System.Text.Encoding.UTF8.GetBytes("VR QA sample, not authentication material");var encrypted=DeviceEncryption.Protect(plain);encryption=plain.SequenceEqual(DeviceEncryption.Unprotect(encrypted))&&!plain.SequenceEqual(encrypted);}catch(Exception ex){errors.Add("Device encryption: "+ex.Message);}
            int passed=0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            yield return LearningReadinessRegression.Run(app,errors,output);
#endif
            string onlyUnit=args.FirstOrDefault(a=>a.StartsWith("--qa-unit="))?.Substring(10);
            var scenarios=app.Catalog.units.Where(u=>onlyUnit==null||u.id==onlyUnit).SelectMany(u=>u.variants).ToArray();
            foreach(var scenario in scenarios)
            {
                app.SendMessage("OnApplicationFocus",true);app.StartActivity(scenario,SessionPhase.Assessment);yield return null;
                if(app.Engine==null||app.Variant.id!=scenario.id){errors.Add("Scenario remained locked: "+scenario.id);continue;}
                foreach(var objective in scenario.objectives)
                {
                    if(app.Engine.Current?.id!=objective.id){errors.Add("Unexpected current objective: "+scenario.id+"/"+objective.id);break;}
                    yield return LearningSceneActionFixture.CompleteCurrent(app,errors);
                    if(scenario.mode==LearningMode.AnatomyCases&&scenario.difficulty==Difficulty.Easy&&Array.IndexOf(scenario.objectives,objective)==0)
                    {
                        var largeChoices=app.Settings.Draft();largeChoices.textScale=1.5f;app.Settings.Apply(largeChoices);yield return null;
                        yield return CaptureStation(scenario.id+"-reasoning",true);
                        largeChoices.textScale=1;app.Settings.Apply(largeChoices);yield return null;
                    }
                    if(!app.Engine.IsComplete(objective.id)){errors.Add("Scene action failed: "+scenario.id+"/"+objective.id+" · "+app.Notice);break;}
                    if(scenario.id=="chest-wall-3-0"&&objective.targetId=="FMA7163")yield return Capture("vr-dissection-skin-opened",true);
                    if(scenario.id=="chest-wall-3-0"&&objective.targetId=="REC_fascia")yield return Capture("vr-dissection-muscles-revealed",true);
                    if(scenario.id=="pneumothorax-0-0"&&Array.IndexOf(scenario.objectives,objective)==2)yield return Capture("vr-case-procedure-step",true);
                    if(scenario.mode==LearningMode.AnatomyCases&&scenario.difficulty==Difficulty.Easy&&Array.IndexOf(scenario.objectives,objective)==2)yield return CaptureStation(scenario.id+"-station");
                }
                app.Finish();if(app.Engine.Attempt.passed)passed++;
                if(!app.Engine.Attempt.passed){
                    File.WriteAllText(Path.Combine(output,scenario.id+"-failed-attempt.json"),JsonUtility.ToJson(app.Engine.Attempt,true));
                    errors.Add("Assessment failed: "+scenario.id+"; score="+app.Engine.Attempt.score+"; mistakes="+app.Engine.Attempt.mistakes+"; preservationFailure="+app.Engine.Attempt.criticalFailure);
                }
                Debug.Log("VR scene fixture: "+scenario.id+" "+(app.Engine.Attempt.passed?"PASS":"FAIL"));
                if(!app.Engine.Attempt.passed)Debug.Log("VR fixture detail: "+string.Join(" | ",errors.Skip(Math.Max(0,errors.Count-3))));
            }
            app.UI.ShowSettings();yield return null;Camera.main.transform.LookAt(app.UI.Root.position);Canvas.ForceUpdateCanvases();yield return null;
            var slider=app.UI.Root.GetComponentsInChildren<Slider>().First();var rect=(RectTransform)slider.transform;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // The native XR raycaster correctly rejects clipped controls. Scroll
            // the test target into the viewport before attempting its drag.
            var viewportScroll=slider.GetComponentInParent<ScrollRect>();viewportScroll.StopMovement();
            var sliderBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(viewportScroll.viewport,rect);
            if(sliderBounds.min.y<viewportScroll.viewport.rect.yMin)viewportScroll.content.anchoredPosition+=Vector2.up*(viewportScroll.viewport.rect.yMin-sliderBounds.min.y+12);
            Canvas.ForceUpdateCanvases();yield return null;
#endif
            var a=rect.TransformPoint(new Vector3(rect.rect.xMin+rect.rect.width*.2f,0,0));var b=rect.TransformPoint(new Vector3(rect.rect.xMin+rect.rect.width*.8f,0,0));
            var origin=Camera.main.transform.position;
            app.UI.RayPointer(new Ray(origin,a-origin),true,held:true,pointerId:42);float before=slider.value;
            app.UI.RayPointer(new Ray(origin,b-origin),false,held:true,pointerId:42);app.UI.RayPointer(new Ray(origin,b-origin),false,held:false,pointerId:42);float after=slider.value;
            bool sliderPassed=after-before>(slider.maxValue-slider.minValue)*.3f;
            app.UI.RayPointer(new Ray(origin,a-origin),false,held:false,pointerId:42);bool releasePassed=Mathf.Approximately(after,slider.value);
            var scroll=slider.GetComponentInParent<ScrollRect>();float scrollBefore=scroll.verticalNormalizedPosition;app.UI.RayPointer(new Ray(origin,a-origin),false,Vector2.down,pointerId:42);bool scrollPassed=scroll.verticalNormalizedPosition<scrollBefore;
            var choose=app.UI.Root.GetComponentsInChildren<Button>().First(t=>t.name=="Choose a settings category");yield return Press(choose,app);
            var audio=app.UI.Root.GetComponentsInChildren<Button>().First(t=>t.name=="Audio");yield return Press(audio,app);
            bool buttonPassed=app.UI.Root.GetComponentsInChildren<Slider>().Any(t=>t.name=="Master volume");
            if(!sliderPassed||!releasePassed||!buttonPassed||!scrollPassed)errors.Add("World-space pointer checks: slider="+sliderPassed+", release="+releasePassed+", button="+buttonPassed+", scroll="+scrollPassed);
            var report=new Result{utc=DateTime.UtcNow.ToString("o"),gpu=SystemInfo.graphicsDeviceName,contentVersion=app.Catalog.version,units=app.Catalog.units.Length,variants=scenarios.Length,sceneAttemptsPassed=passed,orientationGuarded=guarded,easyInitiallyLocked=easyLocked,mediumInitiallyLocked=mediumLocked,hardInitiallyLocked=hardLocked,easyAssemblyOnePiece=onePiece,deviceEncryption=encryption,xrSliderRayInput=sliderPassed,xrReleaseStops=releasePassed,xrButtonRayInput=buttonPassed,xrScrollRayInput=scrollPassed,screenshots=shots.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"runtime-report.json"),JsonUtility.ToJson(report,true));Application.logMessageReceived-=Log;Application.Quit(errors.Count==0?0:1);
        }
        IEnumerator Press(Button button,LabRuntime app)
        {
            var rect=(RectTransform)button.transform;var point=rect.TransformPoint(rect.rect.center);var origin=Camera.main.transform.position;
            app.UI.RayPointer(new Ray(origin,point-origin),true,held:true,pointerId:42);app.UI.RayPointer(new Ray(origin,point-origin),false,held:false,pointerId:42);yield return null;Canvas.ForceUpdateCanvases();yield return null;
        }
        internal IEnumerator Capture(string name,bool includePanel)
        {
            yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.25f);
            var app=LabRuntime.Instance;var clone=new GameObject("QA evidence camera").AddComponent<Camera>();clone.CopyFrom(Camera.main);clone.enabled=false;
            var bounds=originalSpecimenBounds;
            if(includePanel){var corners=new Vector3[4];app.UI.Root.GetWorldCorners(corners);foreach(var point in corners)bounds.Encapsulate(point);}
            clone.transform.rotation=reviewRotation;clone.fieldOfView=50;clone.aspect=1.44f;
            float distance=bounds.extents.magnitude/Mathf.Sin(clone.fieldOfView*Mathf.Deg2Rad*.5f)*1.05f;
            clone.transform.position=bounds.center-clone.transform.forward*distance;clone.transform.LookAt(bounds.center);
            var target=new RenderTexture(1440,1000,24);clone.targetTexture=target;clone.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(1440,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1000),0,0);texture.Apply();
            string file=Path.Combine(output,name+".png");File.WriteAllBytes(file,texture.EncodeToPNG());shots.Add(file);RenderTexture.active=previous;clone.targetTexture=null;Destroy(target);Destroy(texture);Destroy(clone.gameObject);
            foreach(var label in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))if(label.gameObject.activeInHierarchy&&label.isTextOverflowing)errors.Add("Text overflow: "+name+": "+label.text);
        }
        IEnumerator CaptureSpecimen(string name)
        {
            var ui=LabRuntime.Instance.UI.Root.gameObject;ui.SetActive(false);yield return Capture(name,false);ui.SetActive(true);
        }
        IEnumerator CaptureStation(string name,bool reasoning=false)
        {
            Component station=reasoning?(Component)LabRuntime.Instance.Procedure.Reasoning:FindFirstObjectByType<CaseAnatomyWorkbench>();if(!station)yield break;
            yield return null;
            var renderers=station.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();if(renderers.Length==0)yield break;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var camera=new GameObject("QA station detail camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=38;camera.aspect=1.2f;
            camera.transform.rotation=reviewRotation;camera.transform.position=bounds.center-camera.transform.forward*(bounds.extents.magnitude/Mathf.Sin(camera.fieldOfView*Mathf.Deg2Rad*.5f)*1.05f);camera.transform.LookAt(bounds.center);
            var ui=LabRuntime.Instance.UI.Root.gameObject;ui.SetActive(false);
            var target=new RenderTexture(1200,1000,24);camera.targetTexture=target;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(1200,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1000),0,0);texture.Apply();var file=Path.Combine(output,name+".png");File.WriteAllBytes(file,texture.EncodeToPNG());shots.Add(file);
            if(station is CaseAnatomyWorkbench anatomy)Debug.Log("Station geometry "+name+": "+anatomy.VisualTriangleCount+" triangles / "+anatomy.VisualRendererCount+" renderers");
            foreach(var label in station.GetComponentsInChildren<TMP_Text>())if(label.isTextOverflowing)errors.Add("Reasoning label overflow: "+label.text);
            RenderTexture.active=previous;camera.targetTexture=null;Destroy(target);Destroy(texture);Destroy(camera.gameObject);ui.SetActive(true);
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(message);}
    }
}
