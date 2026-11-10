using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class ComfortVignette : MonoBehaviour
    {
        Material material;Mesh mesh;Renderer surface;float amount,target;
        void Awake()
        {
            var shade=Resources.Load<Shader>("Learning/ComfortVignette");material=new Material(shade);var go=new GameObject("VR comfort periphery");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.forward*.25f;
            const int segments=48;var vertices=new Vector3[segments*3];var colors=new Color[vertices.Length];var triangles=new int[segments*12];
            for(int i=0;i<segments;i++)for(int band=0;band<3;band++){float a=i*Mathf.PI*2/segments;float r=band==0?.13f:band==1?.28f:2f;vertices[i*3+band]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);colors[i*3+band]=new Color(0,0,0,band==0?0:1);}
            int index=0;for(int i=0;i<segments;i++)for(int b=0;b<2;b++){int a=i*3+b,c=((i+1)%segments)*3+b;triangles[index++]=a;triangles[index++]=c;triangles[index++]=a+1;triangles[index++]=a+1;triangles[index++]=c;triangles[index++]=c+1;}
            mesh=new Mesh{name="Comfort ring"};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;surface=go.AddComponent<MeshRenderer>();surface.sharedMaterial=material;surface.enabled=false;
        }
        public void SetMoving(bool moving){target=moving?.85f:0;}
        void LateUpdate(){var settings=LabRuntime.Instance?.Settings?.Current;float value=settings!=null&&settings.vignette?target:0;amount=Mathf.MoveTowards(amount,value,Time.unscaledDeltaTime*5);material.SetFloat("_Amount",amount);surface.enabled=amount>.01f;target=0;}
        void OnDestroy(){if(material)Destroy(material);if(mesh)Destroy(mesh);}
    }
}
