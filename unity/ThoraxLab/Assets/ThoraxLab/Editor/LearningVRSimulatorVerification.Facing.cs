#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThoraxLab.Learning;
using UnityEngine;
using UnityEngine.XR;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        // Record every direction in a failing baseline, rather than stopping at the first yaw.
        void FacingCheck(string id,bool passed,string detail)=>report.checks.Add(new LearningVRSimulatorVerification.Check{id=id,passed=passed,detail=detail});

        IEnumerable<int> VerifyStartupFacing()
        {
            var controls=FindFirstObjectByType<LearningControls>();var rig=Camera.main.transform.parent;
            var savedHead=headState;var savedLeft=leftState;var savedRight=rightState;
            var position=rig.position;var rotation=rig.rotation;
            var tracked=typeof(LearningControls).GetField("tracked",BindingFlags.Instance|BindingFlags.NonPublic);
            var expected=Quaternion.Euler(0,-65,0)*Vector3.forward;
            foreach(float yaw in new[]{0f,90f,180f,270f})
            {
                leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;
                SetHead(new Vector3(.45f,1.65f,-.35f),Quaternion.Euler(15,yaw,8),true);
                QueueAll();yield return 2;
                tracked.SetValue(controls,false);yield return 8;
                var actual=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;
                FacingCheck("startup_facing/yaw_"+yaw,Vector3.Angle(actual,expected)<.1f,
                    "Initial local yaw="+yaw+"; world horizontal facing="+actual+"; expected="+expected);
                FacingCheck("startup_facing/aisle_"+yaw,Vector3.Distance(Vector3.ProjectOnPlane(Camera.main.transform.position,Vector3.up),new Vector3(.85f,0,.65f))<.005f,
                    "Arbitrary room origin still puts the head in the clear aisle: "+Camera.main.transform.position);
                var facing=Camera.main.transform.rotation;
                SetHead(headState.devicePosition,headState.deviceRotation*Quaternion.Euler(0,12,0),true);QueueAll();yield return 4;
                FacingCheck("startup_facing/natural_head_turn_"+yaw,Quaternion.Angle(facing,Camera.main.transform.rotation)>11.9f,
                    "Startup alignment must not override later physical head turns.");
            }
            headState=savedHead;leftState=savedLeft;rightState=savedRight;rig.SetPositionAndRotation(position,rotation);QueueAll();yield return 4;
            controls.RecenterTracking();yield return 4;
        }

        IEnumerable<int> VerifyPanelHoverWalking()
        {
            var preferences=app.Settings.Current;var movement=preferences.movement;bool leftHanded=preferences.leftHanded;
            var savedHead=headState;var savedLeft=leftState;var savedRight=rightState;
            var rig=Camera.main.transform.parent;var position=rig.position;var rotation=rig.rotation;
            try
            {
                preferences.movement=MoveStyle.Smooth;
                var variant=app.Catalog.units.SelectMany(u=>u.variants).First(v=>v.mode==LearningMode.IdentifyEnumerate&&v.difficulty==Difficulty.Easy&&app.Structures.ContainsKey(v.objectives[0].targetId));
                app.StartActivity(variant,SessionPhase.Practice);yield return 12;
                Require("panel_walk/workspace",app.CanInteract,"Walking checks use the ordinary tracked lesson gate.");
                foreach(bool handedness in new[]{false,true})
                foreach(var pointerHand in new[]{XRNode.LeftHand,XRNode.RightHand})
                {
                    preferences.leftHanded=handedness;leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;
                    SetAnalog(ref leftState,0,0);SetAnalog(ref rightState,0,0);
                    rig.SetPositionAndRotation(position,rotation);headState=savedHead;QueueAll();yield return 4;app.UI.RecenterPanel();yield return 4;
                    var panel=app.UI.Root;
                    SetHead(headState.devicePosition,Quaternion.Inverse(rig.rotation)*Quaternion.LookRotation(panel.position-Camera.main.transform.position,Vector3.up),true);QueueAll();yield return 3;
                    var origin=Camera.main.transform.position-Camera.main.transform.up*.08f;
                    var target=panel.TransformPoint(new Vector3(0,panel.rect.yMax-20,0));
                    SetHandWorld(pointerHand,origin,Quaternion.LookRotation(target-origin,Vector3.up));
                    SetHandWorld(pointerHand==XRNode.LeftHand?XRNode.RightHand:XRNode.LeftHand,origin,Quaternion.LookRotation(Vector3.up,Vector3.forward));yield return 20;
                    string Gates()=>"focused="+app.Focused+"; paused="+app.Paused+"; workspace="+app.UI.WorkspaceVisible+"; XRReady="+app.XRReady+"; head="+LearningXRInput.Read(XRNode.Head).tracked+"; screen="+app.UI.ScreenName;
                    var pointer=FindObjectsByType<LabHandInteractor>(FindObjectsSortMode.None).Single(h=>h.hand==pointerHand);
                    Require("panel_walk/real_hover/"+handedness+"/"+pointerHand,
                        (bool)typeof(LabHandInteractor).GetField("overUI",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pointer),
                        "The queued controller ray really intersects the UI header, with no trigger or grip; "+Gates());
                    var start=rig.position;var backward=-Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;
                    if(handedness)rightState.primary2DAxis=Vector2.down;else leftState.primary2DAxis=Vector2.down;
                    QueueAll();yield return 12;
                    FacingCheck("panel_walk/passive_hover/"+handedness+"/"+pointerHand,Vector3.Dot(rig.position-start,backward)>.015f,
                        "Movement stick must walk while either ray hovers; delta="+(rig.position-start)+"; capture="+app.UI.XRPanelCaptured+"; "+Gates());
                    leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                }
                // A deliberate panel grab must still suspend locomotion until released and neutral.
                app.UI.RecenterPanel();yield return 4;
                var header=app.UI.Root.TransformPoint(new Vector3(0,app.UI.Root.rect.yMax-20,0));
                var handOrigin=Camera.main.transform.position-Camera.main.transform.up*.08f;
                SetHead(headState.devicePosition,Quaternion.Inverse(rig.rotation)*Quaternion.LookRotation(app.UI.Root.position-Camera.main.transform.position,Vector3.up),true);
                SetHandWorld(handOrigin,Quaternion.LookRotation(header-handOrigin,Vector3.up));yield return 8;
                Grip(true);yield return 8;
                Require("panel_walk/grab_captures",app.UI.XRPanelCaptured,"Gripping the header still captures locomotion.");
                var heldPosition=rig.position;rightState.primary2DAxis=Vector2.down;QueueAll();yield return 12;
                Require("panel_walk/grab_blocks_walk",Vector3.Distance(rig.position,heldPosition)<.001f,"A held panel cannot drift from joystick walking.");
                Grip(false);yield return 20;
                Require("panel_walk/release_requires_neutral",Vector3.Distance(rig.position,heldPosition)<.001f,"Releasing the panel cannot replay a still-held walking stick.");
                rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 20;
                rightState.primary2DAxis=Vector2.down;QueueAll();yield return 12;
                FacingCheck("panel_walk/release_neutral_resumes",Vector3.Distance(rig.position,heldPosition)>.015f,"A fresh stick movement walks after the panel is released.");
            }
            finally
            {
                preferences.movement=movement;preferences.leftHanded=leftHanded;
                headState=savedHead;leftState=savedLeft;rightState=savedRight;rig.SetPositionAndRotation(position,rotation);QueueAll();
            }
            yield return 8;app.UI.RecenterPanel();yield return 4;
        }
    }
}
#endif
