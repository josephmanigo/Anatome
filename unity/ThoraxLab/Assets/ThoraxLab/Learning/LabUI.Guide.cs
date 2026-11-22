using System;
using TMPro;
using UnityEngine;
using static ThoraxLab.Learning.LabWidgets;

namespace ThoraxLab.Learning
{
    public sealed partial class LabUI
    {
        [Serializable] sealed class PlayGuide { public GuidePage[] pages, prompts; }
        [Serializable] sealed class GuidePage { public string id,title,art,text; }
        PlayGuide playGuide;
        int guideIndex;
        bool resumeLessonAfterGuide;
        LessonNarrator guideNarrator;
        RectTransform guideSurface;
        public bool GuideOpen => guideSurface && modal==guideSurface;
        PlayGuide Guide => playGuide ??= JsonUtility.FromJson<PlayGuide>(Resources.Load<TextAsset>("Learning/how-to-play").text);
        public string PromptText(string id) => Array.Find(Guide.prompts, prompt => prompt.id == id).text;

        public void ShowGuide()
        {
            if(modal)return;
            if(app.Session.busy){app.Message("Wait for the current cut to finish, then open Guide.");return;}
            resumeLessonAfterGuide=app.Narrator.HasPlayback && !app.Narrator.Paused;
            if(resumeLessonAfterGuide)app.Narrator.TogglePause();
            if(!guideNarrator)guideNarrator=gameObject.AddComponent<LessonNarrator>();
            RenderGuide();
        }

        void RenderGuide()
        {
            if(guideSurface)DismissModal();
            modal=guideSurface=ModalSurface("How to play guide",CloseGuide);Fill(modal,8);
            float controlHeight=ControlHeight;
            PinnedModalBack("Close guide",CloseGuide,"Close");
            var bottom=Rect("Guide page controls",modal);bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);bottom.pivot=new Vector2(.5f,0);bottom.sizeDelta=new Vector2(-32,controlHeight);bottom.anchoredPosition=new Vector2(0,16);
            var controls=Row(bottom);controls.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childForceExpandWidth=false;Fill(controls);
            Button(controls,"Previous",()=>ChangeGuidePage(-1),false,guideIndex>0);
            var pageNumber=Text(controls,(guideIndex+1)+" / "+Guide.pages.Length,22,Muted);pageNumber.alignment=TextAlignmentOptions.Center;Size(pageNumber,width:72*TextScale);
            Button(controls,"Next",()=>ChangeGuidePage(1),true,guideIndex<Guide.pages.Length-1);
            var area=Rect("Guide body",modal);Fill(area);area.offsetMin=new Vector2(8,controlHeight+40);area.offsetMax=new Vector2(-8,-BackHeight-32);
            var column=Scroll(area);var page=Guide.pages[guideIndex];
            Text(column,page.title,31).fontStyle=FontStyles.Bold;
            LabUIVisuals.Hero(column,page.art,140);
            Text(column,page.text,23);
            Button(column,"Pause / resume guide voice",guideNarrator.TogglePause);
            Button(column,"Replay guide voice",guideNarrator.Replay);
            Text(column,"Female synthetic learning guide",18,Muted);
            guideNarrator.Play(page.id,page.title+". "+page.text);
        }
        void ChangeGuidePage(int delta)
        {
            guideIndex=Mathf.Clamp(guideIndex+delta,0,Guide.pages.Length-1);RenderGuide();
        }
        public void CloseGuide()
        {
            if(!GuideOpen)return;
            guideNarrator.Stop();DismissModal();guideSurface=null;
            if(resumeLessonAfterGuide&&app.Narrator.Paused)app.Narrator.TogglePause();
            resumeLessonAfterGuide=false;
        }
    }
}
