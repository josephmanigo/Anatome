#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.UI;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifySchoolPrivacy()
        {
            app.UI.ShowSignIn();yield return 4;
            var before=app.Progress;var session=app.Account.Session;
            var button=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Privacy and shared headsets");button.onClick.Invoke();yield return 4;
            string text=string.Join("\n",app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text));
            Require("school/privacy_explains_data",text.Contains("Learning attempts")&&text.Contains("synchronized")&&text.Contains("Guest practice"),"Before sign-in, learners can read what is stored and choose session-only guest practice.");
            Require("school/privacy_explains_shared_headsets",text.Contains("sign out")&&text.Contains("Trusted account email addresses")&&text.Contains("exported copies"),"Shared-headset identity visibility, sign-out and deletion limits are explained.");
            Require("school/privacy_does_not_change_progress",ReferenceEquals(before,app.Progress)&&ReferenceEquals(session,app.Account.Session)&&!app.Account.Busy,"Reading privacy does not sign in, send a code or change progress.");
            app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Back").onClick.Invoke();yield return 4;
            Require("school/privacy_returns_to_signin",app.UI.Root.GetComponentsInChildren<Button>().Any(b=>b.name=="Send email code"),"Privacy returns to the original sign-in flow.");
        }
        static float refreshRequest;
        static int refreshResult;
        static int RecordRefreshRequest(ulong session,float rate){refreshRequest=rate;return refreshResult;}
        void VerifyRefreshRates()
        {
            var type=typeof(QuestRefreshFeature);const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var feature=ScriptableObject.CreateInstance<QuestRefreshFeature>();var previous=QuestRefreshFeature.Active;
            var active=type.GetProperty("Active");var supported=type.GetProperty("Supported");
            try
            {
                Require("refresh/no_session",!feature.Apply(0),"Automatic refresh waits for an XR session.");
                type.GetField("session",flags).SetValue(feature,1UL);
                var request=type.GetField("request",flags);
                request.SetValue(feature,System.Delegate.CreateDelegate(request.FieldType,typeof(LearningVRSimulatorVerificationRunner).GetMethod(nameof(RecordRefreshRequest),BindingFlags.Static|BindingFlags.NonPublic)));
                supported.SetValue(feature,new[]{90f,72f,120f});refreshResult=0;refreshRequest=0;
                Require("refresh/auto_highest",feature.Apply(0)&&refreshRequest==120&&feature.RequestedRate==120,"Auto requests 120 Hz from an unsorted 72/90/120 Hz runtime list.");
                Require("refresh/manual_supported",feature.Apply(90)&&refreshRequest==90,"A supported manual rate reaches the native request.");
                refreshRequest=0;
                Require("refresh/unsupported_rejected",!feature.Apply(144)&&refreshRequest==0&&feature.RequestedRate==90,"Unsupported rates never reach the native API.");
                refreshResult=-1;
                Require("refresh/runtime_rejection",!feature.Apply(0)&&feature.RequestedRate==90,"A rejected native request retains the last accepted target.");
                refreshResult=0;supported.SetValue(feature,new[]{72f,90f});
                Require("refresh/auto_headset_specific",feature.Apply(0)&&refreshRequest==90,"Auto uses this headset's maximum, without assuming 120 Hz support.");
                active.SetValue(null,feature);app.UI.ShowSettings();
                typeof(LabUI).GetField("settingCategory",flags).SetValue(app.UI,"Graphics & display");
                var draft=(SettingsProfile)typeof(LabUI).GetField("draft",flags).GetValue(app.UI);draft.platform="Quest";draft.refreshRate=0;InvokeUI("RenderSettingsPage");
                var button=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("Headset refresh rate"));
                Require("refresh/ui_auto_label",button.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Auto · highest supported"),"Quest settings expose automatic maximum refresh.");
                button.onClick.Invoke();Require("refresh/ui_manual_choice",draft.refreshRate==72,"The refresh selector still offers runtime-supported manual choices.");
                button.onClick.Invoke();button.onClick.Invoke();Require("refresh/ui_returns_to_auto",draft.refreshRate==0,"Cycling supported rates returns to automatic.");
                supported.SetValue(feature,System.Array.Empty<float>());
                Require("refresh/empty_supported",!feature.Apply(0),"An unavailable rate list has no invented fallback.");
                type.GetMethod("OnSessionDestroy",flags).Invoke(feature,new object[]{1UL});
                Require("refresh/session_cleanup",feature.RequestedRate==0&&!feature.Apply(90),"Session destruction clears its previous refresh target.");
            }
            finally{active.SetValue(null,previous);Destroy(feature);app.UI.ShowSignIn();}
        }
        IEnumerable<int> VerifyGraphicsTransitions()
        {
            VerifyRefreshRates();
            VerifyExtremeRendering();
            var apis=UnityEditor.PlayerSettings.GetGraphicsAPIs(UnityEditor.BuildTarget.Android);
            FacingCheck("graphics/quest_driver_workaround",apis.Length==1&&apis[0]==UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                "Quest 1.4.6 crashed four times in vulkan.adreno.so/vkCreateImageView; compatibility build must select only OpenGLES3. Configured: "+string.Join(",",apis));
            var saved=app.Settings.Draft();
            try
            {
                for(int cycle=0;cycle<3;cycle++)
                foreach(var preset in new[]{GraphicsPreset.High,GraphicsPreset.Extreme,GraphicsPreset.Low,GraphicsPreset.Medium,GraphicsPreset.VeryHigh})
                {
                    var draft=app.Settings.Draft();draft.platform="Quest";draft.SetPreset(preset);
                    // Replay the exact Quest numeric presets through the live renderer; this is not an Adreno hardware test.
                    draft.platform=app.Settings.Platform;app.Settings.Apply(draft);yield return 8;
                    var pipeline=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                    Require("graphics/preset_transition/"+cycle+"/"+preset,pipeline&&Mathf.Approximately(pipeline.renderScale,draft.renderScale)&&pipeline.msaaSampleCount==draft.msaa&&Mathf.Approximately(pipeline.shadowDistance,draft.shadows?draft.shadowDistance:0)&&QualitySettings.globalTextureMipmapLimit==draft.textureLimit,
                        "Active renderer after Apply: scale="+pipeline.renderScale+"; MSAA="+pipeline.msaaSampleCount+"; shadow distance="+pipeline.shadowDistance+"; mip limit="+QualitySettings.globalTextureMipmapLimit);
                }
            }
            finally{app.Settings.Apply(saved);}
            yield return 8;
        }
        void VerifyExtremeRendering()
        {
            var preset = new SettingsProfile { platform = "Quest" };
            preset.SetPreset(GraphicsPreset.Extreme);
            Require("graphics/extreme_fidelity", preset.renderScale == 1.2f && preset.msaa == 4 &&
                preset.textureLimit == 0 && preset.shadows && preset.shadowDistance == 8f,
                "Quest Extreme retains supersampling, full textures, anti-aliasing and shadows.");
            var skin = app.Structures["FMA7163"];
            var meshes = skin.GetComponentsInChildren<MeshRenderer>(true);
            skin.Highlight(false);
            var stored = new MaterialPropertyBlock();
            stored.SetFloat("_Smoothness", .39f);
            meshes[0].SetPropertyBlock(stored);
            try
            {
                skin.Highlight(true);
                Require("graphics/selection_all_lods", meshes.Length == 3 && meshes.All(r => r.HasPropertyBlock()),
                    "The selection tint reaches the full mesh and both display meshes.");
                skin.Highlight(false);
                var restored = new MaterialPropertyBlock();
                meshes[0].GetPropertyBlock(restored);
                Require("graphics/resting_tissue", Mathf.Approximately(restored.GetFloat("_Smoothness"), .39f) &&
                    !restored.HasColor("_BaseColor") && meshes.Skip(1).All(r => !r.HasPropertyBlock()),
                    "Deselecting restores earlier renderer properties and frees resting meshes for batching.");
            }
            finally { meshes[0].SetPropertyBlock(null); }
            var edges = app.UI.Root.GetComponentsInChildren<Image>().Where(i => i.sprite == LabUIVisuals.Outline).ToArray();
            Require("graphics/console_masks", edges.Length > 0 && edges.All(i => !i.fillCenter && !i.raycastTarget) &&
                LabUIVisuals.Rounded.texture == LabUIVisuals.Outline.texture,
                "Rounded controls and outlines share a texture. Outlines skip their transparent center.");

            var tracked = headState;
            bool available = LearningXRInput.HeadTracked;
            try
            {
                SetHead(tracked.devicePosition, tracked.deviceRotation, false);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(hmd, headState);
                UnityEngine.InputSystem.InputSystem.Update();
                Require("graphics/head_sample_invalidated", available && !LearningXRInput.HeadTracked,
                    "A later input update in the same rendered frame reports tracking loss immediately.");
            }
            finally
            {
                headState = tracked;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(hmd, tracked);
                UnityEngine.InputSystem.InputSystem.Update();
            }
            Require("graphics/head_sample_recovered", LearningXRInput.HeadTracked, "The restored pose is available in the same frame.");
        }
        IEnumerable<int> DragSetting(string name,float fraction)
        {
            var slider=app.UI.Root.GetComponentsInChildren<Slider>().Single(s=>s.name==name);
            var rect=(RectTransform)slider.transform;AimAtControl(rect);yield return 6;
            var origin=hand.transform.position;
            Vector3 Point(float value)=>rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin+12,rect.rect.xMax-12,value),0,0));
            SetHandWorld(origin,Quaternion.LookRotation(Point(.5f)-origin,Vector3.up));yield return 4;
            Trigger(true);yield return 4;
            SetHandWorld(origin,Quaternion.LookRotation(Point(fraction)-origin,Vector3.up));yield return 5;
            Trigger(false);yield return 4;
        }
        IEnumerable<int> ApplySettingsButton()
        {
            var apply=app.UI.Root.GetComponentsInChildren<Button>().First(b=>b.name=="Apply");
            AimAtControl((RectTransform)apply.transform);yield return 6;Trigger(true);yield return 4;Trigger(false);yield return 6;
        }
        IEnumerable<int> VerifySettingsApply()
        {
            foreach(int frames in VerifyAstigmatismReadingMode())yield return frames;
            foreach(int frames in VerifyAccessibilityOnboarding())yield return frames;
            var saved=app.Settings.Draft();var category=typeof(LabUI).GetField("settingCategory",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                FindFirstObjectByType<LearningControls>().RecenterTracking();yield return 5;
                app.UI.ShowSettings();category.SetValue(app.UI,"VR comfort");InvokeUI("RenderSettingsPage");yield return 6;
                float eyeBefore=Camera.main.transform.position.y,boostBefore=app.Settings.Current.heightBoost;
                foreach(int frames in DragSetting("Extra player height (m)",.7f))yield return frames;
                foreach(int frames in DragSetting("Table height offset (m)",.85f))yield return frames;
                var draft=(SettingsProfile)typeof(LabUI).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI);
                float boost=draft.heightBoost,table=draft.tableHeight;
                Require("settings_height_draft_changed",boost>.4f&&table>.12f,"Controller-dragged values: player="+boost+" table="+table);
                Require("settings_draft_does_not_apply_early",Mathf.Approximately(app.Settings.Current.heightBoost,boostBefore),"Draft stays separate until Apply.");
                foreach(int frames in ApplySettingsButton())yield return frames;
                Require("settings_apply_height_reaches_profile",Mathf.Approximately(app.Settings.Current.heightBoost,boost)&&Mathf.Approximately(app.Settings.Current.tableHeight,table),"Apply keeps both requested height values.");
                Require("settings_apply_moves_actual_view",Mathf.Abs(Camera.main.transform.position.y-eyeBefore-(boost-boostBefore))<.01f,"Eye before="+eyeBefore+" after="+Camera.main.transform.position.y+" boost="+boost);
                Require("settings_apply_moves_table_and_specimen",Mathf.Abs(app.Session.specimen.position.y-table)<.001f&&Mathf.Abs(app.Session.table.position.y-table)<.001f,"Table and specimen use applied offset.");
                VerifyDrapeOnTable("lab_drape_after_height_adjustment");
                app.UI.ShowSettings();yield return 4;
                draft=(SettingsProfile)typeof(LabUI).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI);
                Require("settings_reopen_preserves_values",Mathf.Approximately(draft.heightBoost,boost)&&Mathf.Approximately(draft.tableHeight,table),"Reopening settings shows the committed values.");
                category.SetValue(app.UI,"Audio");InvokeUI("RenderSettingsPage");yield return 5;
                foreach(int frames in DragSetting("Master volume",.25f))yield return frames;
                float master=draft.masterVolume;
                foreach(int frames in ApplySettingsButton())yield return frames;
                Require("settings_apply_master_volume",master<.4f&&Mathf.Abs(AudioListener.volume-master)<.001f,"Master slider changes the actual audio listener.");
            }
            finally{app.Settings.Apply(saved);category.SetValue(app.UI,"VR comfort");app.UI.ShowSignIn();}
            yield return 6;
        }
        IEnumerable<int> VerifyAstigmatismReadingMode()
        {
            const string mode = "Astigmatism reading mode";
            var saved = app.Settings.Draft();
            var baseline = app.Settings.Draft();
            baseline.textScale = 1f;
            baseline.captionScale = 1.1f;
            baseline.highContrast = false;
            baseline.astigmatismReadingMode = false;
            baseline.readingBrightnessPercent = 80f;
            baseline.vignette = false;
            var category = typeof(LabUI).GetField("settingCategory", BindingFlags.Instance | BindingFlags.NonPublic);
            string previousCategory = (string)category.GetValue(app.UI);
            try
            {
                app.Settings.Apply(baseline);
                app.UI.ShowSignIn(); yield return 4;
                var note = app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().Single(t => t.text.StartsWith("If you have astigmatism"));
                var viewport = note.GetComponentInParent<ScrollRect>().viewport;
                var noteBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, note.rectTransform);
                Require("reading-mode/opening-note", note.text.Contains(mode) && note.text.Contains("VR comfort & text size")
                    && noteBounds.min.y >= viewport.rect.yMin - 1 && noteBounds.max.y <= viewport.rect.yMax + 1,
                    "Before sign-in or scrolling, the note names the reading mode and tells the player where to enable it.");
                AimAtControl(note.rectTransform); yield return 4;
                RestHandsForCaseCapture(); yield return 4;
                CaptureWorldInterface("astigmatism-reading-mode/opening-note.png");
                foreach(int frames in GuideClick("VR comfort & text size"))yield return frames;
                Require("reading-mode/quick-setup", app.UI.Root.GetComponentsInChildren<Button>().Any(b => b.name == "Off   " + mode),
                    "A single reading mode switch is available before sign-in.");
                foreach(int frames in GuideClick("Off   " + mode))yield return frames;
                var sample = app.UI.Root.GetComponentsInChildren<Image>().Single(i => i.name == "Readability preview");
                Require("reading-mode/live-preview", Mathf.Approximately(sample.GetComponentInChildren<TMPro.TMP_Text>().fontSize, 24f * 1.35f)
                    && Mathf.Approximately(LabWidgets.TextScale, 1f), "The larger reading sample previews without applying the draft.");
                var brightness = app.UI.Root.GetComponentsInChildren<Slider>().SingleOrDefault(s => s.name == "Reading brightness (%)");
                Require("reading-mode/brightness-control", brightness && brightness.minValue == 65f && brightness.maxValue == 100f
                    && Mathf.Approximately(brightness.value, 80f), "Reading mode offers a moderate initial dimming level and adjustable brightness.");
                var shade = Camera.main.GetComponent<ComfortVignette>().GetComponentInChildren<MeshRenderer>();
                RestHandsForCaseCapture(); yield return 4;
                brightness.value = 100f; yield return 80;
                CaptureWorldInterface("astigmatism-reading-mode/brightness-100.png");
                Require("reading-mode/full-brightness", !shade.enabled, "At 100% the stationary view returns to full brightness.");
                brightness.value = 65f; yield return 80;
                CaptureWorldInterface("astigmatism-reading-mode/brightness-65.png");
                Require("reading-mode/dim-preview", shade.enabled && Mathf.Approximately(shade.sharedMaterial.GetFloat("_ReadingDim"), .35f)
                    && shade.sortingOrder > app.UI.Root.GetComponent<Canvas>().sortingOrder,
                    "The preview dims the stationary room and its bright console even with the movement vignette disabled.");
                foreach(int frames in GuideClick("Cancel"))yield return frames;
                yield return 80;
                Require("reading-mode/cancel-brightness", !shade.enabled, "Cancelling the preview restores the preceding brightness.");
                foreach(int frames in GuideClick("VR comfort & text size"))yield return frames;
                foreach(int frames in GuideClick("Off   " + mode))yield return frames;
                foreach(int frames in ApplySettingsButton())yield return frames;
                yield return 80;
                Require("reading-mode/saved-brightness", shade.enabled && Mathf.Approximately(shade.sharedMaterial.GetFloat("_ReadingDim"), .2f)
                    && app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.StartsWith("Astigmatism reading mode is on")),
                    "The saved mode dims the actual view and the opening note confirms it is on.");
                var panel = app.UI.Root.GetComponentsInChildren<Image>().Single(i => i.name == "VR console surface");
                Require("reading-mode/applied", app.UI.ScreenName == "Sign in" && Mathf.Approximately(LabWidgets.TextScale, 1.35f)
                    && panel.color.a == 1f && !panel.GetComponent<LabGlassTint>().isActiveAndEnabled && !LabWidgets.HighContrast,
                    "The saved mode enlarges the interface and uses an opaque, flat console with the player's chosen contrast.");
                foreach(int frames in GuideClick("VR comfort & text size"))yield return frames;
                foreach(int frames in GuideClick("Choose a settings category"))yield return frames;
                foreach(int frames in GuideClick("Learning & accessibility"))yield return frames;
                sample = app.UI.Root.GetComponentsInChildren<Image>().Single(i => i.name == "Readability preview");
                var caption = sample.GetComponentsInChildren<TMPro.TMP_Text>().Single(t => t.text.StartsWith("Caption preview"));
                Require("reading-mode/captions", Mathf.Approximately(caption.fontSize, 22f * 1.5f), "The caption preview uses the mode's larger size.");
                foreach(int frames in GuideClick("On   " + mode))yield return frames;
                foreach(int frames in ApplySettingsButton())yield return frames;
                yield return 80;
                panel = app.UI.Root.GetComponentsInChildren<Image>().Single(i => i.name == "VR console surface");
                Require("reading-mode/restore", Mathf.Approximately(LabWidgets.TextScale, 1f) && app.Settings.Current.captionScale == 1.1f
                    && panel.color.a < 1f && panel.GetComponent<LabGlassTint>().isActiveAndEnabled && !shade.enabled,
                    "Turning the mode off from Learning & accessibility restores the original sizes and console finish.");
            }
            finally { app.Settings.Apply(saved); category.SetValue(app.UI, previousCategory); app.UI.ShowSignIn(); }
        }
        IEnumerable<int> VerifyAccessibilityOnboarding()
        {
            var saved=app.Settings.Draft();
            var category=typeof(LabUI).GetField("settingCategory",BindingFlags.Instance|BindingFlags.NonPublic);
            string previousCategory=(string)category.GetValue(app.UI);
            string progress=JsonUtility.ToJson(app.Progress);
            try
            {
                app.UI.ShowSignIn();yield return 4;
                foreach(int frames in GuideClick("Before you begin"))yield return frames;
                string rules=string.Join("\n",app.UI.Root.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text));
                Require("accessibility/rules_before_signin",rules.Contains("80%")&&rules.Contains("essential step")&&rules.Contains("critical safety")&&rules.Contains("Easy → Medium → Hard")&&rules.Contains("same unit and mode")&&rules.Contains("do not unlock levels"),"Opening instructions explain the actual assessment and progression requirements before an account is needed.");
                foreach(int frames in GuideClick("VR comfort & text size"))yield return frames;
                Require("accessibility/early_setup",app.UI.ScreenName=="Settings"&&(string)category.GetValue(app.UI)=="Quick setup","Comfort and reading controls are accessible before the first activity.");
                string before=JsonUtility.ToJson(app.Settings.Current);
                foreach(float scale in new[]{.85f,1.15f,1.5f})
                {
                    app.UI.Root.GetComponentsInChildren<Slider>().Single(s=>s.name=="Text size").value=scale;
                    yield return 4;Canvas.ForceUpdateCanvases();
                    var sample=app.UI.Root.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Readability preview");
                    var labels=sample.GetComponentsInChildren<TMPro.TMP_Text>();
                    foreach(var label in labels)label.ForceMeshUpdate();
                    Require("accessibility/preview_wraps/"+scale,Mathf.Approximately(labels[0].fontSize,24*scale)&&labels.All(t=>t.preferredHeight<=t.rectTransform.rect.height+1&&!t.enableAutoSizing),"The preview uses the chosen size and grows for wrapped text without shrinking it.");
                }
                var contrast=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name.EndsWith("High contrast"));
                contrast.onClick.Invoke();yield return 4;
                var draft=(SettingsProfile)typeof(LabUI).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI);
                var surface=app.UI.Root.GetComponentsInChildren<Image>().Single(i=>i.name=="Readability preview");
                var previewBase=LabWidgets.Ground;
                previewBase.a=.97f;
                Require("accessibility/contrast_preview",surface.color==(draft.highContrast?Color.black:previewBase),"Contrast preview updates before committing preferences.");
                Require("accessibility/preview_is_not_saved",JsonUtility.ToJson(app.Settings.Current)==before,"Previewing size and contrast cannot silently change saved settings.");
                foreach(int frames in GuideClick("Cancel"))yield return frames;
                Require("accessibility/cancel_returns_to_rules",app.UI.ScreenName=="Getting started"&&JsonUtility.ToJson(app.Settings.Current)==before,"Cancel restores the calling screen and retains the saved settings.");
                foreach(int frames in GuideClick("Back"))yield return frames;
                foreach(int frames in GuideClick("VR comfort & text size"))yield return frames;
                app.UI.Root.GetComponentsInChildren<Slider>().Single(s=>s.name=="Text size").value=1.5f;
                app.UI.Root.GetComponentsInChildren<Slider>().Single(s=>s.name=="Stick deadzone").value=.35f;
                foreach(int frames in ApplySettingsButton())yield return frames;
                Require("accessibility/apply_returns_to_signin",app.UI.ScreenName=="Sign in"&&Mathf.Approximately(app.Settings.Current.textScale,1.5f)&&Mathf.Approximately(app.Settings.Current.stickDeadzone,.35f)&&Mathf.Approximately(LabWidgets.TextScale,1.5f),"Apply commits comfort/readability settings and rebuilds the original sign-in screen.");
                Require("accessibility/help_cannot_unlock_levels",JsonUtility.ToJson(app.Progress)==progress,"Reading guidelines and changing preferences cannot complete orientation or award mastery.");
                foreach(int frames in AuditButtons("accessibility/signin-large"))yield return frames;
            }
            finally{app.Settings.Apply(saved);category.SetValue(app.UI,previousCategory);app.UI.ShowSignIn();}
            yield return 4;
        }
        IEnumerable<int> VerifyPanelManipulation()
        {
            app.UI.ShowSignIn();app.UI.RecenterPanel();yield return 5;
            var panel=app.UI.Root;
            var point=panel.TransformPoint(new Vector3(panel.rect.xMin+90,panel.rect.yMax-45,0));
            var origin=Camera.main.transform.position;var rotation=Quaternion.LookRotation(point-origin,Vector3.up);
            SetHandWorld(origin,rotation);yield return 5;var initial=panel.position;Grip(true);yield return 5;
            Require("panel_pickup_has_no_jump",Vector3.Distance(initial,panel.position)<.001f,"Pressing grip preserves the panel's initial pose.");
            var before=panel.position;SetHandWorld(origin+Vector3.right*.12f,rotation);yield return 6;
            Require("panel_grab_header_translates",Vector3.Distance(panel.position,before+Vector3.right*.12f)<.015f,"The top-left header is grabbable, including its blank/brand area. Delta="+(panel.position-before));
            var beforeRotation=panel.rotation;SetHandWorld(origin+Vector3.right*.12f,Quaternion.AngleAxis(25,Vector3.forward)*rotation);yield return 6;
            Require("panel_grab_wrist_rotates",Quaternion.Angle(beforeRotation,panel.rotation)>20,"Panel rotates with the held controller.");
            Grip(false);yield return 4;before=panel.position;beforeRotation=panel.rotation;
            SetHandWorld(origin,rotation);yield return 6;
            Require("panel_release_stays_put",Vector3.Distance(panel.position,before)<.001f&&Quaternion.Angle(beforeRotation,panel.rotation)<.1f,"Release stops movement and rotation.");
            app.Settings.Apply(app.Settings.Draft());yield return 5;
            Require("settings_do_not_reset_custom_panel_pose",Vector3.Distance(panel.position,before)<.001f&&Quaternion.Angle(beforeRotation,panel.rotation)<.1f,"Applying unrelated preferences preserves the panel position and rotation.");
            app.UI.RecenterPanel();yield return 4;
        }
        IEnumerable<int> VerifyPreferenceCategories()
        {
            var saved=app.Settings.Draft();var category=typeof(LabUI).GetField("settingCategory",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                foreach(var name in new[]{"Quick setup","VR comfort","Audio","Controls","Graphics & display","Learning & accessibility"})
                {
                    app.UI.ShowSettings();category.SetValue(app.UI,name);InvokeUI("RenderSettingsPage");yield return 5;
                    // Presets rebuild the page and supply values for its remaining controls.
                    var preset=app.UI.Root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name.StartsWith("Graphics preset"));
                    if(preset){AimAtControl((RectTransform)preset.transform);yield return 5;Trigger(true);yield return 4;Trigger(false);yield return 5;}
                    var controls=app.UI.Root.GetComponentsInChildren<Button>().Where(b=>!b.name.EndsWith("Fullscreen")&&(b.name.StartsWith("On   ")||b.name.StartsWith("Off   ")||new[]{"Movement   ","Turning   ","Texture mip limit   ","Anti-aliasing   "}.Any(prefix=>b.name.StartsWith(prefix)))).Select(b=>b.name).ToArray();
                    foreach(var label in controls)
                    {
                        var button=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name==label);
                        AimAtControl((RectTransform)button.transform);yield return 4;Trigger(true);yield return 3;Trigger(false);yield return 4;
                    }
                    var sliders=app.UI.Root.GetComponentsInChildren<Slider>().Select(s=>s.name).ToArray();
                    foreach(var label in sliders)foreach(int frames in DragSetting(label,.65f))yield return frames;
                    var expected=SettingsService.Clone((SettingsProfile)typeof(LabUI).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI));
                    var apply=app.UI.Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Apply");
                    var applyBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(app.UI.Root,(RectTransform)apply.transform);
                    Require("settings_apply_always_visible/"+name,apply.GetComponentInParent<ScrollRect>()==null&&applyBounds.min.y>=app.UI.Root.rect.yMin&&applyBounds.max.y<=app.UI.Root.rect.yMax,"Apply remains in the fixed footer after editing the last control.");
                    if(name=="VR comfort")CaptureWorldInterface("quest2-cast-20261011/settings-fixed.png");
                    foreach(int frames in ApplySettingsButton())yield return frames;
                    expected.updatedUtc=app.Settings.Current.updatedUtc;
                    Require("settings_category_applies_all_fields/"+name,JsonUtility.ToJson(expected)==JsonUtility.ToJson(app.Settings.Current),"Every edited preference reaches the committed profile through the actual Apply button.");
                    if(name=="Audio")
                    {
                        var source=(AudioSource)typeof(LessonNarrator).GetField("source",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.Narrator);
                        var audio=app.GetComponent<LabAudio>();
                        var effects=(AudioSource)typeof(LabAudio).GetField("effects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                        var ambience=(AudioSource)typeof(LabAudio).GetField("ambience",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                        Require("settings_audio_reaches_all_sources",Mathf.Approximately(AudioListener.volume,expected.masterVolume)&&Mathf.Approximately(source.volume,expected.narrationVolume)&&Mathf.Approximately(source.pitch,expected.narrationSpeed)&&Mathf.Approximately(effects.volume,expected.effectsVolume)&&Mathf.Approximately(ambience.volume,expected.ambienceVolume),"Master, narration volume/speed, effects and ambience update their real audio sources.");
                    }
                    if(name=="Graphics & display")
                    {
                        var pipeline=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
                        var light=GameObject.Find("Specimen examination light").GetComponent<Light>();
                        Require("settings_graphics_reaches_renderer",Mathf.Approximately(pipeline.renderScale,expected.renderScale)&&pipeline.msaaSampleCount==expected.msaa&&Mathf.Approximately(pipeline.shadowDistance,expected.shadows?expected.shadowDistance:0)&&QualitySettings.globalTextureMipmapLimit==expected.textureLimit&&Mathf.Approximately(light.intensity,VRSpecimenPresentation.ExaminationIntensity*expected.brightness),"Scale, MSAA, shadows, texture detail and brightness reach rendering.");
                    }
                    if(name=="Learning & accessibility")Require("settings_accessibility_rebuilds_ui",Mathf.Approximately(LabWidgets.TextScale,expected.EffectiveTextScale)&&LabWidgets.HighContrast==expected.highContrast,"Rebuilt UI uses the saved text size and contrast.");
                    app.UI.ShowSettings();yield return 4;
                    var reopened=(SettingsProfile)typeof(LabUI).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.UI);
                    Require("settings_category_reopens_saved_values/"+name,JsonUtility.ToJson(reopened)==JsonUtility.ToJson(expected),"Returning to settings preserves every field in this category.");
                }
            }
            finally{app.Settings.Apply(saved);category.SetValue(app.UI,"VR comfort");app.UI.ShowSignIn();}
            yield return 5;
        }
        IEnumerable<int> VerifyRuntimeSettings()
        {
            void Check(string id,bool passed,string detail)=>report.checks.Add(new LearningVRSimulatorVerification.Check{id=id,passed=passed,detail=detail});
            var saved=app.Settings.Draft();
            var original=QualitySettings.renderPipeline;
            try
            {
                // Android uses the Graphics default rather than a quality-level override.
                QualitySettings.renderPipeline=null;
                string folder=System.IO.Path.Combine(Application.temporaryCachePath,"SettingsPipeline-"+System.Guid.NewGuid().ToString("N"));
                using(var settings=new SettingsService(folder))
                {
                    var draft=settings.Draft();draft.renderScale=.72f;draft.msaa=2;draft.shadows=true;draft.shadowDistance=7;
                    settings.Apply(draft);
                    var active=QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                    Check("settings_graphics_default_pipeline",active&&Mathf.Abs(active.renderScale-.72f)<.001f&&active.msaaSampleCount==2&&Mathf.Abs(active.shadowDistance-7)<.001f,"Quest quality levels with no override must still apply render scale, MSAA and shadow distance.");
                }
            }
            finally{QualitySettings.renderPipeline=original;app.Settings.Apply(saved);}
            app.Session.busy=true;
            var pending=app.Settings.Draft();pending.tableHeight=.19f;app.Settings.Apply(pending);
            app.Session.busy=false;yield return 4;
            Check("settings_table_change_survives_busy_cut",Mathf.Abs(app.Session.table.position.y-.19f)<.001f&&Mathf.Abs(app.Session.specimen.position.y-.19f)<.001f,"A setting saved during mesh processing must take effect when processing finishes.");
            app.Settings.Apply(saved);yield return 3;
            var assembly=app.Catalog.units.SelectMany(u=>u.variants).First(v=>v.mode==LearningMode.AssembleAnatomy&&v.difficulty==Difficulty.Easy);
            app.StartActivity(assembly,SessionPhase.Practice);yield return 8;
            var hints=app.Settings.Draft();hints.hints=false;app.Settings.Apply(hints);yield return 5;
            var aids=(GameObject)typeof(GuidedInteractionCoach).GetField("aids",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.Coach);
            Check("settings_hints_off_updates_current_step",!aids||!aids.activeInHierarchy,"Turning hints off removes the current assembly socket immediately.");
            hints.hints=true;app.Settings.Apply(hints);yield return 5;
            aids=(GameObject)typeof(GuidedInteractionCoach).GetField("aids",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app.Coach);
            Check("settings_hints_on_updates_current_step",aids&&aids.activeInHierarchy,"Turning hints on restores the current practice socket.");
            if(aids)
            {
                var socket=aids.GetComponentInChildren<MeshRenderer>();var before=socket.transform.position;
                var moved=app.Settings.Draft();moved.tableHeight+=.08f;app.Settings.Apply(moved);yield return 4;
                Check("settings_height_moves_assembly_socket",Vector3.Distance(socket.transform.position,before+Vector3.up*.08f)<.001f,"Assembly guidance moves with the specimen when table height changes.");
                app.Settings.Apply(saved);yield return 4;
            }
            var medicalCase=app.Catalog.units.Where(u=>u.isCase).SelectMany(u=>u.variants).First(v=>v.mode==LearningMode.AnatomyCases&&v.difficulty==Difficulty.Easy);
            app.StartActivity(medicalCase,SessionPhase.Practice);yield return 8;
            var equipment=GameObject.Find("Procedure equipment").transform;var equipmentBefore=equipment.position;
            var caseSettings=app.Settings.Draft();caseSettings.tableHeight+=.1f;app.Settings.Apply(caseSettings);yield return 5;
            Check("settings_height_moves_case_equipment",Vector3.Distance(equipment.position,equipmentBefore+Vector3.up*.1f)<.001f,"The instrument station follows table height without restarting the case.");
            app.Settings.Apply(saved);yield return 4;
            app.Select(app.Structures[app.Engine.Current.targetId]);yield return 4;
            var decision=app.Engine.Current;var reasoning=app.Procedure.Reasoning;int actions=app.Engine.Attempt.actions.Count;
            app.RevealHint();yield return 4;
            Check("settings_clinical_hint_shown_without_credit",decision.action==ActionKind.ClinicalDecision&&reasoning.HintVisible&&app.Engine.Attempt.actions.Count==actions,"An explicit practice hint reveals the choice without answering it.");
            var clinicalSettings=app.Settings.Draft();clinicalSettings.hints=false;app.Settings.Apply(clinicalSettings);yield return 4;
            Check("settings_hints_off_hides_clinical_choice",!reasoning.HintVisible&&app.Engine.Current==decision&&app.Engine.Attempt.actions.Count==actions,"Turning hints off hides existing clinical choice cues on the same objective without recording a choice.");
            clinicalSettings.hints=true;app.Settings.Apply(clinicalSettings);yield return 4;
            Check("settings_hints_on_does_not_reveal_clinical_answer",!reasoning.HintVisible,"Turning hints back on does not automatically reveal a clinical answer.");
            app.RevealHint();yield return 4;
            Check("settings_clinical_hint_can_be_requested_again",reasoning.HintVisible&&app.Engine.Attempt.actions.Count==actions,"The learner can explicitly request another hint after changing preferences.");
            var chest=app.Catalog.units.First(u=>u.id=="chest-wall").variants.First(v=>v.mode==LearningMode.GuidedDissection&&v.difficulty==Difficulty.Easy);
            app.StartActivity(chest,SessionPhase.Practice);yield return 8;
            Check("dissection_whole_layer_anchored",!app.CanManipulate(app.Structures[app.Engine.Current.targetId]),"Only detached chunks are grabbable while cutting a dissection layer.");
            Check("dissection_instruction_matches_chunk_only_grab",!app.CurrentInstruction.Contains("Whole tissue returns"),"Dissection instructions cannot promise whole-layer inspection while that layer is anchored.");
            app.StartSandbox();yield return 5;
            Check("sandbox_whole_layer_anchored",!app.CanManipulate(app.Structures["FMA7163"]),"Free dissection also keeps uncut tissue anchored.");
            app.Settings.Apply(saved);app.Interrupt();app.UI.ShowSignIn();yield return 4;
        }
    }
}
#endif
