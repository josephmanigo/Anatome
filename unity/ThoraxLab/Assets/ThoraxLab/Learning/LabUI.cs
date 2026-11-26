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
using UnityEngine.XR.Interaction.Toolkit.UI;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI : MonoBehaviour
    {
        internal const int ConsoleSortingOrder = 20;
        LabRuntime app;
        Canvas canvas;
        RectTransform screen,body,content,rail,footer,modal,captionPanel;
        TextMeshProUGUI status,objectiveStatus,objectiveHeading,notice,caption,selectedLabel,stepLabel,windowLabel;
        Button finishButton;
        Image progressFill;
        LearningMode chosenMode;
        LearningUnit chosenUnit;
        Difficulty chosenDifficulty;
        SettingsProfile draft;
        string currentScreen,settingCategory="Quick setup",email="";
        Action previousScreen,modalBack;
        bool xrLayout,runningAction;
        float xrCaptureUntil;
        int panelDragPointer=-1;
        Vector3 panelGrabOffset;
        Quaternion panelGrabRotation;
        sealed class XRPointer {public TrackedDeviceEventData data;public GameObject pressed,drag,hover;public bool dragging,captured,gripHeld;public Vector2 pressLocal;public Vector3 pressWorld;public readonly System.Collections.Generic.List<RaycastResult> hits=new();}
        readonly System.Collections.Generic.Dictionary<int,XRPointer> xrPointers=new System.Collections.Generic.Dictionary<int,XRPointer>();
        public bool XRPanelCaptured => Time.unscaledTime<xrCaptureUntil;
        public bool WorkspaceVisible => currentScreen=="Workspace" && !modal;
        public string ScreenName => currentScreen;
        internal float PreviewReadingDim => (draft != null && (currentScreen == "Settings" || currentScreen == "Settings categories")
            ? draft : app.Settings.Current).ReadingDimAmount;
        public RectTransform Root => screen;
        public bool HasModal => modal;
        public void Initialize(LabRuntime runtime)
        {
            app=runtime;LabWidgets.Font=Resources.Load<TMP_FontAsset>("Fonts/SourceSans3-SDF");
            if(!LabWidgets.Font){var source=Resources.Load<UnityEngine.Font>("Fonts/SourceSans3-Regular");if(source)LabWidgets.Font=TMP_FontAsset.CreateFontAsset(source);}
            if(!LabWidgets.Font)throw new InvalidOperationException("Source Sans 3 is missing from the installation.");
            if(!EventSystem.current){var ev=new GameObject("Learning event system");ev.AddComponent<EventSystem>();ev.AddComponent<InputSystemUIInputModule>();}
            foreach(var module in EventSystem.current.GetComponents<BaseInputModule>())module.enabled=false;
            screen=Rect(LearningBrand.Name+" interface",transform);canvas=screen.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=ConsoleSortingOrder;screen.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();xrLayout=true;
            var scaler=screen.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,1000);scaler.matchWidthOrHeight=.5f;
            app.Changed+=RefreshLive;
            app.Settings.Changed+=ApplyReadingStyle;
            ApplyReadingStyle();
            ShowSignIn();
        }
        void ApplyReadingStyle()
        {
            var settings=app.Settings.Current;
            TextScale=settings.EffectiveTextScale;
            HighContrast=settings.highContrast;
            AstigmatismReadingMode=settings.astigmatismReadingMode;
        }
        void Page(string name,string title,bool workspace=false,Action back=null,string backName="Back")
        {
            if(name!="Workspace"&&app.Inspector)app.Inspector.Close();
            if(currentScreen=="Sign in"&&name!="Sign in"&&welcomeSpoken)app.Narrator.Stop();
            inspectionZoom=null;inspectionButtons=null;inspectionToggle=null;
            museumSelection=null;
            CloseGuide();
            foreach(var pointer in xrPointers.Values){ReleasePointer(pointer,null);Hover(pointer,null);}panelDragPointer=-1;
            DismissModal();
            foreach(Transform child in screen){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            currentScreen=name;objectiveStatus=objectiveHeading=notice=caption=selectedLabel=stepLabel=windowLabel=null;captionPanel=null;finishButton=null;progressFill=null;
            Camera.main.rect=new Rect(0,0,1,1);
            var edge=LabUIVisuals.Surface(screen,"Glass thickness",new Color(.12f,.25f,.31f));edge.raycastTarget=false;Fill(edge.rectTransform);edge.rectTransform.anchoredPosition=new Vector2(5,-7);edge.rectTransform.localPosition+=Vector3.forward*7;LabUIVisuals.Border(edge.rectTransform);
            var glassColor=LabUIVisuals.ConsoleColor(app.Settings.Current);
            var backdrop=LabUIVisuals.Surface(screen,"VR console surface",glassColor);backdrop.raycastTarget=false;Fill(backdrop.rectTransform);LabUIVisuals.Border(backdrop.rectTransform,new Color(.71f,.89f,.96f,.8f));
            float brandHeight=Mathf.Max(58*TextScale-18,back!=null?BackHeight:0);
            float headerHeight=ControlHeight+brandHeight+52,footerHeight=Mathf.Max(84,60*TextScale+40);
            var tr=Rect("Top navigation",screen);tr.anchorMin=new Vector2(0,1);tr.anchorMax=Vector2.one;tr.pivot=new Vector2(.5f,1);tr.sizeDelta=new Vector2(0,headerHeight);tr.anchoredPosition=Vector2.zero;
            tr.gameObject.AddComponent<Image>().color=Color.clear;
            var header=Column(tr,"Console navigation",20,12);Fill(header);
            var brandRow=NavigationRow(header,"Panel grip");Size(brandRow,brandHeight);
            if(back!=null)BackButton(brandRow,back,backName);
            LabUIVisuals.Icon(brandRow,LabGlyph.Layers,Accent,30);
            var brand=Text(brandRow,LearningBrand.Name,28);brand.fontStyle=FontStyles.Bold;Size(brand,flexible:1).minWidth=0;
            var grip=Text(brandRow,"Grip to move",22,Muted);grip.alignment=TextAlignmentOptions.MidlineRight;Size(grip,flexible:1).minWidth=0;
            var row=Row(header,"Navigation buttons",12);Size(row,ControlHeight);
            Button(row,"Guide",ShowGuide);Button(row,workspace?"Pause":"Menu",TogglePauseMenu);
            if(app.Account.UserId!=null)Button(row,"Log out",app.LogOut);
            rail=Rect("Learning content",screen);rail.anchorMin=Vector2.zero;rail.anchorMax=Vector2.one;rail.offsetMin=new Vector2(10,footerHeight+8);rail.offsetMax=new Vector2(-10,-headerHeight-8);
            body=Rect("Body",rail);Fill(body);content=Scroll(body);
            Text(content,title,workspace?31:40).fontStyle=FontStyles.Bold;
            footer=Rect("Save status",screen);footer.anchorMin=Vector2.zero;footer.anchorMax=new Vector2(1,0);footer.pivot=new Vector2(.5f,0);footer.sizeDelta=new Vector2(0,footerHeight);footer.anchoredPosition=Vector2.zero;
            var statusLine=Panel(footer,"Status separator",new Color(.18f,.25f,.29f));statusLine.raycastTarget=false;statusLine.rectTransform.anchorMin=new Vector2(0,1);statusLine.rectTransform.anchorMax=Vector2.one;statusLine.rectTransform.sizeDelta=new Vector2(-44,1);
            status=Text(footer,"",18,Muted);Fill(status.rectTransform,20);RefreshLive();
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
            if(modal){modalBack?.Invoke();return;}
            if(currentScreen=="Menu")ReturnFromMenu();
            else ShowPauseMenu();
        }
        bool InActivity => app.Engine!=null||app.Sandbox||app.OrientationActive||app.MuseumActive;
        void ReturnFromMenu()
        {
            if(InActivity)ShowWorkspace();
            else if(app.Account.UserId!=null||app.Preview)ShowHub();
            else ShowSignIn();
        }
        void ShowPauseMenu()
        {
            bool working=InActivity;
            Page("Menu",working?"Take your time":"Your lab");
            if(app.MuseumActive)RecenterPanel();
            Button(content,working?"Return to activity":app.Account.UserId!=null||app.Preview?"Return to lab":"Return to sign-in",ReturnFromMenu,true);
            Button(content,"Settings",()=>{previousScreen=ReturnFromMenu;ShowSettings();});
            Button(content,"How to play & unlock levels",()=>ShowLearningGuidelines(ShowPauseMenu));
            if(app.Account.UserId!=null||app.Preview)Button(content,"Your progress",()=>Leave(ShowProgress));
            Button(content,"Move panel in front of me",RecenterPanel);
            if(working)Button(content,app.MuseumActive?"Leave museum":"Save step and leave activity",()=>Leave(ShowHub));
            Text(content,"Point to a button and squeeze the trigger. Hold grip on the top bar to move the panel; turn your wrist to rotate it. Release grip to leave it in place.",21,Muted);
        }
        void Leave(Action destination)
        {
            if(app.Session.busy){app.Message("Wait for the current cut to finish.");return;}
            app.Interrupt();destination();
        }
        public void ShowSignIn()
        {
            Page("Sign in","Step into the anatomy lab",back:app.Preview?(Action)ShowHub:null,backName:"Back to lab");
            Text(content,AstigmatismReadingMode
                ? "Astigmatism reading mode is on. You can adjust it in VR comfort & text size."
                : "If you have astigmatism, try Astigmatism reading mode for softer lighting and larger text. Turn it on in VR comfort & text size.",25);
            Button(content,"VR comfort & text size",()=>OpenAccessibilitySetup(ShowSignIn));
            Button(content,"Before you begin",()=>ShowLearningGuidelines(ShowSignIn));
            Text(content,"Sign in with an email code to save your progress.",23,Muted);
            Text(content,"Email address",23);
            var field=Field("Email address",email);field.contentType=TMP_InputField.ContentType.EmailAddress;
            field.onValueChanged.AddListener(value=>email=value);
            Text(content,"Select the field to open the VR keyboard.",20,Muted);
            ErrorArea();
            Button send=null;send=Button(content,"Send email code",()=>Run(async()=>{
                string address=field.text.Trim();email=address;send.interactable=false;send.GetComponentInChildren<TMP_Text>().text="Sending code…";
                try{await app.Account.SendCode(address);if(currentScreen=="Sign in"&&field&&email==address)ShowCode();}
                finally{if(send){send.interactable=true;send.GetComponentInChildren<TMP_Text>().text="Send email code";}}
            }),true);
            Rule(content);
            Button(content,"Try your first VR lesson",app.EnterPreview);
            Text(content,"Guest practice stays on Easy until you pass. It is not saved after this session.",20,Muted);
            Button(content,"Privacy and shared headsets",()=>ShowPrivacy(ShowSignIn));
            foreach(var account in app.Account.Trusted.Accounts())
            {
                var accountCopy=account;Button(content,"Offline access · "+account.email,()=>ShowOffline(accountCopy.userId,accountCopy.email));
            }
        }
        void ShowCode()
        {
            Page("Email code","Check your email",back:ShowSignIn,backName:"Back to email");Text(content,"Enter the six-digit code sent to "+email+".",23,Muted);
            var code=Field("Six-digit code");code.characterLimit=6;code.characterValidation=TMP_InputField.CharacterValidation.Digit;
            Button(content,"Verify and continue",()=>Run(()=>app.Account.VerifyCode(email,code.text)),true);
            Button(content,"Resend code",()=>Run(async()=>{await app.Account.SendCode(email);app.Message("A new code has been sent.");}));
            Text(content,"Codes expire after ten minutes. Wait sixty seconds before requesting another. Check spam if it hasn’t arrived.",21,Muted);
            ErrorArea();
        }
        void ShowPrivacy(Action back)
        {
            Page("Privacy","Your learning data",back:back);
            Text(content,"Guest practice needs no account. Progress lasts only for that session.",23,Muted);
            Text(content,"Email sign-in stores your email with the account service. Learning attempts, results, safe checkpoints and account settings are saved on this device and synchronized to your account when online.",23);
            Text(content,"A device PIN enables offline access only on this headset. Trusted account email addresses appear on its sign-in screen. On a shared headset, use offline access only with your school's permission and keep your PIN private.",23);
            Text(content,"Before handing over a shared headset, finish or pause your activity, sync saved progress and sign out in Settings > Account & saves. Remove offline access there if you no longer use this headset.",23);
            Text(content,"Account & saves also lets you export your learning record, reset progress or delete your account. Account deletion removes cloud records and this app's account files on the current device; other devices and exported copies must be cleared separately.",23);
            Text(content,"This app teaches anatomy using simplified fictional cases. Results are learning feedback and do not certify clinical competence. Ask your instructor about classroom use and school data handling.",21,Muted);
            Text(content,"App "+Application.version+" · Curriculum "+app.Catalog.version,18,Muted);
        }
        void ShowOffline(string id,string address)
        {
            Page("Offline access","Welcome back",back:ShowSignIn,backName:"Back to email");Text(content,address,23,Muted);var pin=Field("Device PIN","",true);pin.characterLimit=6;pin.characterValidation=TMP_InputField.CharacterValidation.Digit;
            Button(content,"Unlock saved progress",()=>Run(()=>{app.Account.UnlockOffline(id,pin.text);return Task.CompletedTask;}),true);
            Text(content,"Forgot your PIN? Sign in online with a fresh email code to replace it.",21,Muted);Button(content,"Email sign-in",ShowSignIn);ErrorArea();
        }
        public void ShowHub()
        {
            if(app.Account.UserId==null&&!app.Preview){ShowSignIn();return;}
            if(!app.Session.busy){app.Interrupt();app.Session.SetMode(LabMode.Explore);if(app.Presentation)app.Presentation.ShowIntact();}
            Page("Career",app.Progress.orientationCompleted?"What will you discover next?":"Your operating room");
            if(!app.Progress.orientationCompleted)
            {
                LearningGuidelines(content);
                Button(content,"VR comfort & text size",()=>OpenAccessibilitySetup(ShowHub));
                Button(content,"Learn the VR controls",ShowOrientation,true);
                Text(content,"Point and trigger-select the skin. Grip, lift slightly, then release. These three actions open your first Easy activity.",24,Muted);
            }
            else
            {
                var next=app.Catalog.units.First(u=>u.id=="chest-wall");
                Text(content,"Begin with the chest wall, then explore at your own pace.",25,Muted);
                bool canContinue=app.Progress.attempts.Any(a=>a.status==AttemptStatus.Active);
                if(canContinue)Button(content,"Continue your activity",app.ResumeLast,true);
                else
                {
                    var first=next.variants.First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy);
                    bool finished=ProgressRules.HasCompletedActivity(app.Progress,first,SessionPhase.Lesson);
                    LabUIVisuals.Card(content,"Chest wall","Build your first map of the thorax.","chest-wall",()=>{chosenMode=first.mode;chosenDifficulty=first.difficulty;app.StartActivity(first,SessionPhase.Lesson);},"Start an Easy lesson",finished?"Easy lesson completed":"Start an Easy lesson",completed:finished);
                }
                Button(content,"Choose an activity",ShowModes,!canContinue);
                Text(content,"Medium opens after you pass Easy. Hard opens after Medium.",22,Muted);
                Button(content,"How to play & unlock levels",()=>ShowLearningGuidelines(ShowHub));
                Button(content,"VR comfort & text size",()=>OpenAccessibilitySetup(ShowHub));
            }
            Button(content,"Visit the Thorax Museum",app.StartMuseum);
            if(app.Preview)Button(content,"Sign in to save progress",()=>{app.Interrupt();ShowSignIn();});
            ErrorArea();
        }
        void ShowModes()
        {
            Page("Modes","Choose how to learn",back:ShowHub,backName:"Back to lab");
            Mode("Identify & Enumerate","Locate anatomy in 3D and mark each structure.",LearningMode.IdentifyEnumerate);
            Mode("Assemble Anatomy","Grip, rotate and reconstruct anatomical relationships.",LearningMode.AssembleAnatomy);
            Mode("Guided Dissection","Cut and reflect layers while preserving protected anatomy.",LearningMode.GuidedDissection);
            Mode("Anatomy Cases","Explain mechanisms, perform procedures and predict responses.",LearningMode.AnatomyCases);
            LabUIVisuals.Card(content,"Thorax Museum","Explore every thorax part in labeled system galleries. Grab and enlarge exhibits to examine their shape.","chest-wall",app.StartMuseum,"Visit the Thorax Museum");
        }
        void Mode(string title,string description,LearningMode mode)
        { LabUIVisuals.Card(content,title,description,ModeArt(mode),()=>ShowUnits(mode),title); }
        static string ModeArt(LearningMode mode)=>new[]{"mode-cases","mode-identify","mode-assemble","mode-dissect"}[(int)mode];
        void ShowOrientation()
        {
            app.StartOrientation();
        }
        public void ShowUnits(LearningMode mode)
        {
            chosenMode=mode;chosenDifficulty=Difficulty.Easy;Page("Units",ModeName(mode),back:ShowModes,backName:"Back to activities");Text(content,"Choose a unit. Start on Easy.",23,Muted);
            foreach(var unit in app.Catalog.units.Where(u=>u.isCase==(mode==LearningMode.AnatomyCases)))
            {
                var selected=unit;var easy=unit.variants.First(v=>v.mode==mode&&v.difficulty==Difficulty.Easy);
                var best=app.Progress.attempts.Where(a=>a.unitId==unit.id&&a.mode==mode&&a.contentVersion==easy.version&&a.phase==SessionPhase.Assessment&&a.passed).OrderByDescending(a=>a.difficulty).FirstOrDefault();
                var finished=unit.variants.Where(v=>v.mode==mode).OrderByDescending(v=>v.difficulty).FirstOrDefault(v=>ProgressRules.HasCompletedActivity(app.Progress,v));
                string status=best!=null?best.difficulty+" mastered":finished!=null?finished.difficulty+" activity completed":"Begin on Easy";
                LabUIVisuals.Card(content,unit.title,unit.summary,unit.id,()=>ShowUnit(selected),unit.title,status,completed:finished!=null);
            }
            if(mode==LearningMode.GuidedDissection){Rule(content);Button(content,"Free dissection sandbox",app.StartSandbox);}
        }
        public void ShowUnit(LearningUnit unit)
        {
            chosenUnit=unit;Page("Unit",unit.title,back:()=>ShowUnits(chosenMode),backName:"Back to units");LabUIVisuals.Hero(content,unit.id,158);Text(content,unit.summary,24,Muted);
            var variant=unit.variants.First(v=>v.mode==chosenMode&&v.difficulty==chosenDifficulty);
            if(!app.Unlocked(variant)){chosenDifficulty=Difficulty.Easy;variant=unit.variants.First(v=>v.mode==chosenMode&&v.difficulty==chosenDifficulty);}
            var selected=variant;Text(content,chosenDifficulty+" · "+variant.objectives.Length+" hands-on steps",25,Accent);
            Text(content,"Watch a step, try it, then check your understanding.",23,Muted);
            Button(content,"Learn step by step",()=>app.StartActivity(selected,SessionPhase.Lesson),true,app.Unlocked(selected),ProgressRules.HasCompletedActivity(app.Progress,selected,SessionPhase.Lesson));
            Button(content,"Practice or assessment",()=>ShowPhaseChoice(selected),false,app.Unlocked(selected));
            Button(content,"Change difficulty",ShowDifficultyChoice);
        }
        void ShowDifficultyChoice()
        {
            Page("Difficulty",chosenUnit.title,back:()=>ShowUnit(chosenUnit));
            foreach(Difficulty level in Enum.GetValues(typeof(Difficulty)))
            {
                var variant=chosenUnit.variants.First(v=>v.mode==chosenMode&&v.difficulty==level);bool unlocked=app.Unlocked(variant);var selected=level;
                string guidance=unlocked?(level==Difficulty.Easy?"One step at a time with clear guides.":level==Difficulty.Medium?"More independence and anatomical relationships.":"Precise work and more complex decisions."):(level==Difficulty.Easy?"Complete the VR introduction first.":"Pass the "+(level==Difficulty.Medium?"Easy":"Medium")+" assessment in this unit and mode first.");
                bool finished=unlocked&&ProgressRules.HasCompletedActivity(app.Progress,variant);
                string status=!unlocked?"Locked":finished?"Activity completed":level==chosenDifficulty?"Selected":"Available";
                LabUIVisuals.Card(content,level.ToString(),guidance,null,()=>{chosenDifficulty=selected;ShowUnit(chosenUnit);},level+(unlocked?"":" · locked"),status,unlocked,unlocked?LabGlyph.Layers:LabGlyph.Lock,finished);
            }
            Text(content,AssessmentRequirements,24,Muted);Text(content,"Lessons, practice and XP do not unlock levels. No time limit; unlimited retries.",22,Muted);
        }
        void ShowPhaseChoice(ScenarioVariant variant)
        {
            Page("Learning phase",chosenUnit.title+" · "+variant.difficulty,back:()=>ShowUnit(chosenUnit));
            LabUIVisuals.Card(content,"Practice with guidance","Replay demonstrations and use placement guides.",null,()=>app.StartActivity(variant,SessionPhase.Practice),symbol:LabGlyph.Play,completed:ProgressRules.HasCompletedActivity(app.Progress,variant,SessionPhase.Practice));
            LabUIVisuals.Card(content,"Take the assessment",AssessmentRequirements+" Unlimited retries.",null,()=>app.StartActivity(variant,SessionPhase.Assessment),symbol:LabGlyph.Check,completed:ProgressRules.HasCompletedActivity(app.Progress,variant,SessionPhase.Assessment));
            Button(content,"Read the anatomy and sources",()=>ShowReferences(chosenUnit));
        }
        void ShowReferences(LearningUnit unit)
        {
            Page("References",unit.title+" · references",back:ReferenceDestination(unit),backName:app.Engine!=null?"Return to activity":"Back to unit");Text(content,unit.limitations,22,Muted);
            foreach(var id in unit.sourceIds){var source=app.Catalog.sources.First(s=>s.id==id);Text(content,source.title,24);Text(content,source.usage,21,Muted);Button(content,"Read source details",()=>ShowSourceDetails(unit,source));}
        }
        public void ShowWorkspace()
        {
            Page("Workspace",app.MuseumActive?"Thorax Museum":app.OrientationActive?"Learn the VR controls":app.Sandbox?"Dissection sandbox":app.Unit?.title??"Anatomy workspace",true);
            if(app.MuseumActive){MuseumWorkspace();RecenterPanel();return;}
            if(app.OrientationActive)
            {
                stepLabel=Text(content,"",23,Accent);
                var instruction=InstructionSurface();LabUIVisuals.Icon(instruction,LabGlyph.Hand,Accent,38);objectiveStatus=Text(instruction,"",28);
                Text(content,app.XRSimulation?"XR simulator: left mouse selects. Hold G to grip; release G to let go.":"Controller: trigger selects, grip holds. Bare hand: pinch to select, close your fingers to hold; open to release.",23,Muted);
                selectedLabel=Text(content,"",23,Muted);notice=Text(content,app.Notice,22,Accent);
                Button(content,"Replay instruction",app.NarrateCurrentStep);
            }
            else if(!app.Sandbox&&app.Engine!=null)
            {
                stepLabel=Text(content,"",22,Muted);
                if(app.Variant.mode==LearningMode.GuidedDissection){
                    var tools=Row(content,"Cutting tools",8);
                    Button(tools,"Bone saw",()=>{app.Session.saw=true;app.Message("Bone saw equipped. Point at bone and hold trigger to cut.");});
                    Button(tools,"Scalpel",()=>{app.Session.saw=false;app.Message("Scalpel equipped. Point at soft tissue and hold trigger to cut.");});
                    Button(tools,"Undo cut",app.Session.Undo);
                    notice=Text(content,"",22,Accent);
                }
                var progress=Panel(content,"Activity progress",Surface);Size(progress,5);progress.raycastTarget=false;
                progressFill=Panel(progress.transform,"Completed steps",Accent);Fill(progressFill.rectTransform);progressFill.raycastTarget=false;
                var instruction=InstructionSurface();objectiveHeading=Text(instruction,"",31);objectiveHeading.fontStyle=FontStyles.Bold;objectiveStatus=Text(instruction,"",25,Muted);
                if(app.Unit.isCase){caseHandling=Text(instruction,"",24,Accent);caseHandling.name="Tool handling feedback";}
                windowLabel=Text(content,"",20,Muted);
                if(app.Variant.mode!=LearningMode.GuidedDissection)notice=Text(content,"",22,Accent);
                InspectionControls();
                if(app.Unit.isCase)Button(content,"Case and controls",ShowCaseControls);
                if(app.Engine.Attempt.phase!=SessionPhase.Assessment)
                    Button(content,"Show me this step",()=>{app.RevealHint();app.Demonstrator.Play();app.NarrateCurrentStep();});
                finishButton=Button(content,app.Engine.Attempt.phase==SessionPhase.Assessment?"Finish assessment":"Finish activity",app.Finish,true,completed:true);
                selectedLabel=Text(content,"",21,Muted);
                Button(content,"Tools and more help",ShowActivityTools);
                Text(content,(app.Variant.mode==LearningMode.GuidedDissection?"Cut a chunk, then grip that separated piece to lift and turn it. Whole layers stay on the table. ":"Grip visible anatomy to lift and turn it. Inspection parts return on release. ")+MovementSummary(),22,Muted);
            }
            else
            {
                Text(content,"Choose a layer, cut a chunk, then grip the separated piece to lift and turn it. Whole layers stay on the table.",26,Muted);
                selectedLabel=Text(content,"",23,Muted);notice=Text(content,app.Notice,22,Accent);
                Button(content,"Tools and layers",ShowActivityTools,true);
                InspectionControls();
            }
            if(!app.OrientationActive)Text(content,InspectionInstructions,22,Muted);
            float captionHeight=112*app.Settings.Current.EffectiveCaptionScale;
            captionPanel=LabUIVisuals.Surface(screen,"Narration captions",Surface).rectTransform;
            captionPanel.anchorMin=Vector2.zero;captionPanel.anchorMax=new Vector2(1,0);captionPanel.pivot=new Vector2(.5f,0);
            captionPanel.sizeDelta=new Vector2(-28,captionHeight);captionPanel.anchoredPosition=new Vector2(0,footer.sizeDelta.y+8);
            caption=Text(captionPanel,"",22,Ink);Fill(caption.rectTransform,16);
            if(app.Settings.Current.captions)rail.offsetMin=new Vector2(10,footer.sizeDelta.y+captionHeight+20);
            RefreshLive();
        }
        RectTransform InstructionSurface()
        {
            var surface=LabUIVisuals.Surface(content,"Current practical instruction",Surface);var column=surface.gameObject.AddComponent<VerticalLayoutGroup>();column.padding=new RectOffset(22,22,22,22);column.spacing=12;column.childControlWidth=true;column.childControlHeight=true;column.childForceExpandHeight=false;LabUIVisuals.Border(surface.rectTransform);return surface.rectTransform;
        }
        void ShowActivityTools()
        {
            Page("Activity tools","Tools and help",back:ShowWorkspace,backName:"Return to your step");
            bool assessment=app.Engine!=null&&app.Engine.Attempt.phase==SessionPhase.Assessment;
            if(!assessment&&app.Engine!=null)
            {
                Button(content,"Read this lesson",ShowTranscript);
                Button(content,"Pause / resume narration",app.Narrator.TogglePause);
            }
            if(app.Unit!=null)Button(content,"References",()=>ShowReferences(app.Unit));
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
            Page("Teaching",app.Unit.title,back:ShowWorkspace,backName:"Return to specimen");Text(content,app.Unit.lesson,24);
            if(app.Engine.Attempt.phase!=SessionPhase.Assessment)foreach(var o in app.Variant.objectives){Text(content,o.title,25,Accent);Text(content,o.instruction+" "+o.explanation,22);}
            else Text(content,"Assessment targets follow your declared rubric. Answer-revealing demonstrations are available in practice.",22,Muted);
        }
        void ShowBrowse()
        {
            Page("Browse","Anatomy collection",back:ShowWorkspace,backName:"Return to specimen");var search=Field("Find a structure");var results=Column(content);
            Action update=()=>{foreach(Transform child in results)Destroy(child.gameObject);foreach(var s in app.Session.originals.Where(s=>!s.record.reference_region&&(string.IsNullOrEmpty(search.text)||s.record.name.IndexOf(search.text,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(s=>s.record.name).Take(60))
                Button(results,s.record.name,()=>{app.Session.Inspect(s);ShowWorkspace();});};search.onValueChanged.AddListener(_=>update());update();
        }
        public void ShowResults()
        {
            var attempt=app.Engine.Attempt;var variant=app.Variant;
            Page("Results",attempt.passed?"Step by step, you got there":"Let's try that again",back:()=>Leave(ShowHub),backName:"Return to lab");
            var resultRow=Row(content,"Assessment result",18);LabUIVisuals.Icon(resultRow,attempt.passed?LabGlyph.Check:LabGlyph.Target,Accent,52);var score=Text(resultRow,attempt.score.ToString("0")+"% · "+(attempt.passed?"Completed":"Keep practising"),34,Accent);score.fontStyle=FontStyles.Bold;
            if(attempt.passed&&attempt.phase==SessionPhase.Assessment&&attempt.difficulty!=Difficulty.Hard)
                Text(content,(attempt.difficulty==Difficulty.Easy?"Medium":"Hard")+" is now open for this activity.",24);
            Text(content,app.Variant.debrief,22,Muted);
            if(attempt.passed&&attempt.phase==SessionPhase.Lesson)Button(content,"Try it with practice guides",()=>app.StartActivity(variant,SessionPhase.Practice),true);
            else if(attempt.passed&&attempt.phase==SessionPhase.Practice)Button(content,"Take the "+variant.difficulty+" assessment",()=>app.StartActivity(variant,SessionPhase.Assessment),true);
            else if(!attempt.passed)Button(content,"Practise this step again",()=>app.StartActivity(variant,SessionPhase.Practice),true);
            else Button(content,"Choose your next activity",()=>Leave(ShowModes),true);
            Button(content,"Review your steps",ShowAttemptFeedback);
        }
        void ShowAttemptFeedback()
        {
            Page("Feedback","Your recorded steps",back:ShowResults,backName:"Back to results");
            var reasoning=app.Variant.objectives.Where(o=>o.action==ActionKind.ClinicalDecision).ToArray();
            if(reasoning.Length>0)Text(content,"Clinical reasoning: "+reasoning.Count(o=>app.Engine.IsComplete(o.id))+" / "+reasoning.Length+" essential decisions completed",24,Accent);
            foreach(var objective in app.Variant.objectives){bool complete=app.Engine.IsComplete(objective.id);Text(content,(complete?"Completed · ":"Revise · ")+objective.title,25,complete?Ink:Accent);if(!complete)Text(content,objective.explanation,22,Muted);}
            foreach(var objective in reasoning)
            {
                foreach(var action in app.Engine.Attempt.actions.Where(a=>a.kind==ActionKind.ClinicalDecision&&a.targetId==objective.targetId))
                {var choice=objective.choices.FirstOrDefault(c=>c.id==action.toolId);if(choice!=null)Text(content,"Your choice: "+choice.label+". "+(action.toolId==objective.toolId?objective.explanation:choice.rationale),22,Muted);}
            }
            Text(content,"Recorded errors: "+app.Engine.Attempt.mistakes+(app.Engine.Attempt.criticalFailure?". A protected structure or essential procedure step needs another attempt.":"."),22,Muted);
        }
        public void ShowProgress()
        {
            Page("Progress","Your learning record",back:ShowPauseMenu,backName:"Back to menu");Text(content,ProgressRules.CareerChapter(app.Progress),28,Accent);
            Text(content,app.Progress.rewards.Sum(r=>r.xp)+" XP earned across "+app.Progress.rewards.Count+" mastered objectives. Each objective rewards once per difficulty.",23,Muted);
            Text(content,"Career titles describe your in-game learning progress. They are not qualifications.",20,Muted);Rule(content);
            if(app.Progress.attempts.Count==0)Text(content,"Complete an activity to start your record.",24);
            foreach(var attempt in app.Progress.attempts.OrderByDescending(a=>a.startedUtc).Take(80))
            {var unit=app.Catalog.units.FirstOrDefault(u=>u.id==attempt.unitId);Text(content,(unit?.title??attempt.unitId)+" · "+attempt.difficulty,23);Text(content,attempt.phase+" · "+attempt.status+" · "+attempt.score.ToString("0")+"%"+(attempt.status==AttemptStatus.Completed?" · "+(attempt.passed?"Passed":"Needs review"):"")+"\n"+attempt.startedUtc,19,Muted);Rule(content);}
            Button(content,"Export progress",()=>app.Message("Exported to "+app.ExportProgress()));Button(content,"Sync now",()=>Run(app.Synchronize));ErrorArea();
        }
        void ErrorArea(){notice=Text(content,app.Notice,21,Accent);}
        TMP_InputField Field(string label,string value="",bool secret=false)
        {var f=Input(content,label,value,secret);f.shouldHideSoftKeyboard=xrLayout;f.shouldHideMobileInput=xrLayout;f.onSelect.AddListener(_=>{if(xrLayout)OpenKeyboard(f);});return f;}
        async void Run(Func<Task> action){if(runningAction)return;runningAction=true;try{await action();RefreshLive();}catch(Exception ex){app.Message(ex.Message);RefreshLive();}finally{runningAction=false;}}
        public void Confirm(string title,string description,string action,Action accepted)
        {
            if(modal)modalBack?.Invoke();
            modal=ModalSurface("Confirmation",DismissModal);Fill(modal,8);
            PinnedModalBack("Cancel",DismissModal);
            var area=Rect("Confirmation body",modal);Fill(area);area.offsetMax=new Vector2(0,-BackHeight-24);
            var c=Scroll(area);Text(c,title,36);Text(c,description,24);
            if(accepted!=null)Button(c,action,()=>{DismissModal();accepted();},true);
        }
        public void RefreshLive()
        {
            if (!app || app.Settings?.Current == null || !app.Session || !app.Narrator) return;
            if(status)status.text=(!app.XRReady?"Tracking paused":app.XRSimulation?"XR simulator":app.Preview?"Guest · this session only":app.SyncStatus)+(app.Settings.Current.showFps?"    |    "+LearningPerformanceMonitor.Display:"");
            if(notice)notice.text=currentScreen=="Workspace"&&app.Engine!=null?app.Notice:app.OrientationActive&&app.Notice==app.CurrentInstruction?"":app.Notice;
            if(objectiveStatus&&app.OrientationActive){objectiveStatus.text=app.CurrentInstruction;if(stepLabel)stepLabel.text=app.OrientationStep==0?"First, select the skin":"Now, hold and release";}
            if(objectiveStatus&&app.Engine!=null)
            {
                var current=app.Engine.Current;bool assessment=app.Engine.Attempt.phase==SessionPhase.Assessment;
                int completed=app.Engine.Attempt.objectives.Count(o=>o.completed),total=app.Variant.objectives.Length;
                if(stepLabel)stepLabel.text=app.Variant.difficulty+" · "+(assessment?"Assessment":"Guided "+(app.Engine.Attempt.phase==SessionPhase.Lesson?"lesson":"practice"))+" · "+(current==null?"All steps complete":"Step "+(completed+1)+" of "+total);
                if(objectiveHeading)objectiveHeading.text=current==null?"Every step complete":current.title;
                objectiveStatus.text=current==null?"Finish when you are ready.":assessment?"":app.CurrentInstruction;
                objectiveStatus.gameObject.SetActive(!string.IsNullOrEmpty(objectiveStatus.text));
                if(caseHandling)caseHandling.text=app.Procedure.StepFeedback;
                if(progressFill)progressFill.rectTransform.anchorMax=new Vector2(total>0?(float)completed/total:0,1);
                if(finishButton){finishButton.interactable=current==null;finishButton.gameObject.SetActive(current==null);}
            }
            if(windowLabel){windowLabel.text=app.TeachingWindowLabel??"";windowLabel.gameObject.SetActive(!string.IsNullOrEmpty(windowLabel.text));}
            if(selectedLabel)selectedLabel.text=(app.Variant?.mode==LearningMode.GuidedDissection||app.Sandbox?(app.Session.saw?"Equipped: BONE SAW":"Equipped: SCALPEL")+" · ray reach 3 m\n":"")+(app.Variant?.mode==LearningMode.IdentifyEnumerate&&app.Markers?"Marked "+app.Markers.Count+" of "+app.Variant.objectives.Length+" in this set.\n":"")+(app.Session.selected&&app.Settings.Current.labels?"Selected: "+app.Session.selected.record.name:"");
            if(caption){caption.text=app.Settings.Current.captions?app.Narrator.Caption:"";caption.fontSize=22*app.Settings.Current.EffectiveCaptionScale;captionPanel.gameObject.SetActive(!string.IsNullOrEmpty(caption.text));}
        }
        bool welcomeSpoken;
        void Update()
        {
            if(app&&app.Settings!=null&&currentScreen=="Headset"&&app.XRReady)ShowSignIn();
            if(!welcomeSpoken && currentScreen=="Sign in" && !modal && app.Focused && app.XRReady)
            {
                welcomeSpoken=true;
                app.Narrator.Play("lab-welcome",PromptText("lab-welcome"));
            }
            if(Time.frameCount%15==0)RefreshLive();
        }
        void SetXRLayout()
        {
            canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;screen.sizeDelta=new Vector2(700,860);screen.localScale=Vector3.one*.00085f;
            Camera.main.rect=new Rect(0,0,1,1);
            // Recenter at eye height even when the learner is looking down at the table.
            var forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.ProjectOnPlane(Camera.main.transform.up,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            var position=Camera.main.transform.position+forward*.95f-right*.55f-Vector3.up*.02f;
            if(app.MuseumActive)position=Camera.main.transform.position+forward*.45f-right*1.1f-Vector3.up*.05f;
            if(!app.MuseumActive&&app.Structures.TryGetValue("FMA7163",out var skin))position.y=Mathf.Max(position.y,skin.Renderer.bounds.min.y+screen.rect.height*screen.localScale.y*.5f+.04f);
            screen.position=position;
            screen.rotation=Quaternion.LookRotation(screen.position-Camera.main.transform.position,Vector3.up);
        }
        public void RecenterPanel(){panelDragPointer=-1;if(xrLayout)SetXRLayout();}
        public bool RayPointer(Ray ray,bool press,Vector2 scroll=default,bool grip=false,bool held=false,int pointerId=1,Quaternion? controllerRotation=null)
        {
            if(!xrLayout||!screen||!EventSystem.current)return false;
            if(app&&!app.Focused){CancelXRPointer(pointerId);return false;}
            if(!xrPointers.TryGetValue(pointerId,out var pointer)){pointer=new XRPointer{data=new TrackedDeviceEventData(EventSystem.current){pointerId=pointerId,button=PointerEventData.InputButton.Left,rayPoints=new System.Collections.Generic.List<Vector3>{Vector3.zero,Vector3.zero},layerMask=~0}};xrPointers[pointerId]=pointer;}
            bool gripDown=grip&&!pointer.gripHeld;pointer.gripHeld=grip;
            var rotation=controllerRotation??Quaternion.LookRotation(ray.direction,Vector3.up);
            if(panelDragPointer==pointerId)
            {
                if(grip){screen.SetPositionAndRotation(ray.origin+rotation*panelGrabOffset,rotation*panelGrabRotation);xrCaptureUntil=Time.unscaledTime+.15f;return true;}
                panelDragPointer=-1;
            }
            if(!held)pointer.captured=false;
            var data=pointer.data;data.rayPoints[0]=ray.origin;data.rayPoints[1]=ray.GetPoint(3);pointer.hits.Clear();
            EventSystem.current.RaycastAll(data,pointer.hits);
            var result=pointer.hits.FirstOrDefault(r=>r.gameObject&&r.gameObject.transform.IsChildOf(modal?modal:screen));data.pointerCurrentRaycast=result;
            Hover(pointer,result.gameObject);
            if(!result.gameObject){ReleasePointer(pointer,null);if(panelDragPointer==pointerId)panelDragPointer=-1;if(pointer.captured)xrCaptureUntil=Time.unscaledTime+.15f;return pointer.captured;}
            var point=result.screenPosition;data.delta=point-data.position;data.position=point;
            // Hover consumes world selection, but only deliberate UI input suspends locomotion.
            if(press||pointer.captured)xrCaptureUntil=Time.unscaledTime+.15f;
            if(Mathf.Abs(scroll.y)>.2f){data.scrollDelta=new Vector2(0,scroll.y*.8f);if(ExecuteEvents.ExecuteHierarchy(result.gameObject,data,ExecuteEvents.scrollHandler))xrCaptureUntil=Time.unscaledTime+.15f;}
            var local=(Vector2)screen.InverseTransformPoint(result.worldPosition);
            if(gripDown&&panelDragPointer<0&&!modal&&local.y>screen.rect.yMax-Mathf.Max(100,48*TextScale+48)&&!result.gameObject.GetComponentInParent<Button>())
            {ReleasePointer(pointer,null);panelGrabOffset=Quaternion.Inverse(rotation)*(screen.position-ray.origin);panelGrabRotation=Quaternion.Inverse(rotation)*screen.rotation;panelDragPointer=pointerId;xrCaptureUntil=Time.unscaledTime+.15f;return true;}
            if(press)
            {
                ReleasePointer(pointer,null);pointer.captured=held;pointer.pressLocal=local;pointer.pressWorld=result.worldPosition;
                data.pressPosition=point;data.pointerPressRaycast=result;data.eligibleForClick=true;data.useDragThreshold=true;data.clickCount=1;data.clickTime=Time.unscaledTime;
                pointer.pressed=ExecuteEvents.ExecuteHierarchy(result.gameObject,data,ExecuteEvents.pointerDownHandler)??ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject);
                data.pointerPress=pointer.pressed;pointer.drag=ExecuteEvents.GetEventHandler<IDragHandler>(result.gameObject);data.pointerDrag=pointer.drag;
                if(pointer.drag)ExecuteEvents.Execute(pointer.drag,data,ExecuteEvents.initializePotentialDrag);
                var selectable=result.gameObject.GetComponentInParent<Selectable>();if(selectable)selectable.Select();
                // Buttons respond on the input edge, without waiting for the learner
                // to release a trigger/pinch. Clear state before callbacks replace
                // the page; holding or releasing cannot click the new page again.
                // Sliders and dragging empty scroll areas keep their drag lifecycle.
                if(pointer.pressed&&pointer.pressed.GetComponent<Button>())
                {ReleasePointer(pointer,result.gameObject);return true;}
            }
            if(!press&&held&&pointer.drag&&data.delta.sqrMagnitude>.01f)
            {
                // Measure deliberate movement on the panel, independent of head
                // motion, stereo projection and render resolution. Sliders retain
                // Unity's immediate drag behavior via useDragThreshold=false.
                data.pressPosition=Camera.main.WorldToScreenPoint(pointer.pressWorld);
                if(!pointer.dragging&&(!data.useDragThreshold||(local-pointer.pressLocal).sqrMagnitude>=64))
                {
                    ExecuteEvents.Execute(pointer.drag,data,ExecuteEvents.beginDragHandler);pointer.dragging=true;data.dragging=true;data.eligibleForClick=false;
                    if(pointer.pressed&&pointer.pressed!=pointer.drag){ExecuteEvents.Execute(pointer.pressed,data,ExecuteEvents.pointerUpHandler);pointer.pressed=null;data.pointerPress=null;}
                }
                if(pointer.dragging)ExecuteEvents.Execute(pointer.drag,data,ExecuteEvents.dragHandler);
            }
            if(!held)ReleasePointer(pointer,result.gameObject);return true;
        }
        void ReleasePointer(XRPointer pointer,GameObject hit)
        {
            var pressed=pointer.pressed;var drag=pointer.drag;bool dragging=pointer.dragging,click=pointer.data.eligibleForClick;
            pointer.pressed=pointer.drag=null;pointer.dragging=false;pointer.data.dragging=false;pointer.data.pointerPress=pointer.data.pointerDrag=null;
            pointer.data.eligibleForClick=false;
            if(pressed)ExecuteEvents.Execute(pressed,pointer.data,ExecuteEvents.pointerUpHandler);
            if(dragging&&drag)ExecuteEvents.Execute(drag,pointer.data,ExecuteEvents.endDragHandler);
            if(pressed&&click&&!dragging&&hit&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)==pressed)ExecuteEvents.Execute(pressed,pointer.data,ExecuteEvents.pointerClickHandler);
        }
        void Hover(XRPointer pointer,GameObject hit){if(pointer.hover==hit)return;if(pointer.hover)ExecuteEvents.ExecuteHierarchy(pointer.hover,pointer.data,ExecuteEvents.pointerExitHandler);pointer.hover=hit;pointer.data.pointerEnter=hit;if(hit)ExecuteEvents.ExecuteHierarchy(hit,pointer.data,ExecuteEvents.pointerEnterHandler);}
        public void CancelXRPointer(int pointerId){if(xrPointers.TryGetValue(pointerId,out var pointer)){pointer.captured=false;pointer.gripHeld=true;ReleasePointer(pointer,null);Hover(pointer,null);}if(panelDragPointer==pointerId||!LearningXRInput.HeadTracked)panelDragPointer=-1;}
        public void EnableXRLayoutForQA(){if(!Environment.GetCommandLineArgs().Contains("--learning-qa"))return;xrLayout=true;SetXRLayout();RenderSettingsPage();}
        public bool PointerOverUI => EventSystem.current&&EventSystem.current.IsPointerOverGameObject();
        static string ModeName(LearningMode mode)=>new[]{"Anatomy Cases","Identify & Enumerate","Assemble Anatomy","Guided Dissection"}[(int)mode];
        void DismissModal()
        {
            if(modal){modal.gameObject.SetActive(false);Destroy(modal.gameObject);}
            modal=null;modalBack=null;
        }
        RectTransform ModalSurface(string title,Action back)
        {
            // Opaque and slightly in front of the page: world-space TMP must not bleed through a modal.
            var surface=LabUIVisuals.Surface(screen,title,new Color(Ground.r,Ground.g,Ground.b,1)).rectTransform;LabUIVisuals.Border(surface);
            modalBack=back;surface.localPosition=new Vector3(0,0,-6);return surface;
        }
        void PinnedModalBack(string name,Action action,string label=null)
        {
            var button=(RectTransform)BackButton(modal,action,name,label??name).transform;
            button.anchorMin=button.anchorMax=button.pivot=Vector2.up;button.anchoredPosition=new Vector2(16,-16);
        }
        void OpenKeyboard(TMP_InputField field)
        {
            if(modal)return;
            void Close(){EventSystem.current.SetSelectedGameObject(null);field.DeactivateInputField();DismissModal();}
            modal=ModalSurface("VR keyboard",Close);Fill(modal,8);
            // Keep the current text, editing controls and Done outside the scrolling keys.
            float headerHeight=BackHeight+ControlHeight+126+24*TextScale,footerHeight=ControlHeight+24;
            var header=Column(modal,"Keyboard input",12,8);header.anchorMin=new Vector2(0,1);header.anchorMax=Vector2.one;header.pivot=new Vector2(.5f,1);header.sizeDelta=new Vector2(0,headerHeight);
            var navigation=NavigationRow(header,"Keyboard navigation");Size(navigation,BackHeight);
            BackButton(navigation,Close);var heading=Text(navigation,field.name,28);heading.alignment=TextAlignmentOptions.MidlineLeft;Size(heading,flexible:1).minWidth=0;
            var display=Panel(header,"Typed value",Surface);Size(display,82);
            var preview=Text(display.transform,"",28,Ink);preview.name="Visible input";Fill(preview.rectTransform,12);preview.alignment=TextAlignmentOptions.MidlineLeft;preview.textWrappingMode=TextWrappingModes.NoWrap;
            var positionLabel=Text(header,"",18,Muted);
            int cursor=field.text.Length;bool upper=false;
            bool digits=field.characterValidation==TMP_InputField.CharacterValidation.Digit||field.contentType==TMP_InputField.ContentType.IntegerNumber||field.name.Contains("PIN");
            bool address=field.contentType==TMP_InputField.ContentType.EmailAddress;
            var keyLabels=new System.Collections.Generic.Dictionary<char,TMP_Text>();
            void Refresh(){
                cursor=Mathf.Clamp(cursor,0,field.text.Length);
                string value=field.contentType==TMP_InputField.ContentType.Password?new string('•',field.text.Length):field.text;
                int start=0,end=value.Length;float width=preview.rectTransform.rect.width>20?preview.rectTransform.rect.width:620;
                string Window()=> (start>0?"…":"")+value.Substring(start,cursor-start)+"|"+value.Substring(cursor,end-cursor)+(end<value.Length?"…":"");
                // A caret-centred window keeps even a 254-character address readable.
                while(preview.GetPreferredValues(Window()).x>width&&end>start){if(cursor-start>end-cursor)start++;else end--;}
                preview.text=Window();positionLabel.text="Cursor "+cursor+" / "+value.Length+" · edits kept";
            }
            void Append(string value){
                if(digits)value=new string(value.Where(c=>c>='0'&&c<='9').ToArray());
                if(address)value=new string(value.Where(c=>!char.IsWhiteSpace(c)).ToArray());
                if(field.characterLimit>0&&field.text.Length+value.Length>field.characterLimit)return;
                field.text=field.text.Insert(cursor,value);cursor+=value.Length;Refresh();
            }
            var editing=Row(header,spacing:6);
            Button(editing,"Left",()=>{cursor--;Refresh();});Button(editing,"Right",()=>{cursor++;Refresh();});
            Button(editing,"Delete",()=>{if(cursor>0){field.text=field.text.Remove(--cursor,1);Refresh();}});
            Button(editing,"Clear",()=>{field.text="";cursor=0;Refresh();});
            var keyArea=Rect("Keyboard keys",modal);Fill(keyArea);keyArea.offsetMin=new Vector2(0,footerHeight);keyArea.offsetMax=new Vector2(0,-headerHeight);
            var keys=Scroll(keyArea);keys.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(12,12,12,12);keys.GetComponent<VerticalLayoutGroup>().spacing=8;
            Refresh();
            foreach(var line in digits?new[]{"123","456","789","0"}:new[]{"1234567890","qwertyuiop","asdfghjkl","zxcvbnm","@.-_+"})
            {
                var row=Row(keys,spacing:3);
                foreach(char key in line){char value=key;var button=Button(row,key.ToString(),()=>Append((upper?char.ToUpperInvariant(value):value).ToString()));button.GetComponent<HorizontalLayoutGroup>().padding=new RectOffset(5,5,12,12);keyLabels[key]=button.GetComponentInChildren<TMP_Text>();}
            }
            if(!digits){var options=Row(keys);Button shift=null;shift=Button(options,"Shift off",()=>{upper=!upper;shift.GetComponentInChildren<TMP_Text>().text=upper?"Shift on":"Shift off";foreach(var pair in keyLabels)pair.Value.text=(upper?char.ToUpperInvariant(pair.Key):pair.Key).ToString();});if(!address)Button(options,"Space",()=>Append(" "));}
            var bottom=Rect("Keyboard footer",modal);bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);bottom.pivot=new Vector2(.5f,0);bottom.sizeDelta=new Vector2(0,footerHeight);
            var done=Button(bottom,"Done",Close,true);Fill((RectTransform)done.transform,12);
            foreach(var button in modal.GetComponentsInChildren<Button>())
            {
                button.GetComponent<HorizontalLayoutGroup>().childAlignment=TextAnchor.MiddleCenter;
                button.GetComponentInChildren<TMP_Text>().alignment=TextAlignmentOptions.Center;
            }
            Canvas.ForceUpdateCanvases();Refresh();
        }
    }
}
