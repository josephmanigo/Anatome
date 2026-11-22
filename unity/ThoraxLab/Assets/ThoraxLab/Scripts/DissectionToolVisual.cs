using UnityEngine;
namespace ThoraxLab {
public class DissectionToolVisual:MonoBehaviour {
 public DissectionSession session;GameObject scalpel,saw;
 LabHandInteractor hand;Transform sawBlade;
 void Start(){
  hand=GetComponent<LabHandInteractor>();
  scalpel=Learning.InstrumentGeometry.Create(transform,"dissection-scalpel");
  saw=Learning.InstrumentGeometry.Create(transform,"dissection-saw");
  sawBlade=saw.transform.Find(Learning.InstrumentGeometry.SawBladeName);
 }
 void LateUpdate(){
  if(!scalpel)return;
  var app=Learning.LabRuntime.Instance;
  bool active=hand&&hand.HasRayCutTarget&&session&&session.mode==LabMode.Free&&(!app||app.CanUseCuttingTool);
  scalpel.SetActive(active&&!session.saw);saw.SetActive(active&&session.saw);
  if(sawBlade){
   bool running=saw.activeSelf&&hand.DrivingBlade&&app?.Settings?.Current.reducedMotion!=true;
   sawBlade.localRotation=running?Quaternion.AngleAxis(1.5f*Mathf.Sin(Time.unscaledTime*2*Mathf.PI*9),Vector3.right):Quaternion.identity;
  }
  if(!active)return;
  float tip=session.saw?Learning.InstrumentGeometry.SawTipZ:Learning.InstrumentGeometry.ScalpelTipZ;
  var model=session.saw?saw:scalpel;
  model.transform.SetPositionAndRotation(hand.RayCutPoint-hand.RayCutRotation*Vector3.forward*tip,hand.RayCutRotation);
 }
}
}
