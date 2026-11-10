using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ThoraxLab {
public enum LabMode { Explore, Guided, Free }
public class DissectionSession : MonoBehaviour {
 public LabMode mode;public bool saw;public Transform specimen,table;public AnatomyStructure selected;
 public string message="Select a structure to inspect its anatomy and source.";public bool busy;public int guidedLayer;
 public string systemFilter="All";int browseIndex;
 public List<AnatomyStructure> originals=new();
 readonly List<AnatomyStructure> fragments=new();readonly List<UndoFrame> undo=new();
 readonly List<Mesh> allocated=new();int revision,nativeInFlight;
 float? pendingTableHeight;
 [Serializable] class PieceState {public AnatomyStructure s;public Mesh mesh;public Material[] materials;public Vector3 position;public Quaternion rotation;public bool active;}
 class UndoFrame {public List<PieceState> pieces=new();public HashSet<AnatomyStructure> existing=new();}
 public int FragmentCount=>fragments.Count;
 public IReadOnlyList<AnatomyStructure> Fragments=>fragments;
 public int UndoCount=>undo.Count;
 public int RetainedDynamicMeshCount=>allocated.Count;
 void Start(){originals=FindObjectsByType<AnatomyStructure>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>!s.fragment).ToList();foreach(var s in originals)s.Initialize();OpenChest();}
 public void OpenChest(){if(busy)return;foreach(var s in originals)s.gameObject.SetActive(!s.record.reference_region&&s.record.layer>=3&&s.record.tissue!="membrane");message="Open teaching view. Select a structure, or choose All for intact layers.";}
 public void Select(AnatomyStructure s){if(busy)return;if(selected)selected.Highlight(false);selected=s;if(s){s.Highlight(true);message=$"{s.record.name}\n{s.record.id} · {s.record.source_kind}\n{s.record.notes}";}}
 public void SetMode(LabMode m){ResetSpecimen();mode=m;message=m==LabMode.Explore?"Explore: select, grab, isolate, remove, and replace.":m==LabMode.Guided?"Guided: advance through chest wall, ribs, membranes, and organs.":"Free dissection: select tissue, sweep a short cut, then grip its separated chunk. Use the saw for bone.";}
 public void ResetSpecimen(){revision++;StopAllCoroutines();busy=false;Select(null);foreach(var s in fragments)if(s){s.gameObject.SetActive(false);Destroy(s.gameObject);}fragments.Clear();undo.Clear();foreach(var s in originals)if(s)s.RestoreOriginal();foreach(var m in allocated)if(m)Destroy(m);allocated.Clear();guidedLayer=0;}
 void OnDestroy(){foreach(var m in allocated)if(m)Destroy(m);}
 public void GuidedNext(){if(busy||mode!=LabMode.Guided)return;guidedLayer=Math.Min(7,guidedLayer+1);StartCoroutine(OpenLayer());}
 IEnumerator OpenLayer(){busy=true;var layer=originals.Where(s=>s&&s.record.layer==guidedLayer-1).ToArray();for(float time=0;time<.8f;time+=Time.deltaTime){float a=Mathf.SmoothStep(0,1,time/.8f);foreach(var s in layer){float side=s.RestPosition.x<0?-1:1;var hinge=s.RestPosition+Vector3.right*side*.12f;var rotation=Quaternion.AngleAxis(a*70,Vector3.forward*side);s.transform.localPosition=hinge+rotation*(s.RestPosition-hinge)+Vector3.right*(side*.15f*a);s.transform.localRotation=rotation*s.RestRotation;}yield return null;}foreach(var s in originals)if(s)s.gameObject.SetActive(!s.record.reference_region&&s.record.layer>=guidedLayer-1);busy=false;message=$"Layer {guidedLayer}/7 opened. Select the exposed structures, or advance.";}
 public void HideSelected(){if(selected&&!busy){selected.gameObject.SetActive(false);Select(null);}}
 public void Isolate(){if(!selected||busy)return;foreach(var s in originals)s.gameObject.SetActive(s==selected);foreach(var s in fragments)s.gameObject.SetActive(s==selected);}
 public void ShowSystem(string system){if(busy)return;systemFilter=system;browseIndex=0;foreach(var s in originals)s.gameObject.SetActive(!s.record.reference_region&&(system=="All"||s.record.system==system));foreach(var s in fragments)if(s)s.gameObject.SetActive(!s.record.reference_region&&(system=="All"||s.record.system==system));}
 public void Inspect(AnatomyStructure s){if(busy)return;s.gameObject.SetActive(true);Select(s);Isolate();}
 public void Browse(int direction){if(busy)return;var list=originals.Where(s=>systemFilter=="All"||s.record.system==systemFilter).OrderBy(s=>s.record.name).ToArray();if(list.Length==0)return;browseIndex=(browseIndex+direction+list.Length)%list.Length;Inspect(list[browseIndex]);}
 public void CycleSystem(){string[] systems={"All","Skeletal","Muscular","Respiratory","Cardiovascular","Nervous","Integumentary","Lymphatic"};ShowSystem(systems[(Array.IndexOf(systems,systemFilter)+1)%systems.Length]);message=systemFilter+" system. Use Previous / Next to inspect structures.";}
 public void ReturnSelected(){if(selected&&!busy)selected.RestorePose();}
 // Accepted guided steps leave the work area. Keep source/cut geometry for reset,
 // but do not let an older cut snapshot resurrect a completed layer on Undo.
 public void RetireCompletedLayer(AnatomyStructure layer){
  if(selected&&selected.parentId==layer.parentId)Select(null);
  foreach(var s in originals.Concat(fragments))if(s&&s.parentId==layer.parentId)s.gameObject.SetActive(false);
  foreach(var frame in undo)foreach(var piece in frame.pieces)if(piece.s&&piece.s.parentId==layer.parentId)piece.active=false;
 }
 public void SetTableHeight(float value){pendingTableHeight=Mathf.Clamp(value,-.2f,.25f);if(busy)return;var p=specimen.position;p.y=pendingTableHeight.Value;specimen.position=p;if(table){var t=table.position;t.y=p.y;table.position=t;}pendingTableHeight=null;}
 void LateUpdate(){if(pendingTableHeight.HasValue&&!busy)SetTableHeight(pendingTableHeight.Value);}
 UndoFrame Snapshot(){var f=new UndoFrame();foreach(var s in originals.Concat(fragments)){if(!s)continue;f.existing.Add(s);f.pieces.Add(new PieceState{s=s,mesh=s.Filter.sharedMesh,materials=s.Renderer.sharedMaterials,position=s.transform.localPosition,rotation=s.transform.localRotation,active=s.gameObject.activeSelf});}return f;}
 void RestoreFrame(UndoFrame f){foreach(var s in fragments.ToArray())if(!f.existing.Contains(s)){fragments.Remove(s);if(s){s.gameObject.SetActive(false);Destroy(s.gameObject);}}foreach(var p in f.pieces)if(p.s){p.s.SetMesh(p.mesh,p.materials);p.s.transform.SetLocalPositionAndRotation(p.position,p.rotation);p.s.gameObject.SetActive(p.active);}}
 void CollectMeshes(){var used=new HashSet<Mesh>(originals.Concat(fragments).Where(s=>s).Select(s=>s.Filter.sharedMesh));foreach(var frame in undo)foreach(var p in frame.pieces)used.Add(p.mesh);foreach(var mesh in allocated.ToArray())if(!used.Contains(mesh)){allocated.Remove(mesh);if(mesh)Destroy(mesh);}}
 public void Undo(){if(busy||undo.Count==0)return;revision++;var f=undo[^1];undo.RemoveAt(undo.Count-1);RestoreFrame(f);Select(null);CollectMeshes();message="Previous cut restored.";}
 public void CommitRemovedChunk(AnatomyStructure chunk){
  if(!chunk||!chunk.fragment||!fragments.Remove(chunk))return;
  if(selected==chunk)Select(null);chunk.gameObject.SetActive(false);Destroy(chunk.gameObject);
  // A credited piece cannot be restored and credited again by Undo.
  undo.Clear();CollectMeshes();
 }
 public async Task<bool> RestoreRemovedChunks(AnatomyStructure target,IEnumerable<Learning.CutStroke> strokes){
  int token=++revision;busy=true;
  try{
   var capture=CutMeshData.Capture(target.Filter.sharedMesh);
   foreach(var stroke in strokes){
    var result=await Task.Run(()=>NativeCutting.Cut(capture,stroke.poses,2,true));
    if(this==null||token!=revision)return false;
    if(result.status!=0)throw new InvalidOperationException(result.message);
    capture=result.pieces[0];
   }
   var mesh=capture.CreateMesh();allocated.Add(mesh);
   target.SetMesh(mesh,new[]{target.Renderer.sharedMaterials[0],target.interiorMaterial});
   return true;
  }catch(Exception ex){message="Saved cut geometry could not be restored: "+ex.Message;return false;}
  finally{if(this!=null&&token==revision)busy=false;}
 }
 void CutFeedback(string value){message=value;Learning.LabRuntime.Instance?.Message(value);}
 public async void Cut(List<float> poses){
  if(busy||mode!=LabMode.Free||selected==null||poses.Count<48)return;
  if(Learning.LabRuntime.Instance&&!Learning.LabRuntime.Instance.CanCut)return;
  var target=selected;
  if(target.record.reference_region){CutFeedback("This marks an anatomical region or empty space. Select the surrounding tissue wall for dissection.");return;}
  if(!target.record.cutting_ready){CutFeedback("This structure has an unresolved cutting-volume validation finding. Original preserved.");return;}
  var objective=Learning.LabRuntime.Instance?.Engine?.Current;
  if(objective?.action==Learning.ActionKind.Expose&&(objective.region=="left"||objective.region=="right")){
   var source=originals.First(s=>s.record.id==target.parentId);float midline=source.SourceBounds.center.x;
   for(int i=0;i<poses.Count;i+=3)if(objective.region=="left"?poses[i]>midline:poses[i]<midline){message="Keep the whole blade on the specimen's "+objective.region+" side. Move farther from the midline.";Learning.LabRuntime.Instance.Message(message);return;}
  }
  bool bone=target.record.tissue=="bone";
  if(bone!=saw){CutFeedback(bone?"Select the bone saw for this structure.":"Select the scalpel for soft tissue.");return;}
  if(target.Grab&&target.Grab.isSelected){CutFeedback("Release the structure before cutting.");return;}
  var deformation=target.GetComponent<SoftHandling>();if(deformation)deformation.Freeze();
  var capture=CutMeshData.Capture(target.Filter.sharedMesh);var saved=Snapshot();int token=++revision;int available=Math.Min(32,33-fragments.Count);
  if(available<1){if(deformation)deformation.Thaw();CutFeedback("Fragment limit reached. Undo or reset.");return;}
  if(Interlocked.CompareExchange(ref nativeInFlight,1,0)!=0){if(deformation)deformation.Thaw();CutFeedback("The previous calculation is finishing. The intact specimen is available; try the next stroke shortly.");return;}
  busy=true;CutFeedback("Processing the stroke…");
  var sampledPoses=poses.ToArray();float metricScale=target.transform.lossyScale.x;
  var prepared=new List<Mesh>();
  var pending=new List<AnatomyStructure>();
  bool committed=false;
  try{
   var result=await Task.Run(()=>{var cut=NativeCutting.Cut(capture,sampledPoses,available,true);if(bone&&cut.status==0)BoneCutDetail.Apply(cut,capture,metricScale);return cut;});
   if(this==null||token!=revision)return;
   if(result.status!=0){CutFeedback(result.message);return;}
   int otherTriangles=originals.Concat(fragments).Where(s=>s&&s!=target&&s.parentId==target.parentId).Sum(s=>s.Filter.sharedMesh.triangles.Length/3);
   if(otherTriangles+result.pieces.Sum(p=>p.indices.Length/3)>100000){CutFeedback("This structure and its fragments would exceed 100,000 triangles. Undo or reset.");return;}
   foreach(var data in result.pieces)prepared.Add(data.CreateMesh());
   // Bake each new, unpublished mesh once on the worker. Unity mesh creation stays on the main thread.
   var meshIds=prepared.Select(mesh=>mesh.GetEntityId()).ToArray();
   await Task.Run(()=>{foreach(var id in meshIds)Physics.BakeMesh(id,false);});
   if(this==null||token!=revision)return;
   // No visible changes before every output mesh and collision shape is ready.
   var materials=new[]{target.Renderer.sharedMaterials[0],target.interiorMaterial};
   for(int i=1;i<prepared.Count;i++){
    var go=new GameObject(target.record.name+" fragment "+i);go.SetActive(false);go.transform.SetParent(target.transform.parent,false);go.transform.SetLocalPositionAndRotation(target.transform.localPosition,target.transform.localRotation);go.transform.localScale=target.transform.localScale;
    go.AddComponent<MeshFilter>().sharedMesh=prepared[i];go.AddComponent<MeshRenderer>().sharedMaterials=materials;go.AddComponent<MeshCollider>().sharedMesh=prepared[i];var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
    var structure=go.AddComponent<AnatomyStructure>();structure.record=target.record;structure.fragment=true;structure.parentId=target.parentId;structure.interiorMaterial=target.interiorMaterial;
    structure.cutObjectiveId=Learning.LabRuntime.Instance?.Engine?.Current?.id;
    structure.chunkCutPoses=sampledPoses;
    structure.chunkToolId=bone?"dissection-saw":"dissection-scalpel";
    var source=originals.First(s=>s.record.id==target.parentId);
    structure.chunkFraction=(float)(result.pieces[i].Volume()/Math.Max(1e-20,source.SourceVolume));
    pending.Add(structure);var grab=go.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.Instantaneous;grab.throwOnDetach=false;structure.Initialize();
   }
   target.SetMesh(prepared[0],materials);foreach(var s in pending){fragments.Add(s);s.gameObject.SetActive(true);}allocated.AddRange(prepared);undo.Add(saved);if(undo.Count>10)undo.RemoveAt(0);CollectMeshes();committed=true;message=$"Cut complete · {prepared.Count} pieces · {fragments.Count}/32 detached pieces.";
  }catch(Exception ex){if(this!=null&&token==revision){RestoreFrame(saved);CutFeedback("Cut could not be applied; previous state restored: "+ex.Message);}}
  finally{
   if(!committed){foreach(var s in pending)if(s){s.gameObject.SetActive(false);Destroy(s.gameObject);}foreach(var m in prepared)if(m)Destroy(m);}
   if(deformation)deformation.Thaw();Interlocked.Exchange(ref nativeInFlight,0);if(this!=null&&token==revision)busy=false;
  }
  if(committed&&this!=null&&token==revision){CutFeedback(message);if(Learning.LabRuntime.Instance)Learning.LabRuntime.Instance.OnCutCommitted(target);}
 }
}
}
