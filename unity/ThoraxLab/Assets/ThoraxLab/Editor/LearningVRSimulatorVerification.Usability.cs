#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyRayScalpel()
        {
            app.StartSandbox();app.Session.saw=false;yield return 12;
            var skin=app.Structures["FMA7163"];Physics.SyncTransforms();
            Require("ray_scalpel_exposed_target",app.Presentation.TryGetExposedSurface(skin,Camera.main.transform.position,out var surface),"The sandbox exposes a real skin surface for reach/cancellation testing; guided regional scoring is tested separately.");
            var forward=(surface-Camera.main.transform.position).normalized;var origin=surface-forward*.70f;var rotation=Quaternion.LookRotation(forward,Vector3.up);
            var tangent=Vector3.Cross(forward,Vector3.up).normalized;
            SetHandWorld(origin,rotation);yield return 6;
            Require("ray_scalpel_reaches_seventy_cm",hand.HasRayCutTarget&&Vector3.Distance(hand.RayCutPoint,surface)<.01f&&Vector3.Distance(hand.transform.position,hand.RayCutPoint)>.65f,"The blade reaches the first surface from 70 cm without extending its cut depth.");
            var strokes=typeof(LabHandInteractor).GetField("stroke",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hand) as List<float>;
            var before=skin.Filter.sharedMesh;
            Trigger(true);yield return 4;
            for(int i=1;i<=6;i++){SetHandWorld(origin,Quaternion.LookRotation(surface+tangent*(.004f*i)-origin,Vector3.up));yield return 3;}
            Require("ray_scalpel_rotation_draws_stroke",strokes.Count>=48&&Vector3.Distance(hand.transform.position,origin)<.001f,"Rotating a stationary controller draws a real cut stroke at the tissue surface.");
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="Editor-only occlusion probe";block.transform.position=hand.transform.position+hand.transform.forward*.30f;block.transform.localScale=Vector3.one*.12f;Physics.SyncTransforms();yield return 5;
            Require("ray_scalpel_occlusion_cancels_stroke",!hand.HasRayCutTarget&&strokes.Count==0,"A non-tissue occluder cancels the pending stroke and cannot be cut through.");
            Trigger(false);yield return 5;
            Require("ray_scalpel_cancel_preserves_mesh",skin.Filter.sharedMesh==before&&!app.Session.busy,"Releasing a cancelled stroke leaves the specimen unchanged.");
            Destroy(block);SetHandWorld(surface-forward*3.2f,rotation);yield return 5;
            Require("ray_scalpel_range_is_bounded",!hand.HasRayCutTarget,"The surface outside the three-metre ray cannot be cut.");
            SetHandWorld(origin,rotation);yield return 5;Trigger(true);yield return 4;
            Menu(true);yield return 5;Menu(false);yield return 3;Menu(true);yield return 5;Menu(false);yield return 3;
            SetHandWorld(origin,Quaternion.LookRotation(surface+tangent*.016f-origin,Vector3.up));yield return 5;
            Require("ray_scalpel_pause_requires_new_press",app.CanInteract&&strokes.Count==0,"A trigger held across menu open/close cannot resume cutting.");
            Trigger(false);yield return 5;
            Require("ray_scalpel_paused_stroke_preserves_mesh",skin.Filter.sharedMesh==before&&!app.Session.busy,"Releasing after a pause cannot submit a stale stroke.");
            SetHandWorld(origin,rotation);yield return 5;Trigger(true);yield return 4;
            for(int i=1;i<=10;i++){SetHandWorld(origin,Quaternion.LookRotation(surface+tangent*(.004f*i)-origin,Vector3.up));yield return 3;}
            Require("ray_scalpel_fresh_stroke_after_cancel",strokes.Count>=48&&app.CanCut,"A released then freshly pressed trigger starts a valid stroke after cancellation.");
            SetHead(headState.devicePosition,headState.deviceRotation,false);QueueAll();yield return 5;
            SetHead(headState.devicePosition,headState.deviceRotation,true);QueueAll();yield return 5;
            Require("ray_scalpel_tracking_recovery_requires_release",strokes.Count==0,"Tracking recovery cannot restart a cut from a held trigger.");
            Trigger(false);yield return 5;
            Require("ray_scalpel_tracking_loss_preserves_mesh",skin.Filter.sharedMesh==before&&!app.Session.busy,"A stroke interrupted by tracking loss is discarded.");
            SetHandWorld(origin,rotation);yield return 5;Trigger(true);yield return 4;
            for(int i=1;i<=10;i++){SetHandWorld(origin,Quaternion.LookRotation(surface+tangent*(.004f*i)-origin,Vector3.up));yield return 3;}
            CaptureWorldInterface("vr-usability/scalpel-ray.png");Trigger(false);yield return 5;
            for(int i=0;i<600&&app.Session.busy;i++)yield return 2;
            Require("ray_scalpel_commits_native_cut",!app.Session.busy&&skin.Filter.sharedMesh!=before,"The actual controller stroke produces a validated native cut. Result="+app.Session.message);
            app.Interrupt();app.UI.ShowHub();yield return 5;
        }

        IEnumerable<int> VerifyVRSignIn()
        {
            var originalHead=headState;var originalLeft=leftState;var originalRight=rightState;
            var emailMember=typeof(LabUI).GetField("email",BindingFlags.Instance|BindingFlags.NonPublic);string originalEmail=(string)emailMember.GetValue(app.UI);
            float originalScale=app.Settings.Current.textScale;
            foreach(float scale in new[]{1f,1.5f}){
                app.Settings.Current.textScale=scale;LabWidgets.TextScale=scale;app.UI.ShowSignIn();yield return 5;
                var field=app.UI.Root.GetComponentsInChildren<TMP_InputField>().Single();field.text="qa@example.org";
                AimAtControl((RectTransform)field.transform);yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 5;
                Require("vr_email_opens_from_controller/"+scale,app.UI.HasModal&&field.shouldHideSoftKeyboard,"A real controller ray opens the in-world keyboard without an Android keyboard covering the email.");
                var preview=app.UI.Root.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Visible input");
                Require("vr_email_preview_is_pinned/"+scale,!preview.GetComponentInParent<ScrollRect>()&&preview.text.Contains("qa@example.org"),"Entered email remains in a dedicated fixed display.");
                var keyScroll=app.UI.Root.GetComponentsInChildren<ScrollRect>().Last();keyScroll.verticalNormalizedPosition=0;yield return 4;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(app.UI.Root,preview.rectTransform);
                Require("vr_email_stays_in_panel_when_keys_scroll/"+scale,app.UI.Root.rect.Contains(new Vector2(bounds.min.x,bounds.min.y))&&app.UI.Root.rect.Contains(new Vector2(bounds.max.x,bounds.max.y)),"Scrolling the keys cannot scroll the email out of the console.");
                foreach(int frames in GuideClick("Left"))yield return frames;
                foreach(int frames in GuideClick("Delete"))yield return frames;
                Require("vr_email_cursor_edits_middle/"+scale,field.text=="qa@example.og","Delete removes the character before the caret, not always the last character.");
                foreach(int frames in GuideClick("r"))yield return frames;
                Require("vr_email_insert_at_caret/"+scale,field.text=="qa@example.org","Typing repairs the address at the selected caret position.");
                CaptureWorldInterface("vr-usability/email-keyboard-"+scale.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png");
                foreach(int frames in GuideClick("Back"))yield return frames;
                Require("vr_keyboard_back_keeps_email/"+scale,!app.UI.HasModal&&field.text=="qa@example.org","Back closes the keyboard and preserves input.");
                app.UI.ShowSignIn();yield return 4;
                Require("vr_email_survives_page_return/"+scale,app.UI.Root.GetComponentInChildren<TMP_InputField>().text=="qa@example.org","Returning to sign-in restores the draft address.");
            }
            var longField=app.UI.Root.GetComponentInChildren<TMP_InputField>();longField.text=new string('a',242)+"@example.org";
            AimAtControl((RectTransform)longField.transform);yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 4;
            var longPreview=app.UI.Root.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Visible input");longPreview.ForceMeshUpdate();
            Require("vr_long_email_window_keeps_caret",longField.text.Length==254&&longPreview.text.EndsWith("|")&&longPreview.preferredWidth<=longPreview.rectTransform.rect.width,"Maximum-length input uses a readable window around the caret.");
            foreach(int frames in GuideClick("q"))yield return frames;
            Require("vr_email_character_limit",longField.text.Length==254,"The keyboard cannot exceed the email character limit.");
            foreach(int frames in GuideClick("Clear"))yield return frames;
            foreach(int frames in GuideClick("Left"))yield return frames;
            foreach(int frames in GuideClick("Delete"))yield return frames;
            Require("vr_empty_email_edit_is_safe",longField.text==""&&longPreview.text=="|","Clear, Left and Delete safely handle empty input.");
            foreach(int frames in GuideClick("Back"))yield return frames;
            longField.text="qa@example.org";
            InvokeUI("ShowCode");yield return 5;
            var code=app.UI.Root.GetComponentInChildren<TMP_InputField>();AimAtControl((RectTransform)code.transform);yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 4;
            Require("vr_code_uses_numeric_keyboard",app.UI.HasModal&&!app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="q"),"The six-digit code uses a numeric keyboard.");
            foreach(char digit in "0123456")foreach(int frames in GuideClick(digit.ToString()))yield return frames;
            Require("vr_code_retains_leading_zero_and_limit",code.text=="012345","The code preserves zero at the start and rejects a seventh digit.");
            foreach(int frames in GuideClick("Done"))yield return frames;
            foreach(int frames in GuideClick("Back to email"))yield return frames;
            Require("vr_code_back_returns_to_email",app.UI.ScreenName=="Sign in"&&app.UI.Root.GetComponentInChildren<TMP_InputField>().text=="qa@example.org","Code entry can return to the editable email without sending another email.");
            app.Settings.Current.textScale=originalScale;LabWidgets.TextScale=originalScale;emailMember.SetValue(app.UI,originalEmail);app.UI.ShowSignIn();
            headState=originalHead;leftState=originalLeft;rightState=originalRight;QueueAll();yield return 5;
        }

        IEnumerable<int> VerifyUsabilityBaseline()
        {
            app.UI.ShowSignIn();yield return 5;
            var field=app.UI.Root.GetComponentsInChildren<TMP_InputField>().Single();field.text="learner@example.org";
            typeof(LabUI).GetMethod("OpenKeyboard",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app.UI,new object[]{field});yield return 5;
            var preview=app.UI.Root.GetComponentsInChildren<TMP_Text>().SingleOrDefault(t=>t.name=="Visible input");
            report.checks.Add(new LearningVRSimulatorVerification.Check{id="email_preview_fixed_above_keys",passed=preview&&preview.text.Contains(field.text)&&!preview.GetComponentInParent<ScrollRect>(),detail="The entered address must remain fixed while keyboard keys scroll."});
            report.checks.Add(new LearningVRSimulatorVerification.Check{id="keyboard_back_available",passed=app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Back"),detail="VR keyboard needs a visible Back action that retains edits."});
            CaptureWorldInterface("vr-usability/email-before.png");app.UI.ShowSignIn();
            foreach(int frames in VerifyInterruptedOrientation())yield return frames;
            var variant=app.Catalog.units.First(u=>u.id=="pneumothorax").variants.First(v=>v.difficulty==Difficulty.Easy);
            app.StartActivity(variant,SessionPhase.Practice);yield return 12;
            var tray=app.Procedure.GetType().GetField("trayRoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.Procedure) as GameObject;
            report.checks.Add(new LearningVRSimulatorVerification.Check{id="case_tray_visible_without_held_tool",passed=tray&&tray.activeInHierarchy,detail="The instrument tray must remain visible throughout the case workspace."});
            var decision=variant.objectives.First(o=>o.action==ActionKind.ClinicalDecision);
            app.Procedure.Reasoning.Configure(app,decision,Vector3.zero);
            var token=app.Procedure.Reasoning.Tokens.Values.First();
            report.checks.Add(new LearningVRSimulatorVerification.Check{id="choice_grab_preserves_pose",passed=!token.grab.snapToColliderVolume&&!token.grab.unparentTransformOnGrab,detail="Far-grab must preserve the token pose and stable workbench parent."});
            report.observations.Add("Baseline records the old behavior; red checks are expected before the usability fixes.");
        }
    }
}
#endif
