using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ThoraxLab.Learning
{
    // Headset and official Editor simulator share the same six-degree-of-freedom path.
    public sealed class LearningControls : MonoBehaviour
    {
        LabRuntime app; Camera view; Transform rig; ComfortVignette comfort;
        float turnCooldown; bool tracked, teleportArmed, menuWasPressed;
        readonly List<XRInputSubsystem> inputSystems=new List<XRInputSubsystem>();
        XRInputSubsystem inputSystem;
        TrackingOriginModeFlags originMode=TrackingOriginModeFlags.Unknown;
        bool heightPending, lastSeated, headWasTracked;
        bool resetHeightCalibration=true, lastAutoHeight, usingFloorPose;
        float floorHeightLift, lastHeightBoost;
        int originChangedFrame=-1;
        float previousHeadHeight, nextOriginRequest;
        XRInteractionSource moveSource,turnSource;
        bool moveInputReady,turnInputReady;
        public void Initialize(LabRuntime value)
        {
            app=value; view=Camera.main; rig=view.transform.parent;
            comfort=view.gameObject.AddComponent<ComfortVignette>();
            var old=FindFirstObjectByType<LabControls>(); if(old)old.enabled=false;
            RefreshTrackingOrigin();
        }
        void Update()
        {
            if(app?.UI==null)return;
            RefreshTrackingOrigin();
            var head=LearningXRInput.Read(XRNode.Head);
            if(!head.tracked||!Finite(head.position)){headWasTracked=false;ResetLocomotion();comfort.SetMoving(false);return;}
            // Hold the last valid camera pose while the runtime changes its
            // coordinate reference; never render its new pose with an old offset.
            if(tracked&&heightPending&&Time.frameCount<=originChangedFrame){comfort.SetMoving(false);return;}
            bool firstTrackedPose=!tracked;
            if(!tracked)
            {
                // Align the learner's actual gaze, independent of the runtime's initial room heading.
                var facing=Vector3.ProjectOnPlane(head.rotation*Vector3.forward,Vector3.up);
                if(facing.sqrMagnitude<.0001f)facing=Vector3.Cross(head.rotation*Vector3.right,Vector3.up);
                rig.rotation=Quaternion.Euler(0,-65-Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg,0);
                // Start in the clear aisle, independent of the headset's room-scale origin.
                rig.position=new Vector3(.85f,0,.65f)-Vector3.ProjectOnPlane(rig.rotation*head.position,Vector3.up);tracked=true;
                // Only the flat Editor simulator uses this preview lens. The
                // headset supplies its own per-eye projection and optical FOV.
                if(LearningXRInput.IsSimulated)view.fieldOfView=app.Settings.Current.simulatorFov;
                // The first console recenter must use the measured HMD pose, not
                // the scene camera's pre-tracking preview rotation.
                heightPending=true;resetHeightCalibration=true;
            }
            view.transform.SetLocalPositionAndRotation(head.position,head.rotation);
            if(!headWasTracked)heightPending=true;
            // Some runtimes deliver a new pose after their origin event. Recover
            // a discontinuous origin jump, without flattening normal crouching.
            bool originJump=headWasTracked&&Mathf.Abs(head.position.y-previousHeadHeight)>.75f&&
                (view.transform.position.y<.35f||view.transform.position.y>2.6f);
            bool floorPoseArrived=!usingFloorPose&&originMode==TrackingOriginModeFlags.Floor&&head.position.y>.35f&&head.position.y<2.6f;
            if(originJump||floorPoseArrived){heightPending=true;resetHeightCalibration=true;}
            if(heightPending&&(firstTrackedPose||Time.frameCount>originChangedFrame))AlignHeight(head);
            previousHeadHeight=head.position.y;headWasTracked=true;
            bool menu=LearningXRInput.Read(XRNode.LeftHand).menu||LearningXRInput.Read(XRNode.RightHand).menu;
            if(app.Focused&&menu&&!menuWasPressed)app.UI.TogglePauseMenu();
            menuWasPressed=menu;
            if(app.CanInteract&&!app.UI.XRPanelCaptured)MoveVR();else{ResetLocomotion();comfort.SetMoving(false);}
        }
        void MoveVR()
        {
            var p=app.Settings.Current;
            var left=LearningXRInput.Read(p.leftHanded?XRNode.RightHand:XRNode.LeftHand);
            var right=LearningXRInput.Read(p.leftHanded?XRNode.LeftHand:XRNode.RightHand);
            if(!left.tracked||moveSource!=left.source){moveInputReady=false;teleportArmed=false;moveSource=left.source;}
            if(!right.tracked||turnSource!=right.source){turnInputReady=false;turnCooldown=0;turnSource=right.source;}
            if(left.tracked&&left.axis.magnitude<.2f)moveInputReady=true;
            if(right.tracked&&right.axis.magnitude<.2f)turnInputReady=true;
            Vector2 move=left.tracked&&moveInputReady?left.axis:default;
            Vector2 turn=right.tracked&&turnInputReady?right.axis:default;
            move=move.magnitude>.2f?Vector2.ClampMagnitude(move,1):Vector2.zero;
            comfort.SetMoving((p.movement==MoveStyle.Smooth&&move.magnitude>.2f)||(p.turning==TurnStyle.Smooth&&Mathf.Abs(turn.x)>.2f));
            var forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(rig.forward,Vector3.up).normalized;
            if(p.movement==MoveStyle.Smooth)
            {
                var delta=(forward*move.y+Vector3.Cross(Vector3.up,forward)*move.x)*p.moveSpeed*Time.deltaTime;
                var controller=rig.GetComponent<CharacterController>();
                if(controller){
                    // Keep the capsule above floor collision skin even when height assistance raises the rig.
                    controller.height=Mathf.Max(controller.radius*2,view.transform.position.y);
                    controller.center=rig.InverseTransformPoint(new Vector3(view.transform.position.x,controller.height*.5f+controller.skinWidth+.01f,view.transform.position.z));
                    controller.stepOffset=0;
                    controller.Move(delta);
                }else rig.position+=delta;
            }
            else
            {
                if(move.y>.7f)teleportArmed=true;
                if(teleportArmed&&Mathf.Abs(move.y)<.2f)
                {
                    var target=rig.position+forward*1.2f;var floor=new Ray(new Vector3(target.x,2,target.z),Vector3.down);
                    if(Physics.Raycast(floor,out var hit,3)&&hit.normal.y>.8f&&hit.point.y<.3f)
                    {var controller=rig.GetComponent<CharacterController>();if(controller)controller.enabled=false;rig.position=new Vector3(hit.point.x,rig.position.y,hit.point.z);if(controller)controller.enabled=true;}
                    teleportArmed=false;
                }
            }
            turnCooldown-=Time.deltaTime;
            if(p.turning==TurnStyle.Smooth&&Mathf.Abs(turn.x)>.2f)rig.RotateAround(view.transform.position,Vector3.up,turn.x*p.turnSpeed*Time.deltaTime);
            else if(p.turning==TurnStyle.Snap&&Mathf.Abs(turn.x)>.7f&&turnCooldown<=0){rig.RotateAround(view.transform.position,Vector3.up,Mathf.Sign(turn.x)*p.snapAngle);turnCooldown=.35f;}
            if(Mathf.Abs(turn.x)<.2f)turnCooldown=0;
        }
        void ResetLocomotion(){teleportArmed=false;moveInputReady=turnInputReady=false;turnCooldown=0;}
        public void ApplyComfort()
        {
            if(!tracked)return;
            // Seated layout keeps real floor height and never changes the runtime's origin.
            // Switching Floor -> Device here used a stale floor pose to calculate
            // a negative offset, then left the camera underground on the next pose.
            if(lastSeated!=app.Settings.Current.seated||lastHeightBoost!=app.Settings.Current.heightBoost)heightPending=true;
            if(lastAutoHeight!=app.Settings.Current.autoHeight){heightPending=true;resetHeightCalibration=true;}
            RefreshTrackingOrigin();
            var head=LearningXRInput.Read(XRNode.Head);
            if(heightPending&&head.tracked&&Finite(head.position)&&Time.frameCount>originChangedFrame)AlignHeight(head);
        }
        void RefreshTrackingOrigin()
        {
            SubsystemManager.GetSubsystems(inputSystems);
            XRInputSubsystem active=null;
            foreach(var system in inputSystems)if(system.running){active=system;break;}
            if(active!=inputSystem)
            {
                if(inputSystem!=null)inputSystem.trackingOriginUpdated-=OriginUpdated;
                inputSystem=active;nextOriginRequest=0;heightPending=true;
                if(inputSystem!=null)inputSystem.trackingOriginUpdated+=OriginUpdated;
            }
            var mode=inputSystem!=null?inputSystem.GetTrackingOriginMode():LearningXRInput.IsSimulated?TrackingOriginModeFlags.Floor:TrackingOriginModeFlags.Unknown;
            if(mode!=originMode){originMode=mode;OriginUpdated(inputSystem);}
            // Prefer a stable floor reference in both layouts, but support devices
            // which reject it. Query the actual mode; a request is not a new pose.
            if(inputSystem!=null&&mode!=TrackingOriginModeFlags.Floor&&Time.unscaledTime>=nextOriginRequest)
            {
                nextOriginRequest=Time.unscaledTime+1;
                if((inputSystem.GetSupportedTrackingOriginModes()&TrackingOriginModeFlags.Floor)!=0)
                    inputSystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
        }
        void OriginUpdated(XRInputSubsystem system){heightPending=true;resetHeightCalibration=true;originChangedFrame=Time.frameCount;}
        void AlignHeight(LearningXRInput.State head)
        {
            bool seated=app.Settings.Current.seated;
            bool usableFloor=originMode==TrackingOriginModeFlags.Floor&&head.position.y>.35f&&head.position.y<2.6f;
            usingFloorPose=usableFloor;
            var settings=app.Settings.Current;
            // Calibrate once, then preserve physical crouching and tracking recovery.
            if(resetHeightCalibration)floorHeightLift=usableFloor&&settings.autoHeight?Mathf.Max(0,1.65f-head.position.y):0;
            float offset=(usableFloor?floorHeightLift:(settings.autoHeight?1.65f:seated?1.25f:1.65f)-head.position.y)+settings.heightBoost;
            var position=rig.position;position.y=offset;
            var controller=rig.GetComponent<CharacterController>();bool enabled=controller&&controller.enabled;
            if(enabled)controller.enabled=false;
            rig.position=position;
            if(enabled)controller.enabled=true;
            lastSeated=seated;lastAutoHeight=settings.autoHeight;lastHeightBoost=settings.heightBoost;heightPending=false;resetHeightCalibration=false;
            app.UI.RecenterPanel();
            Debug.Log("[Anatome XR] Height aligned: origin="+originMode+", seated="+seated+", localHeadY="+head.position.y+", rigY="+offset+", eyeY="+view.transform.position.y);
        }
        public void RecenterTracking()
        {
            RefreshTrackingOrigin();inputSystem?.TryRecenter();
            heightPending=true;resetHeightCalibration=true;originChangedFrame=Time.frameCount;
        }
        void OnDestroy(){if(inputSystem!=null)inputSystem.trackingOriginUpdated-=OriginUpdated;}
        static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
    }
}
