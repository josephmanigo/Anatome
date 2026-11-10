using UnityEngine;
namespace ThoraxLab {
public class DissectionToolVisual:MonoBehaviour {
 public DissectionSession session;GameObject scalpel,saw;
 void Start(){
  scalpel=Learning.InstrumentGeometry.Create(transform,"dissection-scalpel");
  saw=Learning.InstrumentGeometry.Create(transform,"dissection-saw");
 }
 void LateUpdate(){if(!scalpel)return;var hand=GetComponent<LabHandInteractor>();var app=Learning.LabRuntime.Instance;bool tracked=!hand||(Learning.LearningXRInput.Read(hand.hand).tracked&&Learning.LearningXRInput.HeadTracked);bool active=tracked&&session&&session.mode==LabMode.Free&&(!app||app.CanUseCuttingTool);scalpel.SetActive(active&&!session.saw&&hand&&hand.HasRayCutTarget);saw.SetActive(active&&session.saw&&hand&&hand.HasRayCutTarget);if(saw.activeSelf)saw.transform.SetPositionAndRotation(hand.RayCutPoint-hand.RayCutRotation*Vector3.forward*.09f,hand.RayCutRotation);if(scalpel.activeSelf)scalpel.transform.SetPositionAndRotation(hand.RayCutPoint-hand.RayCutRotation*Vector3.forward*.09f,hand.RayCutRotation);}
}
}
