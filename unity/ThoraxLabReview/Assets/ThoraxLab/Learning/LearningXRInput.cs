using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using System.Collections.Generic;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace ThoraxLab.Learning
{
    // XRI's simulator creates Input System devices, not legacy XR.InputDevices.
    public static class LearningXRInput
    {
        public struct State
        {
            public bool tracked, trigger, grip, menu;
            public float triggerValue, gripValue;
            public Vector3 position;
            public Quaternion rotation;
            public Vector2 axis;
            public XRInteractionSource source;
            public Vector3 rayPosition;
            public Quaternion rayRotation;
            public bool separateAim;
        }
        static readonly List<XRHandSubsystem> handSystems=new List<XRHandSubsystem>();
        static XRInteractionGate[] gates={new XRInteractionGate(),new XRInteractionGate()};
        static HandGestureLatch[] gestures={new HandGestureLatch(),new HandGestureLatch()};
        static State[] cached=new State[2];
        static int[] cachedFrame={-1,-1};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInput(){gates=new[]{new XRInteractionGate(),new XRInteractionGate()};gestures=new[]{new HandGestureLatch(),new HandGestureLatch()};cached=new State[2];cachedFrame=new[]{-1,-1};handSystems.Clear();}
        public static bool BareHandsActive=>Read(XRNode.LeftHand).source==XRInteractionSource.BareHand||Read(XRNode.RightHand).source==XRInteractionSource.BareHand;
        public static bool SimulationAllowed
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        public static bool IsSimulated => SimulationAllowed && ReadDevice(XRNode.Head).source==XRInteractionSource.Simulator;
        public static bool HeadTracked => Read(XRNode.Head).tracked;
        static UnityEngine.InputSystem.InputDevice Simulated(XRNode node)
        {
            if (!SimulationAllowed) return null;
            foreach (var device in InputSystem.devices)
            {
                if (node == XRNode.Head && device.layout == "XRSimulatedHMD") return device;
                if (node == XRNode.Head || device.layout != "XRSimulatedController") continue;
                string usage = node == XRNode.LeftHand ? "LeftHand" : "RightHand";
                foreach (var deviceUsage in device.usages) if (deviceUsage == usage) return device;
            }
            return null;
        }
        public static State Read(XRNode node)
        {
            if(node==XRNode.Head)return ReadDevice(node);
            int side=node==XRNode.LeftHand?0:1;
            if(cachedFrame[side]==Time.frameCount)return cached[side];
            var state=ReadDevice(node);
            if(!state.tracked)state=ReadBareHand(node,side);
            else gestures[side].Update(false,0,0);
            bool headTracked=ReadDevice(XRNode.Head).tracked;
            gates[side].Filter(state.source,state.tracked&&headTracked,ref state.trigger,ref state.grip);
            cachedFrame[side]=Time.frameCount;cached[side]=state;return state;
        }
        static State ReadDevice(XRNode node)
        {
            // OpenXR supplies independent grip and aim poses in Input System.
            // Use its controller layout before the legacy compatibility device.
            if(node!=XRNode.Head)
            {
                string usage=node==XRNode.LeftHand?"LeftHand":"RightHand";
                foreach(var device in InputSystem.devices)
                {
                    if(!(device is UnityEngine.InputSystem.XR.XRController)||device is MetaAimHand||device is UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile.HandInteraction||device.layout=="XRSimulatedController"||!device.added)continue;
                    bool handed=false;foreach(var deviceUsage in device.usages)if(deviceUsage==usage)handed=true;
                    if(!handed)continue;
                    var position=device.TryGetChildControl<Vector3Control>("devicePosition");var rotation=device.TryGetChildControl<QuaternionControl>("deviceRotation");
                    bool tracked=device.TryGetChildControl<ButtonControl>("isTracked")?.isPressed??false;
                    if(!tracked||position==null||rotation==null)continue;
                    var state=new State{source=XRInteractionSource.Controller,position=position.ReadValue(),rotation=rotation.ReadValue(),tracked=true};
                    if(!Finite(state.position)||!ValidRotation(state.rotation))continue;
                    state.triggerValue=Analog(device,"trigger","triggerPressed");state.gripValue=Analog(device,"grip","gripPressed");
                    state.trigger=device.TryGetChildControl<ButtonControl>("triggerPressed")?.isPressed??state.triggerValue>=.55f;
                    state.grip=device.TryGetChildControl<ButtonControl>("gripPressed")?.isPressed??state.gripValue>=.55f;
                    state.menu=device.TryGetChildControl<ButtonControl>("menu")?.isPressed??false;
                    state.axis=device.TryGetChildControl<Vector2Control>("thumbstick")?.ReadValue()??Vector2.zero;
                    if(!Finite(state.axis.x)||!Finite(state.axis.y))state.axis=Vector2.zero;
                    var aimPosition=device.TryGetChildControl<Vector3Control>("pointerPosition");var aimRotation=device.TryGetChildControl<QuaternionControl>("pointerRotation");
                    if(aimPosition!=null&&aimRotation!=null){state.rayPosition=aimPosition.ReadValue();state.rayRotation=aimRotation.ReadValue();state.separateAim=Finite(state.rayPosition)&&ValidRotation(state.rayRotation);}
                    return state;
                }
            }
            var physical = InputDevices.GetDeviceAtXRNode(node);
            if (physical.isValid&&(node==XRNode.Head||(physical.characteristics&InputDeviceCharacteristics.HandTracking)==0))
            {
                var state = new State { rotation = Quaternion.identity,source=XRInteractionSource.Controller };
                bool hasTracking = physical.TryGetFeatureValue(XRUsages.isTracked, out bool tracked);
                bool hasPosition = physical.TryGetFeatureValue(XRUsages.devicePosition, out state.position);
                bool hasRotation = physical.TryGetFeatureValue(XRUsages.deviceRotation, out state.rotation);
                state.tracked = (!hasTracking || tracked) && hasPosition && hasRotation;
                bool hasTrigger=physical.TryGetFeatureValue(XRUsages.triggerButton, out state.trigger);
                bool hasGrip=physical.TryGetFeatureValue(XRUsages.gripButton, out state.grip);
                if(!physical.TryGetFeatureValue(XRUsages.trigger, out state.triggerValue))state.triggerValue=state.trigger?1:0;
                if(!physical.TryGetFeatureValue(XRUsages.grip, out state.gripValue))state.gripValue=state.grip?1:0;
                state.triggerValue = Mathf.Clamp01(state.triggerValue);
                state.gripValue = Mathf.Clamp01(state.gripValue);
                if(!hasTrigger)state.trigger=state.triggerValue>=.55f;if(!hasGrip)state.grip=state.gripValue>=.55f;
                physical.TryGetFeatureValue(XRUsages.menuButton, out state.menu);
                physical.TryGetFeatureValue(XRUsages.primary2DAxis, out state.axis);
                if(state.tracked||node==XRNode.Head)return state;
            }
            var simulated = Simulated(node);
            if (simulated == null) return new State { rotation = Quaternion.identity };
            return new State
            {
                tracked = simulated.TryGetChildControl<ButtonControl>("isTracked")?.isPressed ?? false,
                source = XRInteractionSource.Simulator,
                position = simulated.TryGetChildControl<Vector3Control>("devicePosition")?.ReadValue() ?? Vector3.zero,
                rotation = simulated.TryGetChildControl<QuaternionControl>("deviceRotation")?.ReadValue() ?? Quaternion.identity,
                trigger = simulated.TryGetChildControl<ButtonControl>("triggerButton")?.isPressed ?? false,
                grip = simulated.TryGetChildControl<ButtonControl>("gripButton")?.isPressed ?? false,
                triggerValue = Analog(simulated,"trigger","triggerButton"),
                gripValue = Analog(simulated,"grip","gripButton"),
                menu = simulated.TryGetChildControl<ButtonControl>("menuButton")?.isPressed ?? false,
                axis = simulated.TryGetChildControl<Vector2Control>("primary2DAxis")?.ReadValue() ?? Vector2.zero
            };
        }
        static State ReadBareHand(XRNode node,int side)
        {
            var empty=new State{rotation=Quaternion.identity};
            var aim=node==XRNode.LeftHand?MetaAimHand.left:MetaAimHand.right;
            SubsystemManager.GetSubsystems(handSystems);
            XRHandSubsystem subsystem=null;
            foreach(var candidate in handSystems)if(candidate.running){subsystem=candidate;break;}
            var flags=aim!=null?(MetaAimFlags)aim.aimFlags.ReadValue():MetaAimFlags.None;
            bool valid=aim!=null&&aim.added&&aim.isTracked.isPressed&&subsystem!=null&&
                (flags&(MetaAimFlags.Computed|MetaAimFlags.Valid))==(MetaAimFlags.Computed|MetaAimFlags.Valid)&&
                (flags&(MetaAimFlags.SystemGesture|MetaAimFlags.MenuPressed))==0;
            if(!valid){gestures[side].Update(false,0,0);return empty;}
            var hand=node==XRNode.LeftHand?subsystem.leftHand:subsystem.rightHand;
            if(!hand.isTracked||!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm)||
                !Curl(hand,XRHandFingerID.Middle,out var middle)||!Curl(hand,XRHandFingerID.Ring,out var ring)||!Curl(hand,XRHandFingerID.Little,out var little))
            {gestures[side].Update(false,0,0);return empty;}
            var aimPosition=aim.devicePosition.ReadValue();var aimRotation=aim.deviceRotation.ReadValue();
            if(!Finite(palm.position)||!Finite(aimPosition)||!ValidRotation(palm.rotation)||!ValidRotation(aimRotation))
            {gestures[side].Update(false,0,0);return empty;}
            float select=aim.pinchStrengthIndex.ReadValue();
            float grasp=(middle+ring+little)/3;
            if(!Finite(select)||!Finite(grasp)){gestures[side].Update(false,0,0);return empty;}
            select=Mathf.Clamp01(select);grasp=Mathf.Clamp01(grasp);
            gestures[side].Update(true,select,grasp);
            // Object manipulation follows the palm; remote pointing follows the
            // runtime's separate aim pose, so aiming cannot twist a held object.
            return new State{tracked=true,source=XRInteractionSource.BareHand,position=palm.position,rotation=palm.rotation,
                separateAim=true,rayPosition=aimPosition,rayRotation=aimRotation,
                trigger=gestures[side].Select,grip=gestures[side].Grab,triggerValue=select,gripValue=grasp};
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static bool ValidRotation(Quaternion value)
        {
            float norm=value.x*value.x+value.y*value.y+value.z*value.z+value.w*value.w;
            return Finite(norm)&&norm>.5f&&norm<1.5f;
        }
        static bool Curl(XRHand hand,XRHandFingerID finger,out float value)=>hand.CalculateFingerShape(finger,XRFingerShapeTypes.FullCurl).TryGetFullCurl(out value);
        static float Analog(UnityEngine.InputSystem.InputDevice device,string axis,string button)
        {
            float value=device.TryGetChildControl<AxisControl>(axis)?.ReadValue()??0;
            return Mathf.Clamp01(value>0?value:device.TryGetChildControl<ButtonControl>(button)?.isPressed==true?1:0);
        }
    }
}
