#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ThoraxLab.Learning;
using UnityEngine;

namespace ThoraxLab.Editor
{
    public sealed partial class LearningVRSimulatorVerificationRunner
    {
        IEnumerable<int> VerifyAllModeGrabAndWalk()
        {
            var preferences=app.Settings.Current;
            var savedMovement=preferences.movement;var savedHandedness=preferences.leftHanded;
            var savedLeft=leftState;float savedBoost=preferences.heightBoost;
            var legacy=JsonUtility.FromJson<SettingsProfile>("{\"schemaVersion\":1,\"name\":\"Saved settings\",\"platform\":\"Quest\",\"movement\":0,\"heightBoost\":0.3}");legacy.Validate();
            Require("walking_preferences_upgrade",legacy.schemaVersion==1&&legacy.movementRevision==1&&legacy.movement==MoveStyle.Smooth&&legacy.heightBoost==.3f,"The existing settings serializer adopts walking and preserves player height and the cloud RPC's required schema version 1.");
            legacy.movement=MoveStyle.Teleport;var roundTrip=JsonUtility.FromJson<SettingsProfile>(JsonUtility.ToJson(legacy));roundTrip.Validate();
            Require("walking_preferences_teleport_round_trip",roundTrip.movement==MoveStyle.Teleport,"A deliberately saved teleport preference survives reload.");
            try
            {
                preferences.movement=MoveStyle.Smooth;preferences.leftHanded=false;
                foreach(var mode in new[]{LearningMode.IdentifyEnumerate,LearningMode.AnatomyCases,LearningMode.GuidedDissection,LearningMode.AssembleAnatomy})
                {
                    Grip(false);Trigger(false);leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                    var variant=app.Catalog.units.SelectMany(u=>u.variants).First(v=>v.mode==mode&&v.difficulty==Difficulty.Easy&&app.Structures.ContainsKey(v.objectives[0].targetId));
                    app.StartActivity(variant,SessionPhase.Practice);yield return 16;
                    var objective=app.Engine.Current;var target=app.Structures[objective.targetId];
                    Require("all_mode_grab/surface/"+mode,app.Presentation.TryGetExposedSurface(target,Camera.main.transform.position,out var surface),"The current anatomy has an exposed surface.");
                    var direction=(surface-Camera.main.transform.position).normalized;var start=surface-direction*.07f;var rotation=Quaternion.LookRotation(direction,Vector3.up);
                    SetHandWorld(start,rotation);yield return 8;
                    var position=target.transform.position;var targetRotation=target.transform.rotation;var mesh=target.Filter.sharedMesh;int actions=app.Engine.Attempt.actions.Count;
                    if(mode==LearningMode.GuidedDissection)
                    {
                        Grip(true);yield return 8;SetHandWorld(start+Vector3.up*.09f,rotation);yield return 8;Grip(false);yield return 8;
                        Require("all_mode_grab/dissection_anchors_whole_layer",!target.Grab.isSelected&&Vector3.Distance(position,target.transform.position)<.001f&&app.Engine.Attempt.actions.Count==actions,"Cutting holds the whole layer in place; only a separated chunk can move.");
                    }
                    else
                    {
                    Grip(true);yield return 12;
                    Require("all_mode_grab/acquired/"+mode,target.Grab.isSelected&&hand.interactablesSelected.Any(x=>ReferenceEquals(x,target.Grab)),"A real controller grip acquires visible anatomy in "+mode+"; canManipulate="+app.CanManipulate(target));
                    Require("all_mode_grab/no_jump/"+mode,Vector3.Distance(position,target.transform.position)<.005f,"Stationary pickup preserves the anatomy pose.");
                    SetHandWorld(start+Vector3.up*.09f,rotation);yield return 12;
                    Require("all_mode_grab/follows/"+mode,Vector3.Distance(target.transform.position,position+Vector3.up*.09f)<.015f,"The anatomy follows a deliberate controller lift.");
                    SetHandWorld(start+Vector3.up*.09f,Quaternion.AngleAxis(20,Vector3.up)*rotation);yield return 12;
                    Require("all_mode_grab/rotates/"+mode,Quaternion.Angle(target.transform.rotation,targetRotation)>15,"The anatomy can be turned for inspection.");
                    Grip(false);yield return 12;
                    Require("all_mode_grab/no_false_credit/"+mode,app.Engine.Attempt.actions.Count==actions&&!app.Engine.IsComplete(objective.id)&&target.Filter.sharedMesh==mesh,"Inspection does not change the mesh or submit learning evidence.");
                    if(mode!=LearningMode.AssembleAnatomy)
                        Require("all_mode_grab/returns/"+mode,Vector3.Distance(position,target.transform.position)<.005f&&Quaternion.Angle(targetRotation,target.transform.rotation)<.5f,"Inspected anatomy returns on release so the station remains usable.");
                    }

                    // Aim away from world-space UI so the stick is locomotion input.
                    SetHandWorld(Camera.main.transform.position,Quaternion.LookRotation(Vector3.up,Vector3.forward));yield return 5;
                    var rig=Camera.main.transform.parent;var rigPosition=rig.position;
                    leftState.deviceRotation=Quaternion.Inverse(rig.rotation)*Quaternion.LookRotation(Vector3.up,Vector3.forward);QueueAll();yield return 12;
                    leftState.primary2DAxis=new Vector2(.1f,.1f);QueueAll();yield return 10;
                    Require("all_mode_walk/deadzone/"+mode,Vector3.Distance(rig.position,rigPosition)<.001f,"A resting stick cannot drift the player.");
                    foreach(var axis in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
                    {
                        leftState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;rigPosition=rig.position;
                        var forward=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;
                        var expected=forward*axis.y+Vector3.Cross(Vector3.up,forward)*axis.x;
                        leftState.primary2DAxis=axis;QueueAll();yield return 12;
                        Require("all_mode_walk/direction/"+mode+"/"+axis,Vector3.Dot(rig.position-rigPosition,expected)>.015f&&Mathf.Abs(rig.position.y-rigPosition.y)<.001f,"Joystick delta="+(rig.position-rigPosition).ToString("F5")+"; expected direction="+expected+"; dt="+Time.deltaTime+"; canInteract="+app.CanInteract+"; UI capture="+app.UI.XRPanelCaptured+"; input="+LearningXRInput.Read(UnityEngine.XR.XRNode.LeftHand).axis+"; rig="+rigPosition+"; head="+Camera.main.transform.position);
                    }
                    leftState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                    if(mode==LearningMode.IdentifyEnumerate)
                    {
                        SetHandWorld(start,rotation);yield return 6;Grip(true);yield return 8;
                        Require("all_mode_grab/regrip_before_pause",target.Grab.isSelected,"The interruption check starts with a real held part.");
                        SetHandWorld(start+Vector3.up*.09f,rotation);yield return 8;
                        var carriedPosition=target.transform.position;rigPosition=rig.position;
                        leftState.primary2DAxis=Vector2.down;QueueAll();yield return 10;
                        Require("all_mode_grab/carry_while_walking",target.Grab.isSelected&&Vector3.Distance(rig.position,rigPosition)>.015f&&Vector3.Distance(target.transform.position-carriedPosition,rig.position-rigPosition)<.01f,"Held anatomy follows the controller while the player walks.");
                        leftState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                        Menu(true);yield return 8;Menu(false);yield return 6;
                        Require("all_mode_grab/pause_restores",!target.Grab.isSelected&&Vector3.Distance(position,target.transform.position)<.005f,"Pause releases inspected anatomy and returns it without credit.");
                        rigPosition=rig.position;leftState.primary2DAxis=Vector2.up;QueueAll();yield return 8;
                        Require("all_mode_walk/pause_blocks",Vector3.Distance(rig.position,rigPosition)<.001f,"Pause blocks joystick movement.");
                        Menu(true);yield return 8;Menu(false);yield return 8;
                        Require("all_mode_walk/held_stick_resume_blocks",Vector3.Distance(rig.position,rigPosition)<.001f,"Unpausing requires a neutral stick before walking resumes.");
                        Grip(false);leftState.primary2DAxis=Vector2.zero;QueueAll();yield return 6;
                        preferences.leftHanded=true;rightState.primary2DAxis=Vector2.up;QueueAll();yield return 10;
                        Require("all_mode_walk/left_handed",Vector3.Distance(rig.position,rigPosition)>.015f,"Left-handed controls walk with the right stick.");
                        preferences.leftHanded=false;rightState.primary2DAxis=Vector2.zero;QueueAll();yield return 4;
                        var controls=FindFirstObjectByType<LearningControls>();preferences.heightBoost=.8f;controls.ApplyComfort();yield return 8;
                        rigPosition=rig.position;leftState.primary2DAxis=Vector2.down;QueueAll();yield return 12;
                        Require("all_mode_walk/maximum_height_boost",Vector3.Distance(rig.position,rigPosition)>.015f&&Mathf.Abs(rig.position.y-rigPosition.y)<.001f,"Walking preserves the maximum assisted height instead of colliding with the ceiling.");
                        leftState.primary2DAxis=Vector2.zero;QueueAll();yield return 6;
                        preferences.heightBoost=savedBoost;controls.ApplyComfort();yield return 8;
                        SetHandWorld(start,rotation);yield return 6;Grip(true);yield return 8;
                        Require("all_mode_grab/regrip_before_tracking_loss",target.Grab.isSelected,"The tracking-loss check starts with a real held part.");
                        SetHandWorld(start+Vector3.up*.09f,rotation);yield return 8;
                        SetHead(headState.devicePosition,headState.deviceRotation,false);QueueAll();yield return 8;
                        Require("all_mode_grab/tracking_loss_restores",!target.Grab.isSelected&&Vector3.Distance(position,target.transform.position)<.005f&&app.Engine.Attempt.actions.Count==actions,"Lost tracking returns inspected anatomy without credit.");
                        SetHead(headState.devicePosition,headState.deviceRotation,true);QueueAll();yield return 8;
                        Require("all_mode_grab/tracking_recovery_requires_release",!target.Grab.isSelected,"A grip held through tracking recovery cannot reacquire the part.");
                        Grip(false);yield return 4;
                    }
                    if(mode==LearningMode.AssembleAnatomy)
                    {
                        Vector3 otherSurface=default;
                        var other=app.Structures.Values.First(s=>s!=target&&s.gameObject.activeInHierarchy&&app.Presentation.TryGetExposedSurface(s,Camera.main.transform.position,out otherSurface));
                        var otherPosition=other.transform.position;var otherRotation=other.transform.rotation;
                        // A ray pickup starts outside nearby anatomy so a different valid proximity target cannot win.
                        var pickup=Camera.main.transform.position;var toward=(otherSurface-pickup).normalized;
                        SetHandWorld(pickup,Quaternion.LookRotation(toward,Vector3.up));yield return 6;Grip(true);yield return 8;
                        Require("all_mode_grab/assembly_context",other.Grab.isSelected,"Assembly also permits inspection of visible anatomy outside the current placement target. Expected="+other.name+"; selected="+string.Join(",",hand.interactablesSelected.Select(x=>(x as Component)?.name)));
                        SetHandWorld(pickup+Vector3.up*.09f,Quaternion.LookRotation(toward,Vector3.up));yield return 8;Grip(false);yield return 8;
                        Require("all_mode_grab/assembly_context_returns",Vector3.Distance(otherPosition,other.transform.position)<.005f&&Quaternion.Angle(otherRotation,other.transform.rotation)<.5f&&app.Engine.Attempt.actions.Count==actions,"Inspecting another assembly part preserves its previous pose and cannot place it or earn credit.");
                    }
                }
            }
            finally
            {
                preferences.movement=savedMovement;preferences.leftHanded=savedHandedness;preferences.heightBoost=savedBoost;leftState=savedLeft;
                Grip(false);Trigger(false);leftState.primary2DAxis=rightState.primary2DAxis=Vector2.zero;QueueAll();
            }
        }
    }
}
#endif
