#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using ThoraxLab.Learning;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThoraxLab.Editor
{
    public static class CaseVisualVerification
    {
        [Serializable] sealed class Anchor { public string id; public Vector3 position, forward; public Vector3[] route; }
        [Serializable] sealed class Entry { public string disease; public int triangles, renderers, colliders; public Anchor[] anchors; }
        [Serializable] sealed class Report { public string utc; public int geometryVerticesChecked, geometryTrianglesChecked; public List<Entry> cases = new(); public List<string> errors = new(); }
        public static void Capture()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string pass = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--case-visual-pass="))?.Split('=')[1] ?? "after";
            string output = Path.Combine(root, "qa/case-premium-20261014", pass); Directory.CreateDirectory(output);
            var report = new Report { utc = DateTime.UtcNow.ToString("o") };
            if(pass!="before")VerifySurfaces(report);
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .40f, .44f); RenderSettings.fog = false;
            try
            {
                var camera = new GameObject("Review camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .063f, .077f);
                camera.orthographic = true; camera.orthographicSize = .26f; camera.nearClipPlane = .01f; camera.farClipPlane = 5;
                foreach (var light in new[] { new Vector4(35, -30, 0, 1.2f), new Vector4(15, 140, 0, .65f) })
                {
                    var lamp = new GameObject("Review soft light").AddComponent<Light>(); lamp.type = LightType.Directional;
                    lamp.transform.rotation = Quaternion.Euler(light.x, light.y, light.z); lamp.intensity = light.w;
                }
                foreach (string disease in new[] { "asthma", "pneumonia", "pneumothorax", "pleural-effusion", "coronary-disease", "aortic-stenosis" })
                {
                    var station = new GameObject(disease).AddComponent<CaseAnatomyWorkbench>(); station.Build(disease);
                    var entry = new Entry { disease = disease, triangles = station.VisualTriangleCount, renderers = station.VisualRendererCount, colliders = station.GetComponentsInChildren<Collider>(true).Length };
                    entry.anchors = station.Targets.OrderBy(p => p.Key).Select(p => {
                        var points = new List<Vector3>(); station.TryRoute(p.Key, points);
                        return new Anchor { id = p.Key, position = p.Value.localPosition, forward = p.Value.forward, route = points.ToArray() };
                    }).ToArray(); report.cases.Add(entry);
                    if (entry.triangles > 60000 || entry.renderers > 32) report.errors.Add(disease + ": visual budget exceeded");
                    foreach (var renderer in station.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var size = renderer.bounds.size;
                        if (float.IsNaN(size.sqrMagnitude) || float.IsInfinity(size.sqrMagnitude) || size.magnitude > 2) report.errors.Add(disease + ": invalid bounds");
                        if (!renderer.sharedMaterial || !renderer.sharedMaterial.shader.isSupported) report.errors.Add(disease + ": unsupported material");
                    }
                    camera.orthographicSize = .255f;
                    camera.transform.position = new Vector3(.48f, .30f, -.82f); camera.transform.LookAt(new Vector3(0, -.025f, .02f));
                    Render(camera, Path.Combine(output, disease + ".png"));
                    if (disease == "asthma")
                    {
                        camera.orthographicSize = .18f;
                        foreach (var view in new[] { ("front", new Vector3(.1f, .025f, -.85f)), ("profile", new Vector3(.85f, .05f, -.18f)) })
                        {
                            camera.transform.position = view.Item2; camera.transform.LookAt(new Vector3(.1f, .025f, .035f));
                            Render(camera, Path.Combine(output, "head-" + view.Item1 + ".png"));
                        }
                    }
                    UnityEngine.Object.DestroyImmediate(station.gameObject);
                }
                string baselinePath = Path.Combine(root, "qa/case-premium-20261014/before/report.json");
                if (pass != "before" && File.Exists(baselinePath))
                {
                    var baseline = JsonUtility.FromJson<Report>(File.ReadAllText(baselinePath));
                    foreach (var entry in report.cases)
                    {
                        var before = baseline.cases.Single(c => c.disease == entry.disease);
                        if (entry.colliders != before.colliders || entry.anchors.Length != before.anchors.Length) report.errors.Add(entry.disease + ": interaction inventory changed");
                        for (int i = 0; i < Math.Min(before.anchors.Length, entry.anchors.Length); i++)
                            if (JsonUtility.ToJson(before.anchors[i]) != JsonUtility.ToJson(entry.anchors[i])) report.errors.Add(entry.disease + ": target or route changed: " + entry.anchors[i].id);
                    }
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (original.IsValid()) SceneManager.SetActiveScene(original); }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            if (report.errors.Count > 0) throw new Exception(string.Join("; ", report.errors));
            Debug.Log("Case visual verification passed: " + report.cases.Count + " stations; " + output);
        }
        static void VerifySurfaces(Report report)
        {
            // Inspect authored data before GPU upload discards the CPU copy.
            var type=typeof(CaseAnatomyWorkbench).Assembly.GetType("ThoraxLab.Learning.WorkbenchGeometry");
            object geometry=Activator.CreateInstance(type,true);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                type.GetMethod("AirwayManikin").Invoke(geometry,new object[]{material});
                foreach(int side in new[]{-1,1})type.GetMethod("ManikinEar").Invoke(geometry,new object[]{side,material});
                type.GetMethod("TissueBand").Invoke(geometry,new object[]{.093f,-.0445f,-.0355f,material,.085f});
                var surfaces=(IDictionary)type.GetField("surfaces",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(geometry);
                foreach(object surface in surfaces.Values)
                {
                    var vertices=(List<Vector3>)surface.GetType().GetField("vertices").GetValue(surface);
                    var normals=(List<Vector3>)surface.GetType().GetField("normals").GetValue(surface);
                    var triangles=(List<int>)surface.GetType().GetField("triangles").GetValue(surface);
                    for(int i=0;i<vertices.Count;i++)
                    {
                        if(!float.IsFinite(vertices[i].sqrMagnitude)||!float.IsFinite(normals[i].sqrMagnitude)||normals[i].sqrMagnitude<.95f)
                            throw new Exception("Invalid surface position/normal at vertex "+i+": "+normals[i]);
                    }
                    for(int i=0;i<triangles.Count;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        var face=Vector3.Cross((vertices[b]-vertices[a])*10000,(vertices[c]-vertices[a])*10000);
                        if(face.sqrMagnitude>1e-8f&&Vector3.Dot(face.normalized,normals[a]+normals[b]+normals[c])<-.01f)
                            throw new Exception("Inverted surface winding at triangle "+i/3);
                    }
                    report.geometryVerticesChecked+=vertices.Count;report.geometryTrianglesChecked+=triangles.Count/3;
                }
            }
            finally{((IDisposable)geometry).Dispose();UnityEngine.Object.DestroyImmediate(material);}
        }
        static void Render(Camera camera, string path)
        {
            var previous = RenderTexture.active; var target = new RenderTexture(1200, 1000, 24) { antiAliasing = 4 };
            var image = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
            try { camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { camera.targetTexture = null; RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
#endif
