using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThoraxLab.Editor
{
    public static class LaboratoryChairVerification
    {
        const string Root = "Assets/ThoraxLab/";
        const string ScenePath = Root + "Scenes/ThoraxLab.unity";
        const string Model = Root + "Models/LaboratoryChair.fbx";

        [Serializable] sealed class Report
        {
            public string utc;
            public string[] finishes;
            public int triangles, roomRenderers, prefabRenderers, roomMaterials, lightProbes, lightmaps, ceilingLights;
            public Vector3 boundsCenter, boundsSize;
            public float seatContactHeight;
            public bool passed;
        }

        public static void Run()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!Application.isBatchMode) throw new InvalidOperationException("Open the ThoraxLab scene in Edit mode.");
                EditorSceneManager.OpenScene(ScenePath);
            }
            if (Environment.GetCommandLineArgs().Contains("--apply-lab-environment"))
            {
                LaboratoryFinishes.UpdateEnvironment();
                EditorSceneManager.OpenScene(ScenePath);
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../qa/lab-chair"));
            Directory.CreateDirectory(output);
            var room = GameObject.Find("Laboratory");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Laboratory.prefab");
            var sceneParts = Inspect(room);
            var prefabParts = Inspect(prefab);
            if (!sceneParts.Select(p => p.GetComponent<MeshFilter>().sharedMesh).SequenceEqual(prefabParts.Select(p => p.GetComponent<MeshFilter>().sharedMesh)))
                throw new InvalidDataException("Chair meshes differ between the lab and its prefab.");
            var bounds = sceneParts[0].bounds;
            foreach (var part in sceneParts) bounds.Encapsulate(part.bounds);
            var sceneGeometry = room.GetComponentsInChildren<MeshRenderer>().Where(r => !r.GetComponentInParent<ThoraxLab.Learning.OperatingRoomClock>()).ToArray();
            var prefabGeometry = prefab.GetComponentsInChildren<MeshRenderer>().Where(r => !r.GetComponentInParent<ThoraxLab.Learning.OperatingRoomClock>()).ToArray();
            var report = new Report {
                utc = DateTime.UtcNow.ToString("O"), finishes = sceneParts.Select(p => p.sharedMaterial.name).ToArray(),
                triangles = sceneParts.Sum(p => (int)p.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3),
                roomRenderers = sceneGeometry.Length,
                prefabRenderers = prefabGeometry.Length,
                roomMaterials = room.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial).Distinct().Count(),
                lightProbes = LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0,
                lightmaps = LightmapSettings.lightmaps.Count(m => m.lightmapColor),
                ceilingLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.name.StartsWith("Ceiling illumination") && l.lightmapBakeType == LightmapBakeType.Baked),
                boundsCenter = bounds.center, boundsSize = bounds.size
            };
            Physics.SyncTransforms();
            var seat = sceneParts.Single(p => p.sharedMaterial.name == "Guard").GetComponent<MeshCollider>();
            if (seat.Raycast(new Ray(new Vector3(-4.50f, .83f, -.80f), Vector3.down), out var hit, .7f))
                report.seatContactHeight = hit.point.y;
            var roomModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/Laboratory.fbx");
            var expected = roomModel.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh)
                .Concat(sceneParts.Select(r => r.GetComponent<MeshFilter>().sharedMesh)).ToHashSet();
            bool roomMatchesExport = expected.SetEquals(sceneGeometry.Select(r => r.GetComponent<MeshFilter>().sharedMesh))
                && expected.SetEquals(prefabGeometry.Select(r => r.GetComponent<MeshFilter>().sharedMesh));
            report.passed = roomMatchesExport && report.roomRenderers == expected.Count && report.prefabRenderers == expected.Count
                && report.triangles > 0 && report.triangles < 10000 && report.seatContactHeight > .4f && report.seatContactHeight < .75f
                && Mathf.Abs(bounds.min.y) < .025f && bounds.max.y > .8f && bounds.max.y < 1.3f
                && report.lightProbes > 0 && report.lightmaps > 0 && report.ceilingLights == 12;
            File.WriteAllText(Path.Combine(output, "unity.json"), JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("Lab chair validation failed. See qa/lab-chair/unity.json.");
            Draw(output, "unity-chair", new Vector3(-3.61f, 1.28f, 1.20f), new Vector3(-4.48f, .51f, -.82f), 33);
            Draw(output, "unity-station", new Vector3(-2.4f, 1.55f, 1.35f), new Vector3(-4.1f, .73f, -.85f), 58);
        }

        static MeshRenderer[] Inspect(GameObject room)
        {
            var parts = room.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Lab_Chair_"))
                .OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
            if (parts.Length != 3) throw new InvalidDataException(room.name + " has " + parts.Length + " chair renderers.");
            foreach (var part in parts)
            {
                var mesh = part.GetComponent<MeshFilter>().sharedMesh;
                var collider = part.GetComponent<MeshCollider>();
                if (!mesh || AssetDatabase.GetAssetPath(mesh) != Model || mesh.subMeshCount != 1 || !collider || !collider.enabled
                    || collider.sharedMesh != mesh || collider.isTrigger || !part.sharedMaterial || !part.sharedMaterial.shader.isSupported
                    || AssetDatabase.GetAssetPath(part.sharedMaterial) != Root + "Materials/" + part.name.Substring("Lab_Chair_".Length) + ".mat"
                    || part.receiveGI != ReceiveGI.LightProbes || GameObjectUtility.AreStaticEditorFlagsSet(part.gameObject, StaticEditorFlags.ContributeGI))
                    throw new InvalidDataException("Invalid chair mesh, collider, finish or lighting on " + part.name);
            }
            return parts;
        }

        static void Draw(string output, string name, Vector3 from, Vector3 at, float field)
        {
            var camera = new GameObject("Lab chair inspection camera").AddComponent<Camera>();
            var target = RenderTexture.GetTemporary(1200, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 2);
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                camera.fieldOfView = field;
                camera.nearClipPlane = .04f;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
