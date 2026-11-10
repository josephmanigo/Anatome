using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
namespace ThoraxLab {
public sealed class CutMeshData {
 public float[] vertices;public int[] indices;public float[] boneDepth;
 public static CutMeshData Capture(Mesh mesh){
  var p=mesh.vertices;var uv=mesh.uv;var marks=mesh.uv2;var d=new CutMeshData{vertices=new float[p.Length*6],indices=mesh.triangles};
  for(int i=0;i<p.Length;i++){int k=i*6;d.vertices[k]=p[i].x;d.vertices[k+1]=p[i].y;d.vertices[k+2]=p[i].z;d.vertices[k+3]=uv.Length==p.Length?uv[i].x:0;d.vertices[k+4]=uv.Length==p.Length?uv[i].y:0;d.vertices[k+5]=marks.Length==p.Length?marks[i].x:0;}
  double volume=0;for(int i=0;i<d.indices.Length;i+=3)volume+=Vector3.Dot(p[d.indices[i]],Vector3.Cross(p[d.indices[i+1]],p[d.indices[i+2]]));
  if(volume<0)for(int i=0;i<d.indices.Length;i+=3)(d.indices[i+1],d.indices[i+2])=(d.indices[i+2],d.indices[i+1]);
  return d;
 }
 public Mesh CreateMesh(){
  int count=vertices.Length/6;var p=new Vector3[count];var uv=new Vector2[count];var marks=new Vector2[count];
  for(int i=0;i<count;i++){int k=i*6;p[i]=new Vector3(vertices[k],vertices[k+1],vertices[k+2]);uv[i]=new Vector2(vertices[k+3],vertices[k+4]);marks[i]=new Vector2(vertices[k+5],0);}
  var outer=new List<int>();var inner=new List<int>();for(int i=0;i<indices.Length;i+=3){bool cap=marks[indices[i]].x>.5f&&marks[indices[i+1]].x>.5f&&marks[indices[i+2]].x>.5f;var list=cap?inner:outer;list.Add(indices[i]);list.Add(indices[i+1]);list.Add(indices[i+2]);}
  var m=new Mesh{name="Validated dissection fragment",indexFormat=IndexFormat.UInt32};m.vertices=p;m.uv=uv;m.uv2=marks;if(boneDepth!=null){var colors=new Color[count];for(int i=0;i<count;i++)colors[i]=new Color(boneDepth[i],0,0,1);m.colors=colors;}m.subMeshCount=2;m.SetTriangles(outer,0);m.SetTriangles(inner,1);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;
 }
}
public sealed class CutResult {public int status;public string message;public List<CutMeshData> pieces=new();}
public static class NativeCutting {
 const string Dll="thoraxcut";
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr tl_cut(float[]v,int nv,int[]t,int nt,float[]poses,int np,int maxTriangles,int maxPieces);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int tl_status(IntPtr p);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr tl_message(IntPtr p);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int tl_piece_count(IntPtr p);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int tl_piece_sizes(IntPtr p,int i,out int nv,out int nt);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int tl_piece_copy(IntPtr p,int i,[Out]float[]v,int nv,[Out]int[]t,int nt);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void tl_free(IntPtr p);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr tl_version();
 public static string Version=>Marshal.PtrToStringAnsi(tl_version());
 public static CutResult Cut(CutMeshData mesh,float[]poses,int maxPieces){
  IntPtr handle=IntPtr.Zero;
  try{
   handle=tl_cut(mesh.vertices,mesh.vertices.Length/6,mesh.indices,mesh.indices.Length,poses,poses.Length/24,100000,maxPieces);
   var result=new CutResult{status=tl_status(handle),message=Marshal.PtrToStringAnsi(tl_message(handle))};
   if(result.status!=0)return result;
   for(int i=0;i<tl_piece_count(handle);i++){
    if(tl_piece_sizes(handle,i,out int nv,out int nt)!=1||nv>600000||nt>300000)throw new InvalidOperationException("Invalid native output bounds");
    var data=new CutMeshData{vertices=new float[nv*6],indices=new int[nt]};if(tl_piece_copy(handle,i,data.vertices,data.vertices.Length,data.indices,nt)!=1)throw new InvalidOperationException("Native output copy failed");result.pieces.Add(data);
   }return result;
  }catch(Exception ex){return new CutResult{status=4,message=ex.Message};}finally{if(handle!=IntPtr.Zero)tl_free(handle);}
 }
 public static void AddPose(List<float> data,Transform target,Vector3 center,Quaternion rotation,float width,float length,float depth){
  for(int i=0;i<8;i++){var p=center+rotation*new Vector3((i&1)==0?-width:width,(i&2)==0?-length:length,(i&4)==0?-depth:depth);p=target.InverseTransformPoint(p);data.Add(p.x);data.Add(p.y);data.Add(p.z);}
 }
}
}
