using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        public void ShowSettings() { draft=app.Settings.Draft();RenderSettingsPage(); }
        void RenderSettingsPage()
        {
            Page("Settings",settingCategory);
            Text(content,draft.name,23,Muted);
            Button(content,"Choose a settings category",ShowSettingsCategories);
            float actionHeight=ControlHeight,statusHeight=52*TextScale+12;
            footer.sizeDelta=new Vector2(0,actionHeight+statusHeight+20);rail.offsetMin=new Vector2(10,footer.sizeDelta.y+8);
            status.rectTransform.anchorMin=status.rectTransform.anchorMax=Vector2.zero;status.rectTransform.anchorMax=Vector2.right;status.rectTransform.pivot=new Vector2(.5f,0);status.rectTransform.sizeDelta=new Vector2(-40,statusHeight);status.rectTransform.anchoredPosition=new Vector2(0,4);
            var actions=Row(footer);actions.anchorMin=new Vector2(0,1);actions.anchorMax=Vector2.one;actions.pivot=new Vector2(.5f,1);actions.sizeDelta=new Vector2(-40,actionHeight);actions.anchoredPosition=new Vector2(0,-10);
            Button(actions,"Apply",ApplySettings,true);Button(actions,"Cancel",()=>{app.Settings.RevertDisplay();(previousScreen??ShowHub)();});
            Rule(content);
            switch(settingCategory)
            {
                case "Quick setup":
                    ReadabilitySettings(false);
                    Button(content,"Move panel in front of me",RecenterPanel);
                    Rule(content);
                    MovementSettings();
                    Toggle(content,"Left dominant hand",()=>draft.leftHanded,v=>draft.leftHanded=v);
                    Text(content,"For sitting height, table height, grip and audio options, choose a settings category above.",22,Muted);break;
                case "Graphics & display":
                    Cycle(content,"Graphics preset",(GraphicsPreset[])Enum.GetValues(typeof(GraphicsPreset)),()=>draft.preset,v=>{draft.SetPreset(v);RenderSettingsPage();});
                    Slider(content,"Scene render scale",.6f,1.4f,()=>draft.renderScale,v=>draft.renderScale=v);
                    Slider(content,"Brightness",.5f,1.5f,()=>draft.brightness,v=>draft.brightness=v);
                    Cycle(content,"Texture mip limit",new[]{0,1,2,3},()=>draft.textureLimit,v=>draft.textureLimit=v);
                    Cycle(content,"Anti-aliasing",draft.platform=="Quest"?new[]{1,2,4}:new[]{1,2,4,8},()=>draft.msaa,v=>draft.msaa=v);
                    Toggle(content,"Shadows",()=>draft.shadows,v=>draft.shadows=v);Slider(content,"Shadow distance (m)",0,12,()=>draft.shadowDistance,v=>draft.shadowDistance=v);
                    Text(content,"Anatomy and task accuracy stay the same at every graphics level.",20,Muted);
                    if(draft.platform=="Quest")Text(content,"Auto requests the highest refresh rate your headset supports. Lower graphics can help reach it; actual FPS depends on the scene and headset.",20,Muted);
                    if(draft.platform=="Windows")
                    {
                        Cycle(content,"Resolution",new[]{"1280 × 720","1440 × 1000","1920 × 1080","2560 × 1440","3840 × 2160"},()=>draft.width+" × "+draft.height,v=>{var pair=v.Split('×');draft.width=int.Parse(pair[0]);draft.height=int.Parse(pair[1]);});
                        Toggle(content,"Fullscreen",()=>draft.fullscreen,v=>draft.fullscreen=v);Toggle(content,"VSync",()=>draft.vsync,v=>draft.vsync=v);Cycle(content,"Frame cap",new[]{30,60,72,90,120,144},()=>draft.frameCap,v=>draft.frameCap=v);
                    }
                    else
                    {
                        const string automatic="Auto · highest supported";
                        var rates=QuestRefreshFeature.Active?QuestRefreshFeature.Active.Supported:System.Array.Empty<float>();
                        Cycle(content,"Headset refresh rate",new[]{automatic}.Concat(rates.Select(v=>(int)v+" Hz")).ToArray(),()=>draft.refreshRate==0?automatic:draft.refreshRate+" Hz",v=>draft.refreshRate=v==automatic?0:int.Parse(v.Split(' ')[0]));
                        if(rates.Length==0)Text(content,"The headset supplies its available refresh rates when connected.",21,Muted);
                    }
                    Toggle(content,"Show FPS",()=>draft.showFps,v=>draft.showFps=v);Text(content,"Performance suggestions never change your chosen settings automatically.",21,Accent);break;
                case "Audio":
                    Slider(content,"Master volume",0,1,()=>draft.masterVolume,v=>draft.masterVolume=v);Slider(content,"Narration",0,1,()=>draft.narrationVolume,v=>draft.narrationVolume=v);
                    Slider(content,"Effects",0,1,()=>draft.effectsVolume,v=>draft.effectsVolume=v);Slider(content,"Ambience",0,1,()=>draft.ambienceVolume,v=>draft.ambienceVolume=v);
                    Slider(content,"Narration speed",.75f,1.5f,()=>draft.narrationSpeed,v=>draft.narrationSpeed=v);Toggle(content,"Captions",()=>draft.captions,v=>draft.captions=v);break;
                case "Controls":
                    Toggle(content,"Left dominant hand",()=>draft.leftHanded,v=>draft.leftHanded=v);
                    Toggle(content,"Toggle grip",()=>draft.toggleGrip,v=>draft.toggleGrip=v);Toggle(content,"Controller vibration",()=>draft.vibration,v=>draft.vibration=v);
                    Text(content,"Controller: trigger selects or uses a tool; grip holds a part. Bare hand: pinch thumb and index to select or use; curl your other three fingers to hold. Open to release. Pinch and drag to scroll a panel.",24,Muted);
                    Text(content,InspectionInstructions+" The workspace also has zoom buttons.",24,Muted);break;
                case "VR comfort":
                    if(LearningXRInput.IsSimulated)Slider(content,"Simulator FOV (degrees)",50,110,()=>draft.simulatorFov,v=>draft.simulatorFov=v,"0");
                    Toggle(content,"Comfort vignette",()=>draft.vignette,v=>{draft.vignette=v;RenderSettingsPage();});
                    if(draft.vignette)Slider(content,"Comfort view (degrees)",40,100,()=>draft.comfortFov,v=>draft.comfortFov=v,"0");
                    Text(content,"Comfort view narrows the clear area while walking or turning smoothly. Higher values show more. It does not change the headset lenses. Simulator FOV changes only the computer preview. Press Apply to save.",20,Muted);
                    Toggle(content,"Seated fallback height",()=>draft.seated,v=>draft.seated=v);Button(content,"Recenter panel",RecenterPanel);
                    Text(content,"Height assistance raises your view. Extra player height adds to it; table height moves the specimen. Press Apply to save and use these values.",21,Muted);
                    Toggle(content,"Automatic height assistance",()=>draft.autoHeight,v=>draft.autoHeight=v);
                    Text(content,"Floor tracking preserves your real sitting or standing height. Seated fallback is used only without floor height and with automatic assistance off.",20,Muted);
                    Slider(content,"Extra player height (m)",0,.8f,()=>draft.heightBoost,v=>draft.heightBoost=v);
                    Button(content,"Recenter view and height",()=>{var controls=FindFirstObjectByType<LearningControls>();if(controls)controls.RecenterTracking();});
                    Slider(content,"Table height offset (m)",-.2f,.25f,()=>draft.tableHeight,v=>draft.tableHeight=v);
                    MovementSettings();
                    Slider(content,"Turn speed",15,120,()=>draft.turnSpeed,v=>draft.turnSpeed=v);Slider(content,"Snap angle",15,60,()=>draft.snapAngle,v=>draft.snapAngle=v);break;
                case "Learning & accessibility":
                    ReadabilitySettings(true);
                    Text(content,"Practice hints include a dissection start ring, sweep arrow and chunk-lift guide. These guides stay hidden during assessments.",20,Muted);
                    Toggle(content,"Practice hints",()=>draft.hints,v=>draft.hints=v);Toggle(content,"Structure labels",()=>draft.labels,v=>draft.labels=v);Toggle(content,"Reduced motion",()=>draft.reducedMotion,v=>draft.reducedMotion=v);
                    Text(content,"Completion always includes a written status. Demonstrations can be replayed in lessons and practice. There is no mandatory countdown.",21,Muted);break;
                case "Profiles":
                    var name=Field("Profile name",draft.name);name.characterLimit=48;name.onValueChanged.AddListener(v=>draft.name=v);
                    Button(content,"Create default profile",()=>{draft=new SettingsProfile{platform=app.Settings.Platform,name="New profile"};draft.SetPreset(GraphicsPreset.Medium);RenderSettingsPage();});
                    Button(content,"Duplicate current draft",()=>{draft=SettingsService.Clone(draft);draft.id=Guid.NewGuid().ToString("N");draft.name=(draft.name.Length>40?draft.name.Substring(0,40):draft.name)+" copy";RenderSettingsPage();});
                    foreach(var profile in app.Settings.Collection.profiles.Where(p=>p.platform==app.Settings.Platform)){var p=profile;Button(content,"Load · "+p.name,()=>{draft=SettingsService.Clone(p);RenderSettingsPage();});}
                    if(app.Account.UserId!=null)
                    {
                        Button(content,"Save this profile to account",()=>Run(async()=>{draft.Validate();await app.Account.SyncProfiles(draft);app.Message("Profile saved to your account.");}));
                        Button(content,"Load account profiles",()=>Run(async()=>
                        {
                            var profiles=await app.Account.SyncProfiles();Page("Account profiles","Your saved profiles",back:RenderSettingsPage,backName:"Back to settings");
                            foreach(var p in profiles)
                            {
                                Button(content,p.name+" · "+p.platform,()=>{
                                    if(p.platform==app.Settings.Platform)draft=SettingsService.Clone(p);
                                    else
                                    {
                                        draft.hints=p.hints;draft.labels=p.labels;draft.reducedMotion=p.reducedMotion;
                                        draft.textScale=p.textScale;draft.captionScale=p.captionScale;draft.captions=p.captions;draft.highContrast=p.highContrast;
                                        draft.astigmatismReadingMode=p.astigmatismReadingMode;draft.readingBrightnessPercent=p.readingBrightnessPercent;
                                    }
                                    RenderSettingsPage();
                                });
                                Button(content,"Remove account copy · "+p.name,()=>Confirm("Delete account profile?","This removes "+p.name+" from your account. Profiles already stored on a device remain available there.","Delete account copy",()=>Run(async()=>{await app.Account.SyncProfiles(deleteId:p.id);RenderSettingsPage();app.Message("Account profile deleted.");})));
                            }
                            Text(content,"Selecting a profile from another platform copies only learning and accessibility preferences. Choose Apply to keep those changes.",21,Muted);
                        }));
                    }
                    Button(content,"Delete this device's copy",()=>Confirm("Delete local settings profile?",draft.name+" will be removed from this device. Keep at least one local profile. Account copies can be removed in Load account profiles.","Delete local profile",()=>Run(()=>{app.Settings.Delete(draft.id);ShowSettings();return Task.CompletedTask;})));break;
                case "Account & saves":
                    Text(content,app.Account.Email??"Preview access",24);Text(content,app.SyncStatus,22,Accent);Button(content,"Sync now",()=>Run(app.Synchronize));Button(content,"Resume last safe step",app.ResumeLast);Button(content,"Export progress",()=>app.Message("Exported to "+app.ExportProgress()));
                    Button(content,"Privacy and shared headsets",()=>ShowPrivacy(RenderSettingsPage));
                    if(app.Account.UserId!=null)
                    {
                        var pin=Field("New six-digit device PIN","",true);pin.characterLimit=6;Button(content,"Trust this device",()=>Run(()=>{app.Account.TrustDevice(pin.text);pin.text="";app.Message("Offline access is enabled for this account on this device.");return Task.CompletedTask;}));
                        Text(content,"Set or reset a PIN after fresh email sign-in. This PIN unlocks only this device. Trusted account emails appear on the sign-in screen; use offline access on shared headsets only with school permission.",21,Muted);
                        Button(content,"Remove offline access",()=>Confirm("Remove trusted-device access?","You will need an email code for your next sign-in on this device.","Remove access",()=>app.Account.Trusted.Forget(app.Account.UserId)));
                        Button(content,"Reset learning progress",()=>Confirm("Reset all learning progress?","This clears attempts, rewards and mastery on every device after synchronization. This requires internet access.","Reset progress",()=>Run(app.ResetProgress)));
                        Button(content,"Delete account",()=>Confirm("Permanently delete your account?","Your account and cloud progress will be deleted. Export your learning record first if you want a copy.","Delete account",()=>Run(app.Account.DeleteAccount)));
                    }
                    break;
            }
            if(settingCategory!="Account & saves"&&settingCategory!="Profiles")Button(content,"Reset this category",ResetCategory);
            ErrorArea();

        }
        void MovementSettings()
        {
            Cycle(content,"Movement",new[]{MoveStyle.Teleport,MoveStyle.Smooth},()=>draft.movement,v=>draft.movement=v);
            Slider(content,"Movement speed",.2f,1.5f,()=>draft.moveSpeed,v=>draft.moveSpeed=v);
            Cycle(content,"Turning",new[]{TurnStyle.Snap,TurnStyle.Smooth},()=>draft.turning,v=>draft.turning=v);
            Slider(content,"Stick deadzone",.1f,.45f,()=>draft.stickDeadzone,v=>draft.stickDeadzone=v);
            Text(content,"Left stick moves; right stick turns or scrolls a pointed-at panel. Left dominant hand swaps them. Smooth walks in the direction you look; use a lower speed for gentler movement.",22,Muted);
            Text(content,"Teleport: push the movement stick forward, then center it to step ahead onto clear floor. Snap: push the turning stick sideways once per turn, then center it before turning again.",22,Muted);
            Text(content,"A larger deadzone ignores more movement near the stick center. After a pause, panel interaction or tracking interruption, center both sticks before moving again. Teleport and Snap avoid continuous virtual motion.",22,Muted);
        }

        void ReadabilitySettings(bool captions)
        {
            var modeControls=Column(content,"Reading mode",spacing:4);
            var sampleSurface=LabUIVisuals.Surface(content,"Readability preview",Ground);
            var sampleTint=sampleSurface.GetComponent<LabGlassTint>();
            var sampleColumn=sampleSurface.gameObject.AddComponent<VerticalLayoutGroup>();
            sampleColumn.padding=new RectOffset(20,20,18,18);sampleColumn.spacing=12;
            sampleColumn.childControlWidth=sampleColumn.childControlHeight=true;
            sampleColumn.childForceExpandWidth=true;sampleColumn.childForceExpandHeight=false;
            var sample=Text(sampleSurface.transform,"Readability preview: point at the skin, press trigger to select, then grip to hold.",24);
            var hint=Text(sampleSurface.transform,"Text wraps to fit. Apply uses this size throughout the interface.",22,Muted);
            var captionSample=captions?Text(sampleSurface.transform,"Caption preview: lift the skin gently, then release.",22):null;
            RectTransform brightnessControls=null;
            void Preview()
            {
                sample.fontSize=24*draft.EffectiveTextScale;hint.fontSize=22*draft.EffectiveTextScale;
                sample.color=draft.highContrast?Color.white:Ink;hint.color=draft.highContrast?Color.white:Muted;
                sampleSurface.color=LabUIVisuals.ConsoleColor(draft);
                sampleTint.enabled=!draft.astigmatismReadingMode&&!draft.highContrast;
                brightnessControls.gameObject.SetActive(draft.astigmatismReadingMode);
                if(captionSample){captionSample.fontSize=22*draft.EffectiveCaptionScale;captionSample.color=sample.color;}
            }
            Toggle(modeControls,"Astigmatism reading mode",()=>draft.astigmatismReadingMode,v=>{draft.astigmatismReadingMode=v;Preview();});
            Text(modeControls,"Softens bright areas and removes the glass shine on menus. Text is at least 135% and captions are at least 150%.",22,Muted);
            brightnessControls=Column(modeControls,"Reading light adjustment",spacing:4);
            Slider(brightnessControls,"Reading brightness (%)",65f,100f,()=>draft.readingBrightnessPercent,v=>draft.readingBrightnessPercent=v,"0");
            Text(brightnessControls,"Raise brightness if tissue detail is hard to see. Take a break if your eyes feel sore.",22,Muted);
            Slider(content,"Text size",.85f,1.5f,()=>draft.textScale,v=>{draft.textScale=v;Preview();});
            if(captions)Slider(content,"Caption size",.85f,1.7f,()=>draft.captionScale,v=>{draft.captionScale=v;Preview();});
            Toggle(content,"High contrast",()=>draft.highContrast,v=>{draft.highContrast=v;Preview();});
            Text(content,"Grip the panel's top bar to bring it closer or rotate it toward you. Use the stick while pointing at the panel to scroll longer instructions.",22,Muted);
            Preview();
        }
        void ShowSettingsCategories()
        {
            Page("Settings categories","What would you like to change?",back:RenderSettingsPage,backName:"Back to settings");
            foreach(var category in new[]{"Quick setup","VR comfort","Audio","Learning & accessibility","Graphics & display","Controls","Profiles","Account & saves"})
            {var selected=category;Button(content,category,()=>{settingCategory=selected;RenderSettingsPage();},category==settingCategory);}
        }
        void ApplySettings()
        {
            Run(()=>{draft.Validate();var old=app.Settings.Current;
                if(draft.platform=="Windows"&&(draft.width!=old.width||draft.height!=old.height||draft.fullscreen!=old.fullscreen)){app.Settings.PreviewDisplay(draft);StartCoroutine(DisplayConfirmation());}
                else{app.Settings.Apply(draft);app.Message("Settings applied.");(previousScreen??ShowHub)();}return Task.CompletedTask;});
        }
        IEnumerator DisplayConfirmation()
        {
            bool kept=false;Confirm("Keep these display settings?","The previous display returns automatically in 15 seconds.","Keep settings",()=>{kept=true;app.Settings.Apply(draft);(previousScreen??ShowHub)();});
            float deadline=Time.realtimeSinceStartup+15;while(!kept&&modal&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!kept){app.Settings.RevertDisplay();DismissModal();RenderSettingsPage();app.Message("Previous display restored.");}
        }
        IEnumerator Rebind(string name)
        {
            Confirm("Rebind "+name,"Press a keyboard key. Escape cancels.",null,null);yield return null;
            while(modal){if(Keyboard.current!=null)foreach(var key in Keyboard.current.allKeys)if(key.wasPressedThisFrame){if(key.keyCode!=Key.Escape)typeof(SettingsProfile).GetField(name).SetValue(draft,key.keyCode.ToString());DismissModal();RenderSettingsPage();yield break;}yield return null;}
        }
        void ResetCategory()
        {
            var defaults=new SettingsProfile{platform=draft.platform};defaults.SetPreset(GraphicsPreset.Medium);
            const string readingFields="astigmatismReadingMode readingBrightnessPercent textScale highContrast";
            string fields=settingCategory=="Quick setup"?readingFields+" movement moveSpeed turning stickDeadzone leftHanded":settingCategory=="Graphics & display"?"preset renderScale brightness shadowDistance textureLimit msaa width height frameCap refreshRate fullscreen vsync shadows showFps":settingCategory=="Audio"?"masterVolume narrationVolume effectsVolume ambienceVolume narrationSpeed captions":settingCategory=="Controls"?"sensitivity invertLook leftHanded toggleGrip vibration forward backward left right up down reset undo tool":settingCategory=="VR comfort"?"seated autoHeight heightBoost tableHeight movement moveSpeed turning turnSpeed snapAngle stickDeadzone vignette simulatorFov comfortFov":readingFields+" hints labels reducedMotion captionScale";
            foreach(string name in fields.Split(' ')){var field=typeof(SettingsProfile).GetField(name);field.SetValue(draft,field.GetValue(defaults));}RenderSettingsPage();
        }
    }
}
