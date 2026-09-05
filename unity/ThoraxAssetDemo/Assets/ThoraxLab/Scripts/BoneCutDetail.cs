using System.Collections.Generic;
using UnityEngine;
namespace ThoraxLab {
public static class BoneCutDetail {
 // Authored 1 mm cortical appearance, derived from distance to the original
 // external surface. This is visual teaching contrast, not segmented cortex.
 public static void Apply(CutResult result,CutMeshData source,float metresPerUnit){
  var surface=new List<(Vector3 a,Vector3 b,Vector3 c)>();
  for(int i=0;i<source.indices.Length;i+=3){int a=source.indices[i],b=source.indices[i+1],c=source.indices[i+2];if(source.vertices[a*6+5]<.5f&&source.vertices[b*6+5]<.5f&&source.vertices[c*6+5]<.5f)surface.Add((Point(source,a),Point(source,b),Point(source,c)));}
  foreach(var piece in result.pieces){
   var vertices=new List<float>(piece.vertices);var triangles=new List<int>();
   for(int i=0;i<piece.indices.Length;i+=3){int a=piece.indices[i],b=piece.indices[i+1],c=piece.indices[i+2];bool cap=piece.vertices[a*6+5]>.5f&&piece.vertices[b*6+5]>.5f&&piece.vertices[c*6+5]>.5f;
    if(cap){int center=vertices.Count/6;for(int p=0;p<6;p++)vertices.Add((piece.vertices[a*6+p]+piece.vertices[b*6+p]+piece.vertices[c*6+p])/3);triangles.AddRange(new[]{a,b,center,b,c,center,c,a,center});}else triangles.AddRange(new[]{a,b,c});
   }
   piece.vertices=vertices.ToArray();piece.indices=triangles.ToArray();piece.boneDepth=new float[piece.vertices.Length/6];
   for(int i=0;i<piece.boneDepth.Length;i++){if(piece.vertices[i*6+5]<.5f)continue;Vector3 p=Point(piece,i);float nearest=float.MaxValue;foreach(var tri in surface)nearest=Mathf.Min(nearest,DistanceSquared(p,tri.a,tri.b,tri.c));piece.boneDepth[i]=Mathf.Clamp01(Mathf.Sqrt(nearest)*metresPerUnit/.004f);}
  }
 }
 static Vector3 Point(CutMeshData mesh,int i)=>new(mesh.vertices[i*6],mesh.vertices[i*6+1],mesh.vertices[i*6+2]);
 static float DistanceSquared(Vector3 p,Vector3 a,Vector3 b,Vector3 c){
  var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return ap.sqrMagnitude;
  var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return bp.sqrMagnitude;
  float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return(p-(a+ab*(d1/(d1-d3)))).sqrMagnitude;
  var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return cp.sqrMagnitude;
  float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return(p-(a+ac*(d2/(d2-d6)))).sqrMagnitude;
  float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return(p-(b+(c-b)*((d4-d3)/(d4-d3+d5-d6)))).sqrMagnitude;
  float sum=va+vb+vc;if(Mathf.Abs(sum)<1e-20f)return Mathf.Min(ap.sqrMagnitude,Mathf.Min(bp.sqrMagnitude,cp.sqrMagnitude));return(p-(a+ab*(vb/sum)+ac*(vc/sum))).sqrMagnitude;
 }
}
}
