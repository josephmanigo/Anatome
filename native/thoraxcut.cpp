#include "thoraxcut.h"
#include <manifold/manifold.h>
#include <algorithm>
#include <cmath>
#include <memory>
#include <string>
#include <sstream>
#include <vector>
using manifold::Manifold;
using manifold::MeshGL;
using manifold::vec3;
namespace {
struct Result { int status=4; std::string message; std::vector<MeshGL> pieces; };
Result* ptr(void* p) { return static_cast<Result*>(p); }
bool finite(const float* p, size_t n) { for(size_t i=0;i<n;++i) if(!std::isfinite(p[i])) return false; return true; }
void appendMesh(MeshGL& target,const MeshGL& source){
 unsigned offset=unsigned(target.NumVert());target.numProp=6;
 target.vertProperties.insert(target.vertProperties.end(),source.vertProperties.begin(),source.vertProperties.end());
 for(auto i:source.triVerts)target.triVerts.push_back(i+offset);
 for(auto i:source.mergeFromVert)target.mergeFromVert.push_back(i+offset);
 for(auto i:source.mergeToVert)target.mergeToVert.push_back(i+offset);
 target.runIndex.clear();target.runOriginalID.clear();target.runTransform.clear();target.runFlags.clear();target.faceID.clear();target.halfedgeTangent.clear();
}
// Topological decomposition separates enclosed void boundary shells too. They
// are not detached tissue: attach each negative shell to its containing solid.
std::vector<MeshGL> tissuePieces(const Manifold& output,bool removeExternalSourceArtifacts=false){
 auto components=output.Decompose();std::vector<Manifold> positive;std::vector<Manifold> cavities;
 for(auto& p:components)(p.Volume()<0?cavities:positive).push_back(p);
 std::sort(positive.begin(),positive.end(),[](const Manifold&a,const Manifold&b){return a.Volume()>b.Volume();});
 std::vector<MeshGL> meshes;for(auto& p:positive)meshes.push_back(p.GetMeshGL());
 for(auto& cavity:cavities){auto m=cavity.GetMeshGL();int owner=-1;double smallest=1e100;
  for(size_t i=0;i<positive.size();i++){
   auto box=positive[i].BoundingBox();double extent=manifold::la::length(box.Size())+1e-4;int insideSamples=0;
   for(size_t sample=0;sample<5;sample++){
    vec3 point(0.0);size_t tri=(m.NumTri()-1)*sample/4;
    for(int j=0;j<3;j++){auto k=m.triVerts[tri*3+j]*m.numProp;point+=vec3(m.vertProperties[k],m.vertProperties[k+1],m.vertProperties[k+2])/3.;}
    if(!manifold::Box(box.min-vec3(extent*1e-6),box.max+vec3(extent*1e-6)).Contains(point))continue;
    int votes=0;
    for(auto direction:{vec3(1.123,.817,.339),vec3(-.729,1.317,.531),vec3(.317,-.419,1.113)}){
     auto hits=positive[i].RayCast(point,point+direction*extent*8.);
     // First oriented exit avoids double-counting a ray through a shared edge.
     for(auto& hit:hits)if(hit.distance>1e-9){votes+=manifold::la::dot(hit.normal,direction)>0;break;}
    }
    insideSamples+=votes>=2;
   }
   if(insideSamples>=2&&positive[i].Volume()<smallest){owner=int(i);smallest=positive[i].Volume();}
  }
  double overlapRatio=0;
  if(owner<0){
   auto inverted=m;for(size_t k=0;k<inverted.triVerts.size();k+=3)std::swap(inverted.triVerts[k+1],inverted.triVerts[k+2]);inverted.runIndex.clear();inverted.runOriginalID.clear();inverted.runTransform.clear();inverted.runFlags.clear();inverted.faceID.clear();Manifold voidVolume(inverted);
   if(voidVolume.Status()!=Manifold::Error::NoError||voidVolume.Volume()<=0)throw std::runtime_error("Could not validate cavity containment volume");
   for(size_t i=0;i<positive.size();i++){double ratio=(positive[i]^voidVolume).Volume()/voidVolume.Volume();overlapRatio=std::max(overlapRatio,ratio);if(ratio>.9999){owner=int(i);break;}}
  }
  // Offline source repair only: a negative shell outside every positive volume
  // is an invalid negative-occupancy artifact, not a void inside anatomy.
  if(owner<0&&removeExternalSourceArtifacts&&overlapRatio<1e-8)continue;
  if(owner<0){std::ostringstream detail;detail<<"An enclosed void has no containing tissue shell; previous mesh preserved. Void volume="<<cavity.Volume()<<", triangles="<<cavity.NumTri()<<", tissue components="<<positive.size()<<", maximum containment="<<overlapRatio;throw std::runtime_error(detail.str());}
  appendMesh(meshes[owner],m);
 }
 for(auto& m:meshes){Manifold test(m);if(test.Status()!=Manifold::Error::NoError||test.Volume()<=0)throw std::runtime_error("Invalid tissue/void grouping");}
 return meshes;
}
}
void* tl_cut(const float* vertices, int vertexCount, const int* triangles,
             int indexCount, const float* bladeCorners, int poseCount,
             int maxTriangles, int maxPieces) {
  auto r=std::make_unique<Result>();
  try {
    if(!vertices || !triangles || !bladeCorners || vertexCount<4 || vertexCount>600000 ||
       indexCount<12 || indexCount%3 || indexCount>300000 || poseCount<2 || poseCount>128 ||
       maxTriangles<1 || maxTriangles>100000 || maxPieces<1 || maxPieces>32 ||
       !finite(vertices,size_t(vertexCount)*6) || !finite(bladeCorners,size_t(poseCount)*24)) {
      r->status=1; r->message="Invalid input or cut budget."; return r.release();
    }
    MeshGL mesh; mesh.numProp=6;
    mesh.vertProperties.assign(vertices,vertices+size_t(vertexCount)*6);
    for(int i=0;i<indexCount;++i) {
      if(triangles[i]<0 || triangles[i]>=vertexCount) { r->status=1; r->message="Triangle index outside vertex buffer."; return r.release(); }
      mesh.triVerts.push_back(unsigned(triangles[i]));
    }
    mesh.Merge(); // Weld coincident seam vertices; never cap open anatomical holes.
    Manifold body(mesh);
    if(body.Status()!=Manifold::Error::NoError || body.IsEmpty()) {
      r->status=1; r->message="Target is not a closed, oriented tissue volume. Original preserved."; return r.release();
    }
    std::vector<Manifold> segments;
    for(int i=1;i<poseCount;++i) {
      std::vector<vec3> points;
      for(int j=(i-1)*24;j<(i+1)*24;j+=3) points.emplace_back(bladeCorners[j],bladeCorners[j+1],bladeCorners[j+2]);
      auto segment=Manifold::Hull(points);
      if(!segment.IsEmpty()) segments.push_back(segment);
    }
    if(segments.empty()) { r->status=2; r->message="The stroke has no cutting volume."; return r.release(); }
    auto blade=Manifold::BatchBoolean(segments,manifold::OpType::Add)
      .SetProperties(3,[](double* p,vec3 v,const double*) {p[0]=v.x*40; p[1]=v.z*40; p[2]=1;});
    auto output=body-blade;
    if(output.Status()!=Manifold::Error::NoError) {r->message="Boolean failed validation. Original preserved."; return r.release();}
    if(output.IsEmpty()) {r->status=2; r->message="Stroke would erase the entire target; use remove instead."; return r.release();}
    double before=body.Volume(), after=output.Volume();
    if(std::abs(before-after)<=std::max(1e-15,std::abs(before)*1e-8)) {
      r->status=2; r->message="No material intersected."; return r.release();
    }
    auto parts=tissuePieces(output);
    if(parts.size()>size_t(maxPieces) || output.NumTri()>size_t(maxTriangles)) {
      r->status=3; r->message="Cut exceeds triangle or fragment budget. Undo or reset before continuing."; return r.release();
    }
    r->pieces=std::move(parts);
    r->status=0; r->message="Cut validated.";
  } catch(const std::exception& ex) {r->pieces.clear();r->message=ex.what();}
    catch(...) {r->pieces.clear();r->message="Native cutting exception; original preserved.";}
  return r.release();
}
int tl_status(void* r){return r?ptr(r)->status:4;}
void* normalizeImpl(const float* vertices,int vertexCount,const int* triangles,int indexCount,bool resolve){
 auto r=std::make_unique<Result>();
 try{
  if(!vertices||!triangles||vertexCount<4||vertexCount>600000||indexCount<12||indexCount>300000||indexCount%3||!finite(vertices,size_t(vertexCount)*6)){r->status=1;r->message="Invalid normalization input";return r.release();}
  MeshGL m;m.numProp=6;m.vertProperties.assign(vertices,vertices+size_t(vertexCount)*6);
  for(int i=0;i<indexCount;i++){if(triangles[i]<0||triangles[i]>=vertexCount){r->status=1;r->message="Invalid index";return r.release();}m.triVerts.push_back(triangles[i]);}
  m.Merge();Manifold body(m);if(body.Status()!=Manifold::Error::NoError||body.IsEmpty()){r->status=1;r->message="Not a closed volume";return r.release();}
  if(resolve){
   std::vector<Manifold> solids,voids;
   for(auto& part:body.Decompose()){
    if(part.Volume()>=0)solids.push_back(part);
    else{auto hole=part.GetMeshGL();for(size_t i=0;i<hole.triVerts.size();i+=3)std::swap(hole.triVerts[i+1],hole.triVerts[i+2]);hole.runIndex.clear();hole.runOriginalID.clear();hole.runTransform.clear();hole.runFlags.clear();hole.faceID.clear();voids.emplace_back(hole);}
   }
   body=Manifold::BatchBoolean(solids,manifold::OpType::Add);
   if(!voids.empty())body=body-Manifold::BatchBoolean(voids,manifold::OpType::Add);
   if(body.Status()!=Manifold::Error::NoError||body.IsEmpty()){r->status=1;r->message="Compound union-minus-voids failed";return r.release();}
   MeshGL physical;physical.numProp=6;for(auto& part:tissuePieces(body,true))appendMesh(physical,part);body=Manifold(physical);
  }
  r->pieces.push_back(body.GetMeshGL());r->status=0;r->message="Normalized without filling holes";
 }catch(const std::exception& ex){r->message=ex.what();}
 return r.release();
}
void* tl_normalize(const float* v,int nv,const int* t,int nt){return normalizeImpl(v,nv,t,nt,false);}
void* tl_resolve_compound(const float* v,int nv,const int* t,int nt){return normalizeImpl(v,nv,t,nt,true);}
const char* tl_message(void* r){return r?ptr(r)->message.c_str():"No result.";}
int tl_piece_count(void* r){return r?int(ptr(r)->pieces.size()):0;}
int tl_piece_sizes(void* r,int i,int* v,int* t){
 if(!r||!v||!t||i<0||i>=tl_piece_count(r))return 0;
 auto&m=ptr(r)->pieces[i];*v=int(m.NumVert());*t=int(m.triVerts.size());return 1;
}
int tl_piece_copy(void* r,int i,float* v,int vf,int* t,int ti){
 if(!r||!v||!t||i<0||i>=tl_piece_count(r))return 0;
 auto&m=ptr(r)->pieces[i];if(vf!=int(m.vertProperties.size())||ti!=int(m.triVerts.size()))return 0;
 std::copy(m.vertProperties.begin(),m.vertProperties.end(),v);
 std::copy(m.triVerts.begin(),m.triVerts.end(),t);return 1;
}
void tl_free(void*r){delete ptr(r);}
const char* tl_version(){return "ThoraxCut/1.0 Manifold/3.5.2 11235e6b8ebea2dbed8aec4285685aafd3d95667";}
