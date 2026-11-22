using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThoraxLab.Learning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Editor
{
    public static class TissueVisualVerification
    {
        [Serializable] sealed class View
        {
            public string id, source, surfaceShader, cutShader;
            public int sourceTriangles, exposedTriangles;
        }
        [Serializable] sealed class Report
        {
            public string utc, appearanceVersion;
            public List<View> views = new();
        }

        public static void Capture()
        {
            const string learning = "Learning/Specimen/";
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            string phase = Environment.GetCommandLineArgs().Contains("--tissue-before") ? "before" : "after";
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../qa/skin-tissue", phase));
            Directory.CreateDirectory(folder);
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), appearanceVersion = Resources.Load<TextAsset>(learning + "appearance-version").text };
            var structures = UnityEngine.Object.FindObjectsByType<AnatomyStructure>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var id in new[] { "FMA7163", "REC_fat", "REC_fascia", "FMA79979" })
            {
                var target = structures.Single(s => s.record.id == id);
                var mesh = target.GetComponent<MeshFilter>().sharedMesh;
                var outer = Resources.Load<Material>(learning + "Tissue_" + target.record.tissue);
                var inner = Resources.Load<Material>(learning + "Interior_" + target.record.tissue);
                if (!outer || !inner || outer.mainTexture == inner.mainTexture || !outer.shader.isSupported || !inner.shader.isSupported)
                    throw new InvalidDataException(id + " has an invalid outer or exposed surface.");
                var sourcePose = target.transform;
                Draw(Path.Combine(folder, id + ".png"), mesh, sourcePose, new[] { outer }, new Vector3(.25f, 1f, -.12f));
                if (id == "FMA7163")
                    Draw(Path.Combine(folder, "skin-close.png"), mesh, sourcePose, new[] { outer }, new Vector3(.25f,1f,-.12f), zoom: 4);
                var piece = ExtractPiece(mesh, sourcePose);
                try
                {
                    var faces = piece.GetTriangles(1);
                    var v = piece.vertices;
                    int largest = Enumerable.Range(0, faces.Length / 3).OrderByDescending(i => Vector3.Cross(
                        v[faces[3*i+1]] - v[faces[3*i]], v[faces[3*i+2]] - v[faces[3*i]]).sqrMagnitude).First() * 3;
                    Vector3 a = sourcePose.TransformPoint(v[faces[largest]]), b = sourcePose.TransformPoint(v[faces[largest+1]]), c = sourcePose.TransformPoint(v[faces[largest+2]]);
                    Vector3 face = Vector3.Cross(b-a, c-a).normalized;
                    Draw(Path.Combine(folder, id + "-cut.png"), piece, sourcePose, new[] { outer, inner }, face + .55f * (b-a).normalized);
                    report.views.Add(new View { id=id, source=target.record.source_kind, sourceTriangles=mesh.triangles.Length/3,
                        exposedTriangles=faces.Length/3, surfaceShader=outer.shader.name, cutShader=inner.shader.name });
                }
                finally { UnityEngine.Object.DestroyImmediate(piece); }
            }
            var station = new GameObject("Temporary case insert inspection").AddComponent<CaseAnatomyWorkbench>();
            try
            {
                station.Build("pneumothorax");
                var insert = station.Incision;
                insert.Restore(true);
                var mesh = insert.GetComponent<MeshFilter>().sharedMesh;
                var materials = insert.GetComponent<MeshRenderer>().sharedMaterials;
                Draw(Path.Combine(folder, "case-insert.png"), mesh, insert.transform, materials, new Vector3(.04f, .23f, -1f), true);
                Draw(Path.Combine(folder, "case-detail.png"), mesh, insert.transform, materials, new Vector3(.04f,.23f,-1f), true, 2.8f);
                if (Environment.GetCommandLineArgs().Contains("--inspect-case-shadows"))
                    Draw(Path.Combine(folder, "case-detail-unshadowed.png"), mesh, insert.transform, materials, new Vector3(.04f,.23f,-1f), true, 2.8f, false);
                report.views.Add(new View { id="case-insert", source="Virtual training insert", surfaceShader=materials[0].shader.name,
                    cutShader=materials[1].shader.name, sourceTriangles=mesh.triangles.Length/3, exposedTriangles=(int)mesh.GetIndexCount(1)/3 });
            }
            finally { UnityEngine.Object.DestroyImmediate(station.gameObject); }
            File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report,true));
        }

        static Mesh ExtractPiece(Mesh source, Transform placement)
        {
            var v = source.vertices;
            var ix = source.triangles;
            var data = CutMeshData.Capture(source);
            var candidates = Enumerable.Range(0, ix.Length/3).OrderByDescending(i =>
                Vector3.Cross(v[ix[i*3+1]]-v[ix[i*3]], v[ix[i*3+2]]-v[ix[i*3]]).sqrMagnitude).Take(3);
            foreach (int i in candidates)
            {
                Vector3 a=placement.TransformPoint(v[ix[i*3]]), b=placement.TransformPoint(v[ix[i*3+1]]), c=placement.TransformPoint(v[ix[i*3+2]]);
                Vector3 center=(a+b+c)/3, tangent=(b-a).normalized;
                var orientation=Quaternion.LookRotation(Vector3.Cross(b-a,c-a).normalized,tangent);
                var poses=new List<float>();
                NativeCutting.AddPose(poses,placement,center-tangent*.01f,orientation,.008f,.008f,.018f);
                NativeCutting.AddPose(poses,placement,center+tangent*.01f,orientation,.008f,.008f,.018f);
                var result=NativeCutting.Cut(data,poses.ToArray(),32,true);
                if (result.status == 0 && result.pieces.Count > 1) return result.pieces.OrderBy(p => p.Volume()).First().CreateMesh();
            }
            throw new InvalidDataException("No published fragment on " + source.name);
        }

        static void Draw(string path, Mesh mesh, Transform source, Material[] materials, Vector3 direction, bool insert = false, float zoom = 1, bool shadows = true)
        {
            var preview = new PreviewRenderUtility();
            Texture2D image = null;
            try
            {
                var display = new GameObject("Published specimen surface");
                preview.AddSingleGO(display);
                display.transform.rotation = source.rotation;
                display.transform.localScale = source.lossyScale;
                display.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = display.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                if (insert)
                {
                    var original = source.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = original.shadowCastingMode;
                    renderer.receiveShadows = original.receiveShadows;
                }
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var camera = preview.camera;
                LearningCardArt.ConfigureCamera(camera, renderer.bounds, direction);
                if (insert) camera.transform.LookAt(renderer.bounds.center, Vector3.up);
                camera.orthographicSize /= zoom;
                camera.backgroundColor = new Color(.075f,.09f,.10f);
                preview.ambientColor = new Color(.37f,.38f,.39f);
                LearningCardArt.ConfigureLight(preview.lights[0],camera,new Vector3(-.55f,.72f,-.84f),1.12f,Color.white);
                LearningCardArt.ConfigureLight(preview.lights[1],camera,new Vector3(.64f,-.25f,-.55f),.5f,new Color(.94f,.97f,1));
                if (!shadows) foreach (var light in preview.lights) light.shadows = LightShadows.None;
                preview.BeginStaticPreview(new Rect(0,0,896,560));
                preview.Render(true);
                image=preview.EndStaticPreview();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                if (image) UnityEngine.Object.DestroyImmediate(image);
                preview.Cleanup();
            }
        }
    }
}
