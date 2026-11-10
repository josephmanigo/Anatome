using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Editor
{
    /// <summary>
    /// Adapts the official Unity HandVisualizer avatar meshes into visual-only gloves.
    /// Source FBX bytes remain unchanged. No XR Hands subsystem or sample interaction
    /// scripts are installed: the existing controller pose drives these avatar visuals.
    /// </summary>
    public static class GlovedHandAssets
    {
        const string Root = "Assets/ThoraxLab/Resources/Learning/Hands";
        const string Revision = "gloved-avatar-2026.09.06.2";
        const string SourceArchive = "https://download.packages.unity.com/com.unity.xr.hands/-/com.unity.xr.hands-1.6.1.tgz";
        const string LeftHash = "828996c1e01f710222a90caa3451a2099ec82f0651242a9b4847091a508ec521";
        const string RightHash = "215bd9c9782a74c95b47e08e802bd2e37b9bd0e2205428543dbdef9da7990db3";

        [MenuItem("Anatome/Prepare first-person gloved hands")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);
            foreach (string side in new[] { "Left", "Right" })
            {
                string path = Root + "/Source/" + side + "Hand.fbx";
                if (!File.Exists(path)) throw new FileNotFoundException("Official avatar source is missing", path);
                string expected = side == "Left" ? LeftHash : RightHash;
                if (Hash(path) != expected) throw new InvalidOperationException("Hand source hash changed: " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("No hand model importer: " + path);
                if (importer.importAnimation || importer.importCameras || importer.importLights ||
                    importer.addCollider || importer.optimizeGameObjects || !importer.isReadable ||
                    importer.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    importer.importAnimation = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.addCollider = false;
                    importer.optimizeGameObjects = false;
                    importer.isReadable = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.SaveAndReimport();
                }
            }
            string stamp = Root + "/glove-version.txt";
            if (File.Exists(stamp) && File.ReadAllText(stamp) == Revision &&
                File.Exists(Root + "/GloveLeft.prefab") && File.Exists(Root + "/GloveRight.prefab") &&
                File.Exists(Root + "/GloveNitrile.mat") && File.Exists(Root + "/glove-provenance.json")) return;

            var material = CreateMaterial();
            var report = new Provenance { hands = new[] { CreateHand("Left", material), CreateHand("Right", material) } };
            File.WriteAllText(Root + "/glove-provenance.json", JsonUtility.ToJson(report, true));
            File.WriteAllText(stamp, Revision);
            AssetDatabase.ImportAsset(Root + "/glove-provenance.json");
            AssetDatabase.ImportAsset(stamp);
            AssetDatabase.SaveAssets();
            Debug.Log("GLOVED_HAND_ASSETS_READY " + JsonUtility.ToJson(report));
        }

        static Material CreateMaterial()
        {
            const int size = 128;
            string normalPath = Root + "/GloveMicrograin.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color[size * size];
            // Restrained, repeatable equipment micrograin, not invented skin detail.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (Noise(x + 1, y) - Noise(x - 1, y)) * .018f;
                    float dy = (Noise(x, y + 1) - Noise(x, y - 1)) * .018f;
                    Vector3 n = new Vector3(dx, dy, 1).normalized;
                    pixels[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(normalPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "Android", overridden = true, maxTextureSize = size, format = TextureImporterFormat.ASTC_6x6 });
            importer.SaveAndReimport();

            string materialPath = Root + "/GloveNitrile.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.name = "Pale teal nitrile glove";
            material.SetColor("_BaseColor", new Color(.43f, .72f, .74f, 1));
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .28f);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_Cull", 2);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetTextureScale("_BumpMap", new Vector2(10, 10));
            material.SetFloat("_BumpScale", .3f);
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        static HandReport CreateHand(string side, Material material)
        {
            string sourcePath = Root + "/Source/" + side + "Hand.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!source) throw new InvalidOperationException("Imported hand source is missing: " + sourcePath);
            var root = new GameObject("Glove" + side);
            try
            {
                var model = UnityEngine.Object.Instantiate(source, root.transform);
                model.name = "Unity avatar / " + side.ToLowerInvariant() + " glove";
                foreach (var component in model.GetComponentsInChildren<Component>(true))
                    if (!(component is Transform) && !(component is SkinnedMeshRenderer) &&
                        !(component is MeshFilter) && !(component is MeshRenderer))
                        UnityEngine.Object.DestroyImmediate(component);
                string prefix = side == "Left" ? "L_" : "R_";
                var transforms = model.GetComponentsInChildren<Transform>(true);
                Transform Bone(string name) => transforms.Single(t => t.name == prefix + name);
                Transform wrist = Bone("Wrist"), palm = Bone("Palm");
                Vector3 forward = (Bone("MiddleProximal").position - wrist.position).normalized;
                Vector3 towardIndex = (Bone("IndexProximal").position - Bone("LittleProximal").position).normalized;
                Vector3 right = side == "Right" ? -towardIndex : towardIndex;
                Vector3 up = Vector3.Cross(forward, right).normalized;
                Quaternion correction = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
                model.transform.rotation = correction * model.transform.rotation;
                model.transform.position -= palm.position;
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length != 1) throw new InvalidOperationException("Expected one rigged hand renderer: " + side);
                var skin = skins[0];
                skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
                skin.shadowCastingMode = ShadowCastingMode.On;
                skin.receiveShadows = true;
                skin.quality = SkinQuality.Bone4;
                skin.updateWhenOffscreen = false;
                skin.enabled = true;
                if (skin.bones.Length < 20) throw new InvalidOperationException("Incomplete hand skeleton: " + side);

                var baked = new Mesh();
                skin.BakeMesh(baked);
                Vector3[] vertices = baked.vertices.Select(v => root.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 v in vertices) bounds.Encapsulate(v);
                UnityEngine.Object.DestroyImmediate(baked);
                if (bounds.size.magnitude < .12f || bounds.size.magnitude > .4f)
                    throw new InvalidOperationException("Glove import is not at hand scale: " + side + " " + bounds.size);
                CreateCuff(root.transform, vertices, bounds, material, side);
                if (root.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Glove prefab must remain visual only");
                PrefabUtility.SaveAsPrefabAsset(root, Root + "/Glove" + side + ".prefab");
                return new HandReport {
                    side = side, source = sourcePath, sha256 = Hash(sourcePath),
                    meshVertices = skin.sharedMesh.vertexCount, meshTriangles = skin.sharedMesh.triangles.Length / 3,
                    skinBones = skin.bones.Length, worldSize = bounds.size, wrist = wrist.position, palm = palm.position,
                    index = Bone("IndexProximal").position, middle = Bone("MiddleProximal").position,
                    thumb = Bone("ThumbProximal").position,
                    bones = transforms.Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal))
                        .Select(t => new BoneReport { name = t.name, parent = t.parent ? t.parent.name : "", position = t.position,
                            localRotation = t.localRotation, localForward = t.forward }).ToArray()
                };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CreateCuff(Transform parent, Vector3[] sourceVertices, Bounds bounds, Material material, string side)
        {
            // An original small rolled glove edge follows the source wrist opening.
            // It is avatar equipment, and has no collision or clinical meaning.
            float z = bounds.min.z + .008f;
            Vector3[] near = sourceVertices.Where(v => Mathf.Abs(v.z - z) < .009f).ToArray();
            if (near.Length < 4) return;
            float minX = near.Min(v => v.x), maxX = near.Max(v => v.x);
            float minY = near.Min(v => v.y), maxY = near.Max(v => v.y);
            var center = new Vector3((minX + maxX) * .5f, (minY + maxY) * .5f, z);
            float rx = (maxX - minX) * .5f + .0006f, ry = (maxY - minY) * .5f + .0006f;
            const int segments = 40, rings = 8;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s <= segments; s++)
                {
                    float a = s * Mathf.PI * 2 / segments, b = r * Mathf.PI * 2 / rings;
                    float lip = Mathf.Cos(b) * .00085f;
                    vertices.Add(center + new Vector3(Mathf.Cos(a) * (rx + lip), Mathf.Sin(a) * (ry + lip), Mathf.Sin(b) * .00085f));
                    normals.Add(new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(b)).normalized);
                    uv.Add(new Vector2(s / (float)segments, r / (float)rings));
                    if (r < rings && s < segments)
                    {
                        int i = r * (segments + 1) + s, j = i + segments + 1;
                        triangles.AddRange(new[] { i, i + 1, j, i + 1, j + 1, j });
                    }
                }
            var mesh = new Mesh { name = side + " glove cuff / original equipment geometry" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string path = Root + "/Glove" + side + "Cuff.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            var cuff = new GameObject("Rolled nitrile cuff edge");
            cuff.transform.SetParent(parent, false);
            cuff.AddComponent<MeshFilter>().sharedMesh = mesh;
            cuff.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static float Noise(int x, int y)
        {
            unchecked { uint h = (uint)((x & 127) * 374761393 + (y & 127) * 668265263); h = (h ^ (h >> 13)) * 1274126177; return (h & 65535) / 65535f; }
        }
        static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        [Serializable] sealed class Provenance
        {
            public string revision = Revision, package = "com.unity.xr.hands@1.6.1", sourceArchive = SourceArchive;
            public string archiveSha1 = "ff73e7abcedb00c91c9323d447e83f233d142178";
            public string license = "Unity Package Distribution License; source notice retained beside FBX files";
            public string adaptation = "Original FBX bytes retained; canonical palm origin / forward +Z / dorsum +Y, teal nitrile material and original rolled cuff edge. Visual equipment only; no new tracking, interaction or anatomy specimen components.";
            public HandReport[] hands;
        }
        [Serializable] sealed class HandReport
        {
            public string side, source, sha256;
            public int meshVertices, meshTriangles, skinBones;
            public Vector3 worldSize, wrist, palm, index, middle, thumb;
            public BoneReport[] bones;
        }
        [Serializable] sealed class BoneReport { public string name, parent; public Vector3 position, localForward; public Quaternion localRotation; }
    }
}
