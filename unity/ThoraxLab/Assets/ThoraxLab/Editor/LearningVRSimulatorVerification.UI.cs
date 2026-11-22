#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyButtonResponse()
        {
            var originalHead=headState;var originalLeft=leftState;var originalRight=rightState;
            app.UI.ShowSignIn();yield return 5;
            int callbacks=0,activatedFrame=-1;
            app.UI.Confirm("Button response check","A press must respond without waiting for trigger release.","Response probe",()=>{callbacks++;activatedFrame=Time.frameCount;});yield return 5;
            AimAtButton("Response probe");yield return 5;
            int queuedFrame=Time.frameCount;Trigger(true);yield return 2;
            Require("button_responds_on_press",callbacks==1&&activatedFrame<=queuedFrame+1&&LearningXRInput.Read(XRNode.RightHand).trigger,
                "A queued controller press invokes the actual confirmation by the next input frame while still held. Callbacks="+callbacks+"; frames="+(activatedFrame<0?-1:activatedFrame-queuedFrame));
            var away=new Ray(Camera.main.transform.position,-app.UI.Root.forward);
            Require("ui_press_captured_until_release",app.UI.RayPointer(away,false,held:true,pointerId:(int)XRNode.RightHand+1),"Moving off the dismissed button while held cannot leak its trigger into anatomy or cutting.");
            yield return 12;
            Require("held_button_does_not_repeat",callbacks==1&&!app.UI.HasModal&&app.UI.ScreenName=="Sign in","Holding through modal dismissal must not repeat or activate the underlying page.");
            Trigger(false);yield return 3;
            Require("button_release_does_not_repeat",callbacks==1&&app.UI.ScreenName=="Sign in","Release after immediate activation must not send another click.");
            Require("ui_release_restores_world_input",!app.UI.RayPointer(away,false,held:false,pointerId:(int)XRNode.RightHand+1),"Release ends UI capture so the next anatomy action remains available.");
            AimAtButton("Menu");yield return 4;Trigger(true);yield return 2;
            Require("navigation_responds_on_press",app.UI.ScreenName=="Menu"&&LearningXRInput.Read(XRNode.RightHand).trigger,"Ordinary page navigation responds while the trigger remains held.");
            yield return 8;Trigger(false);yield return 3;
            Require("navigation_hold_does_not_replay",app.UI.ScreenName=="Menu","Replacing a page during the press cannot replay its click.");
            app.UI.ShowSignIn();yield return 5;
            Require("button_feedback_has_no_fade",app.UI.Root.GetComponentsInChildren<Button>().All(b=>b.colors.fadeDuration==0),"Buttons change visual feedback immediately.");
            app.UI.Confirm("Disabled button check","Disabled controls cannot run actions.","Disabled probe",()=>callbacks++);yield return 5;
            AimAtButton("Disabled probe");yield return 4;
            app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Disabled probe").interactable=false;
            Trigger(true);yield return 3;Trigger(false);yield return 3;
            Require("disabled_button_does_not_activate",callbacks==1&&app.UI.HasModal,"Immediate activation preserves disabled-control protection.");
            InvokeUI("ShowModes");yield return 5;
            Require("card_feedback_has_no_fade",app.UI.ScreenName=="Modes"&&app.UI.Root.GetComponentsInChildren<Button>().All(b=>b.colors.fadeDuration==0),"All four activity cards also give immediate visual feedback.");
            foreach(int frames in VerifyBackNavigation())yield return frames;
            app.UI.ShowSignIn();headState=originalHead;leftState=originalLeft;rightState=originalRight;QueueAll();yield return 5;
        }

        void VerifyNarrationStartup()
        {
            var settings=app.Settings.Current;float volume=settings.narrationVolume,speed=settings.narrationSpeed;
            try
            {
                // Change only in-memory preferences; even a failing regression must not persist mute.
                settings.narrationVolume=0;settings.narrationSpeed=.8f;
                app.UI.ShowGuide();var narrator=typeof(LabUI).GetField("guideNarrator",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI) as LessonNarrator;
                var source=typeof(LessonNarrator).GetField("source",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(narrator) as AudioSource;
                Require("guide_respects_mute_from_first_sample",Mathf.Approximately(source.volume,0)&&Mathf.Approximately(source.pitch,.8f),"A newly opened guide applies saved mute and speed before starting playback. Volume="+source.volume+"; pitch="+source.pitch);
            }
            finally{app.UI.CloseGuide();settings.narrationVolume=volume;settings.narrationSpeed=speed;}
        }
        void VerifyAnatomyInventory()
        {
            var catalog=JsonUtility.FromJson<AnatomyCatalog>(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ThoraxLab/anatomy-catalog.json").text);
            var originals=app.Session.originals.Where(s=>s&&!s.fragment).ToArray();
            Require("inventory_exact_scene_membership",originals.Length==catalog.records.Length&&originals.Select(s=>s.record.id).Distinct().Count()==catalog.records.Length,"Each catalog record has exactly one intact scene structure, including reference annotations.");
            foreach(var record in catalog.records)
            {
                var structure=originals.SingleOrDefault(s=>s.record.id==record.id);var mesh=structure?structure.Filter?.sharedMesh:null;
                Require("inventory_mesh/"+record.id,mesh&&mesh.vertexCount>2&&mesh.subMeshCount>0&&mesh.GetIndexCount(0)>=3&&mesh.bounds.size.sqrMagnitude>0,"Catalog structure has nonempty mesh geometry: "+record.name);
                Require("inventory_material/"+record.id,structure&&structure.Renderer&&structure.Renderer.sharedMaterials.Length>0&&structure.Renderer.sharedMaterials.All(m=>m&&m.shader&&!m.shader.name.Contains("InternalError")),"Every rendered surface resolves a material and shader.");
                if(!record.reference_region)
                    Require("inventory_interaction/"+record.id,app.Structures.ContainsKey(record.id)&&structure.Grab&&structure.GetComponent<MeshCollider>()?.sharedMesh,"Tissue remains available to learning and has a grab component and collision mesh.");
                else Require("inventory_reference_not_tissue/"+record.id,!app.Structures.ContainsKey(record.id)&&!record.cutting_ready,"Reference markers cannot be confused with cuttable tissue.");
            }
        }

        IEnumerable<int> GuideClick(string name)
        {
            AimAtButton(name);yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 5;
        }
        IEnumerable<int> VerifyGuide()
        {
            app.Interrupt();app.UI.ShowSignIn();yield return 5;
            foreach(int frames in GuideClick("Guide"))yield return frames;
            Require("guide_opens_before_sign_in",app.UI.GuideOpen&&app.UI.ScreenName=="Sign in"&&!app.CanInteract,"The guide opens before an account or orientation without replacing the page.");
            var settings=app.Settings.Draft();settings.textScale=1.5f;app.Settings.Apply(settings);app.UI.CloseGuide();app.UI.ShowGuide();yield return 8;
            var narration=typeof(LabUI).GetField("guideNarrator",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI) as LessonNarrator;
            for(int page=0;page<10;page++)
            {
                Require("guide_voice/"+page,narration&&narration.IsPlaying&&!string.IsNullOrEmpty(narration.Caption),"Bundled female guide clip loads and plays with its visible transcript.");
                foreach(int frames in AuditButtons("guide/"+page))yield return frames;
                if(page<9)foreach(int frames in GuideClick("Next"))yield return frames;
            }
            Require("guide_last_page_bounds",!app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Next").interactable,"Last page disables Next.");
            app.SendMessage("OnApplicationFocus",false);yield return 5;
            Require("guide_audio_pauses_on_focus_loss",!narration.IsPlaying,"Guide narration stops while the application loses focus.");
            app.SendMessage("OnApplicationFocus",true);yield return 5;
            Require("guide_audio_resumes_after_focus",narration.IsPlaying,"Automatic focus pause resumes only the previously playing clip.");
            foreach(int frames in GuideClick("Pause / resume guide voice"))yield return frames;
            app.SendMessage("OnApplicationFocus",false);yield return 5;app.SendMessage("OnApplicationFocus",true);yield return 5;
            Require("guide_manual_pause_survives_focus",narration.Paused&&!narration.IsPlaying,"Focus recovery respects a manual narration pause.");
            foreach(int frames in GuideClick("Replay guide voice"))yield return frames;
            Require("guide_replay_restarts_audio",!narration.Paused&&narration.IsPlaying,"Replay starts the guide clip again after a manual pause.");
            foreach(int frames in GuideClick("Previous"))yield return frames;
            foreach(int frames in GuideClick("Close guide"))yield return frames;
            Require("guide_closes_to_original_page",!app.UI.HasModal&&app.UI.ScreenName=="Sign in"&&!narration.IsPlaying,"Close stops guide audio and preserves the original screen.");
            foreach(int frames in GuideClick("Guide"))yield return frames;
            Require("guide_reopens_at_saved_page",app.UI.GuideOpen&&app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="9. Finish, review and save"),"Reopening retains the guide page.");
            app.UI.TogglePauseMenu();yield return 5;
            Require("guide_menu_shortcut_closes",!app.UI.GuideOpen&&app.UI.ScreenName=="Sign in","The controller Menu action closes the guide without replacing the underlying page.");
            settings.textScale=1;app.Settings.Apply(settings);
            var variant=app.Catalog.units.First().variants.First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy);
            app.StartActivity(variant,SessionPhase.Practice);yield return 5;
            var attempt=app.Engine.Attempt;int before=attempt.actions.Count;var objective=app.Engine.Current;
            foreach(int frames in GuideClick("Guide"))yield return frames;
            Require("guide_pauses_lesson_and_interaction",app.UI.GuideOpen&&!app.CanInteract&&app.Narrator.Paused&&!app.Narrator.IsPlaying,"Activity audio and anatomy interaction pause while the guide is open.");
            app.Select(app.Structures[objective.targetId]);Grip(true);yield return 5;
            app.UI.CloseGuide();yield return 5;
            Require("guide_held_grip_not_replayed",hand.interactablesSelected.Count==0&&attempt.actions.Count==before,"A held grip across guide dismissal cannot manipulate anatomy or award progress.");
            Grip(false);yield return 5;
            Require("guide_preserves_attempt_and_resumes_audio",ReferenceEquals(attempt,app.Engine.Attempt)&&app.Engine.Current==objective&&attempt.actions.Count==before&&!app.Narrator.Paused&&app.Narrator.IsPlaying,"Close returns to the same objective and resumes the interrupted lesson.");
            app.Narrator.TogglePause();app.UI.ShowGuide();yield return 5;app.UI.CloseGuide();yield return 5;
            Require("guide_preserves_manual_audio_pause",app.Narrator.Paused&&!app.Narrator.IsPlaying,"Opening and closing help does not override the learner's manual pause.");
            app.StartActivity(variant,SessionPhase.Assessment);yield return 5;attempt=app.Engine.Attempt;before=attempt.actions.Count;app.UI.ShowGuide();yield return 5;app.UI.CloseGuide();yield return 5;
            Require("guide_does_not_reveal_assessment_answers",attempt.actions.Count==before&&app.Engine.Attempt.phase==SessionPhase.Assessment&&!app.Narrator.IsPlaying,"Generic controls help does not submit actions, award mastery, or start answer narration.");
            app.Interrupt();app.UI.ShowHub();yield return 4;
        }

        IEnumerable<int> VerifyClinicalReasoningGrab()
        {
            var objective=app.Engine.Current;var reasoning=app.Procedure.Reasoning;
            var tray=typeof(ProcedureWorkbench).GetField("trayRoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.Procedure) as GameObject;
            Require("case_tray_visible_during_choices",tray&&tray.activeInHierarchy,"Case tray stays visible before, during and after tool use.");
            Require("clinical_reasoning_has_physical_choices",objective?.action==ActionKind.ClinicalDecision&&reasoning&&reasoning.Tokens.Count==3,"Case inspection opens three physical mechanism cutaways.");
            var settings=app.Settings.Draft();settings.textScale=1.5f;app.Settings.Apply(settings);yield return 8;
            var labels=reasoning.GetComponentsInChildren<TMPro.TextMeshPro>();foreach(var label in labels)label.ForceMeshUpdate();
            Require("clinical_labels_follow_maximum_text_size",labels.Length==4&&labels.All(l=>Mathf.Approximately(l.fontSize,.24f)&&l.rectTransform.sizeDelta.y>=.134f&&!l.isTextOverflowing),"Maximum text size="+app.Settings.Current.textScale+"; active="+reasoning.gameObject.activeInHierarchy+"; labels="+string.Join(" | ",labels.Select(l=>l.text+" font="+l.fontSize+" height="+l.rectTransform.sizeDelta.y+" overflow="+l.isTextOverflowing+" preferred="+l.preferredHeight)));
            int coachedBefore=app.Engine.Attempt.actions.Count;app.RevealHint();app.Procedure.ShowHint();yield return 4;
            Require("clinical_practice_spatial_hint_without_credit",reasoning.HintVisible&&app.Engine.Attempt.actions.Count==coachedBefore,"Guided practice identifies the correct model and its dock without submitting a decision.");
            foreach(var unit in app.Catalog.units.Where(u=>u.isCase))foreach(var decision in unit.variants[0].objectives.Where(o=>o.action==ActionKind.ClinicalDecision))
            {
                reasoning.Configure(app,decision,reasoning.transform.position);yield return 2;
                foreach(var label in reasoning.GetComponentsInChildren<TMPro.TextMeshPro>()){label.ForceMeshUpdate();Require("clinical_label_fits/"+unit.id+"/"+decision.id+"/"+label.text,!label.isTextOverflowing,"Maximum-size authored clinical choice fits its fixed-width, taller label.");}
            }
            reasoning.Configure(app,objective,reasoning.transform.position);yield return 4;
            var phase=app.Engine.Attempt.phase;app.Engine.Attempt.phase=SessionPhase.Assessment;reasoning.ShowHint();
            Require("clinical_assessment_spatial_hint_refused",!reasoning.HintVisible,"Assessment cannot reveal the correct physical choice.");app.Engine.Attempt.phase=phase;
            settings.textScale=1;app.Settings.Apply(settings);yield return 4;
            var wrong=reasoning.Tokens.Values.First(t=>t.toolId!=objective.toolId);int before=app.Engine.Attempt.actions.Count;
            var position=wrong.transform.position;wrong.transform.position=reasoning.TargetPoint;yield return 8;
            Require("clinical_unheld_token_cannot_answer",app.Engine.Attempt.actions.Count==before,"Teleporting an unheld token into the dock grants no decision evidence.");wrong.transform.position=position;
            foreach(int frames in DropReasoningToken(wrong,reasoning.TargetPoint))yield return frames;
            Require("clinical_wrong_choice_records_rationale",app.Engine.Current==objective&&app.Engine.Attempt.mistakes==1&&!string.IsNullOrEmpty(app.Engine.Feedback),"A real controller grab/drop records an incorrect mechanism and its explanation without completing the objective.");
            var token=reasoning.Tokens[objective.toolId];before=app.Engine.Attempt.actions.Count;
            foreach(int frames in DropReasoningToken(token,token.transform.position+Vector3.up*.06f))yield return frames;
            Require("clinical_release_outside_dock_cannot_answer",app.Engine.Current==objective&&app.Engine.Attempt.actions.Count==before,"A release outside the patient-plan dock cannot answer the decision.");
            var bounds=token.GetComponent<Collider>().bounds;var toward=(bounds.center-Camera.main.transform.position).normalized;var start=bounds.center-toward*(bounds.extents.magnitude+.05f);var rotation=Quaternion.LookRotation(toward,Vector3.up);
            SetHandWorld(start,rotation);yield return 8;Grip(true);yield return 12;
            Require("clinical_token_held_before_focus_loss",token.Held,"Actual XRI controller holds the reasoning model before interruption.");
            app.SendMessage("OnApplicationFocus",false);yield return 8;app.SendMessage("OnApplicationFocus",true);yield return 8;Grip(false);yield return 8;
            Require("clinical_focus_loss_cancels_choice",app.Engine.Current==objective&&app.Engine.Attempt.actions.Count==before,"A grip interrupted by focus loss cannot submit a reasoning choice.");
            foreach(int frames in DropReasoningToken(token,reasoning.TargetPoint+Vector3.right*.10f))yield return frames;
            Require("clinical_correct_drop_advances_case",app.Engine.IsComplete(objective.id)&&app.Engine.Current?.toolId=="probe","A new actual grip and release of the correct model advances to patient-plan review.");
            Require("case_tray_visible_after_choice",tray&&tray.activeInHierarchy,"Accepting a choice retains the instrument tray.");
        }
        IEnumerable<int> DropReasoningToken(ProcedureTool token,Vector3 destination)
        {
            Physics.SyncTransforms();var bounds=token.GetComponent<Collider>().bounds;
            var toward=(bounds.center-Camera.main.transform.position).normalized;var start=bounds.center-toward*(bounds.extents.magnitude+.055f);var rotation=Quaternion.LookRotation(toward,Vector3.up);
            SetHandWorld(start,rotation);yield return 8;Grip(true);yield return 12;
            Require("clinical_grip/"+token.toolId+"/"+report.checks.Count,token.Held&&hand.interactablesSelected.Any(x=>ReferenceEquals(x,token.grab)),"Queued controller grip acquires the actual decision token.");
            Require("clinical_stable_grab/"+report.checks.Count,!token.grab.snapToColliderVolume&&token.transform.parent==app.Procedure.Reasoning.transform,"Decision pickup preserves its pose and workbench parent.");
            var displacement=destination-token.transform.position;SetHandWorld(start+displacement,rotation);yield return 12;
            Require("clinical_move/"+token.toolId+"/"+report.checks.Count,Vector3.Distance(token.transform.position,destination)<.01f,"Held token follows the controller into the requested world position.");
            Grip(false);yield return 12;
        }
        IEnumerable<int> VerifyWidgetInput()
        {
            app.UI.ShowSettings();
            typeof(LabUI).GetField("settingCategory",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app.UI,"Audio");InvokeUI("RenderSettingsPage");yield return 8;
            var slider=app.UI.Root.GetComponentsInChildren<Slider>().First();var rect=(RectTransform)slider.transform;
            var origin=Camera.main.transform.position+Camera.main.transform.right*.04f;
            var a=rect.TransformPoint(new Vector3(rect.rect.xMin+rect.rect.width*.2f,0,0));var b=rect.TransformPoint(new Vector3(rect.rect.xMin+rect.rect.width*.8f,0,0));
            SetHandWorld(origin,Quaternion.LookRotation(a-origin,Vector3.up));yield return 5;
            Trigger(true);yield return 4;float before=slider.value;
            SetHandWorld(origin,Quaternion.LookRotation(b-origin,Vector3.up));yield return 5;Trigger(false);yield return 5;
            Require("controller_slider_drag_changes_value",slider.value-before>(slider.maxValue-slider.minValue)*.3f,"Actual controller trigger-drag changes a visible slider. Before="+before+"; after="+slider.value);
            float after=slider.value;SetHandWorld(origin,Quaternion.LookRotation(a-origin,Vector3.up));yield return 5;
            Require("controller_slider_release_stops_drag",Mathf.Approximately(after,slider.value),"Pointing after trigger release cannot continue changing a slider.");
            var scroll=slider.GetComponentInParent<ScrollRect>();float scrollBefore=scroll.verticalNormalizedPosition;
            rightState.primary2DAxis=Vector2.down;QueueAll();yield return 8;rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 5;
            Require("controller_thumbstick_scrolls_panel",scroll.verticalNormalizedPosition<scrollBefore,"Controller thumbstick scrolling moves the actual content. Before="+scrollBefore+"; after="+scroll.verticalNormalizedPosition);
            scroll.StopMovement();scroll.verticalNormalizedPosition=.5f;Canvas.ForceUpdateCanvases();
            var blank=scroll.viewport.TransformPoint(new Vector3(scroll.viewport.rect.xMin+6,scroll.viewport.rect.center.y,0));
            SetHandWorld(origin,Quaternion.LookRotation(blank-origin,Vector3.up));yield return 4;
            float dragBefore=scroll.content.anchoredPosition.y;Trigger(true);yield return 3;
            SetHandWorld(origin,Quaternion.LookRotation(blank+app.UI.Root.up*.02f-origin,Vector3.up));yield return 3;
            SetHandWorld(origin,Quaternion.LookRotation(blank+app.UI.Root.up*.07f-origin,Vector3.up));yield return 5;Trigger(false);yield return 3;
            Require("controller_empty_area_drag_scrolls",Mathf.Abs(scroll.content.anchoredPosition.y-dragBefore)>10,"Dragging the empty panel margin still scrolls content. Before="+dragBefore+"; after="+scroll.content.anchoredPosition.y);
            app.UI.ShowSignIn();yield return 5;
        }

        IEnumerable<int> VerifyFocusAndMonitor()
        {
            var monitor=FindFirstObjectByType<LearningPerformanceMonitor>();
            while(monitor.Capture().frames==0)yield return 5;
            int before=monitor.Capture().frames;
            monitor.SendMessage("OnApplicationPause",true);yield return 5;
            Require("performance_pause_stops_sampling",monitor.Capture().frames==before,"Paused frames cannot enter the session performance sample.");
            var path=System.IO.Path.Combine(Application.persistentDataPath,"learning-performance.json");
            var saved=JsonUtility.FromJson<LearningPerformanceMonitor.Measurement>(System.IO.File.ReadAllText(path));
            Require("performance_pause_persists_measurement",saved.frames==before,"Android pause persists the measured session before the OS can terminate the app.");
            monitor.SendMessage("OnApplicationPause",false);yield return 5;
            Require("performance_resume_samples_again",monitor.Capture().frames>before,"Sampling resumes after skipping the pause-transition frame.");
            AimAtButton("Menu");yield return 5;
            var menu=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Menu");
            var probe=menu.gameObject.AddComponent<LearningVRPointerEventProbe>();
            app.SendMessage("OnApplicationFocus",false);Trigger(true);yield return 5;
            Require("lost_focus_blocks_ui",!app.Focused&&probe.pointerDowns==0&&app.UI.ScreenName=="Sign in",
                "A still-tracked controller cannot activate UI after application focus is lost. Screen="+app.UI.ScreenName+"; pointer-downs="+probe.pointerDowns);
            app.SendMessage("OnApplicationFocus",true);yield return 5;Trigger(false);yield return 5;
            Require("focus_recovery_requires_fresh_press",probe.pointerDowns==0&&app.UI.ScreenName=="Sign in","A trigger held across focus recovery cannot become a new click on release.");
            Trigger(true);yield return 4;Trigger(false);yield return 5;
            Require("focus_recovery_accepts_fresh_click",app.UI.ScreenName=="Menu","A released then newly pressed trigger restores normal menu interaction.");
            Destroy(probe);app.UI.ShowSignIn();yield return 5;
        }

        IEnumerable<int> VerifyOpenXRController()
        {
            var controller=InputSystem.AddDevice<OculusTouchControllerProfile.OculusTouchController>();
            InputSystem.SetDeviceUsage(controller,"RightHand");
            var gripPosition=new Vector3(.2f,1.1f,-.5f);
            var aimPosition=gripPosition+Vector3.forward*.04f;
            var gripRotation=Quaternion.Euler(25,0,0);var aimRotation=Quaternion.identity;
            void Sample(bool trigger)
            {
                using(StateEvent.From(controller,out var inputEvent))
                {
                    controller.isTracked.WriteValueIntoEvent(1f,inputEvent);
                    controller.devicePosition.WriteValueIntoEvent(gripPosition,inputEvent);
                    controller.deviceRotation.WriteValueIntoEvent(gripRotation,inputEvent);
                    controller.pointerPosition.WriteValueIntoEvent(aimPosition,inputEvent);
                    controller.pointerRotation.WriteValueIntoEvent(aimRotation,inputEvent);
                    controller.trigger.WriteValueIntoEvent(trigger?1f:0f,inputEvent);
                    controller.triggerPressed.WriteValueIntoEvent(trigger?1f:0f,inputEvent);
                    controller.thumbstick.WriteValueIntoEvent(new Vector2(.35f,-.8f),inputEvent);
                    InputSystem.QueueEvent(inputEvent);
                }
            }
            try
            {
                Sample(false);yield return 8;
                var state=LearningXRInput.Read(XRNode.RightHand);
                Require("openxr_touch_grip_and_aim_separate",state.tracked&&state.source==XRInteractionSource.Controller&&state.separateAim&&
                    Vector3.Distance(state.position,gripPosition)<.001f&&Vector3.Distance(state.rayPosition,aimPosition)<.001f&&
                    Quaternion.Angle(state.rotation,gripRotation)<.1f&&Quaternion.Angle(state.rayRotation,aimRotation)<.1f,
                    "Synthetic events in Unity's actual OpenXR Touch layout retain grip for manipulation and independent aim for pointing.");
                Sample(true);yield return 8;
                Require("openxr_touch_trigger_reaches_input",LearningXRInput.Read(XRNode.RightHand).trigger,
                    "The physical OpenXR control name triggerPressed reaches the shipping input reader.");
                Require("openxr_touch_right_stick_reaches_input",Vector2.Distance(LearningXRInput.Read(XRNode.RightHand).axis,new Vector2(.35f,-.8f))<.001f,
                    "The real Oculus Touch layout routes both thumbstick axes to the shipping movement reader.");
                InputSystem.SetDeviceUsage(controller,"LeftHand");Sample(false);yield return 8;
                Require("openxr_touch_left_stick_reaches_input",Vector2.Distance(LearningXRInput.Read(XRNode.LeftHand).axis,new Vector2(.35f,-.8f))<.001f,
                    "The default movement hand uses the same verified Oculus Touch thumbstick binding.");
            }
            finally{InputSystem.RemoveDevice(controller);}
            SetAnalog(ref rightState,0,0);QueueAll();yield return 8;
        }

        IEnumerable<int> VerifyUIAudit()
        {
            app.Interrupt();app.UI.ShowHub();yield return 4;
            foreach(int frames in AuditButtons("career"))yield return frames;
            InvokeUI("ShowPauseMenu");yield return 4;
            foreach(int frames in AuditButtons("menu"))yield return frames;
            app.UI.ShowSettings();yield return 4;
            var category=typeof(LabUI).GetField("settingCategory",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(string name in new[]{"Quick setup","VR comfort","Audio","Controls","Graphics & display","Learning & accessibility","Profiles","Account & saves"})
            {
                category.SetValue(app.UI,name);InvokeUI("RenderSettingsPage");yield return 4;
                foreach(int frames in AuditButtons("settings/"+name))yield return frames;
            }
            InvokeUI("ShowSettingsCategories");yield return 4;
            foreach(int frames in AuditButtons("settings/categories"))yield return frames;
            InvokeUI("ShowModes");yield return 4;
            foreach(int frames in AuditButtons("modes"))yield return frames;
            foreach(LearningMode mode in Enum.GetValues(typeof(LearningMode)))
            {
                app.UI.ShowUnits(mode);yield return 4;
                foreach(int frames in AuditButtons("units/"+mode))yield return frames;
                foreach(var unit in app.Catalog.units.Where(u=>u.isCase==(mode==LearningMode.AnatomyCases)))
                {
                    app.UI.ShowUnit(unit);yield return 4;
                    foreach(int frames in AuditButtons("unit/"+mode+"/"+unit.id))yield return frames;
                }
                InvokeUI("ShowDifficultyChoice");yield return 4;
                foreach(int frames in AuditButtons("difficulty/"+mode))yield return frames;
                var example=app.Catalog.units.First(u=>u.isCase==(mode==LearningMode.AnatomyCases));
                var variant=example.variants.First(v=>v.mode==mode&&v.difficulty==Difficulty.Easy);
                app.UI.ShowUnit(example);yield return 4;
                InvokeUI("ShowPhaseChoice",variant);yield return 4;
                foreach(int frames in AuditButtons("phase/"+mode))yield return frames;
                InvokeUI("ShowReferences",example);yield return 4;
                foreach(int frames in AuditButtons("references/"+mode))yield return frames;
                app.StartActivity(variant,SessionPhase.Practice);yield return 8;
                foreach(int frames in AuditButtons("workspace/"+mode))yield return frames;
                InvokeUI("ShowActivityTools");yield return 4;
                foreach(int frames in AuditButtons("tools/"+mode))yield return frames;
                InvokeUI("ShowTranscript");yield return 4;
                foreach(int frames in AuditButtons("teaching/"+mode))yield return frames;
                app.Finish();yield return 4;
                foreach(int frames in AuditButtons("results/"+mode))yield return frames;
                InvokeUI("ShowAttemptFeedback");yield return 4;
                foreach(int frames in AuditButtons("feedback/"+mode))yield return frames;
                app.Interrupt();
            }
            app.UI.ShowProgress();yield return 4;
            foreach(int frames in AuditButtons("progress"))yield return frames;
            bool confirmed=false;app.UI.Confirm("XR confirmation probe","Confirmations must block the underlying page.","Confirm probe",()=>confirmed=true);yield return 4;
            foreach(int frames in AuditButtons("confirmation"))yield return frames;
            AimAtButton("Confirm probe");yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 4;
            Require("controller_confirmation_callback",confirmed&&!app.UI.HasModal,"The actual trigger press invokes confirmation once and closes the modal.");
            app.UI.ShowSignIn();yield return 4;
            foreach(int frames in AuditButtons("sign-in"))yield return frames;
            var field=app.UI.Root.GetComponentsInChildren<TMPro.TMP_InputField>().First();
            field.Select();yield return 4;
            foreach(int frames in AuditButtons("keyboard"))yield return frames;
            string before=field.text;AimAtButton("q");yield return 3;Trigger(true);yield return 3;Trigger(false);yield return 4;
            Require("controller_keyboard_types",field.text==before+"q","A controller click types one key into the real input field.");
            AimAtButton("Done");yield return 3;Trigger(true);yield return 3;Trigger(false);yield return 4;
            Require("controller_keyboard_done_closes_modal",!app.UI.HasModal,"Done closes the keyboard through actual controller events.");
            AimAtButton("Menu");yield return 4;
            var menu=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Menu");
            var menuTarget=((RectTransform)menu.transform).TransformPoint(((RectTransform)menu.transform).rect.center);
            var leftOrigin=Camera.main.transform.position-Camera.main.transform.right*.04f;
            SetHandWorld(XRNode.LeftHand,leftOrigin,Quaternion.LookRotation(menuTarget-leftOrigin,Vector3.up));yield return 4;
            leftState.trigger=1;leftState=leftState.WithButton(UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.ControllerButton.TriggerButton,true);QueueAll();yield return 4;
            leftState.trigger=0;leftState=leftState.WithButton(UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.ControllerButton.TriggerButton,false);QueueAll();yield return 4;
            Require("left_controller_clicks_ui",app.UI.ScreenName=="Menu","The left controller independently activates the real Menu button.");
            app.UI.ShowHub();yield return 4;
        }

        void InvokeUI(string method,params object[] arguments)=>typeof(LabUI).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app.UI,arguments);

        IEnumerable<int> AuditButtons(string page)
        {
            var modal=app.UI.Root.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Confirmation"||r.name=="VR keyboard"||r.name=="How to play guide");
            var root=modal?modal:app.UI.Root;
            var buttons=root.GetComponentsInChildren<Button>().Where(b=>b.IsInteractable()).ToArray();
            foreach(var button in buttons)
            {
                // Hidden Editor windows can lose OS focus during this long audit.
                // Focus-loss cancellation is verified separately; restore its precondition here.
                app.SendMessage("OnApplicationFocus",true);
                var rect=(RectTransform)button.transform;
                foreach(var scroll in button.GetComponentsInParent<ScrollRect>())
                {
                    Canvas.ForceUpdateCanvases();
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,rect);
                    float offset=bounds.min.y<scroll.viewport.rect.yMin?scroll.viewport.rect.yMin-bounds.min.y+4:
                        bounds.max.y>scroll.viewport.rect.yMax?scroll.viewport.rect.yMax-bounds.max.y-4:0;
                    scroll.StopMovement();scroll.content.anchoredPosition+=Vector2.up*offset;
                }
                yield return 3;Canvas.ForceUpdateCanvases();
                var target=rect.TransformPoint(rect.rect.center);var origin=Camera.main.transform.position;
                var rotation=Quaternion.LookRotation(target-origin,Vector3.up);
                SetHandWorld(origin+Camera.main.transform.right*.04f,Quaternion.LookRotation(target-(origin+Camera.main.transform.right*.04f),Vector3.up));yield return 3;
                Require("ui_pose/"+page+"/"+button.name,app.Focused&&Vector3.Angle(hand.transform.forward,target-hand.transform.position)<.5f,
                    "The focused simulator must apply its queued controller pose before a click is audited.");
                var probe=button.gameObject.AddComponent<LearningVRPointerEventProbe>();
                // Coverage auditing must never run account, network or destructive
                // actions. Observe the real Button event route with a local callback.
                var action=button.onClick;int callbacks=0;
                button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(()=>callbacks++);
                try
                {
                    Trigger(true);yield return 3;
                    Require("ui_target/"+page+"/"+button.name,probe.pointerDowns==1&&probe.clicks==1&&callbacks==1&&probe.clickFrame==probe.pressFrame,
                        "Actual controller press activates this rendered button in the same frame. Target="+button.name+"; callbacks="+callbacks+"; frame delay="+(probe.clickFrame-probe.pressFrame)+"; aimed="+target+"; current="+rect.TransformPoint(rect.rect.center)+"; ray="+hand.transform.forward);
                    app.UI.CancelXRPointer((int)XRNode.RightHand+1);Trigger(false);yield return 3;
                    Require("ui_no_repeat/"+page+"/"+button.name,probe.clicks==1&&callbacks==1,"Cancellation and release cannot repeat an already activated button.");
                }
                finally{button.onClick=action;Destroy(probe);}
            }
        }
    }
}
#endif
