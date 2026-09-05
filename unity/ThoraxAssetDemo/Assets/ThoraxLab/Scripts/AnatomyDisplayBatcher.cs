using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace ThoraxLab {
// Display-only batches retain the individual selection/grab/cutting components.
// Session mesh changes never replace these immutable display sources.
public class AnatomyDisplayBatcher:MonoBehaviour {
 DissectionSession session;readonly List<GameObject> batches=new();readonly List<AnatomyStructure> hidden=new();int previous;bool valid;
 void Start(){session=GetComponent<DissectionSession>();}
 public void Rebuild(){valid=false;}
 void LateUpdate(){
  if(!session||session.originals.Count==0)return;
  if(session.mode==LabMode.Guided&&session.busy){if(valid){Clear();valid=false;}return;}
  var candidates=new List<AnatomyStructure>();int signature=17;
  unchecked{foreach(var s in session.originals){if(!s)continue;bool include=s.gameObject.activeInHierarchy&&!s.record.reference_region&&s.IsIntact&&s!=session.selected&&!(s.Grab&&s.Grab.isSelected);signature=signature*31+s.GetInstanceID();signature=signature*31+(include?1:0);if(include){candidates.Add(s);signature=signature*31+s.transform.localPosition.GetHashCode();signature=signature*31+s.transform.localRotation.GetHashCode();signature=signature*31+s.transform.localScale.GetHashCode();}}}
  if(valid&&signature==previous)return;
  Clear();var grouped=new Dictionary<Material,List<CombineInstance>>();
  foreach(var s in candidates){var lod=s.GetComponent<LODGroup>();var source=s.Renderer;if(lod){var levels=lod.GetLODs();if(levels.Length>1)source=(MeshRenderer)levels[1].renderers[0];}var filter=source.GetComponent<MeshFilter>();var material=source.sharedMaterial;if(!grouped.TryGetValue(material,out var list)){list=new();grouped[material]=list;}list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=0,transform=session.specimen.worldToLocalMatrix*source.localToWorldMatrix});}
  foreach(var pair in grouped){var mesh=new Mesh{name="Intact display batch",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();var go=new GameObject("Display · "+pair.Key.name);go.transform.SetParent(session.specimen,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=ShadowCastingMode.Off;batches.Add(go);}
  foreach(var s in candidates){var lod=s.GetComponent<LODGroup>();if(lod)lod.enabled=false;foreach(var renderer in s.GetComponentsInChildren<Renderer>())renderer.enabled=false;hidden.Add(s);}
  previous=signature;valid=true;
 }
 void Clear(){foreach(var s in hidden){if(!s)continue;bool intact=s.IsIntact;foreach(var renderer in s.GetComponentsInChildren<Renderer>())renderer.enabled=intact||renderer==s.Renderer;var lod=s.GetComponent<LODGroup>();if(lod){lod.enabled=intact;if(intact)lod.ForceLOD(session&&s==session.selected?0:-1);}}hidden.Clear();foreach(var go in batches)if(go){Destroy(go.GetComponent<MeshFilter>().sharedMesh);go.SetActive(false);Destroy(go);}batches.Clear();}
 void OnDisable(){Clear();valid=false;}
}
}
