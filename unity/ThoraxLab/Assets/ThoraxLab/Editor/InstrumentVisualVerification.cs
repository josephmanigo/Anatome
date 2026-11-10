#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThoraxLab.Learning;
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
            public bool finite;
            public Vector3 size;
        }
        [Serializable] sealed class Report
        {
            public string utc,status;
            public int sharedFinishes=6;
            public List<Entry> instruments=new List<Entry>();
            public string[] renders;
        }
        public static void Capture()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
            string output=Path.Combine(root,".impeccable","instrument-review");Directory.CreateDirectory(output);
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),status="passed"};
            var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var priorAmbient=RenderSettings.ambientLight;var priorMode=RenderSettings.ambientMode;var priorFog=RenderSettings.fog;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.34f,.39f,.42f);RenderSettings.fog=false;
            try
            {
                var holder=new GameObject("Instrument inspection / temporary");
                SceneManager.MoveGameObjectToScene(holder,scene);
                foreach(string id in InstrumentGeometry.SupportedIds)
                {
                    var go=InstrumentGeometry.Create(holder.transform,id);var mesh=go.GetComponent<MeshFilter>().sharedMesh;
                    var entry=new Entry{id=id,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,renderers=go.GetComponentsInChildren<Renderer>().Length,colliders=go.GetComponentsInChildren<Collider>().Length,size=mesh.bounds.size,finite=true};
                    for(int sub=0;sub<mesh.subMeshCount;sub++)if(mesh.GetIndexCount(sub)>0)entry.materialPasses++;
                    foreach(var v in mesh.vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))entry.finite=false;
                    if(!entry.finite||entry.colliders!=0||entry.renderers!=1||entry.materialPasses>6||entry.triangles>12000)report.status="failed";
                    report.instruments.Add(entry);go.SetActive(false);
                }
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
                string[] selected={"scalpel","dissection-saw","suture","anaesthetic","inhaler","spacer","ultrasound","valve"};
                var paths=new List<string>();
                foreach(string id in selected)
                {
                    var visual=holder.transform.Cast<Transform>().First(t=>t.name=="Crafted "+id+" / visual only").gameObject;
                    visual.layer=31;visual.SetActive(true);visual.transform.localRotation=id=="inhaler"?Quaternion.Euler(-50,150,-25):Quaternion.Euler(65,0,145);
                    var localCenter=visual.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                    visual.transform.localPosition=-(visual.transform.localRotation*localCenter);
                    string path=Path.Combine(output,id+".png");Render(camera,path);paths.Add(path);visual.SetActive(false);
                }
                report.renders=paths.ToArray();
            }
            finally
            {
                RenderSettings.ambientLight=priorAmbient;RenderSettings.ambientMode=priorMode;RenderSettings.fog=priorFog;
                EditorSceneManager.CloseScene(scene,true);if(original.IsValid())SceneManager.SetActiveScene(original);
            }
            File.WriteAllText(Path.Combine(root,"qa","instrument-visual-verification.json"),JsonUtility.ToJson(report,true));
            if(report.status!="passed")throw new Exception("Instrument geometry inspection failed; see qa/instrument-visual-verification.json.");
            Debug.Log("Instrument inspection passed: "+report.instruments.Count+" meshes, maximum "+report.instruments.Max(i=>i.triangles)+" triangles, one renderer per tool.");
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
