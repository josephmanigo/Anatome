using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace ThoraxLab {
public class RuntimeVerification:MonoBehaviour {
 [Serializable] class Check {public string name;public bool passed;public string detail;}
 [Serializable] class ViewCapture {public string view,captureMethod="Forced offscreen 1440x1000 rendering in hidden Windows player; not headset timing";public long maxDrawCalls,maxTriangles;public float medianFrameMs,p95FrameMs;public bool renderingCountersAvailable;}
 [Serializable] class Report {public string platform,nativeVersion,graphicsDevice;public List<Check> checks=new();public List<ViewCapture> rendering=new();public int failures;public long managedBytesBefore,managedBytesAfter;}
 readonly Report report=new();DissectionSession session;AnatomyStructure fixture;string output;bool enabledByArgs;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){if(Environment.GetCommandLineArgs().Contains("-selftest"))new GameObject("Runtime verification").AddComponent<RuntimeVerification>();}
 void CheckResult(string name,bool ok,string detail=""){report.checks.Add(new Check{name=name,passed=ok,detail=detail});if(!ok)report.failures++;Debug.Log("SELFTEST "+name+" "+ok+" "+detail);}
 IEnumerator Start(){
  enabledByArgs=true;var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-testOutput");output=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"runtime-tests.json");report.platform=Application.platform.ToString();report.nativeVersion=NativeCutting.Version;
  report.graphicsDevice=SystemInfo.graphicsDeviceName;yield return null;session=FindFirstObjectByType<DissectionSession>();CheckResult("scene catalog loaded",session&&session.originals.Count==249,session?session.originals.Count.ToString():"No session");
  if(!session){Finish();yield break;}
  report.managedBytesBefore=GC.GetTotalMemory(true);session.SetMode(LabMode.Free);
  var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Cutting QA fixture";Destroy(go.GetComponent<BoxCollider>());go.transform.SetParent(session.specimen);go.transform.position=new Vector3(0,1.3f,1.35f);go.transform.localScale=Vector3.one*.15f;
  var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;go.AddComponent<Rigidbody>().isKinematic=true;
  fixture=go.AddComponent<AnatomyStructure>();fixture.record=new AnatomyRecord{id="QA_FIXTURE",name="Synthetic soft-tissue fixture",tissue="muscle",cutting_ready=true};fixture.interiorMaterial=session.originals.First(s=>s.record.tissue=="muscle").interiorMaterial;fixture.Initialize();session.originals.Add(fixture);
  var grab=go.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.Instantaneous;grab.throwOnDetach=false;yield return null;
  var manager=FindFirstObjectByType<XRInteractionManager>();foreach(var hand in FindObjectsByType<LabHandInteractor>(FindObjectsSortMode.None)){manager.SelectEnter((IXRSelectInteractor)hand,grab);CheckResult(hand.hand+" XRI selection event",grab.isSelected);fixture.transform.localPosition=fixture.RestPosition+Vector3.right*.015f;manager.SelectExit((IXRSelectInteractor)hand,grab);CheckResult(hand.hand+" release snaps to rest pose",Vector3.Distance(fixture.transform.localPosition,fixture.RestPosition)<.00001f);}
  var original=fixture.Filter.sharedMesh;session.Select(fixture);yield return CutMiddle(fixture,0);
  CheckResult("swept cut creates detached piece",session.FragmentCount==1,session.message);CheckResult("mesh and collider replaced together",fixture.Filter.sharedMesh==fixture.GetComponent<MeshCollider>().sharedMesh&&fixture.Filter.sharedMesh!=original);
  CheckResult("interior submesh assigned",fixture.Filter.sharedMesh.subMeshCount==2&&fixture.Filter.sharedMesh.GetIndexCount(1)>0);
  session.Undo();yield return null;CheckResult("undo restores original mesh and pieces",fixture.Filter.sharedMesh==original&&session.FragmentCount==0);
  fixture.record.tissue="bone";session.saw=true;fixture.interiorMaterial=session.originals.First(s=>s.record.tissue=="bone"&&s!=fixture).interiorMaterial;yield return CutMiddle(fixture,0);var colors=fixture.Filter.sharedMesh.colors;CheckResult("bone cut carries cortical and cancellous appearance data",colors.Any(c=>c.r>.3f)&&colors.Any(c=>c.r<.1f));session.Undo();yield return null;fixture.record.tissue="muscle";session.saw=false;fixture.interiorMaterial=session.originals.First(s=>s.record.tissue=="muscle"&&s!=fixture).interiorMaterial;
  session.Select(fixture);var bad=Stroke(fixture,0);bad[0]=float.NaN;session.Cut(bad);while(session.busy)yield return null;
  CheckResult("invalid cut preserves previous state",fixture.Filter.sharedMesh==original&&session.FragmentCount==0,session.message);
  session.Select(fixture);session.Cut(Stroke(fixture,0));session.ResetSpecimen();yield return new WaitForSeconds(.2f);
  CheckResult("reset discards in-flight result",fixture.Filter.sharedMesh==original&&!session.busy&&session.FragmentCount==0);
  for(int i=0;i<33;i++){
   var target=FindObjectsByType<AnatomyStructure>(FindObjectsSortMode.None).Where(s=>s.parentId=="QA_FIXTURE").OrderByDescending(s=>{var b=s.Renderer.bounds.size;return b.x*b.y*b.z;}).First();session.Select(target);yield return CutMiddle(target,i%3);
   if(i==11)CheckResult("undo retains ten steps",session.UndoCount==10,session.UndoCount.ToString());
  }
  CheckResult("session enforces 32 detached fragments",session.FragmentCount==32,session.FragmentCount+" "+session.message);CheckResult("dynamic meshes bounded by reachable history",session.RetainedDynamicMeshCount<70,session.RetainedDynamicMeshCount.ToString());
  for(int i=0;i<10;i++){session.Undo();yield return null;}CheckResult("ten undo operations succeed",session.UndoCount==0&&session.FragmentCount<32);
  session.SetMode(LabMode.Explore);yield return null;CheckResult("mode change restores intact specimen",session.FragmentCount==0&&fixture.Filter.sharedMesh==original&&session.RetainedDynamicMeshCount==0);
  fixture.transform.localPosition+=Vector3.right*.025f;fixture.TrySnap();CheckResult("snap returns to recorded pose",Vector3.Distance(fixture.transform.localPosition,fixture.RestPosition)<.00001f);
  session.SetTableHeight(.15f);CheckResult("table and anatomy share height offset",session.table&&Mathf.Abs(session.table.position.y-session.specimen.position.y)<.00001f);session.SetTableHeight(0);
  session.originals.Remove(fixture);Destroy(fixture.gameObject);session.SetMode(LabMode.Guided);session.GuidedNext();while(session.busy)yield return null;CheckResult("guided opening advances prepared layer",session.guidedLayer==1);session.SetMode(LabMode.Free);var annotation=session.originals.First(s=>s.record.reference_region);session.Inspect(annotation);session.Cut(Stroke(annotation,0));CheckResult("region annotations cannot be cut",!session.busy&&session.FragmentCount==0&&session.message.Contains("region"));session.SetMode(LabMode.Explore);session.OpenChest();yield return CapturePerformance("open chest");session.ShowSystem("All");yield return CapturePerformance("all layers");session.OpenChest();yield return new WaitForEndOfFrame();
  var camera=Camera.main;var rt=new RenderTexture(1440,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1440,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,1000),0,0);image.Apply();File.WriteAllBytes(Path.ChangeExtension(output,".png"),image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Destroy(rt);Destroy(image);yield return null;report.managedBytesAfter=GC.GetTotalMemory(true);Finish();
 }
 IEnumerator CapturePerformance(string label){
  using var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count");using var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");var frames=new List<float>();var capture=new ViewCapture{view=label};
  var camera=Camera.main;var target=new RenderTexture(1440,1000,24);camera.targetTexture=target;
  for(int i=0;i<140;i++){camera.Render();yield return null;if(i<20)continue;frames.Add(Time.unscaledDeltaTime*1000);capture.maxDrawCalls=Math.Max(capture.maxDrawCalls,draws.LastValue);capture.maxTriangles=Math.Max(capture.maxTriangles,tris.LastValue);}
  camera.targetTexture=null;Destroy(target);
  frames.Sort();capture.medianFrameMs=frames[frames.Count/2];capture.p95FrameMs=frames[(int)(frames.Count*.95f)];capture.renderingCountersAvailable=capture.maxDrawCalls>0&&capture.maxTriangles>0;report.rendering.Add(capture);
 }
 IEnumerator CutMiddle(AnatomyStructure s,int axis){session.Select(s);session.Cut(Stroke(s,axis));float start=Time.realtimeSinceStartup;while(session.busy&&Time.realtimeSinceStartup-start<20)yield return null;CheckResult("cut completes within timeout",!session.busy);yield return null;}
 static List<float> Stroke(AnatomyStructure s,int axis){var b=s.Renderer.bounds;var poses=new List<float>();var q=axis==0?Quaternion.identity:axis==1?Quaternion.Euler(0,0,90):Quaternion.Euler(0,90,0);var direction=q*Vector3.up;float extent=b.size.magnitude;NativeCutting.AddPose(poses,s.transform,b.center-direction*extent,q,.00035f,extent*.15f,extent);NativeCutting.AddPose(poses,s.transform,b.center+direction*extent,q,.00035f,extent*.15f,extent);return poses;}
 void Finish(){Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));Debug.Log("SELFTEST_FINISHED "+report.failures);Application.Quit(report.failures==0?0:1);}
 void OnDestroy(){if(enabledByArgs&&report.checks.Count==0)Debug.LogError("Runtime test did not execute");}
}
}
