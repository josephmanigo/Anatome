#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThoraxLab.Editor
{
    // Real imported layers and the native cutting/render-publication seam.
    public static class LearningDissectionLayerVerification
    {
        [Serializable] sealed class Check { public string id,detail; public bool passed; }
        [Serializable] sealed class Report { public bool passed; public List<Check> checks=new(); }
        [Serializable] sealed class MeshDump { public float[] vertices;public int[] indices; }
        [Serializable] sealed class CutDump { public MeshDump[] pieces; }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            var report=new Report();
            var structures=UnityEngine.Object.FindObjectsByType<AnatomyStructure>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(string id in new[]{"FMA7163","REC_fat","REC_fascia","FMA79979","FMA79980","MAL_RUL","FMA9457","FMA7957","FMA8148","REC_Pericardium","FMA7394"})
            {
                var matches=structures.Where(s=>s.record.id==id).ToArray();
                CheckResult(id+"/one-original",matches.Length==1,"Original objects="+matches.Length);
                if(matches.Length!=1)continue;
                var target=matches[0];target.gameObject.SetActive(true);target.Initialize();
                var source=target.Filter.sharedMesh;var data=CutMeshData.Capture(source);
                string diagnostics=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/dissection-overhaul/geometry"));Directory.CreateDirectory(diagnostics);
                File.WriteAllText(Path.Combine(diagnostics,id+"-source.json"),JsonUtility.ToJson(data));
                var vertices=source.vertices;var indices=source.triangles;
                var triangles=Enumerable.Range(0,indices.Length/3).OrderByDescending(i=>Vector3.Cross(vertices[indices[i*3+1]]-vertices[indices[i*3]],vertices[indices[i*3+2]]-vertices[indices[i*3]]).sqrMagnitude).Take(3);
                foreach(int triangle in triangles)
                {
                    var a=target.transform.TransformPoint(vertices[indices[triangle*3]]);var b=target.transform.TransformPoint(vertices[indices[triangle*3+1]]);var c=target.transform.TransformPoint(vertices[indices[triangle*3+2]]);
                    var center=(a+b+c)/3;var tangent=(b-a).normalized;var normal=Vector3.Cross(b-a,c-a).normalized;var rotation=Quaternion.LookRotation(normal,tangent);var poses=new List<float>();
                    NativeCutting.AddPose(poses,target.transform,center-tangent*.01f,rotation,.008f,.008f,.018f);
                    NativeCutting.AddPose(poses,target.transform,center+tangent*.01f,rotation,.008f,.008f,.018f);
                    var cut=NativeCutting.Cut(data,poses.ToArray(),32,true);
                    CheckResult(id+"/cut-"+triangle,cut.status==0,cut.message+"; pieces="+cut.pieces.Count);
                    if(cut.status!=0)continue;
                    File.WriteAllText(Path.Combine(diagnostics,id+"-cut-"+triangle+".json"),JsonUtility.ToJson(new CutDump{pieces=cut.pieces.Select(p=>new MeshDump{vertices=p.vertices,indices=p.indices}).ToArray()}));
                    double before=Volume(data),after=cut.pieces.Sum(Volume);
                    CheckResult(id+"/no-detached-cavity-"+triangle,cut.pieces.All(p=>Volume(p)>0),"Individual piece volumes="+string.Join(",",cut.pieces.Select(p=>Volume(p).ToString("R"))));
                    CheckResult(id+"/no-duplicated-volume-"+triangle,after>0&&Math.Abs(after-before)<before*.0001,"Before="+before+"; after="+after);
                    var mesh=cut.pieces[0].CreateMesh();target.SetMesh(mesh,new[]{target.Renderer.sharedMaterial,target.interiorMaterial});
                    var renderers=target.GetComponentsInChildren<MeshRenderer>(true);
                    CheckResult(id+"/single-cut-renderer-"+triangle,!target.GetComponent<LODGroup>().enabled&&renderers.Count(r=>r.enabled)==1&&target.Renderer.enabled,
                        string.Join("; ",renderers.Select(r=>r.name+"="+r.enabled+" mesh="+r.GetComponent<MeshFilter>().sharedMesh.name)));
                    target.gameObject.SetActive(false);target.gameObject.SetActive(true);
                    CheckResult(id+"/no-stale-renderer-after-reactivation-"+triangle,renderers.Count(r=>r.enabled)==1,"Enabled renderers="+renderers.Count(r=>r.enabled));
                    target.RestoreOriginal();UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            report.passed=report.checks.All(c=>c.passed);
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/dissection-overhaul/mesh-report.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Dissection layer regression failed: "+path);
            Debug.Log("Dissection layer regression passed: "+path);
            void CheckResult(string id,bool passed,string detail)=>report.checks.Add(new Check{id=id,passed=passed,detail=detail});
        }
        static double Volume(CutMeshData mesh)
        {
            double volume=0;var v=mesh.vertices;var t=mesh.indices;
            for(int i=0;i<t.Length;i+=3){int a=t[i]*6,b=t[i+1]*6,c=t[i+2]*6;volume+=(double)v[a]*(v[b+1]*v[c+2]-v[b+2]*v[c+1])+(double)v[a+1]*(v[b+2]*v[c]-v[b]*v[c+2])+(double)v[a+2]*(v[b]*v[c+1]-v[b+1]*v[c]);}
            return volume/6;
        }
    }
}
#endif
