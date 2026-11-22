#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        void InspectionPress(XRNode node, bool held)
        {
            if(node==XRNode.LeftHand)leftState=leftState.WithButton(ControllerButton.PrimaryButton,held);
            else rightState=rightState.WithButton(ControllerButton.PrimaryButton,held);
            QueueAll();
        }

        void VerifySingleSolidModel(string id, MeshRenderer original)
        {
            var mesh = original.GetComponent<MeshFilter>().sharedMesh;
            // The museum exhibits and the source specimen occupy separate rooms.
            var workArea = new Bounds(original.bounds.center, Vector3.one * 8);
            var solid = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(filter => filter.sharedMesh == mesh)
                .Select(filter => filter.GetComponent<MeshRenderer>())
                .Where(renderer => renderer && renderer.enabled && !renderer.forceRenderingOff && workArea.Intersects(renderer.bounds) &&
                    renderer.sharedMaterials.Any(mat => mat && mat.renderQueue <= (int)UnityEngine.Rendering.RenderQueue.GeometryLast))
                .ToArray();
            Require(id, solid.Length == 1 && solid[0] == original,
                "Solid instances of " + mesh.name + " in the activity: " +
                string.Join(", ", solid.Select(renderer => renderer.name + " at " + renderer.bounds.center.ToString("F3"))));
        }

        IEnumerable<int> VerifyModelInspection()
        {
            var settings=app.Settings.Draft();
            var savedHead=headState;var savedLeft=leftState;var savedRight=rightState;
            try
            {
                app.EnterPreview();app.Progress.orientationCompleted=true;
                app.Settings.Current.leftHanded=false;
                SetController(ref leftState,leftState.devicePosition,leftState.deviceRotation,true,false,false);
                SetController(ref rightState,rightState.devicePosition,rightState.deviceRotation,true,false,false);
                leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;
                leftState=leftState.WithButton(ControllerButton.PrimaryButton,false).WithButton(ControllerButton.MenuButton,false);
                rightState=rightState.WithButton(ControllerButton.PrimaryButton,false).WithButton(ControllerButton.MenuButton,false);
                QueueAll();yield return 4;

                app.StartOrientation();yield return 8;
                var audio=(AudioSource)typeof(LessonNarrator).GetField("source",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app.Narrator);
                Require("voice/orientation_select",app.Narrator.IsPlaying && audio.clip.name=="orientation-select" && app.Narrator.Caption.Length>0,"Orientation speaks the selection instruction.");
                app.Select(app.Structures["FMA7163"]);yield return 8;
                Require("voice/orientation_lift",app.Narrator.IsPlaying && audio.clip.name=="orientation-lift","Selecting the skin starts the spoken lift instruction.");
                var samples=new float[1024];bool audible=false;
                for(int frame=0;frame<90&&!audible;frame++){
                    audio.GetOutputData(samples,0);audible=samples.Any(sample=>Mathf.Abs(sample)>.001f);yield return 1;
                }
                Require("voice/output",audible && AudioListener.volume>0 && audio.volume>0 && Camera.main.GetComponent<AudioListener>().isActiveAndEnabled,"The narration source produces non-silent output with an active listener.");
                app.Interrupt();

                var head=Camera.main.transform;
                var rig=head.parent;
                foreach(var mode in new[]{LearningMode.IdentifyEnumerate,LearningMode.AssembleAnatomy,LearningMode.GuidedDissection,LearningMode.AnatomyCases})
                {
                    var unit=app.Catalog.units.Single(u=>u.id==(mode==LearningMode.AnatomyCases?"pneumothorax":"chest-wall"));
                    var variant=unit.variants.First(v=>v.mode==mode&&v.difficulty==Difficulty.Easy);
                    app.StartActivity(variant,SessionPhase.Practice);yield return 8;
                    var engine=app.Engine;
                    var objective=engine.Current;
                    var target=app.Structures[objective.targetId];
                    var sourcePose=target.transform.position;
                    var sourceScale=target.transform.lossyScale;
                    string attempt=JsonUtility.ToJson(engine.Attempt);
                    if(mode==LearningMode.IdentifyEnumerate)
                    {
                        Require("voice/briefing",app.Narrator.IsPlaying&&audio.clip.name=="intro-"+variant.id,"Starting guided practice speaks its briefing.");
                        audio.timeSamples=audio.clip.samples-Mathf.Min(2048,audio.clip.samples);
                        for(int frame=0;frame<200&&audio.clip.name!=objective.narration;frame++)yield return 1;
                        Require("voice/first_step",app.Narrator.IsPlaying&&audio.clip.name==objective.narration,"The first objective follows the briefing without a button press.");
                    }
                    var playerPosition=head.position;
                    SetHandWorld(playerPosition+head.right*.25f-Vector3.up*.2f,Quaternion.LookRotation(-head.forward));
                    InspectionPress(XRNode.RightHand,false);yield return 4;
                    var playingClip=audio.clip;
                    InspectionPress(XRNode.RightHand,true);yield return 6;
                    Require("inspection/open/"+mode,app.Inspector.IsOpen && app.CanInteract && app.UI.WorkspaceVisible && !app.Narrator.Paused && app.Narrator.IsPlaying && audio.clip==playingClip,
                        "A keeps the same activity workspace, tools and voice active.");
                    InspectionPress(XRNode.RightHand,false);yield return 4;
                    rightState.primary2DAxis=Vector2.up;QueueAll();yield return 12;
                    Require("inspection/zoom_in/"+mode,app.Inspector.Magnification>1&&head.parent.lossyScale.x<1&&hand.transform.parent==head.parent,"Both the tracked eyes and the working hand use the enlarged view.");
                    float enlarged=app.Inspector.Magnification;
                    rightState.primary2DAxis=Vector2.down;QueueAll();yield return 8;
                    Require("inspection/zoom_out/"+mode,app.Inspector.Magnification<enlarged,"Pulling the stick down shrinks the view.");
                    rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                    app.Inspector.ZoomIn();float beforeScroll=app.Inspector.Magnification;
                    AimAtButton("Zoom in +");yield return 4;
                    rightState.primary2DAxis=Vector2.up;QueueAll();yield return 8;
                    Require("inspection/panel_scroll/"+mode,app.Inspector.Magnification==beforeScroll,"Scrolling the workspace cannot also zoom the model.");
                    rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                    app.Inspector.ResetView();
                    Require("inspection/reset/"+mode,app.Inspector.Magnification==1&&Vector3.Distance(head.position,playerPosition)<.001f,"Reset restores the normal view.");
                    app.Inspector.ZoomIn();yield return 3;
                    Require("inspection/live_target/"+mode,target.GetComponent<Collider>().enabled&&!target.Renderer.forceRenderingOff&&Vector3.Distance(target.transform.position,sourcePose)<.001f&&target.transform.lossyScale==sourceScale,
                        "Actual visible anatomy and colliders retain the original pose and metric scale.");
                    VerifySingleSolidModel("inspection/single_solid/"+mode,target.Renderer);
                    Require("inspection/no_credit/"+mode,JsonUtility.ToJson(engine.Attempt)==attempt,"Changing zoom alone cannot score an action.");
                    if(mode==LearningMode.IdentifyEnumerate||mode==LearningMode.AnatomyCases)
                    {
                        Require("inspection/exposed/"+mode,app.Presentation.TryGetExposedSurface(target,head.position,out var surface),"A real surface of the current target is reachable.");
                        var direction=(surface-head.position).normalized;
                        SetHandWorld(surface-direction*.07f,Quaternion.LookRotation(direction));yield return 8;
                        Trigger(true);yield return 5;Trigger(false);yield return 6;
                        Require("inspection/score/"+mode,app.Inspector.IsOpen&&app.CanInteract&&ReferenceEquals(engine,app.Engine)&&engine.IsComplete(objective.id),
                            "The controller trigger completes the current objective while zoomed. "+app.Notice);
                        Require("voice/next_step/"+mode,app.Narrator.IsPlaying&&audio.clip.name==engine.Current.narration,"The next spoken instruction starts while zoom remains open.");
                    }
                    InspectionPress(XRNode.RightHand,true);yield return 6;
                    Require("inspection/return/"+mode,!app.Inspector.IsOpen&&app.CanInteract&&head.parent==rig&&hand.transform.parent==rig,"Closing zoom restores normal tracking while keeping the same workspace.");
                    yield return 4;
                    Require("inspection/held_button/"+mode,!app.Inspector.IsOpen,"Holding A after returning cannot reopen zoom.");
                    InspectionPress(XRNode.RightHand,false);yield return 4;
                }

                app.Settings.Current.leftHanded=true;yield return 4;
                InspectionPress(XRNode.RightHand,true);yield return 4;
                Require("inspection/left_handed_mapping",!app.Inspector.IsOpen,"With left dominant hand, A does not toggle inspection.");
                InspectionPress(XRNode.RightHand,false);
                InspectionPress(XRNode.LeftHand,true);yield return 8;
                Require("inspection/x_button",app.Inspector.IsOpen,"X opens inspection with left dominant hand.");
                SetHead(headState.devicePosition,headState.deviceRotation,false);QueueAll();yield return 5;
                Require("inspection/tracking_loss",!app.Inspector.IsOpen&&head.parent==rig&&app.UI.WorkspaceVisible,"Tracking loss closes the preview and restores the activity model.");
                SetHead(headState.devicePosition,headState.deviceRotation,true);QueueAll();yield return 5;
                Require("inspection/tracking_recovery",!app.Inspector.IsOpen,"A held X after tracking recovery cannot reopen inspection.");
                InspectionPress(XRNode.LeftHand,false);yield return 4;
                app.Inspector.Open();yield return 8;
                app.UI.TogglePauseMenu();yield return 4;
                Require("inspection/menu_cleanup",!app.Inspector.IsOpen&&head.parent==rig,"Navigating to Menu also clears the preview.");
                app.StartSandbox();yield return 6;
                Require("voice/sandbox",app.Narrator.IsPlaying&&audio.clip.name=="sandbox-intro","Free dissection starts with spoken controls.");
                app.Inspector.Open();yield return 8;
                Require("inspection/sandbox",app.Inspector.IsOpen&&app.CanUseCuttingTool,"The dissection sandbox also offers model enlargement.");
                app.Inspector.ZoomIn();
                VerifySingleSolidModel("inspection/single_solid/sandbox",app.Structures["FMA7163"].Renderer);
                app.Inspector.Close();app.StartOrientation();yield return 4;
                app.Inspector.Open();yield return 3;
                Require("inspection/orientation_guard",!app.Inspector.IsOpen,"Inspection cannot bypass the introductory controls exercise.");
            }
            finally
            {
                app.Inspector.Close();app.Interrupt();app.Session.ResetSpecimen();app.Presentation.ShowIntact();
                app.Settings.Apply(settings);headState=savedHead;leftState=savedLeft;rightState=savedRight;QueueAll();app.UI.ShowSignIn();
            }
            yield return 4;
        }
    }
}
#endif
