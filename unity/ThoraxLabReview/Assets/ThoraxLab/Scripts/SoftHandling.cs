using UnityEngine;
namespace ThoraxLab {
// Small, reversible whole-volume strain for handling feedback. No physiology model.
// Applied to object scale so mesh and collision agree; settles before a cut starts.
public class SoftHandling:MonoBehaviour {
 AnatomyStructure structure;Vector3 restScale;float strain,velocity;
 void Start(){structure=GetComponent<AnatomyStructure>();restScale=transform.localScale;}
 void LateUpdate(){if(!structure||structure.record.tissue=="bone")return;bool held=structure.Grab&&structure.Grab.isSelected;strain=Mathf.SmoothDamp(strain,held?.012f:0,ref velocity,.1f);if(strain<.0001f&&!held)strain=0;transform.localScale=Vector3.Scale(restScale,new Vector3(1+strain,1-strain*2,1+strain));}
 void OnDisable(){if(structure)transform.localScale=restScale;strain=velocity=0;}
 public void Freeze(){enabled=false;}
 public void Thaw(){enabled=true;}
}
}
