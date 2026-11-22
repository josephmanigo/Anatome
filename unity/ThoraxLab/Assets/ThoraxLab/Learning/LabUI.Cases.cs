using System;
using TMPro;
using UnityEngine;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        TextMeshProUGUI caseHandling;
        Action browserReturn;
        bool browserWasAway;

        Action ReferenceDestination(LearningUnit unit) => app.Engine!=null?(Action)ShowWorkspace:()=>ShowUnit(unit);
        void ShowSourceDetails(LearningUnit unit,SourceReference source)
        {
            Page("Source details",source.title,back:()=>ShowReferences(unit),backName:"Back to references");
            Button(content,app.Engine!=null?"Return to activity":"Open unit overview",ReferenceDestination(unit),true);
            Text(content,"Anatome lesson summary",25,Accent);Text(content,unit.lesson,23);
            Text(content,"This summary is written for the game. The original reference is available on its publisher's website.",21,Muted);
            Text(content,source.url,20,Muted);
            Button(content,"Open website in browser",()=>OpenReferenceWebsite(unit,source));
        }
        void PrepareBrowserReturn(LearningUnit unit)
        {
            browserReturn=ReferenceDestination(unit);browserWasAway=false;
            app.Save();Page("Browser","Your place is saved");
            Button(content,app.Engine!=null?"Return to activity":"Return to unit",ReturnFromBrowser,true);
            Text(content,"Close the browser window to return. If the headset menu is still open, select Anatome and Resume.",25);
            Text(content,"Your current step stays open. Release the trigger and grip before continuing.",22,Muted);
        }
        void OpenReferenceWebsite(LearningUnit unit,SourceReference source)
        {
            if(!Uri.TryCreate(source.url,UriKind.Absolute,out var uri)||uri.Scheme!="https"){app.Message("This source address is unavailable. Read the lesson summary or choose another reference.");return;}
            PrepareBrowserReturn(unit);
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Horizon's system browser opens over the retained immersive application.
                // Do not launch an unrelated Android browser task or restart the XR loader.
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent=new AndroidJavaObject("android.content.Intent","android.intent.action.VIEW");
                using var data=new AndroidJavaClass("android.net.Uri");
                using var browser=data.CallStatic<AndroidJavaObject>("parse","systemux://browser");
                intent.Call<AndroidJavaObject>("setClassName","com.oculus.vrshell","com.oculus.vrshell.MainActivity").Dispose();
                intent.Call<AndroidJavaObject>("setData",browser).Dispose();
                intent.Call<AndroidJavaObject>("putExtra","uri",uri.AbsoluteUri).Dispose();
                activity.Call("startActivity",intent);
#else
                Application.OpenURL(uri.AbsoluteUri);
#endif
            }
            catch(Exception)
            {
                browserReturn=null;ShowSourceDetails(unit,source);app.Message("The browser could not open. The lesson summary is still available here.");ErrorArea();
            }
        }
        public void ApplicationAvailabilityChanged(bool available)
        {
            if(browserReturn==null)return;
            if(!available){browserWasAway=true;return;}
            if(browserWasAway&&currentScreen=="Browser")ReturnFromBrowser();
        }
        void ReturnFromBrowser()
        {
            var destination=browserReturn;browserReturn=null;browserWasAway=false;
            destination?.Invoke();RecenterPanel();
        }
        void ShowCaseControls()
        { CaseOverview(false); }
        public void ShowCaseIntroduction()
        { CaseOverview(true); }
        void CaseOverview(bool introduction)
        {
            Page("Case and controls",app.Unit.title,back:introduction?null:(Action)ShowWorkspace,backName:"Return to activity");
            if(introduction)Button(content,"Begin this case",ShowWorkspace,true);
            Text(content,app.Unit.summary,25);Text(content,app.Unit.lesson,23,Muted);
            Text(content,"How this case works",28,Accent);
            Text(content,"1. Find the affected anatomy with the ray and trigger.\n2. Grip a cutaway, move it onto the answer dock, then release.\n3. Use the named tool on the enlarged model beside the specimen.\n4. Predict the response and reassess the anatomy.",23);
            Text(content,"Grip holds a tool. Moving it handles connections, routes and contact. Trigger operates an active control, such as a puff or deployment. Follow the live instruction for the current step.",23);
            Text(content,"The pointed working end must reach the target. Rotate your wrist to align the tool. A ring shows the target direction; numbered points show a route in guided practice.",23,Muted);
            Text(content,"These are enlarged learning models. Pointer checks and route markers represent clinical tasks; they do not simulate every device, tissue force, prescription or patient check.",21,Muted);
            Button(content,"Return loose tools to tray",()=>{app.Procedure.ReturnLooseTools();ShowWorkspace();});
            Button(content,"References",()=>ShowReferences(app.Unit));
        }
    }
}
