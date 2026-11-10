using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace ThoraxLab.Editor
{
    // One audited derived working mesh; the imported healthy source is immutable.
    public static class LearningCutMeshRepair
    {
        const string Source="Assets/ThoraxLab/Models/Thorax.fbx";
        const string Output="Assets/ThoraxLab/Resources/Learning/Specimen/FMA9757-validated-working-mesh.asset";
        [Serializable] sealed class Audit
        {
            public string utc,anatomyId="FMA9757",source=Source,sourceSha256,derived=Output;
            public string operation="Remove exactly one disconnected six-triangle negative-occupancy numerical artifact; retain every vertex and all vertex channels. No surface is moved or filled.";
            public int sourceTriangles,derivedTriangles,components,removedTriangles;
            public double removedSignedVolumeM3;
            public float removedDiagonalM;
            public bool sourceFileUnchanged,vertexChannelsUnchanged,boundsUnchanged;
            public int[] removedSourceTriangleIndices;
            public string[] cutValidation;
        }
        [MenuItem("Anatome/Prepare audited intercostal working mesh")]
        public static void Prepare()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var filter=model.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="A_FMA9757");
            var source=filter.sharedMesh;var vertices=source.vertices;var indices=source.triangles;
            if(indices.Length/3!=3976)throw new InvalidDataException("FMA9757 source changed; audit the new source before applying the bounded repair.");
            string hash=Hash(File.ReadAllBytes(Source));
            var welded=new Dictionary<Vector3,int>();var parents=new List<int>();var aliases=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++){if(!welded.TryGetValue(vertices[i],out int index)){index=parents.Count;parents.Add(index);welded.Add(vertices[i],index);}aliases[i]=index;}
            int Find(int value){while(parents[value]!=value){parents[value]=parents[parents[value]];value=parents[value];}return value;}
            for(int i=0;i<indices.Length;i+=3){int a=Find(aliases[indices[i]]);parents[Find(aliases[indices[i+1]])]=a;parents[Find(aliases[indices[i+2]])]=a;}
            var groups=new Dictionary<int,List<int>>();
            for(int i=0;i<indices.Length;i+=3){int key=Find(aliases[indices[i]]);if(!groups.TryGetValue(key,out var list))groups[key]=list=new List<int>();list.Add(i/3);}
            var world=vertices.Select(filter.transform.TransformPoint).ToArray();
            double total=Volume(indices.Select((_,i)=>i/3).Distinct().ToList(),indices,vertices);
            var bad=groups.Values.Where(g=>Volume(g,indices,vertices)*Math.Sign(total)<0).ToArray();
            if(groups.Count!=19||bad.Length!=1||bad[0].Count!=6)throw new InvalidDataException("The source does not match the one diagnosed six-triangle component; no repair was applied.");
            var scale=filter.transform.lossyScale;double metricVolumeScale=Math.Abs((double)scale.x*scale.y*scale.z);
            var removed=bad[0];double volume=Volume(removed,indices,vertices)*Math.Sign(total)*metricVolumeScale;
            var points=removed.SelectMany(t=>new[]{world[indices[t*3]],world[indices[t*3+1]],world[indices[t*3+2]]}).ToArray();
            var artifactBounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)artifactBounds.Encapsulate(point);
            if(volume>=0||Math.Abs(volume)>1e-13||artifactBounds.size.magnitude>.002f)throw new InvalidDataException("The component exceeds the diagnosed numerical-artifact bounds. Original preserved.");
            var removedSet=new HashSet<int>(removed);var repaired=UnityEngine.Object.Instantiate(source);repaired.name="FMA9757 - audited derived cut mesh";
            int offset=0;
            for(int submesh=0;submesh<source.subMeshCount;submesh++)
            {
                var input=source.GetTriangles(submesh);var output=new List<int>();
                for(int triangle=0;triangle<input.Length/3;triangle++)if(!removedSet.Contains(offset+triangle))output.AddRange(new[]{input[triangle*3],input[triangle*3+1],input[triangle*3+2]});
                offset+=input.Length/3;repaired.SetTriangles(output,submesh,false);
            }
            repaired.bounds=source.bounds;
            bool channels=vertices.SequenceEqual(repaired.vertices)&&source.uv.SequenceEqual(repaired.uv)&&source.normals.SequenceEqual(repaired.normals)&&source.tangents.SequenceEqual(repaired.tangents);
            var retainedBounds=new Bounds(world[repaired.triangles[0]],Vector3.zero);foreach(int index in repaired.triangles)retainedBounds.Encapsulate(world[index]);
            var originalBounds=new Bounds(world[indices[0]],Vector3.zero);foreach(int index in indices)originalBounds.Encapsulate(world[index]);
            bool bounds=Vector3.Distance(retainedBounds.min,originalBounds.min)<1e-7f&&Vector3.Distance(retainedBounds.max,originalBounds.max)<1e-7f;
            if(!channels||!bounds)throw new InvalidDataException("Repair changed source channels or anatomical bounds.");
            var validation=ValidateCuts(repaired,filter.transform);
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Output);
            if(existing){EditorUtility.CopySerialized(repaired,existing);UnityEngine.Object.DestroyImmediate(repaired);EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(repaired,Output);
            AssetDatabase.SaveAssets();
            var audit=new Audit{utc=DateTime.UtcNow.ToString("o"),sourceSha256=hash,sourceTriangles=indices.Length/3,derivedTriangles=indices.Length/3-6,components=groups.Count,removedTriangles=6,removedSignedVolumeM3=volume,removedDiagonalM=artifactBounds.size.magnitude,sourceFileUnchanged=hash==Hash(File.ReadAllBytes(Source)),vertexChannelsUnchanged=channels,boundsUnchanged=bounds,removedSourceTriangleIndices=removed.ToArray(),cutValidation=validation};
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
            File.WriteAllText(Path.Combine(root,"qa/vr-fma9757-derived-mesh-audit.json"),JsonUtility.ToJson(audit,true));
            File.WriteAllText(Path.ChangeExtension(Output,"provenance.json"),JsonUtility.ToJson(audit,true));AssetDatabase.Refresh();
        }
        static double Volume(List<int> triangles,int[] indices,Vector3[] vertices)
        {
            var origin=vertices[indices[triangles[0]*3]];double volume=0;
            foreach(int triangle in triangles)
            {
                var pa=vertices[indices[triangle*3]];var pb=vertices[indices[triangle*3+1]];var pc=vertices[indices[triangle*3+2]];
                double ax=(double)pa.x-origin.x,ay=(double)pa.y-origin.y,az=(double)pa.z-origin.z;
                double bx=(double)pb.x-origin.x,by=(double)pb.y-origin.y,bz=(double)pb.z-origin.z;
                double cx=(double)pc.x-origin.x,cy=(double)pc.y-origin.y,cz=(double)pc.z-origin.z;
                volume+=ax*(by*cz-bz*cy)+ay*(bz*cx-bx*cz)+az*(bx*cy-by*cx);
            }
            return volume/6;
        }
        static string[] ValidateCuts(Mesh mesh,Transform transform)
        {
            var vertices=mesh.vertices;var indices=mesh.triangles;var results=new List<string>();
            var candidates=Enumerable.Range(0,indices.Length/3).OrderByDescending(i=>Vector3.Cross(vertices[indices[i*3+1]]-vertices[indices[i*3]],vertices[indices[i*3+2]]-vertices[indices[i*3]]).sqrMagnitude).Take(3).ToArray();
            foreach(int triangle in candidates)
            {
                var a=transform.TransformPoint(vertices[indices[triangle*3]]);var b=transform.TransformPoint(vertices[indices[triangle*3+1]]);var c=transform.TransformPoint(vertices[indices[triangle*3+2]]);
                var center=(a+b+c)/3;var normal=Vector3.Cross(b-a,c-a).normalized;var tangent=(b-a).normalized;var rotation=Quaternion.LookRotation(normal,tangent);var poses=new List<float>();
                NativeCutting.AddPose(poses,transform,center-tangent*.004f,rotation,.0007f,.003f,.006f);NativeCutting.AddPose(poses,transform,center+tangent*.004f,rotation,.0007f,.003f,.006f);
                var result=NativeCutting.Cut(CutMeshData.Capture(mesh),poses.ToArray(),32);
                if(result.status!=0||result.pieces.Count<1||result.pieces.Count>32||result.pieces.Sum(p=>p.indices.Length/3)>100000)throw new InvalidDataException("Derived cut validation failed: "+result.message);
                results.Add("Localized native incision: "+result.pieces.Count+" outputs, "+result.pieces.Sum(p=>p.indices.Length/3)+" triangles; "+result.message);
            }
            return results.ToArray();
        }
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
