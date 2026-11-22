using UnityEngine;

namespace ThoraxLab.Learning
{
    public sealed class ComfortVignette : MonoBehaviour
    {
        Material material;Mesh mesh;Renderer surface;float amount,target,readingDim;
        void Awake()
        {
            var shade=Resources.Load<Shader>("Learning/ComfortVignette");material=new Material(shade);var go=new GameObject("VR comfort periphery");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.forward*.25f;
            const int segments=48;
            const int center=segments*3;
            var vertices=new Vector3[center+1];var colors=new Color[vertices.Length];var triangles=new int[segments*15];
            for(int i=0;i<segments;i++)for(int band=0;band<3;band++){float a=i*Mathf.PI*2/segments;float r=band==0?.13f:band==1?.28f:2f;vertices[i*3+band]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);colors[i*3+band]=new Color(0,0,0,band==0?0:1);}
            // The center fan carries only the reading dimmer; motion shading starts outside it.
            int index=0;
            for(int i=0;i<segments;i++)
            {
                int inner=i*3,next=((i+1)%segments)*3;
                triangles[index++]=center;triangles[index++]=inner;triangles[index++]=next;
                for(int band=0;band<2;band++)
                {
                    int a=inner+band,c=next+band;
                    triangles[index++]=a;triangles[index++]=c;triangles[index++]=a+1;
                    triangles[index++]=a+1;triangles[index++]=c;triangles[index++]=c+1;
                }
            }
            mesh=new Mesh{name="Comfort ring"};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;surface=go.AddComponent<MeshRenderer>();surface.sharedMaterial=material;
            readingDim=LabRuntime.Instance?.Settings?.Current?.ReadingDimAmount??0f;
            material.SetFloat("_ReadingDim",readingDim);surface.enabled=readingDim>0f;
        }
        public void SetMoving(bool moving){target=moving?.85f:0;}
        void LateUpdate(){
            var app=LabRuntime.Instance;
            var settings=app?.Settings?.Current;
            // Change the clear aperture only; XR still owns the per-eye projection.
            float scale=.25f*Mathf.Tan((settings?.comfortFov??55)*Mathf.Deg2Rad*.5f)/.13f;
            surface.transform.localScale=new Vector3(scale,scale,1);
            float value=settings!=null&&settings.vignette?target:0;amount=Mathf.MoveTowards(amount,value,Time.unscaledDeltaTime*5);
            float requestedDim=app && app.UI ? app.UI.PreviewReadingDim : settings?.ReadingDimAmount??0f;
            readingDim=Mathf.MoveTowards(readingDim,requestedDim,Time.unscaledDeltaTime);
            // Dim bright controls together with the room. Motion-only shading stays behind the console.
            int sortingOrder=readingDim>0f?LabUI.ConsoleSortingOrder+1:0;
            if(surface.sortingOrder!=sortingOrder)surface.sortingOrder=sortingOrder;
            material.SetFloat("_Amount",amount);material.SetFloat("_ReadingDim",readingDim);
            surface.enabled=amount>.01f||readingDim>0f;target=0;
        }
        void OnDestroy(){if(material)Destroy(material);if(mesh)Destroy(mesh);}
    }
}
