using UnityEngine;
namespace ThoraxLab {
public class DissectionToolVisual:MonoBehaviour {
 readonly System.Collections.Generic.List<Material> owned=new();
 public DissectionSession session;GameObject scalpel,saw;Transform blade;
 void Start(){
  scalpel=new GameObject("Scalpel");scalpel.transform.SetParent(transform,false);
  Part(scalpel.transform,"Textured scalpel handle",new Vector3(0,0,.035f),new Vector3(.011f,.008f,.08f),new Color(.32f,.37f,.4f));
  Part(scalpel.transform,"Scalpel blade",new Vector3(0,0,.09f),new Vector3(.0014f,.006f,.036f),new Color(.74f,.77f,.78f));
  saw=new GameObject("Bone saw");saw.transform.SetParent(transform,false);
  Part(saw.transform,"Powered saw grip",new Vector3(0,-.008f,.026f),new Vector3(.028f,.035f,.075f),new Color(.045f,.08f,.09f));
  Part(saw.transform,"Saw motor housing",new Vector3(0,0,.07f),new Vector3(.034f,.024f,.035f),new Color(.56f,.60f,.62f));
  blade=Part(saw.transform,"Saw blade",new Vector3(0,0,.105f),new Vector3(.002f,.027f,.048f),new Color(.7f,.73f,.75f)).transform;
  for(int i=0;i<12;i++)Part(blade,"Blade tooth",new Vector3(0,-.54f,-.46f+i*.084f),new Vector3(1,.12f,.035f),new Color(.7f,.73f,.75f));
 }
 GameObject Part(Transform parent,string name,Vector3 position,Vector3 scale,Color color){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;Destroy(go.GetComponent<Collider>());var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Metallic",.75f);m.SetFloat("_Smoothness",.5f);go.GetComponent<Renderer>().sharedMaterial=m;owned.Add(m);return go;}
 void Update(){if(!scalpel)return;bool active=session&&session.mode==LabMode.Free;scalpel.SetActive(active&&!session.saw);saw.SetActive(active&&session.saw);}
 void OnDestroy(){foreach(var m in owned)if(m)Destroy(m);}
}
}
