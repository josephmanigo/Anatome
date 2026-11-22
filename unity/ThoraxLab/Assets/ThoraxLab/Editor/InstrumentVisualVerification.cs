#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThoraxLab.Learning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThoraxLab.Editor
{
    /// <summary>Inspect authored instruments without changing the learning scene.</summary>
    public static class InstrumentVisualVerification
    {
        [Serializable] sealed class Entry
        {
            public string id;
            public int vertices,triangles,materialPasses,renderers,colliders;
            public bool finite,metricUVs,tangentFrames,authoredFinishes,tipAligned;
            public Vector3 size;
        }
        [Serializable] sealed class Report
        {
            public string utc,status;
            public int sharedFinishes;
            public List<Entry> instruments=new List<Entry>();
            public Entry radialBand;
            public MaskEntry oxygenMask;
            public string[] renders;
        }
        [Serializable] sealed class MaskEntry
        {
            public float sealPlaneZ,strapRearZ;
            public bool sealFacesForward,openingClear,strapBehind;
        }
        public static void Capture()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
            string output=Path.Combine(root,".impeccable","instrument-review");Directory.CreateDirectory(output);
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),status="passed"};
            var original=SceneManager.GetActiveScene();
            if(Application.isBatchMode && string.IsNullOrEmpty(original.path))
                original=EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.34f,.39f,.42f);RenderSettings.fog=false;
                var holder=new GameObject("Instrument inspection / temporary");
                var finishes=new HashSet<Material>();
                var visuals=new Dictionary<string,GameObject>();
                foreach(string id in InstrumentGeometry.SupportedIds.Concat(new[]{"radial-band"}))
                {
                    var go=InstrumentGeometry.Create(holder.transform,id);visuals.Add(id,go);
                    var entry=Inspect(go,id,finishes);
                    if(id=="oxygen")
                    {
                        report.oxygenMask=InspectMask(go);
                        entry.tipAligned=report.oxygenMask.sealFacesForward&&report.oxygenMask.openingClear&&report.oxygenMask.strapBehind;
                    }
                    int expectedRenderers=id=="dissection-saw"?2:1;
                    if(!entry.finite||!entry.metricUVs||!entry.tangentFrames||!entry.authoredFinishes||!entry.tipAligned||
                        entry.colliders!=0||entry.renderers!=expectedRenderers||entry.materialPasses>6||entry.triangles>12000)
                        report.status="failed";
                    if(id=="radial-band")report.radialBand=entry;else report.instruments.Add(entry);
                    foreach(var child in go.GetComponentsInChildren<Transform>())child.gameObject.layer=31;
                    go.SetActive(false);
                }
                report.sharedFinishes=finishes.Count;
                if(report.sharedFinishes!=7)report.status="failed";
                var cameraObject=new GameObject("Instrument inspection camera");cameraObject.transform.SetParent(holder.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.022f,.036f,.046f);camera.orthographic=true;camera.orthographicSize=.123f;camera.nearClipPlane=.05f;camera.farClipPlane=3;camera.cullingMask=1<<31;camera.allowMSAA=true;
                cameraObject.transform.position=new Vector3(0,0,-.8f);cameraObject.transform.rotation=Quaternion.identity;
                Light Lamp(string title,Quaternion angle,Color color,float intensity)
                {
                    var go=new GameObject(title);go.transform.SetParent(holder.transform,false);go.transform.rotation=angle;
                    var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.cullingMask=1<<31;light.shadows=LightShadows.None;return light;
                }
                Lamp("Soft key",Quaternion.Euler(35,-25,0),new Color(1,.94f,.87f),1.45f);
                Lamp("Cool edge",Quaternion.Euler(-30,140,0),new Color(.68f,.88f,1),.85f);
                var paths=new List<string>();
                void Save(string name){string path=Path.Combine(output,name+".png");Render(camera,path);paths.Add(path);}
                // Retain the original eight camera and lighting setups for direct image comparisons.
                foreach(string id in new[]{"scalpel","dissection-saw","suture","anaesthetic","inhaler","spacer","ultrasound","valve"})
                {
                    var visual=visuals[id];visual.SetActive(true);
                    var localCenter=WholeBounds(visual).center;
                    var rotation=id=="inhaler"?Quaternion.Euler(-50,150,-25):Quaternion.Euler(65,0,145);
                    visual.transform.localRotation=rotation;
                    visual.transform.localPosition=-(rotation*localCenter);
                    Save(id);
                    visual.SetActive(false);
                }
                RenderSettings.ambientLight=new Color(.42f,.45f,.46f);
                RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/ThoraxLab/Settings/LaboratoryReflection.exr");
                RenderSettings.reflectionIntensity=1;
                camera.backgroundColor=new Color(.085f,.115f,.13f);
                foreach(string id in new[]{"scalpel","dissection-saw","dissection-scalpel","dressing","swab","radial-band","oxygen"})
                {
                    var visual=visuals[id];visual.SetActive(true);
                    bool dissection=id.StartsWith("dissection-",StringComparison.Ordinal);
                    var rotation=Quaternion.AngleAxis(138,Vector3.forward)*Quaternion.Euler(71,0,dissection?71:-8);
                    if(id=="dressing"||id=="radial-band")rotation=Quaternion.Euler(-24,165,-19);
                    if(id=="oxygen")rotation=Quaternion.Euler(-14,119,-10);
                    visual.transform.SetLocalPositionAndRotation(Vector3.zero,rotation);
                    var bounds=WholeBounds(visual);
                    visual.transform.localPosition=-bounds.center;
                    camera.orthographicSize=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.13f;
                    Save(id=="scalpel"?"scalpel-three-quarter":id=="dissection-saw"?"saw-three-quarter":id);
                    if(id=="scalpel"||id=="dissection-saw")
                    {
                        bool saw=id=="dissection-saw";
                        visual.transform.localPosition=-(rotation*(Vector3.forward*(saw?.107f:.085f)));
                        camera.orthographicSize=saw?.032f:.030f;
                        Save(saw?"saw-teeth":"scalpel-bevel");
                    }
                    if(id=="oxygen")
                    {
                        rotation=Quaternion.Euler(-16,139,-7);
                        visual.transform.SetLocalPositionAndRotation(-(rotation*(Vector3.forward*InstrumentGeometry.OxygenSealZ)),rotation);
                        camera.orthographicSize=.073f;Save("oxygen-seal");
                    }
                    visual.SetActive(false);
                }
                report.renders=paths.ToArray();
            }
            finally
            {
                if(original.IsValid())SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);
            }
            File.WriteAllText(Path.Combine(root,"qa","instrument-visual-verification.json"),JsonUtility.ToJson(report,true));
            if(report.status!="passed")throw new Exception("Instrument geometry inspection failed; see qa/instrument-visual-verification.json.");
            Debug.Log("Instrument inspection passed for "+report.instruments.Count+" instruments and the radial band. Maximum "+report.instruments.Max(i=>i.triangles)+" triangles.");
        }

        static Bounds WholeBounds(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<MeshRenderer>();
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static Entry Inspect(GameObject go,string id,HashSet<Material> shared)
        {
            bool saw=id=="dissection-saw";
            bool cutting=saw||id=="scalpel"||id=="dissection-scalpel";
            var wanted=Vector3.forward*(saw?InstrumentGeometry.SawTipZ:InstrumentGeometry.ScalpelTipZ);
            var entry=new Entry{id=id,renderers=go.GetComponentsInChildren<Renderer>().Length,colliders=go.GetComponentsInChildren<Collider>().Length,size=WholeBounds(go).size,
                finite=true,metricUVs=true,tangentFrames=true,authoredFinishes=true,tipAligned=!cutting};
            foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                var vertices=mesh.vertices;var uv=mesh.uv;var indices=mesh.triangles;var tangents=mesh.tangents;
                entry.vertices+=vertices.Length;entry.triangles+=indices.Length/3;entry.materialPasses+=mesh.subMeshCount;
                foreach(var vertex in vertices)
                {
                    float length=vertex.sqrMagnitude;
                    if(float.IsNaN(length)||float.IsInfinity(length))entry.finite=false;
                    var local=go.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if(cutting&&(local-wanted).sqrMagnitude<1e-10f)entry.tipAligned=true;
                }
                entry.tangentFrames&=tangents.Length==vertices.Length&&tangents.All(t=>
                    Mathf.Abs(new Vector3(t.x,t.y,t.z).sqrMagnitude-1)<.02f&&Mathf.Abs(Mathf.Abs(t.w)-1)<.01f);
                if(uv.Length!=vertices.Length)entry.metricUVs=false;
                else for(int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];var du=uv[b]-uv[a];var dv=uv[c]-uv[a];
                    float area=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).magnitude;
                    float projected=Mathf.Abs(du.x*dv.y-du.y*dv.x);
                    if(!(projected>=area*.55f&&projected<=area*1.02f&&projected>0))entry.metricUVs=false;
                }
                var finishes=filter.GetComponent<MeshRenderer>().sharedMaterials;
                entry.authoredFinishes&=finishes.Length==mesh.subMeshCount;
                foreach(var finish in finishes)
                {
                    shared.Add(finish);
                    entry.authoredFinishes&=finish&&AssetDatabase.Contains(finish)&&finish.shader.name=="Universal Render Pipeline/Lit"&&
                        (finish.renderQueue==(int)RenderQueue.Transparent?finish.GetFloat("_ZWrite")==0:
                        finish.GetTexture("_BaseMap")&&finish.GetTexture("_BumpMap")&&finish.IsKeywordEnabled("_NORMALMAP"));
                }
            }
            if(saw)
            {
                var blade=go.transform.Find(InstrumentGeometry.SawBladeName);
                entry.tipAligned&=blade&&blade.localRotation==Quaternion.identity&&blade.localPosition.z>0&&blade.localPosition.z<InstrumentGeometry.SawTipZ;
            }
            return entry;
        }

        static MaskEntry InspectMask(GameObject go)
        {
            var mesh=go.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
            var materials=go.GetComponent<MeshRenderer>().sharedMaterials;
            Vector3[] Surface(int finish)
            {
                int sub=Array.IndexOf(materials,InstrumentGeometry.FinishMaterial(finish));
                return sub<0?Array.Empty<Vector3>():mesh.GetTriangles(sub).Distinct().Select(i=>vertices[i]).ToArray();
            }
            float target=InstrumentGeometry.OxygenSealZ;
            var rim=Surface(InstrumentGeometry.Teal).Where(v=>Mathf.Abs(v.z-target)<.004f).ToArray();
            var rubber=Surface(InstrumentGeometry.Ink);
            var result=new MaskEntry();
            if(rim.Length==0||rubber.Length==0)return result;
            result.sealPlaneZ=(rim.Min(v=>v.z)+rim.Max(v=>v.z))*.5f;
            result.strapRearZ=rubber.Max(v=>v.z);
            result.sealFacesForward=Mathf.Abs(result.sealPlaneZ-target)<.0001f&&
                rim.Min(v=>v.x)<-.044f&&rim.Max(v=>v.x)>.044f&&rim.Min(v=>v.y)<-.058f&&rim.Max(v=>v.y)>.058f&&
                Surface(InstrumentGeometry.Clear).Any(v=>v.z<target-.050f)&&
                Surface(InstrumentGeometry.Satin).All(v=>v.y>.039f&&v.z<target-.020f);
            result.strapBehind=result.strapRearZ>target+.13f&&rubber.Any(v=>v.x>.075f&&v.z>target)&&rubber.Any(v=>v.x<-.075f&&v.z>target);
            // Test the aperture at its center and four surrounding points. An end cap or strap across it blocks a ray.
            var samples=new[]{Vector2.zero,Vector2.right*.022f,Vector2.left*.022f,Vector2.up*.026f,Vector2.down*.026f};
            var faces=mesh.triangles;result.openingClear=true;
            for(int i=0;i<faces.Length;i+=3)
            {
                var a=vertices[faces[i]];var b=vertices[faces[i+1]];var c=vertices[faces[i+2]];
                if(Mathf.Max(a.z,Mathf.Max(b.z,c.z))<target-.006f||Mathf.Min(a.z,Mathf.Min(b.z,c.z))>target+.006f)continue;
                var ab=b-a;var ac=c-a;float area=ab.x*ac.y-ab.y*ac.x;
                if(Mathf.Abs(area)<1e-12f)continue;
                foreach(var sample in samples)
                {
                    var p=sample-(Vector2)a;float u=(p.x*ac.y-p.y*ac.x)/area,v=(ab.x*p.y-ab.y*p.x)/area;
                    if(u>=0&&v>=0&&u+v<=1&&Mathf.Abs(a.z+u*ab.z+v*ac.z-target)<.006f)result.openingClear=false;
                }
            }
            return result;
        }
        static void Render(Camera camera,string path)
        {
            var previous=RenderTexture.active;var texture=new RenderTexture(1000,1000,24){antiAliasing=4};var image=new Texture2D(1000,1000,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1000,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
#endif
