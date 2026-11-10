using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ThoraxLab {
public class LabHandInteractor:XRBaseInteractor {
 readonly Learning.DissectionContactMonitor cuttingContacts=new();
 bool gripArmed=true;
 Learning.XRInteractionSource lastSource;
 Transform controllerVisual,pointerTip;
 public XRNode hand;public DissectionSession session;InputDevice device;bool grip,trigger,previousTrigger,previousGrip,toggledGrip,overUI;AnatomyStructure hit;LabButton button;Learning.ProcedureTool procedureTool;LineRenderer line;readonly List<float> stroke=new();Vector3 previousPoint;readonly Collider[] near=new Collider[32];
 public override bool isSelectActive{get{var app=Learning.LabRuntime.Instance;var current=Learning.LearningXRInput.Read(hand);bool pressed=app?.Settings?.Current.toggleGrip==true&&current.source!=Learning.XRInteractionSource.BareHand?grip:current.grip;return lastSource==current.source&&gripArmed&&current.tracked&&pressed&&session&&!session.busy&&!overUI&&(!app||app.CanInteract);}}
 public override void GetValidTargets(List<IXRInteractable> targets){targets.Clear();if(procedureTool&&procedureTool.grab)targets.Add(procedureTool.grab);else if(hit&&hit.Grab&&(!Learning.LabRuntime.Instance||Learning.LabRuntime.Instance.CanManipulate(hit)))targets.Add(hit.Grab);}
 protected override void Start(){base.Start();line=GetComponent<LineRenderer>();var tools=gameObject.AddComponent<DissectionToolVisual>();tools.session=session;var avatar=GetComponent<Learning.GlovedHandVisual>();if(avatar){avatar.enabled=false;if(avatar.VisualRoot)Destroy(avatar.VisualRoot.gameObject);Destroy(avatar);}CreateControllerVisual();}
 void CreateControllerVisual(){
  var pointerMaterial=Resources.Load<Material>("Learning/GuidanceLine");
  if(!pointerMaterial){Debug.LogError("Controller feedback material is missing.");return;}
  var controllerMaterial=Resources.Load<Material>("Learning/ControllerBody");var faceMaterial=Resources.Load<Material>("Learning/ControllerFace");
  if(!controllerMaterial||!faceMaterial){Debug.LogError("Bundled controller materials are missing. Run LearningBuild.Prepare.");return;}
  controllerVisual=new GameObject(hand+" visible controller").transform;controllerVisual.SetParent(transform,false);
  VisualPart("Grip",PrimitiveType.Capsule,controllerVisual,new Vector3(0,-.035f,-.025f),new Vector3(.036f,.06f,.036f),controllerMaterial);
  VisualPart("Controller head",PrimitiveType.Sphere,controllerVisual,new Vector3(0,.022f,.005f),new Vector3(.055f,.025f,.065f),faceMaterial);
  VisualPart("Thumbstick",PrimitiveType.Cylinder,controllerVisual,new Vector3(hand==XRNode.LeftHand?.012f:-.012f,.037f,.01f),new Vector3(.014f,.004f,.014f),faceMaterial);
  VisualPart("Trigger",PrimitiveType.Cube,controllerVisual,new Vector3(0,.005f,.045f),new Vector3(.018f,.014f,.014f),pointerMaterial);
  pointerTip=VisualPart("Pointer target",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.007f,pointerMaterial);
  controllerVisual.gameObject.SetActive(false);pointerTip.gameObject.SetActive(false);
  if(line){line.sharedMaterial=pointerMaterial;line.startWidth=.002f;line.endWidth=.0012f;}
 }
 static Transform VisualPart(string name,PrimitiveType shape,Transform parent,Vector3 position,Vector3 scale,Material material){
  var part=GameObject.CreatePrimitive(shape);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
  var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return part.transform;
 }
 void Update(){
  var app=Learning.LabRuntime.Instance;
  // Keep observing button edges while hidden so tracking recovery cannot replay a held trigger.
  device=InputDevices.GetDeviceAtXRNode(hand);var xr=Learning.LearningXRInput.Read(hand);bool sourceChanged=lastSource!=xr.source;lastSource=xr.source;if(sourceChanged||!xr.tracked||!Learning.LearningXRInput.HeadTracked||(app&&!app.Focused)){if(line)line.enabled=false;grip=false;gripArmed=false;previousGrip=xr.grip;previousTrigger=xr.trigger;toggledGrip=false;stroke.Clear();cuttingContacts.EndStroke();foreach(var selected in interactablesSelected){var held=(selected as Component)?.GetComponent<Learning.ProcedureTool>();if(held)held.activating=false;}if(app&&app.UI)app.UI.CancelXRPointer((int)hand+1);return;}
  transform.SetLocalPositionAndRotation(xr.position,xr.rotation);
  grip=xr.grip;trigger=xr.trigger;
  var preferences=app?.Settings?.Current;
  bool rawGrip=grip;if(!rawGrip)gripArmed=true;if(preferences!=null&&preferences.toggleGrip&&xr.source!=Learning.XRInteractionSource.BareHand){if(gripArmed&&grip&&!previousGrip)toggledGrip=!toggledGrip;grip=toggledGrip;}previousGrip=rawGrip;
  var ray=InputRay(xr);hit=null;button=null;procedureTool=null;
  Vector2 uiScroll=xr.axis;overUI=app&&app.UI&&app.UI.RayPointer(ray,trigger&&!previousTrigger,uiScroll,rawGrip,trigger,(int)hand+1);
  if(Physics.Raycast(ray,out var h,3)) {hit=h.collider.GetComponentInParent<AnatomyStructure>();button=h.collider.GetComponent<LabButton>();procedureTool=h.collider.GetComponentInParent<Learning.ProcedureTool>();if(line){line.SetPosition(0,ray.origin);line.SetPosition(1,h.point);}}
  else if(line){line.SetPosition(0,ray.origin);line.SetPosition(1,ray.GetPoint(2));}
  float closest=float.MaxValue;int count=Physics.OverlapSphereNonAlloc(transform.position,.035f,near);for(int i=0;i<count;i++){var s=near[i].GetComponent<AnatomyStructure>();if(s){float distance=s.Renderer.bounds.SqrDistance(transform.position);if(distance<closest){closest=distance;hit=s;}}}
  foreach(var selected in interactablesSelected){var held=(selected as Component)?.GetComponent<Learning.ProcedureTool>();if(held)held.activating=trigger&&!overUI&&app&&app.CanInteract;}
  if(app&&(!app.CanInteract||overUI)){if(!app.CanInteract){gripArmed=!rawGrip;toggledGrip=false;}stroke.Clear();cuttingContacts.EndStroke();previousTrigger=trigger;return;}
  if(trigger&&!previousTrigger){if(button){button.Invoke();}else if(hit&&!procedureTool){if(app)app.Select(hit,h.collider&&h.collider.GetComponentInParent<AnatomyStructure>()==hit?h.point:(Vector3?)null);else if(session.selected!=hit)session.Select(hit);stroke.Clear();previousPoint=transform.position;}}
  if(trigger&&session.mode==LabMode.Free&&app&&app.CanUseCuttingTool&&interactablesSelected.Count==0)
   cuttingContacts.Sample(app,transform.position+transform.forward*.09f,transform.rotation,session.saw);
  else cuttingContacts.EndStroke();
  if(cuttingContacts.StrokeBlocked){stroke.Clear();previousTrigger=trigger;return;}
  if(trigger&&session.mode==LabMode.Free&&(!app||app.CanCut)&&interactablesSelected.Count==0&&session.selected&&Vector3.Distance(previousPoint,transform.position)>.002f&&stroke.Count<128*24){
    var center=transform.position+transform.forward*.09f;
    if(session.selected.Renderer.bounds.SqrDistance(center)<.0025f){NativeCutting.AddPose(stroke,session.selected.transform,center,transform.rotation,session.saw?.0015f:.0007f,session.saw?.015f:.003f,session.saw?.025f:.018f);previousPoint=transform.position;if(xr.source==Learning.XRInteractionSource.Controller&&(preferences==null||preferences.vibration))device.SendHapticImpulse(0,session.saw?.12f:.035f,.025f);}
  }
  if(!trigger&&previousTrigger&&stroke.Count>=48){session.Cut(stroke);stroke.Clear();}
  previousTrigger=trigger;
 }
 Ray InputRay(Learning.LearningXRInput.State state)=>state.separateAim?
  new Ray(transform.parent.TransformPoint(state.rayPosition),transform.parent.rotation*state.rayRotation*Vector3.forward):new Ray(transform.position,transform.forward);
 void LateUpdate(){
  var app=Learning.LabRuntime.Instance;var input=Learning.LearningXRInput.Read(hand);bool tracked=input.tracked&&Learning.LearningXRInput.HeadTracked&&(!app||app.Focused);
  if(controllerVisual)controllerVisual.gameObject.SetActive(tracked&&input.source!=Learning.XRInteractionSource.BareHand);
  if(pointerTip)pointerTip.gameObject.SetActive(false);
  if(!line)return;line.enabled=false;
  if(!tracked||interactablesSelected.Count>0)return;
  var ray=InputRay(input);bool show=false;var endpoint=ray.GetPoint(2);
  if(app&&app.UI){var panel=app.UI.Root;var plane=new Plane(panel.forward,panel.position);if(plane.Raycast(ray,out float distance)&&distance>0&&distance<3){var local=panel.InverseTransformPoint(ray.GetPoint(distance));if(panel.rect.Contains(new Vector2(local.x,local.y))){show=true;endpoint=ray.GetPoint(distance);}}}
  if(!show&&Physics.Raycast(ray,out var contact,3)){show=contact.collider.GetComponentInParent<AnatomyStructure>()||contact.collider.GetComponentInParent<Learning.ProcedureTool>()||contact.collider.GetComponent<LabButton>();endpoint=contact.point;}
  line.enabled=true;line.SetPosition(0,ray.origin);line.SetPosition(1,endpoint);
  if(pointerTip&&show){pointerTip.position=endpoint;pointerTip.gameObject.SetActive(true);}
 }
}
}
