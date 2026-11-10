using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI : MonoBehaviour
    {
        LabRuntime app;
        Canvas canvas;
        RectTransform screen,body,content,rail,footer,modal,captionPanel;
        TextMeshProUGUI status,objectiveStatus,notice,caption,selectedLabel,stepLabel,windowLabel;
        Button finishButton;
        Image progressFill;
        LearningMode chosenMode;
        LearningUnit chosenUnit;
        Difficulty chosenDifficulty;
        SettingsProfile draft;
        string currentScreen,settingCategory="VR comfort",email="";
        Action previousScreen;
        bool xrLayout;
        float xrCaptureUntil;
        bool panelDragging;
        Vector3 previousPanelPoint;
        sealed class XRPointer {public PointerEventData data;public GameObject pressed,drag,hover;public bool dragging;}
        readonly System.Collections.Generic.Dictionary<int,XRPointer> xrPointers=new System.Collections.Generic.Dictionary<int,XRPointer>();
        public bool XRPanelCaptured => Time.unscaledTime<xrCaptureUntil;
        public bool WorkspaceVisible => currentScreen=="Workspace" && !modal;
        public string ScreenName => currentScreen;
        public RectTransform Root => screen;
        public bool HasModal => modal;
        public void Initialize(LabRuntime runtime)
        {
            app=runtime;LabWidgets.Font=Resources.Load<TMP_FontAsset>("Fonts/SourceSans3-SDF");
            if(!LabWidgets.Font){var source=Resources.Load<UnityEngine.Font>("Fonts/SourceSans3-Regular");if(source)LabWidgets.Font=TMP_FontAsset.CreateFontAsset(source);}
            if(!LabWidgets.Font)throw new InvalidOperationException("Source Sans 3 is missing from the installation.");
            if(!EventSystem.current){var ev=new GameObject("Learning event system");ev.AddComponent<EventSystem>();ev.AddComponent<InputSystemUIInputModule>();}
            foreach(var module in EventSystem.current.GetComponents<BaseInputModule>())module.enabled=false;
            screen=Rect("Thorax Lab interface",transform);canvas=screen.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=20;screen.gameObject.AddComponent<GraphicRaycaster>();xrLayout=true;
            var scaler=screen.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,1000);scaler.matchWidthOrHeight=.5f;
            app.Changed+=RefreshLive;app.Settings.Changed+=()=>{TextScale=app.Settings.Current.textScale;HighContrast=app.Settings.Current.highContrast;};TextScale=app.Settings.Current.textScale;HighContrast=app.Settings.Current.highContrast;
            ShowSignIn();
        }
        void Page(string name,string title,bool workspace=false)
        {
            foreach(var pointer in xrPointers.Values){ReleasePointer(pointer,null);pointer.hover=null;}
            if(modal)Destroy(modal.gameObject);modal=null;
            foreach(Transform child in screen)Destroy(child.gameObject);
            currentScreen=name;objectiveStatus=notice=caption=selectedLabel=stepLabel=windowLabel=null;captionPanel=null;finishButton=null;progressFill=null;
            Camera.main.rect=new Rect(0,0,1,1);
            var backdrop=Panel(screen,"VR console surface",Ground);backdrop.raycastTarget=false;Fill(backdrop.rectTransform);
            float headerHeight=Mathf.Max(94,48*TextScale+48),footerHeight=Mathf.Max(58,32*TextScale+24);
            var top=Panel(screen,"Top navigation",Ground);var tr=top.rectTransform;tr.anchorMin=new Vector2(0,1);tr.anchorMax=Vector2.one;tr.pivot=new Vector2(.5f,1);tr.sizeDelta=new Vector2(0,headerHeight);tr.anchoredPosition=Vector2.zero;
            var row=Row(tr);Fill(row,14);var brand=Text(row,"THORAX LAB",23);brand.fontStyle=FontStyles.Bold;Size(brand,flexible:1);
            Size(Button(row,workspace?"Pause":"Menu",ShowPauseMenu),width:145);
            rail=Rect("Learning content",screen);rail.anchorMin=Vector2.zero;rail.anchorMax=Vector2.one;rail.offsetMin=new Vector2(10,footerHeight+8);rail.offsetMax=new Vector2(-10,-headerHeight-8);
            body=Rect("Body",rail);Fill(body);content=Scroll(body);
            Text(content,title,workspace?30:38).fontStyle=FontStyles.Bold;
            footer=Panel(screen,"Save status",Ground).rectTransform;footer.anchorMin=Vector2.zero;footer.anchorMax=new Vector2(1,0);footer.pivot=new Vector2(.5f,0);footer.sizeDelta=new Vector2(0,footerHeight);footer.anchoredPosition=Vector2.zero;
            status=Text(footer,"",16,Muted);Fill(status.rectTransform,10);RefreshLive();
            if(screen.localScale==Vector3.one)SetXRLayout();
        }
        public void ShowHeadsetGate()
        {
            Page("Headset","Your VR lab is ready");
            Text(content,"Put on your headset to begin.",28,Accent);
            Text(content,"In the Unity Editor, use the XR Device Simulator to control your headset and hands.",24,Muted);
            Text(content,"Tracking is required for anatomy interactions.",21,Muted);
            Button(content,"Check tracking",()=>{if(app.XRReady)ShowSignIn();else app.Message(app.XRStatus);},true);ErrorArea();
        }
        public void TogglePauseMenu()
        {
            if(currentScreen=="Menu"&&(app.Engine!=null||app.Sandbox||app.OrientationActive))ShowWorkspace();
            else ShowPauseMenu();
        }
        void ShowPauseMenu()
        {
            bool working=app.Engine!=null||app.Sandbox||app.OrientationActive;
            Page("Menu",working?"Take your time":"Your lab");
            if(working)Button(content,"Return to activity",ShowWorkspace,true);
            else if(app.Account.UserId!=null||app.Preview)Button(content,"Return to lab",ShowHub,true);
            else Button(content,"Return to sign-in",ShowSignIn,true);
            Button(content,"Settings",()=>{previousScreen=working?(Action)ShowWorkspace:ShowHub;ShowSettings();});
            if(app.Account.UserId!=null||app.Preview)Button(content,"Your progress",()=>Leave(ShowProgress));
            Button(content,"Move panel in front of me",RecenterPanel);
            if(working)Button(content,"Save step and leave activity",()=>Leave(ShowHub));
            Text(content,"Point to a button and squeeze the trigger. Grip the top edge to move this panel.",21,Muted);
        }
        void Leave(Action destination)
        {
            if(app.Session.busy){app.Message("Wait for the current cut to finish.");return;}
            app.Interrupt();destination();
        }
        public void ShowSignIn()
        {
            Page("Sign in","Step into the anatomy lab");
            Text(content,"Learn one hands-on step at a time.",25,Muted);
            Button(content,"Try your first VR lesson",app.EnterPreview,true);
            Text(content,"Guest practice stays on Easy until you pass. It is not saved after this session.",20,Muted);
            Rule(content);Text(content,"Save your learning",27);Text(content,"Sign in with an email code to keep your progress.",21,Muted);
            var field=Field("Email address",email);field.contentType=TMP_InputField.ContentType.EmailAddress;
            Button(content,"Send email code",()=>Run(async()=>{email=field.text.Trim();await app.Account.SendCode(email);ShowCode();}));
            foreach(var account in app.Account.Trusted.Accounts())
            {
                var accountCopy=account;Button(content,"Offline access · "+account.email,()=>ShowOffline(accountCopy.userId,accountCopy.email));
            }
            ErrorArea();
        }
        void ShowCode()
        {
            Page("Email code","Check your email");Text(content,"Enter the six-digit code sent to "+email+".",23,Muted);
            var code=Field("Six-digit code");code.characterLimit=6;code.contentType=TMP_InputField.ContentType.IntegerNumber;
            Button(content,"Verify and continue",()=>Run(()=>app.Account.VerifyCode(email,code.text)),true);
            Button(content,"Resend code",()=>Run(async()=>{await app.Account.SendCode(email);app.Message("A new code has been sent.");}));
            Text(content,"Codes expire after ten minutes. Wait sixty seconds before requesting another. Check spam if it hasn’t arrived.",21,Muted);
            Button(content,"Use a different email",ShowSignIn);ErrorArea();
        }
        void ShowOffline(string id,string address)
        {
            Page("Offline access","Welcome back");Text(content,address,23,Muted);var pin=Field("Device PIN","",true);pin.characterLimit=6;
            Button(content,"Unlock saved progress",()=>Run(()=>{app.Account.UnlockOffline(id,pin.text);return Task.CompletedTask;}),true);
            Text(content,"Forgot your PIN? Sign in online with a fresh email code to replace it.",21,Muted);Button(content,"Email sign-in",ShowSignIn);ErrorArea();
        }
        public void ShowHub()
        {
            if(app.Account.UserId==null&&!app.Preview){ShowSignIn();return;}
            if(!app.Session.busy){app.Interrupt();app.Session.SetMode(LabMode.Explore);if(app.Presentation)app.Presentation.ShowIntact();}
            Page("Career",app.Progress.orientationCompleted?"What will you discover next?":"Welcome to your lab");
            if(!app.Progress.orientationCompleted)
            {
                Text(content,"Start with your hands. Learn to point, hold and release before your first Easy activity.",27,Muted);
                Button(content,"Learn the VR controls",ShowOrientation,true);
                Text(content,"Three short actions. No timer.",22,Muted);
            }
            else
            {
                var next=app.Catalog.units.First(u=>u.id=="chest-wall");
                Text(content,"Begin with the chest wall, then explore at your own pace.",25,Muted);
                if(app.Progress.attempts.Any(a=>a.status==AttemptStatus.Active))Button(content,"Continue your activity",app.ResumeLast,true);
                else Button(content,"Start an Easy lesson",()=>{chosenMode=LearningMode.IdentifyEnumerate;chosenDifficulty=Difficulty.Easy;app.StartActivity(next.variants.First(v=>v.mode==chosenMode&&v.difficulty==Difficulty.Easy),SessionPhase.Lesson);},true);
                Button(content,"Choose an activity",ShowModes);
                Text(content,"Medium opens after you pass Easy. Hard opens after Medium.",22,Muted);
            }
            if(app.Preview)Button(content,"Sign in to save progress",()=>{app.Interrupt();ShowSignIn();});
            ErrorArea();
        }
        void ShowModes()
        {
            Page("Modes","Choose how to learn");
            Mode("Identify & Enumerate","Find and name structures.",LearningMode.IdentifyEnumerate);
            Mode("Assemble Anatomy","Return one part to its place.",LearningMode.AssembleAnatomy);
            Mode("Guided Dissection","Reveal the anatomy layer by layer.",LearningMode.GuidedDissection);
            Mode("Anatomy Cases","Follow a patient from findings to reassessment.",LearningMode.AnatomyCases);
            Button(content,"Back to lab",ShowHub);
        }
        void Mode(string title,string description,LearningMode mode)
        { Button(content,title,()=>ShowUnits(mode));Text(content,description,20,Muted); }
        void ShowOrientation()
        {
            app.StartOrientation();
        }
        public void ShowUnits(LearningMode mode)
        {
            chosenMode=mode;chosenDifficulty=Difficulty.Easy;Page("Units",ModeName(mode));Text(content,"Choose a unit. Start on Easy.",23,Muted);
            foreach(var unit in app.Catalog.units.Where(u=>u.isCase==(mode==LearningMode.AnatomyCases)))
            { var selected=unit;Button(content,unit.title,()=>ShowUnit(selected)); }
            if(mode==LearningMode.GuidedDissection){Rule(content);Button(content,"Free dissection sandbox",app.StartSandbox);}
            Button(content,"Back to activities",ShowModes);
        }
        public void ShowUnit(LearningUnit unit)
        {
            chosenUnit=unit;Page("Unit",unit.title);Text(content,unit.summary,24,Muted);
            var variant=unit.variants.First(v=>v.mode==chosenMode&&v.difficulty==chosenDifficulty);
            if(!app.Unlocked(variant)){chosenDifficulty=Difficulty.Easy;variant=unit.variants.First(v=>v.mode==chosenMode&&v.difficulty==chosenDifficulty);}
            var selected=variant;Text(content,chosenDifficulty+" · "+variant.objectives.Length+" hands-on steps",25,Accent);
            Text(content,"Watch a step, try it, then check your understanding.",23,Muted);
            Button(content,"Learn step by step",()=>app.StartActivity(selected,SessionPhase.Lesson),true,app.Unlocked(selected));
            Button(content,"Practice or assessment",()=>ShowPhaseChoice(selected),false,app.Unlocked(selected));
            Button(content,"Change difficulty",ShowDifficultyChoice);
            Button(content,"Back to units",()=>ShowUnits(chosenMode));
        }
        void ShowDifficultyChoice()
        {
            Page("Difficulty",chosenUnit.title);
            foreach(Difficulty level in Enum.GetValues(typeof(Difficulty)))
            {
                var variant=chosenUnit.variants.First(v=>v.mode==chosenMode&&v.difficulty==level);bool unlocked=app.Unlocked(variant);var selected=level;
                Button(content,level+(unlocked?"":" · locked"),()=>{chosenDifficulty=selected;ShowUnit(chosenUnit);},level==chosenDifficulty,unlocked);
                Text(content,unlocked?(level==Difficulty.Easy?"One step at a time with clear guides.":level==Difficulty.Medium?"More independence and anatomical relationships.":"Precise work and more complex decisions."):(level==Difficulty.Easy?"Complete the VR introduction first.":"Pass "+(level==Difficulty.Medium?"Easy":"Medium")+" in this activity first."),22,Muted);
            }
            Text(content,"Pass with at least 80% and every essential step. There is no time limit.",21,Muted);Button(content,"Back",()=>ShowUnit(chosenUnit));
        }
        void ShowPhaseChoice(ScenarioVariant variant)
        {
            Page("Learning phase",chosenUnit.title+" · "+variant.difficulty);
            Button(content,"Practice with guidance",()=>app.StartActivity(variant,SessionPhase.Practice),true);
            Text(content,"Replay demonstrations and use placement guides.",23,Muted);
            Button(content,"Take the assessment",()=>app.StartActivity(variant,SessionPhase.Assessment));
            Text(content,"Pass with 80% and every essential step. Your actions are assessed; you can retry as often as you need.",23,Muted);
            Button(content,"Read the anatomy and sources",()=>ShowReferences(chosenUnit));Button(content,"Back",()=>ShowUnit(chosenUnit));
        }
        void ShowReferences(LearningUnit unit)
        {
            Page("References",unit.title+" · references");Text(content,unit.limitations,22,Muted);
            foreach(var id in unit.sourceIds){var source=app.Catalog.sources.First(s=>s.id==id);Text(content,source.title,24);Text(content,source.usage,21,Muted);Button(content,"Open source",()=>Application.OpenURL(source.url));}
            Button(content,"Back to unit",()=>ShowUnit(unit));
        }
        public void ShowWorkspace()
        {
            Page("Workspace",app.OrientationActive?"Meet your VR hands":app.Sandbox?"Dissection sandbox":app.Unit?.title??"Anatomy workspace",true);
            if(app.OrientationActive)
            {
                stepLabel=Text(content,"",23,Accent);
                objectiveStatus=Text(content,"",28);
                Text(content,"Point with your hand. Trigger selects. Grip holds.",23,Muted);
                selectedLabel=Text(content,"",23,Muted);notice=Text(content,app.Notice,22,Accent);
            }
            else if(!app.Sandbox&&app.Engine!=null)
            {
                stepLabel=Text(content,"",23,Accent);
                var progress=Panel(content,"Activity progress",Surface);Size(progress,5);progress.raycastTarget=false;
                progressFill=Panel(progress.transform,"Completed steps",Accent);Fill(progressFill.rectTransform);progressFill.raycastTarget=false;
                objectiveStatus=Text(content,"",27);
                windowLabel=Text(content,"",20,Muted);
                notice=Text(content,"",22,Accent);
                if(app.Engine.Attempt.phase!=SessionPhase.Assessment)
                    Button(content,"Show me this step",()=>{app.RevealHint();app.Demonstrator.Play();app.Narrator.Replay();});
                finishButton=Button(content,app.Engine.Attempt.phase==SessionPhase.Assessment?"Finish assessment":"Finish activity",app.Finish,true);
                selectedLabel=Text(content,"",21,Muted);
                Button(content,"Tools and more help",ShowActivityTools);
            }
            else
            {
                Text(content,"Choose a layer to inspect. Grip tissue to move it, or use the cutting tool to open it.",26,Muted);
                selectedLabel=Text(content,"",23,Muted);notice=Text(content,app.Notice,22,Accent);
                Button(content,"Tools and layers",ShowActivityTools,true);
            }
            float captionHeight=112*app.Settings.Current.captionScale;
            captionPanel=Panel(screen,"Narration captions",Surface).rectTransform;
            captionPanel.anchorMin=Vector2.zero;captionPanel.anchorMax=new Vector2(1,0);captionPanel.pivot=new Vector2(.5f,0);
            captionPanel.sizeDelta=new Vector2(-28,captionHeight);captionPanel.anchoredPosition=new Vector2(0,footer.sizeDelta.y+8);
            caption=Text(captionPanel,"",22,Ink);Fill(caption.rectTransform,16);
            if(app.Settings.Current.captions)rail.offsetMin=new Vector2(10,footer.sizeDelta.y+captionHeight+20);
            RefreshLive();
        }
        void ShowActivityTools()
        {
            Page("Activity tools","Tools and help");
            Button(content,"Return to your step",ShowWorkspace,true);
            bool assessment=app.Engine!=null&&app.Engine.Attempt.phase==SessionPhase.Assessment;
            if(!assessment&&app.Engine!=null)
            {
                Button(content,"Read this lesson",ShowTranscript);
                Button(content,"Pause / resume narration",app.Narrator.TogglePause);
            }
            if(app.Sandbox||app.Variant?.mode==LearningMode.GuidedDissection)
            {
                Button(content,app.Session.saw?"Using bone saw · switch to scalpel":"Using scalpel · switch to bone saw",()=>{app.Session.saw=!app.Session.saw;ShowActivityTools();});
                Button(content,"Undo last cut",app.Session.Undo);
            }
            if(!assessment){Button(content,"Isolate selected anatomy",()=>{app.Session.Isolate();ShowWorkspace();});Button(content,"Find a structure",ShowBrowse);}
            Button(content,"Restore activity position",()=>{app.RestoreActivity();ShowWorkspace();});
            if(app.Engine!=null)Button(content,"Finish this attempt early",()=>Confirm("Finish with incomplete steps?","Missing essential steps prevent a pass. You can practise and retry.","Finish attempt",app.Finish));
            Text(content,"Grip to hold and rotate. Trigger to select or use a tool. Release gently at the guide.",22,Muted);
        }
        void ShowTranscript()
        {
            Page("Teaching",app.Unit.title);Text(content,app.Unit.lesson,24);
            if(app.Engine.Attempt.phase!=SessionPhase.Assessment)foreach(var o in app.Variant.objectives){Text(content,o.title,25,Accent);Text(content,o.instruction+" "+o.explanation,22);}
            else Text(content,"Assessment targets follow your declared rubric. Answer-revealing demonstrations are available in practice.",22,Muted);
            Button(content,"Return to specimen",ShowWorkspace,true);
        }
        void ShowBrowse()
        {
            Page("Browse","Anatomy collection");var search=Field("Find a structure");var results=Column(content);
            Action update=()=>{foreach(Transform child in results)Destroy(child.gameObject);foreach(var s in app.Session.originals.Where(s=>!s.record.reference_region&&(string.IsNullOrEmpty(search.text)||s.record.name.IndexOf(search.text,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(s=>s.record.name).Take(60))
                Button(results,s.record.name,()=>{app.Session.Inspect(s);ShowWorkspace();});};search.onValueChanged.AddListener(_=>update());update();Button(content,"Return to specimen",ShowWorkspace);
        }
        public void ShowResults()
        {
            var attempt=app.Engine.Attempt;var variant=app.Variant;
            Page("Results",attempt.passed?"Step by step, you got there":"Let's try that again");
            Text(content,attempt.score.ToString("0")+"% · "+(attempt.passed?"Completed":"Keep practising"),30,Accent);
            if(attempt.passed&&attempt.phase==SessionPhase.Assessment&&attempt.difficulty!=Difficulty.Hard)
                Text(content,(attempt.difficulty==Difficulty.Easy?"Medium":"Hard")+" is now open for this activity.",24);
            Text(content,app.Variant.debrief,22,Muted);
            if(attempt.passed&&attempt.phase==SessionPhase.Lesson)Button(content,"Try it with practice guides",()=>app.StartActivity(variant,SessionPhase.Practice),true);
            else if(attempt.passed&&attempt.phase==SessionPhase.Practice)Button(content,"Take the "+variant.difficulty+" assessment",()=>app.StartActivity(variant,SessionPhase.Assessment),true);
            else if(!attempt.passed)Button(content,"Practise this step again",()=>app.StartActivity(variant,SessionPhase.Practice),true);
            else Button(content,"Choose your next activity",()=>Leave(ShowModes),true);
            Button(content,"Review your steps",ShowAttemptFeedback);Button(content,"Return to lab",()=>Leave(ShowHub));
        }
        void ShowAttemptFeedback()
        {
            Page("Feedback","Your recorded steps");
            foreach(var objective in app.Variant.objectives){bool complete=app.Engine.IsComplete(objective.id);Text(content,(complete?"Completed · ":"Revise · ")+objective.title,25,complete?Ink:Accent);if(!complete)Text(content,objective.explanation,22,Muted);}
            Text(content,"Recorded errors: "+app.Engine.Attempt.mistakes+(app.Engine.Attempt.criticalFailure?". A protected structure or essential procedure step needs another attempt.":"."),22,Muted);
            Button(content,"Back to results",ShowResults,true);
        }
        public void ShowProgress()
        {
            Page("Progress","Your learning record");Text(content,ProgressRules.CareerChapter(app.Progress),28,Accent);
            Text(content,app.Progress.rewards.Sum(r=>r.xp)+" XP earned across "+app.Progress.rewards.Count+" mastered objectives. Each objective rewards once per difficulty.",23,Muted);
            Text(content,"Career titles describe your in-game learning progress. They are not qualifications.",20,Muted);Rule(content);
            if(app.Progress.attempts.Count==0)Text(content,"Complete an activity to start your record.",24);
            foreach(var attempt in app.Progress.attempts.OrderByDescending(a=>a.startedUtc).Take(80))
            {var unit=app.Catalog.units.FirstOrDefault(u=>u.id==attempt.unitId);Text(content,(unit?.title??attempt.unitId)+" · "+attempt.difficulty,23);Text(content,attempt.phase+" · "+attempt.status+" · "+attempt.score.ToString("0")+"% · "+(attempt.passed?"Passed":"In progress or revision")+"\n"+attempt.startedUtc,19,Muted);Rule(content);}
            Button(content,"Export progress",()=>app.Message("Exported to "+app.ExportProgress()));Button(content,"Sync now",()=>Run(app.Synchronize));ErrorArea();
        }
        void ErrorArea(){notice=Text(content,app.Notice,21,Accent);}
        TMP_InputField Field(string label,string value="",bool secret=false)
        {var f=Input(content,label,value,secret);f.onSelect.AddListener(_=>{if(xrLayout)OpenKeyboard(f);});return f;}
        async void Run(Func<Task> action){try{await action();RefreshLive();}catch(Exception ex){app.Message(ex.Message);RefreshLive();}}
        public void Confirm(string title,string description,string action,Action accepted)
        {
            if(modal)Destroy(modal.gameObject);modal=ModalSurface("Confirmation");Fill(modal,8);var c=Scroll(modal);Text(c,title,36);Text(c,description,24);Button(c,action,()=>{Destroy(modal.gameObject);modal=null;accepted();},true);Button(c,"Cancel",()=>{Destroy(modal.gameObject);modal=null;});
        }
        public void RefreshLive()
        {
            if(status)status.text=(!app.XRReady?"Tracking paused":app.XRSimulation?"XR simulator":app.Preview?"Guest · this session only":app.SyncStatus)+(app.Settings.Current.showFps?"    |    "+LearningPerformanceMonitor.Display:"");
            if(notice)notice.text=currentScreen=="Workspace"&&app.Engine!=null?app.Notice:app.OrientationActive&&app.Notice==app.CurrentInstruction?"":app.Notice;
            if(objectiveStatus&&app.OrientationActive){objectiveStatus.text=app.CurrentInstruction;if(stepLabel)stepLabel.text=app.OrientationStep==0?"First, select the skin":"Now, hold and release";}
            if(objectiveStatus&&app.Engine!=null)
            {
                var current=app.Engine.Current;bool assessment=app.Engine.Attempt.phase==SessionPhase.Assessment;
                int completed=app.Engine.Attempt.objectives.Count(o=>o.completed),total=app.Variant.objectives.Length;
                if(stepLabel)stepLabel.text=app.Variant.difficulty+" · "+(assessment?"Assessment":"Guided "+(app.Engine.Attempt.phase==SessionPhase.Lesson?"lesson":"practice"))+" · "+(current==null?"All steps complete":"Step "+(completed+1)+" of "+total);
                objectiveStatus.text=current==null?"You have completed every step. Finish when you are ready.":current.title+(assessment?"":"\n\n"+app.CurrentInstruction);
                if(progressFill)progressFill.rectTransform.anchorMax=new Vector2(total>0?(float)completed/total:0,1);
                if(finishButton){finishButton.interactable=current==null;finishButton.gameObject.SetActive(current==null);}
            }
            if(windowLabel){windowLabel.text=app.TeachingWindowLabel??"";windowLabel.gameObject.SetActive(!string.IsNullOrEmpty(windowLabel.text));}
            if(selectedLabel)selectedLabel.text=app.Session.selected&&app.Settings.Current.labels?"Selected: "+app.Session.selected.record.name:"";
            if(caption){caption.text=app.Settings.Current.captions?app.Narrator.Caption:"";caption.fontSize=20*app.Settings.Current.captionScale;captionPanel.gameObject.SetActive(!string.IsNullOrEmpty(caption.text));}
        }
        void Update()
        {
            if(currentScreen=="Headset"&&app.XRReady)ShowSignIn();
            if(Time.frameCount%15==0)RefreshLive();
        }
        void SetXRLayout()
        {
            canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;screen.sizeDelta=new Vector2(700,860);screen.localScale=Vector3.one*.00085f;
            Camera.main.rect=new Rect(0,0,1,1);
            screen.position=Camera.main.transform.position+Camera.main.transform.forward*1.05f-Camera.main.transform.right*.38f;
            screen.rotation=Quaternion.LookRotation(screen.position-Camera.main.transform.position,Vector3.up);
        }
        public void RecenterPanel(){if(xrLayout)SetXRLayout();}
        public bool RayPointer(Ray ray,bool press,Vector2 scroll=default,bool grip=false,bool held=false,int pointerId=1)
        {
            if(!xrLayout)return false;
            if(!xrPointers.TryGetValue(pointerId,out var pointer)){pointer=new XRPointer{data=new PointerEventData(EventSystem.current){pointerId=pointerId,button=PointerEventData.InputButton.Left,useDragThreshold=false}};xrPointers[pointerId]=pointer;}
            var plane=new Plane(screen.forward,screen.position);if(!plane.Raycast(ray,out float d)||d>3){ReleasePointer(pointer,null);return false;}
            var point=(Vector2)Camera.main.WorldToScreenPoint(ray.GetPoint(d));var data=pointer.data;data.delta=point-data.position;data.position=point;
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);var result=hits.FirstOrDefault(r=>r.gameObject&&r.gameObject.transform.IsChildOf(screen));data.pointerCurrentRaycast=result;
            if(pointer.hover!=result.gameObject){if(pointer.hover)ExecuteEvents.ExecuteHierarchy(pointer.hover,data,ExecuteEvents.pointerExitHandler);pointer.hover=result.gameObject;if(pointer.hover)ExecuteEvents.ExecuteHierarchy(pointer.hover,data,ExecuteEvents.pointerEnterHandler);}
            if(!result.gameObject){ReleasePointer(pointer,null);return false;}
            xrCaptureUntil=Time.unscaledTime+.15f;
            if(Mathf.Abs(scroll.y)>.2f){data.scrollDelta=new Vector2(0,scroll.y*.8f);ExecuteEvents.ExecuteHierarchy(result.gameObject,data,ExecuteEvents.scrollHandler);}
            var local=screen.InverseTransformPoint(ray.GetPoint(d));
            if(grip&&(panelDragging||local.y>screen.rect.yMax-100)){var current=ray.GetPoint(.9f);if(panelDragging)screen.position+=current-previousPanelPoint;previousPanelPoint=current;panelDragging=true;}else panelDragging=false;
            if(press)
            {
                data.pressPosition=point;data.pointerPressRaycast=result;data.eligibleForClick=true;
                pointer.pressed=ExecuteEvents.ExecuteHierarchy(result.gameObject,data,ExecuteEvents.pointerDownHandler)??ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
                data.pointerPress=pointer.pressed;pointer.drag=ExecuteEvents.GetEventHandler<IDragHandler>(result.gameObject);data.pointerDrag=pointer.drag;
                if(pointer.drag)ExecuteEvents.Execute(pointer.drag,data,ExecuteEvents.initializePotentialDrag);
                var selectable=result.gameObject.GetComponentInParent<Selectable>();if(selectable)selectable.Select();
            }
            if(!press&&held&&pointer.drag&&data.delta.sqrMagnitude>.01f)
            {
                if(!pointer.dragging){ExecuteEvents.Execute(pointer.drag,data,ExecuteEvents.beginDragHandler);pointer.dragging=true;data.dragging=true;}
                ExecuteEvents.Execute(pointer.drag,data,ExecuteEvents.dragHandler);
            }
            if(!held)ReleasePointer(pointer,result.gameObject);return true;
        }
        void ReleasePointer(XRPointer pointer,GameObject hit)
        {
            if(pointer.pressed)ExecuteEvents.Execute(pointer.pressed,pointer.data,ExecuteEvents.pointerUpHandler);
            if(pointer.pressed&&!pointer.dragging&&hit&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)==pointer.pressed)ExecuteEvents.Execute(pointer.pressed,pointer.data,ExecuteEvents.pointerClickHandler);
            if(pointer.dragging&&pointer.drag)ExecuteEvents.Execute(pointer.drag,pointer.data,ExecuteEvents.endDragHandler);
            pointer.pressed=pointer.drag=null;pointer.dragging=false;pointer.data.dragging=false;pointer.data.pointerPress=pointer.data.pointerDrag=null;
        }
        public void CancelXRPointer(int pointerId){if(xrPointers.TryGetValue(pointerId,out var pointer))ReleasePointer(pointer,null);}
        public void EnableXRLayoutForQA(){if(!Environment.GetCommandLineArgs().Contains("--learning-qa"))return;xrLayout=true;SetXRLayout();RenderSettingsPage();}
        public bool PointerOverUI => EventSystem.current&&EventSystem.current.IsPointerOverGameObject();
        static string ModeName(LearningMode mode)=>new[]{"Anatomy Cases","Identify & Enumerate","Assemble Anatomy","Guided Dissection"}[(int)mode];
        RectTransform ModalSurface(string title)
        {
            // Opaque and slightly in front of the page: world-space TMP must not bleed through a modal.
            var surface=Panel(screen,title,new Color(Ground.r,Ground.g,Ground.b,1)).rectTransform;
            surface.localPosition=new Vector3(0,0,-6);return surface;
        }
        void OpenKeyboard(TMP_InputField field)
        {
            if(modal)return;modal=ModalSurface("VR keyboard");Fill(modal,8);
            var keys=Scroll(modal);Text(keys,field.name,28);
            var preview=Text(keys,"",24,Accent);bool upper=false;var keyLabels=new System.Collections.Generic.Dictionary<char,TMP_Text>();
            void Refresh(){preview.text=field.contentType==TMP_InputField.ContentType.Password?new string('•',field.text.Length):string.IsNullOrEmpty(field.text)?"Your text will appear here":field.text;}
            void Append(string value){if(field.characterLimit<=0||field.text.Length+value.Length<=field.characterLimit){field.text+=value;Refresh();}}
            Refresh();
            foreach(var line in new[]{"1234567890","qwertyuiop","asdfghjkl","zxcvbnm@.-_+"})
            {
                var row=Row(keys,spacing:3);
                foreach(char key in line){char value=key;var button=Button(row,key.ToString(),()=>Append((upper?char.ToUpperInvariant(value):value).ToString()));button.GetComponent<HorizontalLayoutGroup>().padding=new RectOffset(5,5,12,12);keyLabels[key]=button.GetComponentInChildren<TMP_Text>();}
            }
            var editing=Row(keys);Button shift=null;shift=Button(editing,"Shift off",()=>{upper=!upper;shift.GetComponentInChildren<TMP_Text>().text=upper?"Shift on":"Shift off";foreach(var pair in keyLabels)pair.Value.text=(upper?char.ToUpperInvariant(pair.Key):pair.Key).ToString();});Button(editing,"Space",()=>Append(" "));Button(editing,"Delete",()=>{if(field.text.Length>0){field.text=field.text.Substring(0,field.text.Length-1);Refresh();}});
            Button(keys,"Done",()=>{Destroy(modal.gameObject);modal=null;},true);
        }
    }
}
