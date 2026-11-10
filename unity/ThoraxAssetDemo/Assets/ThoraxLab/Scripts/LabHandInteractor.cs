using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ThoraxLab {
public class LabHandInteractor:XRBaseInteractor {
 public XRNode hand;public DissectionSession session;InputDevice device;bool grip,trigger,previousTrigger;AnatomyStructure hit;LabButton button;LineRenderer line;readonly List<float> stroke=new();Vector3 previousPoint;readonly Collider[] near=new Collider[32];
 public override bool isSelectActive=>grip&&!session.busy;
 public override void GetValidTargets(List<IXRInteractable> targets){targets.Clear();if(hit&&hit.Grab)targets.Add(hit.Grab);}
 protected override void Start(){base.Start();line=GetComponent<LineRenderer>();var tools=gameObject.AddComponent<DissectionToolVisual>();tools.session=session;}
 void Update(){
  device=InputDevices.GetDeviceAtXRNode(hand);if(!device.isValid){grip=false;return;}
  device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p);device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q);transform.SetLocalPositionAndRotation(p,q);
  device.TryGetFeatureValue(CommonUsages.gripButton,out grip);device.TryGetFeatureValue(CommonUsages.triggerButton,out trigger);
  var ray=new Ray(transform.position,transform.forward);hit=null;button=null;
  if(Physics.Raycast(ray,out var h,3)) {hit=h.collider.GetComponent<AnatomyStructure>();button=h.collider.GetComponent<LabButton>();if(line){line.SetPosition(0,ray.origin);line.SetPosition(1,h.point);}}
  else if(line){line.SetPosition(0,ray.origin);line.SetPosition(1,ray.GetPoint(2));}
  float closest=float.MaxValue;int count=Physics.OverlapSphereNonAlloc(transform.position,.035f,near);for(int i=0;i<count;i++){var s=near[i].GetComponent<AnatomyStructure>();if(s){float distance=s.Renderer.bounds.SqrDistance(transform.position);if(distance<closest){closest=distance;hit=s;}}}
  if(trigger&&!previousTrigger){if(button){button.Invoke();}else if(hit){if(session.selected!=hit)session.Select(hit);stroke.Clear();previousPoint=transform.position;}}
  if(trigger&&session.mode==LabMode.Free&&session.selected&&Vector3.Distance(previousPoint,transform.position)>.002f&&stroke.Count<128*24){
    var center=transform.position+transform.forward*.09f;
    if(session.selected.Renderer.bounds.SqrDistance(center)<.0025f){NativeCutting.AddPose(stroke,session.selected.transform,center,transform.rotation,session.saw?.0015f:.0007f,session.saw?.015f:.003f,session.saw?.025f:.018f);previousPoint=transform.position;device.SendHapticImpulse(0,session.saw?.12f:.035f,.025f);}
  }
  if(!trigger&&previousTrigger&&stroke.Count>=48){session.Cut(stroke);stroke.Clear();}
  previousTrigger=trigger;
 }
}
}
