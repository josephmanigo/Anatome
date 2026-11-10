#include "thoraxcut.h"
#include <manifold/manifold.h>
#include <iostream>
#include <vector>
#include <cmath>
#include <limits>
using namespace manifold;
int failures=0, checks=0;
void check(bool ok,const char* name){++checks;std::cout<<(ok?"PASS ":"FAIL ")<<name<<"\n";if(!ok)++failures;}
std::vector<float> poses(float x,float halfWidth=.001f){
 std::vector<float> a;
 for(float y:{-.3f,0.f,.3f})for(int i=0;i<8;++i){a.push_back(x+((i&1)?halfWidth:-halfWidth));a.push_back(y+((i&2)?.01f:-.01f));a.push_back((i&4)?.5f:-.5f);}
 return a;
}
void* cut(Manifold body,std::vector<float>p,int maxTris=100000,int maxPieces=32){
 auto m=body.SetProperties(3,[](double*v,vec3 p,const double*){v[0]=p.x;v[1]=p.y;v[2]=0;}).GetMeshGL();
 std::vector<int>t(m.triVerts.begin(),m.triVerts.end());
 return tl_cut(m.vertProperties.data(),m.NumVert(),t.data(),int(t.size()),p.data(),int(p.size()/24),maxTris,maxPieces);
}
int main(){
 auto skin=Manifold::Cube({.3,.3,.004},true);
 auto airway=Manifold::Cylinder(.2,.025,.025,40,true)-Manifold::Cylinder(.3,.02,.02,40,true);
 auto organ=Manifold::Sphere(.1,32);
 auto rib=Manifold::Cylinder(.25,.009,.009,24,true);
 for(auto fixture:std::vector<std::pair<const char*,Manifold>>{{"skin shell",skin},{"hollow airway",airway},{"organ",organ},{"rib",rib}}){
   void*r=cut(fixture.second,poses(0));check(tl_status(r)==0,fixture.first);check(tl_piece_count(r)==2,"complete separation produces two pieces");
   for(int i=0;i<tl_piece_count(r);++i){int n=0,t=0;check(tl_piece_sizes(r,i,&n,&t)==1,"bounded buffer sizing");std::vector<float>v(n*6);std::vector<int>idx(t);check(tl_piece_copy(r,i,v.data(),int(v.size()),idx.data(),t)==1,"copy buffers");MeshGL m;m.numProp=6;m.vertProperties=v;m.triVerts.assign(idx.begin(),idx.end());m.Merge();Manifold part(m);check(part.Status()==Manifold::Error::NoError,"fragment manifold");bool caps=false;for(int j=5;j<int(v.size());j+=6)caps|=v[j]>.5f;check(caps,"cut material retained");}
   tl_free(r);
 }
 auto r=cut(airway,poses(.2));check(tl_status(r)==2,"miss preserves original");tl_free(r);
 // A grazing stroke must not detach a sealed cavity as an inverted solid.
 auto enclosed=Manifold::Sphere(.1,32)-Manifold::Sphere(.07,32);
 r=cut(enclosed,poses(.085f));std::cout<<"Enclosed result: "<<tl_status(r)<<" "<<tl_message(r)<<"\n";check(tl_status(r)==0,"enclosed chamber grazing cut");check(tl_piece_count(r)==2,"cavity boundary stays with its tissue owner");
 double sum=0;bool negative=false;for(int i=0;i<tl_piece_count(r);i++){int nv,nt;tl_piece_sizes(r,i,&nv,&nt);std::vector<float>p(nv*6);std::vector<int>t(nt);tl_piece_copy(r,i,p.data(),p.size(),t.data(),t.size());MeshGL m;m.numProp=6;m.vertProperties=p;m.triVerts.assign(t.begin(),t.end());m.Merge();Manifold piece(m);negative|=piece.Volume()<=0;sum+=piece.Volume();}
 check(!negative,"no negative-volume detached fragments");check(sum<enclosed.Volume()&&sum>enclosed.Volume()*.8,"enclosed cavity volume preserved");tl_free(r);
 r=cut(organ,poses(0),4);check(tl_status(r)==3,"triangle limit rejects");tl_free(r);
 r=cut(organ,poses(0),100000,1);check(tl_status(r)==3,"fragment limit rejects");tl_free(r);
 // Pre-existing islands are still one source record, not forty new fragments.
 std::vector<Manifold> islands;
 for(int i=0;i<40;i++)islands.push_back(Manifold::Cube({.01,.01,.01},true).Translate({i*.025,0,0}));
 auto compound=Manifold::BatchBoolean(islands,OpType::Add);
 r=cut(compound,poses(0),100000,2);
 check(tl_status(r)==0,"localized cut preserves untouched source islands without fragment inflation");
 check(tl_piece_count(r)==2,"only newly separated cut pieces become output fragments");
 bool normalized=true;double compoundVolume=0;
 for(int i=0;i<tl_piece_count(r);i++){
   int nv=0,nt=0;tl_piece_sizes(r,i,&nv,&nt);std::vector<float>v(nv*6);std::vector<int>t(nt);tl_piece_copy(r,i,v.data(),int(v.size()),t.data(),int(t.size()));
   for(int p=5;p<int(v.size());p+=6)normalized&=v[p]<=1;
   MeshGL mesh;mesh.numProp=6;mesh.vertProperties=v;mesh.triVerts.assign(t.begin(),t.end());mesh.Merge();Manifold result(mesh);
   check(result.Status()==Manifold::Error::NoError,"retained source island group remains manifold");compoundVolume+=result.Volume();
 }
 check(normalized,"temporary cut provenance marker does not leak into renderer material values");
 check(compoundVolume<compound.Volume()&&compoundVolume>compound.Volume()*.99,"untouched source island volumes are preserved");tl_free(r);
 // A blade which really crosses every island must still obey the fragment cap.
 auto across=poses(0);for(size_t i=0;i<across.size();i+=3)std::swap(across[i],across[i+1]);
 // Extend the stroke along the row while retaining its narrow section through y=0.
 for(size_t i=0;i<across.size();i+=3)across[i]=across[i]*4.f+.45f;
 r=cut(compound,across,100000,32);check(tl_status(r)==3,"more than32 newly cut islands still fail the fragment budget");tl_free(r);
 auto p=poses(0);p[0]=std::numeric_limits<float>::quiet_NaN();r=cut(organ,p);check(tl_status(r)==1,"NaN rejected");tl_free(r);
 auto mesh=skin.GetMeshGL();std::vector<int>bad(mesh.triVerts.begin(),mesh.triVerts.end()-3);std::vector<float>v;for(size_t i=0;i<mesh.vertProperties.size();i+=3){v.insert(v.end(),{mesh.vertProperties[i],mesh.vertProperties[i+1],mesh.vertProperties[i+2],0,0,0});}p=poses(0);r=tl_cut(v.data(),int(v.size()/6),bad.data(),int(bad.size()),p.data(),3,100000,32);check(tl_status(r)==1,"open mesh rejected without hole fill");tl_free(r);
 for(int i=0;i<25;++i){r=cut(organ,poses(float(i%3)*.01f));check(tl_status(r)==0,"repeat allocation/free");tl_free(r);}
 // User regression: moving a cut chunk must leave a complementary hole, not a second full shell.
 for(auto body:{skin,airway,organ,rib,enclosed,compound}){
  auto m=body.SetProperties(3,[](double*v,vec3 p,const double*){v[0]=p.x;v[1]=p.y;v[2]=0;}).GetMeshGL();
  std::vector<int>idx(m.triVerts.begin(),m.triVerts.end());auto stroke=poses(0,.004f);
  r=tl_extract_chunk(m.vertProperties.data(),m.NumVert(),idx.data(),int(idx.size()),stroke.data(),3,100000,32);
  check(tl_status(r)==0&&tl_piece_count(r)==2,"chunk and anchored remainder produced");
  std::vector<Manifold> parts;
  for(int i=0;i<tl_piece_count(r);i++){int nv,nt;tl_piece_sizes(r,i,&nv,&nt);MeshGL out;out.numProp=6;out.vertProperties.resize(nv*6);std::vector<int>t(nt);tl_piece_copy(r,i,out.vertProperties.data(),nv*6,t.data(),nt);out.triVerts.assign(t.begin(),t.end());out.Merge();parts.emplace_back(out);}
  if(parts.size()==2){
   check(std::abs(parts[0].Volume()+parts[1].Volume()-body.Volume())<body.Volume()*1e-5,"cut chunk conserves original tissue volume");
   check((parts[0]^parts[1]).Volume()<body.Volume()*1e-6,"chunk never overlaps remaining tissue");
   check(parts[1].BoundingBox().Size().x<=.00801,"movable piece stays inside stroke width, no full shell duplicate");
  }
  tl_free(r);
 }
 std::cout<<"RESULT "<<checks-failures<<"/"<<checks<<"\n";return failures?1:0;
}
