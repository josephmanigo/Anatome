#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        void CheckBackLayout(Button button,string scenario)
        {
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var label=button.GetComponentInChildren<TMP_Text>();label.ForceMeshUpdate();
            var icon=button.GetComponentInChildren<LabIcon>();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(app.UI.Root,rect);
            Require("back/layout/"+scenario,rect.rect.width>=104&&rect.rect.width<=app.UI.Root.rect.width*.35f
                &&rect.rect.height>=56&&rect.rect.height<LabWidgets.ControlHeight&&!button.GetComponentInParent<ScrollRect>()
                &&app.UI.Root.rect.Contains(bounds.min)&&app.UI.Root.rect.Contains(bounds.max),
                button.name+" stays on the panel, outside scrolling content. Controller target "+rect.rect.size+" canvas units.");
            Require("back/text/"+scenario,icon&&icon.glyph==LabGlyph.Back&&icon.rectTransform.GetSiblingIndex()<label.rectTransform.GetSiblingIndex()
                &&label.textInfo.lineCount==1&&label.preferredWidth<=label.rectTransform.rect.width+1
                &&(!LabWidgets.HighContrast||(label.color==Color.white&&icon.color==Color.white)),
                "The leading arrow and complete one-line label fit at "+LabWidgets.TextScale+" text scale. Label "+label.text+".");
        }

        IEnumerable<int> VerifyBackNavigation()
        {
            var settings=app.Settings.Current;float savedScale=settings.textScale;bool savedContrast=settings.highContrast;
            app.UI.ShowSignIn();yield return 4;
            string savedEmail=app.UI.Root.GetComponentInChildren<TMP_InputField>().text;
            try
            {
                foreach(float scale in new[]{1f,1.5f})
                {
                    string mode=scale==1?"normal":"large-high-contrast";
                    settings.textScale=LabWidgets.TextScale=scale;settings.highContrast=LabWidgets.HighContrast=scale>1;
                    app.UI.ShowSignIn();yield return 4;
                    foreach(int frames in GuideClick("Before you begin"))yield return frames;
                    var back=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Back");
                    CheckBackLayout(back,"page/"+mode);
                    RestHandsForCaseCapture();yield return 4;
                    CaptureWorldInterface("panel-back/page-"+mode+".png");
                    var content=app.UI.Root.GetComponentInChildren<ScrollRect>();
                    var pinned=back.transform.position;content.verticalNormalizedPosition=0;yield return 4;
                    Require("back/page_pinned/"+mode,content.content.rect.height>content.viewport.rect.height
                        &&Vector3.Distance(pinned,back.transform.position)<.0001f,"Back stays fixed when the reading panel scrolls to its last line.");
                    var idle=back.targetGraphic.canvasRenderer.GetColor();
                    AimAtControl((RectTransform)back.transform);yield return 5;
                    var hovered=back.targetGraphic.canvasRenderer.GetColor();
                    Require("back/hover/"+mode,idle!=hovered&&back.colors.pressedColor!=back.colors.highlightedColor&&back.colors.fadeDuration==0,
                        "Ray hover changes the rendered face immediately; pressing has its own tint.");
                    var panelPosition=app.UI.Root.position;Grip(true);yield return 3;
                    SetHandWorld(hand.transform.position+Camera.main.transform.right*.035f,hand.transform.rotation);yield return 3;
                    Require("back/grip_keeps_panel_still/"+mode,Vector3.Distance(panelPosition,app.UI.Root.position)<.001f&&app.UI.ScreenName=="Getting started",
                        "Gripping the Back control does not move the panel or navigate.");
                    Grip(false);yield return 3;AimAtControl((RectTransform)back.transform);yield return 4;
                    int calls=0;back.onClick.AddListener(()=>calls++);
                    Trigger(true);yield return 2;
                    Require("back/press/"+mode,calls==1&&app.UI.ScreenName=="Sign in"&&LearningXRInput.Read(XRNode.RightHand).trigger,
                        "Trigger press returns to the originating page while the trigger remains down.");
                    yield return 6;Trigger(false);yield return 4;
                    Require("back/one_page/"+mode,calls==1&&app.UI.ScreenName=="Sign in"&&!app.UI.HasModal,
                        "Holding and releasing the trigger cannot replay Back on the destination.");

                    var field=app.UI.Root.GetComponentInChildren<TMP_InputField>();field.text="learner@example.org";
                    AimAtControl((RectTransform)field.transform);yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 4;
                    var keyboard=app.UI.Root.Find("VR keyboard");
                    back=keyboard.GetComponentsInChildren<Button>().Single(b=>b.name=="Back");
                    CheckBackLayout(back,"keyboard/"+mode);
                    var preview=keyboard.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Visible input");
                    var title=keyboard.GetComponentsInChildren<TMP_Text>().Single(t=>t.text==field.name);
                    var backBox=RectTransformUtility.CalculateRelativeRectTransformBounds(keyboard,(RectTransform)back.transform);
                    var titleBox=RectTransformUtility.CalculateRelativeRectTransformBounds(keyboard,title.rectTransform);
                    var previewPosition=preview.transform.position;pinned=back.transform.position;
                    keyboard.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition=0;yield return 4;
                    Require("back/keyboard_pinned/"+mode,backBox.max.x+3<titleBox.min.x&&preview.text.Contains(field.text)&&!preview.GetComponentInParent<ScrollRect>()
                        &&Vector3.Distance(pinned,back.transform.position)<.0001f&&Vector3.Distance(previewPosition,preview.transform.position)<.0001f,
                        "The field heading clears Back. Scrolling keys leaves Back and the typed address fixed.");
                    RestHandsForCaseCapture();yield return 4;CaptureWorldInterface("panel-back/keyboard-"+mode+".png");
                    foreach(int frames in GuideClick("Back"))yield return frames;
                    Require("back/keyboard_retains_input/"+mode,!app.UI.HasModal&&app.UI.ScreenName=="Sign in"&&field.text=="learner@example.org",
                        "Closing the keyboard preserves edits without sending a code.");
                }

                app.UI.ShowUnits(LearningMode.IdentifyEnumerate);yield return 3;
                var unit=app.Catalog.units.First(u=>u.id=="chest-wall");
                app.UI.ShowUnit(unit);yield return 4;
                foreach(int frames in GuideClick("Back to units"))yield return frames;
                Require("back/unit_parent",app.UI.ScreenName=="Units","The unit page returns to its mode's units.");
                foreach(int frames in GuideClick("Back to activities"))yield return frames;
                Require("back/mode_parent",app.UI.ScreenName=="Modes","The unit list returns one level to the activity choices.");
                var scroll=app.UI.Root.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return 4;
                float position=scroll.verticalNormalizedPosition;
                foreach(int frames in GuideClick("Guide"))yield return frames;
                var guide=app.UI.Root.Find("How to play guide");
                CheckBackLayout(guide.GetComponentsInChildren<Button>().Single(b=>b.name=="Close guide"),"guide");
                foreach(int frames in GuideClick("Close guide"))yield return frames;
                Require("back/guide_underlying_page",!app.UI.HasModal&&app.UI.ScreenName=="Modes"&&Mathf.Abs(scroll.verticalNormalizedPosition-position)<.02f,
                    "Closing the guide returns to the same page and scroll position.");
                bool accepted=false;
                app.UI.Confirm("Leave this page?","Dismiss this confirmation to keep reading.","Confirm probe",()=>accepted=true);yield return 4;
                var dialog=app.UI.Root.Find("Confirmation");
                CheckBackLayout(dialog.GetComponentsInChildren<Button>().Single(b=>b.name=="Cancel"),"confirmation");
                foreach(int frames in GuideClick("Cancel"))yield return frames;
                Require("back/cancel_preserves_page",!accepted&&!app.UI.HasModal&&app.UI.ScreenName=="Modes",
                    "Cancel dismisses the active confirmation without activating the action below.");
                app.UI.ShowSignIn();yield return 4;
                var email=app.UI.Root.GetComponentInChildren<TMP_InputField>();email.Select();yield return 4;
                Menu(true);yield return 3;Menu(false);yield return 3;
                Require("back/menu_dismisses_keyboard",!app.UI.HasModal&&app.UI.ScreenName=="Sign in"&&email.text=="learner@example.org",
                    "The controller Menu control dismisses the keyboard and retains the field.");
                Menu(true);yield return 3;Menu(false);yield return 3;
                Require("back/menu_opens",app.UI.ScreenName=="Menu","The next separate Menu press opens the panel menu.");
                Menu(true);yield return 3;Menu(false);yield return 3;
                Require("back/menu_closes",app.UI.ScreenName==(app.Preview||app.Account.UserId!=null?"Career":"Sign in"),
                    "A second separate Menu press leaves the pause menu outside an activity.");
            }
            finally
            {
                settings.textScale=LabWidgets.TextScale=savedScale;settings.highContrast=LabWidgets.HighContrast=savedContrast;
                app.UI.ShowSignIn();app.UI.Root.GetComponentInChildren<TMP_InputField>().text=savedEmail;
            }
        }
    }
}
#endif
