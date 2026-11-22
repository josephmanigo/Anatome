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
        const string OrganRevision = "vr-tissue-appearance-2026.11.21.3";
        const string FirstWallRevision = "vr-tissue-appearance-2026.11.21.4";
        const string Revision = "vr-tissue-appearance-2026.11.21.5";
        const int Size = 512;
        readonly struct Tissue
        {
            public readonly string id;
            public readonly Color color;
            public readonly float smoothness, relief, outerMm, innerMm, cutSmoothness, cutRelief;
            public bool Superficial => outerMm > 0;
            public bool OrganSurface => id is "lung" or "cardiac" or "valve" or "artery" or "vein" or "esophagus" or "membrane" or "airway";
            public Tissue(string id, Color color, float smoothness, float relief,
                float outerMm = 0, float innerMm = 0, float cutSmoothness = 0, float cutRelief = 0)
            {
                this.id = id; this.color = color; this.smoothness = smoothness; this.relief = relief;
                this.outerMm = outerMm; this.innerMm = innerMm; this.cutSmoothness = cutSmoothness; this.cutRelief = cutRelief;
            }
            public float MapScale(bool interior) => Superficial ? 125f / (interior ? innerMm : outerMm) : interior ? 2.5f : 1f;
        }
        static readonly Tissue[] Tissues =
        {
            // Maps in pipeline/build_superficial_tissues.py. Tile periods are millimetres in the specimen frame.
            new Tissue("skin", Color.white, .41f, .013f, 64, 55, .57f, .048f),
            new Tissue("fat", Color.white, .48f, .050f, 83, 70, .54f, .055f),
            new Tissue("fascia", Color.white, .55f, .017f, 91, 74, .51f, .024f),
            new Tissue("muscle", Color.white, .49f, .046f, 103, 80, .55f, .050f),
            new Tissue("cardiac", new Color(.50f,.235f,.20f), .53f, .13f),
            new Tissue("valve", new Color(.79f,.745f,.685f), .54f, .06f),
            new Tissue("esophagus", new Color(.65f,.425f,.36f), .44f, .10f),
            new Tissue("lung", new Color(.72f,.48f,.465f), .61f, .21f),
            new Tissue("bone", new Color(.83f,.76f,.60f), .20f, .09f),
            new Tissue("cartilage", new Color(.74f,.77f,.69f), .36f, .08f),
            new Tissue("membrane", new Color(.82f,.755f,.68f), .62f, .06f),
            new Tissue("artery", new Color(.68f,.415f,.35f), .51f, .11f),
            new Tissue("vein", new Color(.49f,.335f,.37f), .54f, .10f),
            new Tissue("nerve", new Color(.86f,.73f,.38f), .23f, .11f),
            new Tissue("airway", new Color(.77f,.625f,.53f), .44f, .09f),
            new Tissue("duct", new Color(.66f,.67f,.38f), .31f, .08f)
        };

        [MenuItem("Anatome/Prepare VR tissue appearance")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root);
            string stamp = Root + "/appearance-version.txt";
            string previous = File.Exists(stamp) ? File.ReadAllText(stamp) : "";
            foreach (var tissue in Tissues)
            {
                bool rebuild = previous != Revision && (tissue.Superficial || (previous != OrganRevision && previous != FirstWallRevision));
                if (rebuild || !File.Exists(Root + "/Tissue_" + tissue.id + ".mat") ||
                    (tissue.id != "bone" && !File.Exists(Root + "/Interior_" + tissue.id + ".mat")))
                {
                    Create(tissue, false);
                    if (tissue.id != "bone") Create(tissue, true);
                }
            }
            File.WriteAllText(stamp, Revision);
            File.WriteAllText(Root + "/appearance-provenance.json", JsonUtility.ToJson(new Provenance(), true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            ValidateSuperficialMaterials();
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
            public string anatomy = "Anatomical geometry is sourced separately from BodyParts3D and the Mauritanian Anatomy Laboratory thoracic atlas. Specimen materials do not displace anatomy.";
            public string method = "Organ and chest-wall exteriors and cut faces use separate object-space colour, smoothness and relief maps. Chest-wall maps depict a thin-skin surface, cut dermal connective tissue, adipose lobules, a collagen sheet and skeletal muscle fascicles. Pigmentation, strand directions and cut texture are authored gross appearance cues. Source references and limits are in docs/SUPERFICIAL-TISSUE-APPEARANCE.md.";
            public string reconstruction = "FMA7163 skin has an authored wall; REC_fat and REC_fascia remain authored envelope-based teaching layers. Their measured thickness and exact microstructure are not claimed.";
            public string bone = "The existing bone-cap shader and distance-derived cortical/cancellous teaching contrast are retained.";
            public string preservation = "Materials and PNGs are stored separately under Resources/Learning/Specimen. Original source maps, meshes, LODs and geometry provenance remain unchanged.";
            public int textureSize = Size;
        }

        static void Create(Tissue tissue, bool interior)
        {
            string name = (interior ? "Interior_" : "Tissue_") + tissue.id;
            string colorPath = Root + "/" + name + "_Albedo.png", normalPath = Root + "/" + name + "_Normal.png";
            bool projected = tissue.OrganSurface || tissue.Superficial;
            if (tissue.Superficial)
            {
                if (!File.Exists(colorPath)) throw new FileNotFoundException("Run pipeline/build_superficial_tissues.py to author the chest-wall maps.", colorPath);
                AssetDatabase.ImportAsset(colorPath, ImportAssetOptions.ForceSynchronousImport);
            }
            else GenerateMaps(tissue, interior, colorPath, normalPath);
            string path = Root + "/" + name + ".mat";
            var shader = Shader.Find(projected ? "ThoraxLab/Organ Surface" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidDataException("Missing presentation shader for " + name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else if (material.shader != shader)
            {
                if (projected && material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
                material.shader = shader;
            }
            material.name = name;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
            float gloss = tissue.smoothness;
            if (interior) gloss = tissue.Superficial ? tissue.cutSmoothness : tissue.id == "lung" ? .32f : Mathf.Min(.43f, gloss + .04f);
            material.SetFloat("_Smoothness", gloss);
            if (projected)
            {
                material.SetTextureScale("_BaseMap", Vector2.one * tissue.MapScale(interior));
                float relief = tissue.Superficial ? (interior ? tissue.cutRelief : tissue.relief) : tissue.relief * (interior ? .38f : .17f);
                material.SetFloat("_BumpScale", relief);
            }
            else
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.SetFloat("_BumpScale", 1);
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_SmoothnessTextureChannel", 0);
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                material.SetColor("_EmissionColor", Color.black);
            }
            material.SetFloat("_Cull", 2);
            material.SetFloat("_Surface", 0); material.SetFloat("_ZWrite", 1);
            if(projected) material.DisableKeyword("_NORMALMAP");
            else material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_EMISSION");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        static void ValidateSuperficialMaterials()
        {
            foreach (var tissue in Tissues.Where(t => t.Superficial))
            foreach (bool interior in new[] { false, true })
            {
                string path = Root + "/" + (interior ? "Interior_" : "Tissue_") + tissue.id;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
                string png = path + "_Albedo.png";
                var importer = AssetImporter.GetAtPath(png) as TextureImporter;
                if (!material || material.shader.name != "ThoraxLab/Organ Surface" ||
                    material.GetTexture("_BaseMap") != AssetDatabase.LoadAssetAtPath<Texture2D>(png) ||
                    material.GetTextureScale("_BaseMap") != Vector2.one * tissue.MapScale(interior) ||
                    !importer || !importer.DoesSourceTextureHaveAlpha() || !importer.sRGBTexture || !importer.mipmapEnabled ||
                    importer.GetPlatformTextureSettings("Android").format != TextureImporterFormat.ASTC_6x6)
                    throw new InvalidDataException("Invalid chest-wall appearance asset: " + path);
            }
        }

        static void GenerateMaps(Tissue tissue, bool interior, string colorPath, string normalPath)
        {
            var height = new float[Size * Size]; var albedo = new Color[height.Length];
            Color baseColor = tissue.color;
            if (interior)
            {
                if (tissue.id == "lung") baseColor = new Color(.57f,.285f,.28f);
                else if (tissue.id == "valve") baseColor = new Color(.81f,.72f,.61f);
                else if (tissue.id == "cardiac") baseColor *= .86f;
                else baseColor = Color.Lerp(baseColor, new Color(.67f,.36f,.29f), .16f);
                baseColor.a = 1;
            }
            int seed = 31; foreach (char c in tissue.id) seed = seed * 31 + c;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float broad = Noise(u, v, 6, seed), fine = Noise(u, v, 91, seed + 7);
                float pattern = fine, variation = (broad - .5f) * .065f + (fine - .5f) * .026f;
                if (tissue.id == "lung" && interior)
                {
                    // Gross cut-face grain. Its scale does not resolve individual alveoli.
                    float grain = Mathf.Clamp01(1 - CellDistance(u, v, 80, seed) * 2.2f);
                    pattern = .64f - grain * .52f + (fine - .5f) * .16f;
                    variation += (.2f - grain) * .083f;
                }
                else if (tissue.OrganSurface)
                {
                    float middle = Noise(u, v, 17, seed + 37);
                    pattern = .5f + (middle - .5f) * .38f + (fine - .5f) * .18f;
                    float contrast = tissue.id == "lung" ? .115f : tissue.id == "valve" ? .055f : .085f;
                    variation = (broad - .5f) * contrast + (middle - .5f) * .032f + (fine - .5f) * .012f;
                }
                height[y * Size + x] = pattern;
                float gloss = tissue.OrganSurface ? Mathf.Clamp01(.86f + (broad - .5f) * .22f + (pattern - .5f) * .23f) : 1f;
                float warmth = tissue.OrganSurface ? (broad - .5f) * (tissue.id == "lung" ? .048f : .025f) : 0f;
                albedo[y * Size + x] = new Color(Mathf.Clamp01(baseColor.r + variation + warmth), Mathf.Clamp01(baseColor.g + variation * .72f), Mathf.Clamp01(baseColor.b + variation * .60f - warmth), gloss);
            }
            WriteTexture(colorPath, albedo, false);
            if (!tissue.OrganSurface)
            {
                var normals = new Color[height.Length];
                for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                {
                    float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                    float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                    var normal = new Vector3(-dx * tissue.relief * 2.5f, -dy * tissue.relief * 2.5f, 1).normalized;
                    normals[y * Size + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
                }
                WriteTexture(normalPath, normals, true);
            }
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
