using System.Collections.Generic;
using UnityEngine;

namespace ThoraxLab.Learning
{
    // Original enlarged cutaways. Their dimensions explain mechanisms, not patient measurements.
    public sealed class DiseaseTeachingModel : MonoBehaviour
    {
        string disease;
        Material tissue,airway,fluid,plaque,steel,flow,infected;
        readonly List<Transform> walls=new List<Transform>(),particles=new List<Transform>(),cusps=new List<Transform>(),replacementCusps=new List<Transform>(),alveoli=new List<Transform>();
        Transform lung,collection,scaffold,lining;
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        public void Build(string condition)
        {
            disease=condition;tissue=Mat("Lung schematic",new Color(.69f,.35f,.32f));airway=Mat("Airway lining",new Color(.83f,.66f,.54f));fluid=Mat("Pleural collection",new Color(.74f,.52f,.22f));plaque=Mat("Atheroma schematic",new Color(.84f,.64f,.28f));steel=Mat("Device scaffold",new Color(.68f,.75f,.79f),.7f);flow=Mat("Flow indicators",new Color(.2f,.81f,.7f));infected=Mat("Inflammatory material",new Color(.83f,.76f,.54f));
            if(condition=="pneumothorax"||condition=="pleural-effusion")
            {
                var boundary=Shell("Parietal pleural cutaway",.111f,.108f,.08f,airway);boundary.localScale=new Vector3(1,1.3f,1);
                lung=Part("Lung compartment",PrimitiveType.Sphere,Vector3.zero,new Vector3(.13f,.21f,.075f),tissue);
                collection=Part(condition=="pneumothorax"?"Pleural air compartment":"Pleural fluid compartment",PrimitiveType.Sphere,new Vector3(.045f,-.045f,-.025f),new Vector3(.08f,.14f,.025f),condition=="pneumothorax"?flow:fluid);
            }
            else if(condition=="pneumonia")
            {
                for(int i=0;i<14;i++)
                {
                    float a=i*2.399963f;float r=.021f*Mathf.Sqrt(i);var center=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(a)*.018f);
                    var sac=Shell("Opened alveolar sac",.021f,.018f,.028f,tissue);sac.localPosition=center;
                    alveoli.Add(Part("Air-space inflammatory material",PrimitiveType.Sphere,center,Vector3.one*.028f,infected));
                    Wire("Capillary beside alveolus",new[]{center+new Vector3(-.02f,-.01f,.014f),center+new Vector3(-.016f,.021f,.014f),center+new Vector3(.017f,.02f,.014f),center+new Vector3(.022f,-.005f,.014f)},.001f,flow);
                }
            }
            else
            {
                Shell(condition=="asthma"?"Bronchial wall cutaway":"Arterial wall cutaway",.087f,.074f,.145f,tissue);
                lining=Shell("Inner lumen lining",.073f,.061f,.148f,airway);
                if(condition=="asthma")for(int i=0;i<5;i++){var band=Shell("Smooth muscle band",.089f,.086f,.008f,plaque);band.localPosition=new Vector3(0,0,-.055f+i*.027f);walls.Add(band);}
                if(condition=="coronary-disease")collection=Part("Plaque limiting the lumen",PrimitiveType.Sphere,new Vector3(.033f,0,0),new Vector3(.067f,.10f,.085f),plaque);
                if(condition=="aortic-stenosis")for(int i=0;i<3;i++)
                {
                    cusps.Add(Leaflet("Native calcified cusp",i,airway));var replacement=Leaflet("Replacement tissue leaflet",i,tissue);replacement.gameObject.SetActive(false);replacementCusps.Add(replacement);
                    for(int n=0;n<3;n++){float a=(i*120+n*20-20)*Mathf.Deg2Rad;Part("Calcification on native cusp",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.04f,Mathf.Sin(a)*.04f,.006f),Vector3.one*.009f,plaque);}
                }
                scaffold=new GameObject("Treatment scaffold").transform;scaffold.SetParent(transform,false);
                if(condition!="asthma")
                {
                    var vertices=new List<Vector3>();var triangles=new List<int>();
                    for(int row=0;row<4;row++)for(int col=0;col<12;col++)
                    {float a=col*Mathf.PI/6,z=-.059f+row*.035f;Vector3 At(float angle,float depth)=>new Vector3(Mathf.Cos(angle)*.058f,Mathf.Sin(angle)*.058f,depth);AppendWire(new[]{At(a,z),At(a+Mathf.PI/12,z+.0175f),At(a,z+.035f),At(a-Mathf.PI/12,z+.0175f),At(a,z)},.00075f,vertices,triangles);}
                    MeshPart("Diamond woven scaffold",vertices,triangles,steel).SetParent(scaffold,true);
                }
                scaffold.gameObject.SetActive(false);
                for(int i=0;i<10;i++)particles.Add(Part("Flow indicator",PrimitiveType.Sphere,new Vector3(0,0,i*.022f-.1f),Vector3.one*.006f,flow));
            }
        }
        public void SetResponse(float response,bool reducedMotion)
        {
            float r=Mathf.Clamp01(response);
            if(lung)lung.localScale=new Vector3(Mathf.Lerp(.075f,.15f,r),Mathf.Lerp(.15f,.24f,r),.075f);
            if(collection){if(disease=="coronary-disease"){collection.localPosition=new Vector3(Mathf.Lerp(.033f,.064f,r),0,0);collection.localScale=new Vector3(Mathf.Lerp(.067f,.029f,r),Mathf.Lerp(.10f,.12f,r),Mathf.Lerp(.085f,.12f,r));}else collection.localScale=new Vector3(.08f,.14f,.025f)*Mathf.Lerp(1,.08f,r);}
            if(scaffold)scaffold.gameObject.SetActive(r>.55f);
            if(disease=="asthma"&&lining)lining.localScale=new Vector3(Mathf.Lerp(.72f,1,r),Mathf.Lerp(.72f,1,r),1);
            // Native leaflets remain displaced around the replacement; they do not disappear or become a new valve.
            for(int i=0;i<cusps.Count;i++)cusps[i].localRotation=Quaternion.Euler(0,Mathf.Lerp(2,78,r),i*120);
            for(int i=0;i<replacementCusps.Count;i++){var cusp=replacementCusps[i];cusp.gameObject.SetActive(r>.6f);cusp.localRotation=Quaternion.Euler(0,reducedMotion?55:Mathf.Lerp(8,67,(Mathf.Sin(Time.time*6)+1)*.5f),i*120);}
            foreach(var alveolus in alveoli)alveolus.localScale=Vector3.one*.028f*Mathf.Lerp(1,.1f,r);
            if(!reducedMotion)for(int i=0;i<particles.Count;i++)particles[i].localPosition=new Vector3(0,0,Mathf.Repeat(Time.time*Mathf.Lerp(.03f,.08f,r)+i*.022f,.22f)-.11f);
        }
        Material Mat(string name,Color color,float metallic=0){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.4f);materials.Add(m);return m;}
        Transform Part(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material material){var go=GameObject.CreatePrimitive(type);go.name=name;Destroy(go.GetComponent<Collider>());go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        Transform MeshPart(string title,List<Vector3> vertices,List<int> triangles,Material material)
        {var mesh=new Mesh{name=title};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();meshes.Add(mesh);var go=new GameObject(title);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;}
        Transform Shell(string title,float outside,float inside,float length,Material material)
        {
            const int segments=64;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<=segments;i++){float angle=Mathf.Lerp(30,330,i/(float)segments)*Mathf.Deg2Rad;var d=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);vertices.Add(d*outside+Vector3.back*length*.5f);vertices.Add(d*outside+Vector3.forward*length*.5f);vertices.Add(d*inside+Vector3.back*length*.5f);vertices.Add(d*inside+Vector3.forward*length*.5f);}
            void Quad(int a,int b,int c,int d){triangles.AddRange(new[]{a,b,c,a,c,d});}
            for(int i=0;i<segments;i++){int n=i*4;Quad(n,n+4,n+5,n+1);Quad(n+2,n+3,n+7,n+6);Quad(n,n+2,n+6,n+4);Quad(n+1,n+5,n+7,n+3);}
            Quad(0,1,3,2);int end=segments*4;Quad(end,end+2,end+3,end+1);return MeshPart(title,vertices,triangles,material);
        }
        Transform Leaflet(string title,int index,Material material)
        {
            var pivot=new GameObject(title).transform;pivot.SetParent(transform,false);float a=index*120*Mathf.Deg2Rad;pivot.localPosition=new Vector3(Mathf.Cos(a)*.063f,Mathf.Sin(a)*.063f,0);pivot.localRotation=Quaternion.Euler(0,0,index*120);
            var vertices=new List<Vector3>{new Vector3(-.045f,0,.014f)};var triangles=new List<int>();
            for(int i=0;i<=18;i++){float arc=Mathf.Lerp(-60,60,i/18f)*Mathf.Deg2Rad;vertices.Add(new Vector3(Mathf.Cos(arc)*.063f-.063f,Mathf.Sin(arc)*.063f,0));}
            for(int i=1;i<19;i++)triangles.AddRange(new[]{0,i,i+1,0,i+1,i});var surface=MeshPart("Curved leaflet surface",vertices,triangles,material);surface.SetParent(pivot,false);return pivot;
        }
        Transform Wire(string title,Vector3[] points,float radius,Material material)
        {var vertices=new List<Vector3>();var triangles=new List<int>();AppendWire(points,radius,vertices,triangles);return MeshPart(title,vertices,triangles,material);}
        static void AppendWire(Vector3[] points,float radius,List<Vector3> vertices,List<int> triangles)
        {
            const int sides=6;int offset=vertices.Count;
            for(int i=0;i<points.Length;i++)
            {
                var tangent=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;var right=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(tangent,right).normalized;
                for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;vertices.Add(points[i]+radius*(right*Mathf.Cos(a)+up*Mathf.Sin(a)));}
                if(i==0)continue;for(int j=0;j<sides;j++){int a=offset+(i-1)*sides+j,b=offset+(i-1)*sides+(j+1)%sides,c=offset+i*sides+j,d=offset+i*sides+(j+1)%sides;triangles.AddRange(new[]{a,c,d,a,d,b});}
            }
        }
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
