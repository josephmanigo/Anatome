using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ThoraxLab.Editor
{
    /// <summary>
    /// Builds separate, deterministic appearance assets. Texture motifs suggest tissue
    /// character at close range; they are not histology, segmented vessels, or landmarks.
    /// Original source geometry, source maps and production materials remain untouched.
    /// </summary>
    public static class VRSpecimenAssets
    {
        const string Root = "Assets/ThoraxLab/Resources/Learning/Specimen";
        const string Revision = "vr-tissue-appearance-2026.09.06.3";
        const int Size = 512;
        readonly struct Tissue
        {
            public readonly string id;
            public readonly Color color;
            public readonly float smoothness, relief;
            public Tissue(string id, Color color, float smoothness, float relief)
            { this.id = id; this.color = color; this.smoothness = smoothness; this.relief = relief; }
        }
        static readonly Tissue[] Tissues =
        {
            new Tissue("skin", new Color(.56f,.37f,.29f), .16f, .035f),
            new Tissue("fat", new Color(.80f,.68f,.43f), .23f, .20f),
            new Tissue("fascia", new Color(.80f,.78f,.69f), .37f, .17f),
            new Tissue("muscle", new Color(.43f,.125f,.105f), .34f, .35f),
            new Tissue("cardiac", new Color(.42f,.12f,.11f), .40f, .25f),
            new Tissue("lung", new Color(.63f,.34f,.32f), .32f, .30f),
            new Tissue("bone", new Color(.83f,.76f,.60f), .20f, .09f),
            new Tissue("cartilage", new Color(.74f,.77f,.69f), .36f, .08f),
            new Tissue("membrane", new Color(.73f,.69f,.61f), .42f, .08f),
            new Tissue("artery", new Color(.59f,.13f,.105f), .36f, .11f),
            new Tissue("vein", new Color(.19f,.30f,.45f), .34f, .10f),
            new Tissue("nerve", new Color(.86f,.73f,.38f), .23f, .11f),
            new Tissue("airway", new Color(.75f,.58f,.43f), .28f, .09f),
            new Tissue("duct", new Color(.66f,.67f,.38f), .31f, .08f)
        };

        [MenuItem("Anatome/Prepare VR tissue appearance")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);
            string stamp = Root + "/appearance-version.txt";
            bool rebuild = !File.Exists(stamp) || File.ReadAllText(stamp) != Revision;
            foreach (var tissue in Tissues)
            {
                if (rebuild || !File.Exists(Root + "/Tissue_" + tissue.id + ".mat"))
                {
                    Create(tissue, false);
                    if (tissue.id != "bone") Create(tissue, true);
                }
            }
            File.WriteAllText(stamp, Revision);
            File.WriteAllText(Root + "/appearance-provenance.json", JsonUtility.ToJson(new Provenance(), true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            ValidateSourceMeshChannels();
        }

        [Serializable] sealed class MeshCheck
        {
            public string id, tissue, sourceKind;
            public int vertices, triangles, uvVertices, tangentVertices, degenerateUvTriangles;
            public bool validUv, hasTangents;
        }
        [Serializable] sealed class AssetCheck
        {
            public string generatedUtc = DateTime.UtcNow.ToString("o"), appearanceVersion = Revision;
            public string scope = "Imported source-mesh UV/tangent channels and separate presentation assets. Not anatomical or headset acceptance.";
            public int materialCount, albedoMapCount, normalMapCount;
            public bool sourceChannelsPassed;
            public MeshCheck[] sourceMeshes;
        }

        static void ValidateSourceMeshChannels()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThoraxLab/Models/Thorax.fbx");
            var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ThoraxLab/anatomy-catalog.json");
            var catalog = JsonUtility.FromJson<AnatomyCatalog>(catalogAsset.text);
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            var checks = new List<MeshCheck>();
            foreach (var record in catalog.records.Where(r => !r.reference_region))
            {
                var filter = filters.FirstOrDefault(f => f.name == record.object_name);
                if (!filter) throw new InvalidDataException("Presentation source mesh missing: " + record.id);
                var mesh = filter.sharedMesh; var uv = mesh.uv; var indices = mesh.triangles;
                var check = new MeshCheck { id = record.id, tissue = record.tissue, sourceKind = record.source_kind,
                    vertices = mesh.vertexCount, triangles = indices.Length / 3, uvVertices = uv.Length, tangentVertices = mesh.tangents.Length,
                    validUv = uv.Length == mesh.vertexCount && uv.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y)),
                    hasTangents = mesh.tangents.Length == mesh.vertexCount };
                if (check.validUv) for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector2 a = uv[indices[i + 1]] - uv[indices[i]], b = uv[indices[i + 2]] - uv[indices[i]];
                    if (Mathf.Abs(a.x * b.y - a.y * b.x) < 1e-12f) check.degenerateUvTriangles++;
                }
                checks.Add(check);
            }
            var report = new AssetCheck { materialCount = Directory.GetFiles(Root, "*.mat").Length,
                albedoMapCount = Directory.GetFiles(Root, "*_Albedo.png").Length, normalMapCount = Directory.GetFiles(Root, "*_Normal.png").Length,
                sourceMeshes = checks.ToArray(), sourceChannelsPassed = checks.All(c => c.validUv && c.hasTangents && c.degenerateUvTriangles < c.triangles) };
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            Directory.CreateDirectory(Path.Combine(repository, "qa"));
            File.WriteAllText(Path.Combine(repository, "qa/vr-specimen-assets.json"), JsonUtility.ToJson(report, true));
            if (!report.sourceChannelsPassed) throw new InvalidDataException("The source mesh UV/tangent audit failed; see qa/vr-specimen-assets.json.");
        }

        [Serializable] sealed class Provenance
        {
            public string version = Revision;
            public string role = "Derived appearance assets for the original Anatome specimen";
            public string anatomy = "No mesh, tissue boundary, collider or anatomical landmark is changed. BodyParts3D and Medical Augmented Learning source attribution remains in anatomy-catalog.json and source manifests.";
            public string method = "Original deterministic procedural albedo and height-derived normal maps. Pores, fine striation and lobular texture are illustrative surface appearance, not segmented histology or patient measurements.";
            public string reconstruction = "FMA7163 skin has an authored wall; REC_fat and REC_fascia remain authored envelope-based teaching layers. Their measured thickness and exact microstructure are not claimed.";
            public string bone = "The existing bone-cap shader and distance-derived cortical/cancellous teaching contrast are retained.";
            public string preservation = "Materials and PNGs are stored separately under Resources/Learning/Specimen. Original source maps, meshes, LODs and geometry provenance remain unchanged.";
            public int textureSize = Size;
        }

        static void Create(Tissue tissue, bool interior)
        {
            string name = (interior ? "Interior_" : "Tissue_") + tissue.id;
            var height = new float[Size * Size]; var albedo = new Color[height.Length];
            Color baseColor = tissue.color;
            if (interior)
            {
                if (tissue.id == "skin") baseColor = new Color(.73f,.43f,.34f);
                else if (tissue.id == "fat") baseColor = new Color(.86f,.73f,.45f);
                else if (tissue.id == "lung") baseColor = new Color(.61f,.28f,.26f);
                else if (tissue.id == "muscle" || tissue.id == "cardiac") baseColor *= .86f;
                else baseColor = Color.Lerp(baseColor, new Color(.67f,.36f,.29f), .16f);
                baseColor.a = 1;
            }
            int seed = 31; foreach (char c in tissue.id) seed = seed * 31 + c;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float broad = Noise(u, v, 6, seed), fine = Noise(u, v, 91, seed + 7);
                float pattern = fine, variation = (broad - .5f) * .065f + (fine - .5f) * .026f;
                if (tissue.id == "muscle" || tissue.id == "cardiac")
                {
                    // An appearance cue only: source UVs do not establish fibre orientation.
                    float strand = .5f + .5f * Mathf.Sin((u * 88 + .35f * Mathf.Sin(v * Mathf.PI * 8)) * Mathf.PI * 2);
                    float bundles = .5f + .5f * Mathf.Sin(u * Mathf.PI * 2 * 13);
                    pattern = strand * .52f + bundles * .20f + fine * .28f;
                    variation += (pattern - .5f) * .025f;
                }
                else if (tissue.id == "fat")
                {
                    float cell = CellDistance(u, v, 19, seed);
                    pattern = Mathf.SmoothStep(1, 0, Mathf.Clamp01(cell));
                    variation += (pattern - .5f) * .12f;
                }
                else if (tissue.id == "skin")
                {
                    float pore = Mathf.Pow(Mathf.Clamp01(1 - CellDistance(u, v, 58, seed) * 4), 2);
                    pattern = .5f - pore * .5f + (fine - .5f) * .25f;
                    // The source's packed UV islands need nearly uniform skin albedo.
                    // Visible tangent-space pores produced triangular island artifacts
                    // in the actual XR render; exterior skin deliberately has no bump.
                    variation = (broad - .5f) * .010f + (fine - .5f) * .003f - pore * .0015f;
                }
                else if (tissue.id == "lung")
                {
                    float lobule = CellDistance(u, v, 31, seed);
                    pattern = .5f + (fine - .5f) * .5f + (lobule - .5f) * .3f;
                    variation += (lobule - .5f) * .11f;
                }
                else if (tissue.id == "fascia")
                {
                    pattern = .5f + .20f * Mathf.Sin(u * Mathf.PI * 96) * Mathf.Sin(v * Mathf.PI * 68);
                    variation += (pattern - .5f) * .06f;
                }
                height[y * Size + x] = pattern;
                albedo[y * Size + x] = new Color(Mathf.Clamp01(baseColor.r + variation), Mathf.Clamp01(baseColor.g + variation * .72f), Mathf.Clamp01(baseColor.b + variation * .60f), 1);
            }
            var normals = new Color[height.Length];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                var normal = new Vector3(-dx * tissue.relief * 2.5f, -dy * tissue.relief * 2.5f, 1).normalized;
                normals[y * Size + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
            }
            string colorPath = Root + "/" + name + "_Albedo.png", normalPath = Root + "/" + name + "_Normal.png";
            WriteTexture(colorPath, albedo, false); WriteTexture(normalPath, normals, true);
            string path = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.name = name;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", tissue.id == "muscle" || tissue.id == "cardiac" ? .2f : 1);
            material.SetFloat("_Smoothness", interior ? Mathf.Min(.43f, tissue.smoothness + .04f) : tissue.smoothness);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Cull", 2);
            material.SetFloat("_Surface", 0); material.SetFloat("_ZWrite", 1);
            if(tissue.id == "skin" && !interior) material.DisableKeyword("_NORMALMAP");
            else material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black); material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        static void WriteTexture(string path, Color[] pixels, bool normal)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, normal);
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.mipmapEnabled = true; importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2; importer.isReadable = false; importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = Size, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 70 });
            importer.SaveAndReimport();
        }

        static float Noise(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = Mathf.SmoothStep(0, 1, x - ix), fy = Mathf.SmoothStep(0, 1, y - iy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix % cells, iy % cells, seed), Hash((ix + 1) % cells, iy % cells, seed), fx),
                Mathf.Lerp(Hash(ix % cells, (iy + 1) % cells, seed), Hash((ix + 1) % cells, (iy + 1) % cells, seed), fx), fy);
        }

        static float CellDistance(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells; int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float best = 4;
            for (int yy = -1; yy <= 1; yy++) for (int xx = -1; xx <= 1; xx++)
            {
                int px = ix + xx, py = iy + yy, hx = (px + cells) % cells, hy = (py + cells) % cells;
                float dx = px + Hash(hx, hy, seed) - x, dy = py + Hash(hx, hy, seed + 117) - y;
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
            return Mathf.Sqrt(best);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }
    }
}
