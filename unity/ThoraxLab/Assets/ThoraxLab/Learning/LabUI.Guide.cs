using System;
using TMPro;
using UnityEngine;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        [Serializable] sealed class PlayGuide { public GuidePage[] pages; }
        [Serializable] sealed class GuidePage { public string id,title,art,text; }
        PlayGuide playGuide;
        int guideIndex;
        bool resumeLessonAfterGuide;
        LessonNarrator guideNarrator;
        RectTransform guideSurface;
        public bool GuideOpen => guideSurface && modal==guideSurface;

        public void ShowGuide()
        {
            if(modal)return;
            if(app.Session.busy){app.Message("Wait for the current cut to finish, then open Guide.");return;}
            playGuide??=JsonUtility.FromJson<PlayGuide>(Resources.Load<TextAsset>("Learning/how-to-play").text);
            resumeLessonAfterGuide=app.Narrator.IsPlaying;
            if(resumeLessonAfterGuide)app.Narrator.TogglePause();
            if(!guideNarrator)guideNarrator=gameObject.AddComponent<LessonNarrator>();
            RenderGuide();
        }

        void RenderGuide()
        {
            if(guideSurface){guideSurface.gameObject.SetActive(false);Destroy(guideSurface.gameObject);}
            modal=guideSurface=ModalSurface("How to play guide");Fill(modal,8);
            float controlHeight=ControlHeight;
            var top=Rect("Guide close control",modal);top.anchorMin=new Vector2(0,1);top.anchorMax=Vector2.one;top.pivot=new Vector2(.5f,1);top.sizeDelta=new Vector2(-32,controlHeight);top.anchoredPosition=new Vector2(0,-16);
            var close=Button(top,"Close guide",CloseGuide);Fill(close.GetComponent<RectTransform>());
            var bottom=Rect("Guide page controls",modal);bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);bottom.pivot=new Vector2(.5f,0);bottom.sizeDelta=new Vector2(-32,controlHeight);bottom.anchoredPosition=new Vector2(0,16);
            var controls=Row(bottom);controls.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth=false;Fill(controls);
            Button(controls,"Previous",()=>ChangeGuidePage(-1),false,guideIndex>0);
            var pageNumber=Text(controls,(guideIndex+1)+" / "+playGuide.pages.Length,22,Muted);pageNumber.alignment=TextAlignmentOptions.Center;Size(pageNumber,width:72*TextScale);
            Button(controls,"Next",()=>ChangeGuidePage(1),true,guideIndex<playGuide.pages.Length-1);
            var area=Rect("Guide body",modal);Fill(area);area.offsetMin=new Vector2(8,controlHeight+40);area.offsetMax=new Vector2(-8,-controlHeight-32);
            var column=Scroll(area);var page=playGuide.pages[guideIndex];
            Text(column,page.title,31).fontStyle=FontStyles.Bold;
            LabUIVisuals.Hero(column,page.art,140);
            Text(column,page.text,23);
            if(page.id=="guide-grip")Text(column,InspectionInstructions,23);
            Button(column,"Pause / resume guide voice",guideNarrator.TogglePause);
            Button(column,"Replay guide voice",guideNarrator.Replay);
            Text(column,"Female synthetic learning guide",18,Muted);
            guideNarrator.Play(page.id,page.title+". "+page.text);
        }
        void ChangeGuidePage(int delta)
        {
            guideIndex=Mathf.Clamp(guideIndex+delta,0,playGuide.pages.Length-1);RenderGuide();
        }
        public void CloseGuide()
        {
            if(!GuideOpen)return;
            guideNarrator.Stop();guideSurface.gameObject.SetActive(false);Destroy(guideSurface.gameObject);guideSurface=null;modal=null;
            if(resumeLessonAfterGuide&&app.Narrator.Paused)app.Narrator.TogglePause();
            resumeLessonAfterGuide=false;
        }
    }
}
