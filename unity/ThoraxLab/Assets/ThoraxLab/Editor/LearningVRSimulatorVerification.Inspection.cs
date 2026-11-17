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

                foreach(var mode in new[]{LearningMode.IdentifyEnumerate,LearningMode.AssembleAnatomy,LearningMode.GuidedDissection,LearningMode.AnatomyCases})
                {
                    var unit=app.Catalog.units.Single(u=>u.id==(mode==LearningMode.AnatomyCases?"pneumothorax":"heart-coronaries"));
                    var variant=unit.variants.First(v=>v.mode==mode&&v.difficulty==Difficulty.Easy);
                    app.StartActivity(variant,SessionPhase.Practice);yield return 8;
                    if(mode==LearningMode.AnatomyCases)
                    {
                        app.Select(app.Structures[app.Engine.Current.targetId]);yield return 6;
                        Require("inspection/case_models_available",app.Procedure.InspectionRenderers.Any(),"Case inspection includes the visible mechanism choices used in the current step.");
                    }
                    var sources=app.Procedure.InspectionRenderers.ToArray();
                    if(sources.Length==0)sources=app.Session.originals.Concat(app.Session.Fragments).Where(s=>s&&s.gameObject.activeInHierarchy&&!s.record.reference_region).Select(s=>(Renderer)s.Renderer).ToArray();
                    sources=sources.Where(r=>r.enabled&&!r.forceRenderingOff&&r.GetComponent<MeshFilter>()&&!r.GetComponent<TMPro.TMP_Text>()).ToArray();
                    var meshes=sources.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                    var positions=sources.Select(r=>r.transform.position).ToArray();
                    var rotations=sources.Select(r=>r.transform.rotation).ToArray();
                    var scales=sources.Select(r=>r.transform.lossyScale).ToArray();
                    string attempt=JsonUtility.ToJson(app.Engine.Attempt),progress=JsonUtility.ToJson(app.Progress);
                    string objective=app.Engine.Current.id;
                    var playerPosition=Camera.main.transform.position;
                    SetHandWorld(playerPosition+Camera.main.transform.right*.25f-Vector3.up*.2f,Quaternion.LookRotation(-Camera.main.transform.forward));
                    InspectionPress(XRNode.RightHand,false);yield return 4;
                    InspectionPress(XRNode.RightHand,true);yield return 8;
                    Require("inspection/open/"+mode,app.Inspector.IsOpen&&app.Inspector.Visual&&app.UI.ScreenName=="Model inspection"&&!app.CanInteract,"A opens the same inspection flow in every mode and pauses activity interactions.");
                    var visual=app.Inspector.Visual;
                    Require("inspection/geometry/"+mode,visual.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).SequenceEqual(meshes)&&visual.GetComponentsInChildren<Collider>().Length==0&&visual.GetComponentsInChildren<AnatomyStructure>().Length==0,"The enlarged view shares the visible meshes, including cut geometry, and has no gameplay colliders or scoring scripts.");
                    Require("inspection/source_hidden/"+mode,sources.All(r=>r.forceRenderingOff),"The original model is hidden while its inspection view is displayed.");
                    var initialScale=visual.localScale;
                    InspectionPress(XRNode.RightHand,false);yield return 4;
                    rightState.primary2DAxis=Vector2.up;QueueAll();yield return 12;
                    Require("inspection/zoom_in/"+mode,app.Inspector.Magnification>1&&visual.localScale.x>initialScale.x,"The controller stick enlarges the 3D geometry.");
                    float enlarged=app.Inspector.Magnification;
                    rightState.primary2DAxis=Vector2.down;QueueAll();yield return 12;
                    Require("inspection/zoom_out/"+mode,app.Inspector.Magnification<enlarged,"Pulling the same stick down shrinks the inspection model.");
                    rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                    app.Inspector.ZoomIn();float beforeScroll=app.Inspector.Magnification;
                    AimAtButton("Zoom in +");yield return 4;
                    rightState.primary2DAxis=Vector2.up;QueueAll();yield return 8;
                    Require("inspection/panel_scroll/"+mode,app.Inspector.Magnification==beforeScroll,"Pointing at the panel routes the stick to scrolling, without simultaneous zoom.");
                    rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                    app.Inspector.ResetView();
                    Require("inspection/reset/"+mode,app.Inspector.Magnification==1&&Vector3.Distance(visual.localScale,initialScale)<.00001f,"Reset restores the fitted model size.");
                    Require("inspection/source_unchanged/"+mode,sources.Select((r,i)=>r.GetComponent<MeshFilter>().sharedMesh==meshes[i]&&Vector3.Distance(r.transform.position,positions[i])<.00001f&&Quaternion.Angle(r.transform.rotation,rotations[i])<.001f&&Vector3.Distance(r.transform.lossyScale,scales[i])<.00001f).All(v=>v),"Inspection cannot alter original geometry, piece positions, scale or placement tolerances.");
                    Require("inspection/no_progress/"+mode,JsonUtility.ToJson(app.Engine.Attempt)==attempt&&JsonUtility.ToJson(app.Progress)==progress&&app.Engine.Current.id==objective&&Vector3.Distance(Camera.main.transform.position,playerPosition)<.001f,"Zooming cannot add evidence, finish a step, award mastery or move the player.");
                    InspectionPress(XRNode.RightHand,true);yield return 6;
                    Require("inspection/return/"+mode,!app.Inspector.IsOpen&&!app.Inspector.Visual&&app.CanInteract&&sources.All(r=>!r.forceRenderingOff),"A returns to the activity, restores original rendering and removes the temporary view.");
                    yield return 4;
                    Require("inspection/held_button/"+mode,!app.Inspector.IsOpen,"Holding A after returning cannot reopen inspection.");
                    InspectionPress(XRNode.RightHand,false);yield return 4;
                }

                app.Settings.Current.leftHanded=true;yield return 4;
                InspectionPress(XRNode.RightHand,true);yield return 4;
                Require("inspection/left_handed_mapping",!app.Inspector.IsOpen,"With left dominant hand, A does not toggle inspection.");
                InspectionPress(XRNode.RightHand,false);
                InspectionPress(XRNode.LeftHand,true);yield return 8;
                Require("inspection/x_button",app.Inspector.IsOpen,"X opens inspection with left dominant hand.");
                SetHead(headState.devicePosition,headState.deviceRotation,false);QueueAll();yield return 5;
                Require("inspection/tracking_loss",!app.Inspector.IsOpen&&!app.Inspector.Visual&&app.UI.WorkspaceVisible,"Tracking loss closes the preview and restores the activity model.");
                SetHead(headState.devicePosition,headState.deviceRotation,true);QueueAll();yield return 5;
                Require("inspection/tracking_recovery",!app.Inspector.IsOpen,"A held X after tracking recovery cannot reopen inspection.");
                InspectionPress(XRNode.LeftHand,false);yield return 4;
                app.Inspector.Open();yield return 8;
                app.UI.TogglePauseMenu();yield return 4;
                Require("inspection/menu_cleanup",!app.Inspector.IsOpen&&!app.Inspector.Visual,"Navigating to Menu also clears the preview.");
                app.StartSandbox();yield return 6;
                app.Inspector.Open();yield return 8;
                Require("inspection/sandbox",app.Inspector.IsOpen&&app.Inspector.Visual,"The dissection sandbox also offers model enlargement.");
                app.Inspector.ReturnToActivity();app.StartOrientation();yield return 4;
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
