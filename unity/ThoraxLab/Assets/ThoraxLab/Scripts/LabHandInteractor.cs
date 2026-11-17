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
 Learning.MuseumExhibit museumExhibit;
 Transform controllerVisual,pointerTip;
 bool cutCancelled;
 public bool HasRayCutTarget {get;private set;}
 public Vector3 RayCutPoint {get;private set;}
 public Quaternion RayCutRotation {get;private set;}
 public XRNode hand;public DissectionSession session;InputDevice device;bool grip,trigger,previousTrigger,previousGrip,toggledGrip,overUI;AnatomyStructure hit;LabButton button;Learning.ProcedureTool procedureTool;LineRenderer line;readonly List<float> stroke=new();Vector3 previousPoint;readonly Collider[] near=new Collider[32];
 public override bool isSelectActive{get{var app=Learning.LabRuntime.Instance;var current=Learning.LearningXRInput.Read(hand);bool pressed=app?.Settings?.Current.toggleGrip==true&&current.source!=Learning.XRInteractionSource.BareHand?grip:current.grip;return lastSource==current.source&&gripArmed&&current.tracked&&pressed&&session&&!session.busy&&(!overUI||interactablesSelected.Count>0)&&(!app||app.CanInteract);}}
 public override void GetValidTargets(List<IXRInteractable> targets){targets.Clear();if(interactablesSelected.Count>0){targets.Add(interactablesSelected[0]);return;}if(museumExhibit&&museumExhibit.Grab&&Learning.LabRuntime.Instance&&Learning.LabRuntime.Instance.MuseumActive)targets.Add(museumExhibit.Grab);else if(procedureTool&&procedureTool.grab)targets.Add(procedureTool.grab);else if(hit&&hit.Grab&&(!Learning.LabRuntime.Instance||Learning.LabRuntime.Instance.CanManipulate(hit)))targets.Add(hit.Grab);}
 protected override void Start(){base.Start();line=GetComponent<LineRenderer>();var tools=gameObject.AddComponent<DissectionToolVisual>();tools.session=session;var avatar=GetComponent<Learning.GlovedHandVisual>();if(avatar){avatar.enabled=false;if(avatar.VisualRoot)Destroy(avatar.VisualRoot.gameObject);Destroy(avatar);}CreateControllerVisual();}
 void CreateControllerVisual(){
  var pointerMaterial=Resources.Load<Material>("Learning/GuidanceLine");
  if(!pointerMaterial){Debug.LogError("Controller feedback material is missing.");return;}
  var controllerMaterial=Resources.Load<Material>("Learning/ControllerBody");var faceMaterial=Resources.Load<Material>("Learning/ControllerFace");
  if(!controllerMaterial||!faceMaterial){Debug.LogError("Bundled controller materials are missing. Run LearningBuild.Prepare.");return;}
  controllerVisual=new GameObject(hand+" visible controller").transform;controllerVisual.SetParent(transform,false);
  controllerVisual.gameObject.AddComponent<Learning.ControllerVisual>().Initialize(hand,controllerMaterial,faceMaterial,pointerMaterial);
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
  HasRayCutTarget=false;
  // Keep observing button edges while hidden so tracking recovery cannot replay a held trigger.
  device=InputDevices.GetDeviceAtXRNode(hand);var xr=Learning.LearningXRInput.Read(hand);bool sourceChanged=lastSource!=xr.source;lastSource=xr.source;if(sourceChanged||!xr.tracked||!Learning.LearningXRInput.HeadTracked||(app&&!app.Focused)){if(line)line.enabled=false;grip=false;gripArmed=false;previousGrip=xr.grip;previousTrigger=xr.trigger;toggledGrip=false;cutCancelled|=xr.trigger;stroke.Clear();cuttingContacts.EndStroke();foreach(var selected in interactablesSelected){var held=(selected as Component)?.GetComponent<Learning.ProcedureTool>();if(held)held.activating=false;}if(app&&app.UI)app.UI.CancelXRPointer((int)hand+1);return;}
  transform.SetLocalPositionAndRotation(xr.position,xr.rotation);
  grip=xr.grip;trigger=xr.trigger;
  var preferences=app?.Settings?.Current;
  bool rawGrip=grip;if(!rawGrip)gripArmed=true;if(preferences!=null&&preferences.toggleGrip&&xr.source!=Learning.XRInteractionSource.BareHand){if(gripArmed&&grip&&!previousGrip)toggledGrip=!toggledGrip;grip=toggledGrip;}previousGrip=rawGrip;
  var ray=InputRay(xr);hit=null;button=null;procedureTool=null;museumExhibit=null;
  // During a lesson, the movement stick stays available even when its ray crosses the panel.
  bool movementHand=hand==(preferences?.leftHanded==true?XRNode.RightHand:XRNode.LeftHand);
  Vector2 uiScroll=app&&app.CanInteract&&movementHand?Vector2.zero:xr.axis;
  overUI=app&&app.UI&&app.UI.RayPointer(ray,trigger&&!previousTrigger,uiScroll,rawGrip&&interactablesSelected.Count==0,trigger,(int)hand+1,transform.rotation);
  if(app&&app.Inspector&&!movementHand)app.Inspector.HandleControllerAxis(xr,overUI);
  if(Physics.Raycast(ray,out var h,3,~0,QueryTriggerInteraction.Ignore)) {hit=h.collider.GetComponentInParent<AnatomyStructure>();button=h.collider.GetComponent<LabButton>();procedureTool=h.collider.GetComponentInParent<Learning.ProcedureTool>();museumExhibit=h.collider.GetComponentInParent<Learning.MuseumExhibit>();if(line){line.SetPosition(0,ray.origin);line.SetPosition(1,h.point);}}
  else if(line){line.SetPosition(0,ray.origin);line.SetPosition(1,ray.GetPoint(2));}
  float closest=float.MaxValue;int count=Physics.OverlapSphereNonAlloc(transform.position,.035f,near);for(int i=0;i<count;i++){var exhibit=near[i].GetComponentInParent<Learning.MuseumExhibit>();if(exhibit&&app&&app.MuseumActive){float distance=exhibit.Renderer.bounds.SqrDistance(transform.position);if(distance<closest){closest=distance;museumExhibit=exhibit;hit=null;procedureTool=null;}continue;}var s=near[i].GetComponentInParent<AnatomyStructure>();if(s&&(!app||app.CanManipulate(s))){float distance=s.Renderer.bounds.SqrDistance(transform.position);if(distance<closest){closest=distance;hit=s;procedureTool=null;museumExhibit=null;}}}
  foreach(var selected in interactablesSelected){var held=(selected as Component)?.GetComponent<Learning.ProcedureTool>();if(held)held.activating=trigger&&!overUI&&app&&app.CanInteract;}
  if(app&&(!app.CanInteract||overUI)){if(!app.CanInteract){gripArmed=!rawGrip;toggledGrip=false;}stroke.Clear();cutCancelled|=trigger;cuttingContacts.EndStroke();previousTrigger=trigger;return;}
  var selectionHit=app&&app.CanUseCuttingTool?(h.collider?h.collider.GetComponentInParent<AnatomyStructure>():null):hit;
  if(trigger&&!previousTrigger){stroke.Clear();cutCancelled=false;cuttingContacts.EndStroke();if(button){button.Invoke();}else if(museumExhibit&&app&&app.MuseumActive){museumExhibit.Select();}else if(selectionHit&&!procedureTool){if(app)app.Select(selectionHit,h.collider&&h.collider.GetComponentInParent<AnatomyStructure>()==selectionHit?h.point:(Vector3?)null);else if(session.selected!=selectionHit)session.Select(selectionHit);}}
  var center=transform.position+transform.forward*.09f;var bladeRotation=transform.rotation;
  bool toolActive=session.mode==LabMode.Free&&app&&app.CanUseCuttingTool&&interactablesSelected.Count==0;
  var rayStructure=h.collider?h.collider.GetComponentInParent<AnatomyStructure>():null;
  if(toolActive&&rayStructure){
   // Remote reach ends at the first real surface. Blade depth stays unchanged.
   center=h.point-h.normal*.001f;
   var bladeUp=Vector3.ProjectOnPlane(transform.up,h.normal).normalized;
   if(bladeUp.sqrMagnitude<.01f)bladeUp=Vector3.ProjectOnPlane(transform.right,h.normal).normalized;
   bladeRotation=Quaternion.LookRotation(-h.normal,bladeUp);
   HasRayCutTarget=true;RayCutPoint=center;RayCutRotation=bladeRotation;
  }
  bool contact=toolActive&&HasRayCutTarget;
  if(trigger&&contact&&!cutCancelled){
   // Learners may hold the trigger before the blade reaches the surface.
   if(stroke.Count==0&&rayStructure!=session.selected)app.Select(rayStructure,h.point);
   // A missed surface, occluder or large jump must never bridge separate tissue.
   if(stroke.Count>0&&(rayStructure!=session.selected||Vector3.Distance(previousPoint,center)>.06f)){stroke.Clear();cutCancelled=true;app.Message("Stroke cancelled. Keep the ray on one surface, release, then try again.");}
   if(!cutCancelled){cuttingContacts.Sample(app,center,bladeRotation,session.saw);if(cuttingContacts.StrokeBlocked){stroke.Clear();cutCancelled=true;}}
   if(!cutCancelled&&app.CanCut&&session.selected&&rayStructure==session.selected&&(stroke.Count==0||Vector3.Distance(previousPoint,center)>.002f)&&stroke.Count<128*24&&session.selected.Renderer.bounds.SqrDistance(center)<.0025f){
    NativeCutting.AddPose(stroke,session.selected.transform,center,bladeRotation,session.saw?.008f:.008f,session.saw?.012f:.008f,session.saw?.025f:.018f);previousPoint=center;
    if(xr.source==Learning.XRInteractionSource.Controller&&(preferences==null||preferences.vibration))device.SendHapticImpulse(0,session.saw?.12f:.035f,.025f);
   }
  }else{cuttingContacts.EndStroke();if(trigger&&stroke.Count>0){stroke.Clear();cutCancelled=true;app.Message("Stroke cancelled. Keep the ray on one surface, release, then try again.");}}
  if(!trigger&&previousTrigger){if(!cutCancelled&&stroke.Count>=48)session.Cut(stroke);else if(!cutCancelled&&stroke.Count>0)app.Message("Hold trigger and move the tool a short distance along the tissue, then release.");stroke.Clear();cutCancelled=false;}
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
  if(!show&&Physics.Raycast(ray,out var contact,3)){show=contact.collider.GetComponentInParent<AnatomyStructure>()||contact.collider.GetComponentInParent<Learning.ProcedureTool>()||contact.collider.GetComponentInParent<Learning.MuseumExhibit>()||contact.collider.GetComponent<LabButton>();endpoint=contact.point;}
  line.enabled=true;line.SetPosition(0,ray.origin);line.SetPosition(1,endpoint);
  if(pointerTip&&show){pointerTip.position=endpoint;pointerTip.gameObject.SetActive(true);}
 }
}
}
