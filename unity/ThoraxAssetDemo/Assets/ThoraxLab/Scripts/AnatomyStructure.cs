using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ThoraxLab {
[Serializable] public class AnatomyRecord {
 public string id, name, object_name, system, tissue, source_kind, source_record, required_group, notes,anatomical_identifier;
 public int layer; public bool cutting_ready,reference_region; public string[] mesh_ids;
}
[Serializable] public class AnatomyCatalog { public AnatomyRecord[] records; }
public class AnatomyStructure : MonoBehaviour {
 public AnatomyRecord record;
 public Material interiorMaterial;
 public bool fragment;
 public string parentId;
 public MeshFilter Filter => GetComponent<MeshFilter>();
 public MeshRenderer Renderer => GetComponent<MeshRenderer>();
 public XRGrabInteractable Grab => GetComponent<XRGrabInteractable>();
 public bool IsIntact=>originalMesh!=null&&Filter.sharedMesh==originalMesh;
 public Vector3 RestPosition {get;private set;}
 public Quaternion RestRotation {get;private set;}
 Mesh originalMesh; Material[] originalMaterials; Vector3 originalScale;
 MaterialPropertyBlock block;
 public void Initialize() {
  RestPosition=transform.localPosition; RestRotation=transform.localRotation;originalScale=transform.localScale;
  originalMesh=Filter.sharedMesh; originalMaterials=Renderer.sharedMaterials;parentId=record.id;
 }
 void Start(){if(originalMesh==null)Initialize();if(Grab!=null)Grab.selectExited.AddListener(_=>TrySnap());}
 public void Highlight(bool on) {
  block??=new MaterialPropertyBlock();Renderer.GetPropertyBlock(block);
  block.SetColor("_BaseColor",on?new Color(.55f,1f,.90f):Color.white);foreach(var renderer in GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(block);var lod=GetComponent<LODGroup>();if(lod)lod.ForceLOD(on?0:-1);
 }
 public void TrySnap(){if(!fragment && Vector3.Distance(transform.localPosition,RestPosition)<.075f && Quaternion.Angle(transform.localRotation,RestRotation)<35)RestorePose();}
 public void RestorePose(){var rb=GetComponent<Rigidbody>();if(rb){if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}rb.isKinematic=true;}transform.SetLocalPositionAndRotation(RestPosition,RestRotation);transform.localScale=originalScale;}
 public void RestoreOriginal(){gameObject.SetActive(!record.reference_region);RestorePose();if(originalMesh)SetMesh(originalMesh,originalMaterials);}
 public void SetMesh(Mesh mesh,Material[] materials){
  Filter.sharedMesh=mesh;Renderer.sharedMaterials=materials;
  var col=GetComponent<MeshCollider>();if(col){col.sharedMesh=null;col.sharedMesh=mesh;}
  var rb=GetComponent<Rigidbody>();if(rb)rb.isKinematic=true;
  var lod=GetComponent<LODGroup>();if(lod){bool intact=mesh==originalMesh;lod.enabled=intact;foreach(var child in GetComponentsInChildren<Renderer>())child.enabled=intact||child==Renderer;}
 }
}
}
