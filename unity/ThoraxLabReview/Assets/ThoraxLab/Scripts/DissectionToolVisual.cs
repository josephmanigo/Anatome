using UnityEngine;
namespace ThoraxLab {
public class DissectionToolVisual:MonoBehaviour {
 public DissectionSession session;GameObject scalpel,saw;
 void Start(){
  scalpel=Learning.InstrumentGeometry.Create(transform,"dissection-scalpel");
  saw=Learning.InstrumentGeometry.Create(transform,"dissection-saw");
 }
 void Update(){if(!scalpel)return;var hand=GetComponent<LabHandInteractor>();var app=Learning.LabRuntime.Instance;bool tracked=!hand||(Learning.LearningXRInput.Read(hand.hand).tracked&&Learning.LearningXRInput.HeadTracked);bool active=tracked&&session&&session.mode==LabMode.Free&&(!app||app.CanUseCuttingTool);scalpel.SetActive(active&&!session.saw);saw.SetActive(active&&session.saw);}
}
}
