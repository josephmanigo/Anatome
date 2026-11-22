using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ThoraxLab {
[Serializable] public class AnatomyRecord {
 public string id, name, object_name, system, tissue, source_kind, source_record, required_group, notes,anatomical_identifier;
 public int layer; public bool cutting_ready,reference_region; public string[] mesh_ids;
 public string AppearanceTissue => required_group switch {
  "tricuspid_valve" or "mitral_valve" or "pulmonary_valve" or "aortic_valve" => "valve",
  "esophagus" => "esophagus",
  _ => tissue
 };
}
[Serializable] public class AnatomyCatalog { public AnatomyRecord[] records; }
public class AnatomyStructure : MonoBehaviour {
 public AnatomyRecord record;
 public Material interiorMaterial;
 public bool fragment;
 public string parentId;
 public string cutObjectiveId,chunkToolId;
 public float chunkFraction;
 public float[] chunkCutPoses;
 public Bounds SourceBounds {get;private set;}
 public double SourceVolume {get;private set;}
 [SerializeField,HideInInspector] Mesh bakedVolumeMesh;
 [SerializeField,HideInInspector] double bakedVolume;
#if UNITY_EDITOR
 public void BakeSourceVolume(){
  bakedVolumeMesh=Filter.sharedMesh;
  bakedVolume=CutMeshData.Capture(bakedVolumeMesh).Volume();
 }
#endif
 void SetSourceGeometry(Mesh mesh){
  SourceBounds=mesh.bounds;
  SourceVolume=mesh==bakedVolumeMesh?bakedVolume:CutMeshData.Capture(mesh).Volume();
 }
 public MeshFilter Filter => GetComponent<MeshFilter>();
 public MeshRenderer Renderer => GetComponent<MeshRenderer>();
 public XRGrabInteractable Grab => GetComponent<XRGrabInteractable>();
 public Vector3 RestPosition {get;private set;}
 public Quaternion RestRotation {get;private set;}
 Mesh originalMesh,validatedWorkingMesh; Material[] originalMaterials; Vector3 originalScale;
 MaterialPropertyBlock block;
 bool inspectionGrab;
 Vector3 inspectionPosition;
 Quaternion inspectionRotation;
 public void Initialize() {
  RestPosition=transform.localPosition; RestRotation=transform.localRotation;originalScale=transform.localScale;
  originalMesh=Filter.sharedMesh; originalMaterials=Renderer.sharedMaterials;parentId=record.id;
  SetSourceGeometry(originalMesh);
  // Preserve the exact pose at the grasp point. Static pivot attachment jumps a
  // whole anatomical mesh toward the hand and can masquerade as a deliberate lift.
  var grab=Grab;if(grab){grab.useDynamicAttach=true;grab.matchAttachPosition=true;grab.matchAttachRotation=true;grab.snapToColliderVolume=false;grab.unparentTransformOnGrab=false;}
 }
 void Start(){if(originalMesh==null)Initialize();if(Grab!=null){Grab.selectEntered.AddListener(_=>BeginGrab());Grab.selectExited.AddListener(_=>TrySnap());}}
 void BeginGrab(){
  var runtime=Learning.LabRuntime.Instance;
  inspectionGrab=runtime&&runtime.Engine!=null&&!runtime.OrientationActive&&!fragment&&
   (runtime.Variant.mode!=Learning.LearningMode.AssembleAnatomy||runtime.Engine.Current?.targetId!=record.id);
  inspectionPosition=transform.localPosition;inspectionRotation=transform.localRotation;
 }
 public void Highlight(bool on) {
  block??=new MaterialPropertyBlock();Renderer.GetPropertyBlock(block);
  // Preserve tissue colour: a strong cyan multiplier turns skin green and hides
  // the distinction between layers. Selection also has a named UI status.
  block.SetColor("_BaseColor",on?new Color(1.08f,1.08f,1.06f):Color.white);foreach(var renderer in GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(block);var lod=GetComponent<LODGroup>();if(lod&&lod.enabled&&gameObject.activeInHierarchy)lod.ForceLOD(on?0:-1);
 }
 public void TrySnap(){
  // Inspection is temporary handling; it must never submit placement/removal evidence.
  if(inspectionGrab){inspectionGrab=false;RestorePose();transform.SetLocalPositionAndRotation(inspectionPosition,inspectionRotation);return;}
  var runtime=Learning.LabRuntime.Instance;if(runtime&&(runtime.Engine!=null||runtime.OrientationActive)){runtime.Released(this,Vector3.Distance(transform.position,transform.parent.TransformPoint(RestPosition)),Quaternion.Angle(transform.localRotation,RestRotation));return;}if(!fragment && Vector3.Distance(transform.localPosition,RestPosition)<.075f && Quaternion.Angle(transform.localRotation,RestRotation)<35)RestorePose();
 }
 public void RestorePose(){var rb=GetComponent<Rigidbody>();if(rb){if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}rb.isKinematic=true;}transform.SetLocalPositionAndRotation(RestPosition,RestRotation);transform.localScale=originalScale;}
 // The original imported mesh remains retained. A separately audited derived mesh
 // may be used for working geometry without modifying that source asset.
 public void UseValidatedWorkingMesh(Mesh mesh){if(!mesh)throw new ArgumentNullException(nameof(mesh));validatedWorkingMesh=mesh;SetSourceGeometry(mesh);SetMesh(mesh,originalMaterials);}
 public void SetRestingMaterials(Material outer,Material inner){
  if(!outer||originalMaterials==null||originalMaterials.Length==0)return;
  originalMaterials[0]=outer;
  if(inner&&originalMaterials.Length>1)originalMaterials[1]=inner;
 }
 public void RestoreOriginal(){inspectionGrab=false;gameObject.SetActive(!record.reference_region);RestorePose();if(originalMesh)SetMesh(validatedWorkingMesh?validatedWorkingMesh:originalMesh,originalMaterials);}
 public void SetMesh(Mesh mesh,Material[] materials){
  Filter.sharedMesh=mesh;Renderer.sharedMaterials=materials;
  var col=GetComponent<MeshCollider>();if(col){col.sharedMesh=null;col.sharedMesh=mesh;}
  var rb=GetComponent<Rigidbody>();if(rb)rb.isKinematic=true;
  var lod=GetComponent<LODGroup>();if(lod){bool intact=mesh==originalMesh||mesh==validatedWorkingMesh;lod.enabled=intact;foreach(var child in GetComponentsInChildren<Renderer>())child.enabled=intact||child==Renderer;}
 }
}
}
