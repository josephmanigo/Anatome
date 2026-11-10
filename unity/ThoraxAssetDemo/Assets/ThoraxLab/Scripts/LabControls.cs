using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ThoraxLab {
public class LabControls:MonoBehaviour {
 public DissectionSession session;public Camera view;public Transform rig;public TextMesh panelText;
 readonly List<float> stroke=new();AnatomyStructure dragging;float grabDistance;Vector3 lastPoint;bool desktop,trackingStarted;string search="";Vector2 catalogScroll,panelScroll;float turnCooldown;
 void Update(){
  var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);desktop=!head.isValid;
  if(head.isValid){if(!trackingStarted){var inputs=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(inputs);foreach(var subsystem in inputs)subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);rig.position=new Vector3(.70f,0,1.0f);rig.rotation=Quaternion.Euler(0,-65,0);trackingStarted=true;}head.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p);head.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q);view.transform.SetLocalPositionAndRotation(p,q);}
  if(!desktop){var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);left.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 moveAxis);right.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 turnAxis);var forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;var locomotion=(forward*moveAxis.y+Vector3.Cross(Vector3.up,forward)*moveAxis.x)*(.8f*Time.deltaTime);var controller=rig.GetComponent<CharacterController>();if(controller)controller.Move(locomotion+Vector3.down*(.5f*Time.deltaTime));else rig.position+=locomotion;turnCooldown-=Time.deltaTime;if(Mathf.Abs(turnAxis.x)>.7f&&turnCooldown<=0){rig.RotateAround(view.transform.position,Vector3.up,Mathf.Sign(turnAxis.x)*30);turnCooldown=.35f;}if(Mathf.Abs(turnAxis.x)<.2f)turnCooldown=0;}
  if(panelText){string label=session.selected?session.selected.record.name+"\n"+session.selected.record.id:session.busy?"Processing cut…":"Select a structure";panelText.text=session.mode+"  ·  "+(session.saw?"BONE SAW":"SCALPEL")+"\n"+label;}
  if(!desktop||Keyboard.current==null||Mouse.current==null)return;
  var k=Keyboard.current;var mouse=Mouse.current;
  if(k.digit1Key.wasPressedThisFrame)session.SetMode(LabMode.Explore);if(k.digit2Key.wasPressedThisFrame)session.SetMode(LabMode.Guided);if(k.digit3Key.wasPressedThisFrame)session.SetMode(LabMode.Free);
  if(k.rKey.wasPressedThisFrame)session.ResetSpecimen();if(k.zKey.wasPressedThisFrame)session.Undo();if(k.nKey.wasPressedThisFrame)session.GuidedNext();if(k.bKey.wasPressedThisFrame)session.saw=!session.saw;
  float move=Time.deltaTime*1.1f;var delta=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));rig.position+=view.transform.TransformDirection(delta)*move;
  if(mouse.rightButton.isPressed){var d=mouse.delta.ReadValue();view.transform.Rotate(Vector3.up,d.x*.12f,Space.World);view.transform.Rotate(Vector3.right,-d.y*.12f,Space.Self);}
  if(mouse.leftButton.wasReleasedThisFrame){if(dragging){dragging.TrySnap();dragging=null;}if(stroke.Count>=48)session.Cut(stroke);stroke.Clear();}
  if(mouse.position.ReadValue().x<370||mouse.position.ReadValue().y>Screen.height-45)return;
  var ray=view.ScreenPointToRay(mouse.position.ReadValue());
  if(Physics.Raycast(ray,out var h,6)){
   var hit=h.collider.GetComponent<AnatomyStructure>();var button=h.collider.GetComponent<LabButton>();
   if(mouse.leftButton.wasPressedThisFrame){if(button)button.Invoke();else if(hit){session.Select(hit);stroke.Clear();lastPoint=h.point;if(session.mode==LabMode.Explore){dragging=hit;grabDistance=h.distance;}}}
   if(mouse.leftButton.isPressed&&session.mode==LabMode.Free&&session.selected&&hit==session.selected&&(stroke.Count==0||Vector3.Distance(lastPoint,h.point)>.002f)&&stroke.Count<128*24){NativeCutting.AddPose(stroke,hit.transform,h.point,Quaternion.LookRotation(-h.normal,view.transform.up),session.saw?.0015f:.0007f,.012f,.055f);lastPoint=h.point;}
  }
  if(dragging&&mouse.leftButton.isPressed){if(k.leftShiftKey.isPressed){var deltaMouse=mouse.delta.ReadValue();dragging.transform.Rotate(view.transform.up,-deltaMouse.x*.6f,Space.World);dragging.transform.Rotate(view.transform.right,deltaMouse.y*.6f,Space.World);}else dragging.transform.position=ray.GetPoint(grabDistance);}
 }
 void OnGUI(){if(!desktop)return;GUI.skin.button.fontSize=15;GUI.skin.label.fontSize=14;GUILayout.BeginArea(new Rect(16,16,345,Screen.height-32),GUI.skin.box);panelScroll=GUILayout.BeginScrollView(panelScroll);GUILayout.Label("THORAX LAB\nAnatomy teaching collection");GUILayout.Space(12);GUILayout.BeginHorizontal();if(GUILayout.Button("1 · Explore"))session.SetMode(LabMode.Explore);if(GUILayout.Button("2 · Guided"))session.SetMode(LabMode.Guided);if(GUILayout.Button("3 · Free"))session.SetMode(LabMode.Free);GUILayout.EndHorizontal();GUILayout.Space(12);GUILayout.Label(session.message);GUILayout.Space(12);
  if(session.mode==LabMode.Guided&&GUILayout.Button("Next layer [N]"))session.GuidedNext();
  if(session.mode==LabMode.Free){if(GUILayout.Button(session.saw?"Bone saw [B]":"Scalpel [B]"))session.saw=!session.saw;if(GUILayout.Button("Undo cut [Z]"))session.Undo();}
  GUILayout.BeginHorizontal();if(GUILayout.Button("Hide"))session.HideSelected();if(GUILayout.Button("Isolate"))session.Isolate();if(GUILayout.Button("Return"))session.ReturnSelected();GUILayout.EndHorizontal();
  foreach(string s in new[]{"All","Skeletal","Muscular","Respiratory","Cardiovascular","Nervous"})if(GUILayout.Button(s))session.ShowSystem(s);
  if(GUILayout.Button("Open chest"))session.OpenChest();GUILayout.Label("Find a structure");search=GUILayout.TextField(search);catalogScroll=GUILayout.BeginScrollView(catalogScroll,GUILayout.Height(150));foreach(var s in session.originals.Where(s=>string.IsNullOrEmpty(search)||s.record.name.IndexOf(search,System.StringComparison.OrdinalIgnoreCase)>=0||s.record.id.IndexOf(search,System.StringComparison.OrdinalIgnoreCase)>=0).OrderBy(s=>s.record.name))if(GUILayout.Button(s.record.name)){session.Inspect(s);view.transform.LookAt(s.Renderer.bounds.center);}GUILayout.EndScrollView();
  GUILayout.Label("Table height");float height=GUILayout.HorizontalSlider(session.specimen.position.y,-.2f,.25f);if(Mathf.Abs(height-session.specimen.position.y)>.001f)session.SetTableHeight(height);
  if(GUILayout.Button("Restore specimen [R]"))session.ResetSpecimen();GUILayout.Space(12);GUILayout.Label("WASD move · Q/E down/up\nRight mouse: look\nLeft mouse: select / drag / cut\nChanges to mode restore the specimen.");GUILayout.EndScrollView();GUILayout.EndArea();}
}
}
