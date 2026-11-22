using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThoraxLab.Editor
{
    /// <summary>
    /// Offline, source-derived card artwork. Only temporary preview-scene clones are
    /// composed: source geometry, materials, collider geometry and saved scenes are
    /// never edited. Runtime cards load ordinary compressed textures, not cameras.
    /// </summary>
    public static class LearningCardArt
    {
        const string Root = "Assets/ThoraxLab/Resources/Learning/CardArt";
        const string ModelPath = "Assets/ThoraxLab/Models/Thorax.fbx";
        const string CatalogPath = "Assets/ThoraxLab/anatomy-catalog.json";
        const string MaterialRoot = "Assets/ThoraxLab/Resources/Learning/Specimen";
        const string Revision = "source-card-art-2026.09.06.2";
        const int Width = 512, Height = 320;
        static readonly Color Background = new Color(.041f, .065f, .084f, 1);

        sealed class Composition
        {
            public string id, explanation;
            public Func<AnatomyRecord, bool> include, emphasis;
            public Func<AnatomyRecord, Vector3> offset;
            public Vector3 view;
            public Composition(string id, string explanation, Func<AnatomyRecord, bool> include,
                Func<AnatomyRecord, bool> emphasis, Vector3 view, Func<AnatomyRecord, Vector3> offset = null)
            { this.id = id; this.explanation = explanation; this.include = include; this.emphasis = emphasis; this.view = view; this.offset = offset; }
        }

        [Serializable] sealed class SourcePart
        {
            public string id, name, sourceKind, sourceRecord, meshAsset;
            public int sourceTriangles;
            public Vector3 presentationTranslation;
        }
        [Serializable] sealed class CardEntry
        {
            public string id, resourceKey, imageSha256, composition;
            public int width = Width, height = Height;
            public float imageContentFraction;
            public Vector3 viewDirection;
            public SourcePart[] anatomy;
        }
        [Serializable] sealed class Manifest
        {
            public string revision = Revision, generatedUtc, dependencyFingerprint, sourceSha256, catalogSha256;
            public string method = "Unity URP PreviewRenderUtility. Source FBX mesh instances only, isolated in a temporary preview scene. Curated visibility and, where recorded, translated components; no vertex, normal, boundary or source material changes.";
            public string interpretation = "Healthy anatomical subject previews, not diagnostic images or representations of disease severity. Teal is interface emphasis. Exploded assembly views are presentation only. No labels or physiological claims are baked into the pixels.";
            public string attribution = "BodyParts3D / The Database Center for Life Science (CC BY 4.0), Mauritanian Anatomy Laboratory thoracic atlas lung models, and authored teaching reconstructions. Each component lists its source record.";
            public string runtime = "16 static 512 x 320 textures; mipmaps; trilinear filtering; BC7 on Windows, ASTC 6x6 on Android. No runtime render textures, live cameras, or anatomical colliders are added.";
            public bool sourceFileUnchanged;
            public CardEntry[] cards;
        }

        [MenuItem("Anatome/Prepare source anatomy card artwork")]
        public static void Prepare()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var catalogText = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            if (!source || !catalogText) throw new InvalidDataException("Card artwork requires the existing Thorax.fbx and anatomy catalog.");
            var catalog = JsonUtility.FromJson<AnatomyCatalog>(catalogText.text);
            if (catalog?.records == null) throw new InvalidDataException("Anatomy catalog has no records.");
            var compositions = Compositions();
            if (compositions.Length != 16 || compositions.Select(x => x.id).Distinct().Count() != 16)
                throw new InvalidDataException("Card artwork must have exactly 16 stable resource keys.");
            Directory.CreateDirectory(Root);
            string sourceHash = Hash(ModelPath), catalogHash = Hash(CatalogPath);
            string materialHash = AssetDatabase.GetAssetDependencyHash(MaterialRoot).ToString();
            // Dependency hashes on a folder are not sufficient on every Editor version.
            string materials = string.Join(";", Directory.GetFiles(MaterialRoot, "*.mat").OrderBy(x => x)
                .Select(x => x + ":" + AssetDatabase.GetAssetDependencyHash(x.Replace('\\', '/'))));
            string fingerprint = Revision + "|" + sourceHash + "|" + catalogHash + "|" + materialHash + "|" + materials;
            string manifestPath = Root + "/card-art-provenance.json";
            if (File.Exists(manifestPath))
            {
                var previous = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                if (previous != null && previous.dependencyFingerprint == fingerprint && previous.sourceFileUnchanged &&
                    previous.cards?.Length == 16 && compositions.All(c => File.Exists(Root + "/" + c.id + ".png"))) return;
            }

            var entries = new List<CardEntry>();
            foreach (var composition in compositions)
                entries.Add(Render(source, catalog.records, composition));
            var manifest = new Manifest
            {
                generatedUtc = DateTime.UtcNow.ToString("o"), dependencyFingerprint = fingerprint,
                sourceSha256 = sourceHash, catalogSha256 = catalogHash,
                sourceFileUnchanged = sourceHash == Hash(ModelPath), cards = entries.ToArray()
            };
            if (!manifest.sourceFileUnchanged) throw new InvalidDataException("Card preparation changed the source FBX.");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("Anatome: prepared 16 source-derived anatomy card images, with unchanged source FBX.");
        }

        static CardEntry Render(GameObject source, AnatomyRecord[] catalog, Composition composition)
        {
            var preview = new PreviewRenderUtility();
            var materials = new List<Material>();
            Texture2D image = null;
            try
            {
                var clone = UnityEngine.Object.Instantiate(source);
                clone.name = "Temporary card composition " + composition.id;
                clone.hideFlags = HideFlags.HideAndDontSave;
                preview.AddSingleGO(clone);
                foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var camera in clone.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in clone.GetComponentsInChildren<Light>(true)) light.enabled = false;
                var filters = clone.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, f => f);
                var renderers = new List<MeshRenderer>();
                var parts = new List<SourcePart>();
                foreach (var record in catalog.Where(r => !r.reference_region && composition.include(r)))
                {
                    if (!filters.TryGetValue(record.object_name, out var filter) || !filter.sharedMesh)
                        throw new InvalidDataException("Card " + composition.id + " is missing source mesh " + record.id);
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer) throw new InvalidDataException("Card source has no renderer: " + record.id);
                    var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "/Tissue_" + record.AppearanceTissue + ".mat");
                    if (!sourceMaterial) throw new InvalidDataException("Prepare VRSpecimenAssets before card artwork: " + record.tissue);
                    var material = new Material(sourceMaterial) { name = "Temporary artwork " + record.tissue, hideFlags = HideFlags.HideAndDontSave };
                    bool selected = composition.emphasis?.Invoke(record) == true;
                    // The tint is a selection cue. Tissue colour/texture stays visible,
                    // and no invented vessels, lesions or histological marks are added.
                    material.SetColor("_BaseColor", selected ? new Color(.66f, 1f, .92f) : new Color(.93f, .89f, .82f));
                    material.DisableKeyword("_EMISSION");
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                    renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, filter.sharedMesh.subMeshCount)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.enabled = true;
                    Vector3 offset = composition.offset?.Invoke(record) ?? Vector3.zero;
                    filter.transform.position += offset;
                    materials.Add(material); renderers.Add(renderer);
                    parts.Add(new SourcePart { id = record.id, name = record.name, sourceKind = record.source_kind,
                        sourceRecord = record.source_record, meshAsset = ModelPath + "#" + filter.sharedMesh.name,
                        sourceTriangles = filter.sharedMesh.triangles.Length / 3, presentationTranslation = offset });
                }
                if (renderers.Count == 0) throw new InvalidDataException("No source anatomy selected for card " + composition.id);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                ConfigureCamera(preview.camera, bounds, composition.view);
                bool lungComposition = catalog.Any(r => Lungs(r) && composition.include(r));
                preview.ambientColor = lungComposition ? new Color(.39f, .40f, .40f) : new Color(.31f, .33f, .34f);
                ConfigureLight(preview.lights[0], preview.camera, new Vector3(-.5f, .8f, -.7f), 1.05f, new Color(1f, .96f, .90f));
                // A frontal, inferior fill preserves lower-lobe readability. The
                // earlier rear rim left these source surfaces almost black in cards.
                if (lungComposition)
                    ConfigureLight(preview.lights[1], preview.camera, new Vector3(.65f, -.35f, -.7f), .90f, new Color(.92f, .97f, 1f));
                else
                    ConfigureLight(preview.lights[1], preview.camera, new Vector3(.8f, .25f, .45f), .75f, new Color(.70f, .86f, .91f));
                preview.BeginStaticPreview(new Rect(0, 0, Width, Height));
                preview.Render(true);
                image = preview.EndStaticPreview();
                if (!image) throw new InvalidDataException("Unity did not render card " + composition.id);
                float coverage = ContentFraction(image);
                if (coverage < .005f) throw new InvalidDataException("Card render appears empty: " + composition.id);
                string path = Root + "/" + composition.id + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                ImportTexture(path);
                return new CardEntry { id = composition.id, resourceKey = "Learning/CardArt/" + composition.id,
                    imageSha256 = Hash(path), composition = composition.explanation, anatomy = parts.ToArray(),
                    viewDirection = composition.view.normalized, imageContentFraction = coverage };
            }
            finally
            {
                if (image) UnityEngine.Object.DestroyImmediate(image);
                preview.Cleanup();
                foreach (var material in materials) if (material) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        internal static void ConfigureCamera(Camera camera, Bounds bounds, Vector3 view)
        {
            Vector3 direction = view.normalized;
            // Imported specimen lies supine: anterior is +Y, superior is -Z.
            // Keep the anatomical superior direction at the top of every card.
            camera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.back);
            float radius = Mathf.Max(.02f, bounds.extents.magnitude);
            camera.transform.position = bounds.center + direction * (radius * 3 + .5f);
            camera.orthographic = true;
            camera.aspect = Width / (float)Height;
            var inverse = Quaternion.Inverse(camera.transform.rotation);
            float halfX = 0, halfY = 0;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 p = inverse * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                halfX = Mathf.Max(halfX, Mathf.Abs(p.x)); halfY = Mathf.Max(halfY, Mathf.Abs(p.y));
            }
            camera.orthographicSize = Mathf.Max(halfY, halfX / camera.aspect) * 1.13f;
            camera.nearClipPlane = .01f; camera.farClipPlane = radius * 7 + 2;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
            camera.allowHDR = false; camera.allowMSAA = true; camera.useOcclusionCulling = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.renderShadows = true;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            data.volumeLayerMask = 0; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        internal static void ConfigureLight(Light light, Camera camera, Vector3 positionRelativeToView, float intensity, Color color)
        {
            light.type = LightType.Directional; light.intensity = intensity; light.color = color;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.LookRotation(camera.transform.rotation * -positionRelativeToView.normalized, camera.transform.up);
        }

        static void ImportTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.None;
            importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = true;
            importer.streamingMipmaps = false; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true,
                maxTextureSize = 512, format = TextureImporterFormat.BC7, compressionQuality = 70 });
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true,
                maxTextureSize = 512, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 70 });
            importer.SaveAndReimport();
        }

        static float ContentFraction(Texture2D image)
        {
            var pixels = image.GetPixels32(); var corner = pixels[0]; int count = 0;
            foreach (var p in pixels)
                if (Math.Abs(p.r - corner.r) + Math.Abs(p.g - corner.g) + Math.Abs(p.b - corner.b) > 24) count++;
            return count / (float)pixels.Length;
        }
        static string Hash(string path)
        {
            using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static bool Has(AnatomyRecord r, params string[] ids) => Array.IndexOf(ids, r.id) >= 0;
        static bool Ribs(AnatomyRecord r) => r.tissue == "bone" && (r.name.Contains("rib") || Has(r, "FMA7486", "FMA7487", "FMA7488"));
        static bool Lungs(AnatomyRecord r) => r.tissue == "lung";
        static bool Heart(AnatomyRecord r) => r.tissue == "cardiac" || Has(r, "FMA50039", "FMA50040", "FMA4706", "FMA3736", "FMA8612", "FMA4707");
        static bool Airways(AnatomyRecord r) => r.tissue == "airway";
        // Thumbnail composition keeps complete central source structures and omits
        // long peripheral source branches; no vessel mesh is cropped or shortened.
        static bool CentralVessels(AnatomyRecord r) => Has(r, "FMA3736", "FMA3768", "FMA3932", "FMA4720", "FMA4751", "FMA4761", "FMA8612");
        static bool AorticCusps(AnatomyRecord r) => Has(r, "FMA7252", "FMA7253", "FMA7254");

        static Composition[] Compositions()
        {
            var front = new Vector3(.18f, 1f, -.18f);
            var oblique = new Vector3(.42f, 1f, -.25f);
            return new[]
            {
                new Composition("mode-cases", "Anterior oblique lungs and exposed heart, with superficial coverings omitted. Healthy reference subject for case investigation.",
                    r => Lungs(r) || Heart(r) || Has(r, "FMA7394", "FMA3768"), Heart, oblique),
                new Composition("mode-identify", "Anterior thoracic cage with the sternum emphasised; original rest relationships retained.",
                    Ribs, r => Has(r, "FMA7486", "FMA7487", "FMA7488"), front),
                new Composition("mode-assemble", "Five lung lobes and bronchial tree, in a small exploded teaching composition. Every translation is recorded; source geometry is unchanged.",
                    r => Lungs(r) || Airways(r), r => r.id == "MAL_RML", front,
                    r => !Lungs(r) ? Vector3.zero : new Vector3(r.id.StartsWith("MAL_R") ? -.028f : .028f, r.id == "MAL_RML" ? .028f : 0, r.id.EndsWith("LL") ? .022f : -.016f)),
                new Composition("mode-dissect", "One-sided chest wall exposure with right pectoral muscle translated anteriorly to reveal ribs. No cut or histology is depicted.",
                    r => Ribs(r) || Has(r, "FMA79979", "FMA13375", "MAL_LUL", "MAL_LLL", "FMA13884"),
                    r => r.id == "FMA13375", oblique,
                    r => r.id == "FMA79979" ? new Vector3(-.035f, .035f, 0) : Vector3.zero),
                new Composition("chest-wall", "Ribs and sternum in original rest relationships. The right fifth rib is emphasised as a selection cue.",
                    Ribs, r => r.id == "FMA8066", oblique),
                new Composition("pleura-lungs", "Lung lobes, trachea and diaphragm. Pleural coverings are omitted for clear lobe relationships.",
                    r => Lungs(r) || Has(r, "FMA7394", "FMA13295"), r => r.id == "MAL_RML", front),
                new Composition("airways", "Source trachea and bronchial tree isolated from the lungs, retaining their shared source coordinates.",
                    Airways, r => Has(r, "FMA68418", "FMA7396"), front),
                new Composition("heart-coronaries", "Heart walls, coronary vessels and proximal outflow, with the pericardial covering omitted.",
                    Heart, r => Has(r, "FMA50039", "FMA50040"), new Vector3(-.18f, 1, -.3f)),
                new Composition("vessels-valves", "Central great vessels in their original spatial arrangement. The aortic arch is emphasised. Peripheral subclavian/pulmonary branches and long descending vessels are omitted to keep the central relationships readable at card size.",
                    CentralVessels, r => Has(r, "FMA3736", "FMA3768"), oblique),
                new Composition("mediastinum", "Central airway, esophagus, central great vessels and existing authored phrenic/vagus nerve models; peripheral branches, lung and chest-wall coverings omitted for thumbnail readability.",
                    r => CentralVessels(r) || Has(r, "FMA7394", "FMA7131", "REC_Right_Phrenic_nerve", "REC_Left_Phrenic_nerve", "REC_Right_Vagus_nerve", "REC_Left_Vagus_nerve"),
                    r => r.id == "FMA7131", new Vector3(-.48f, 1, -.12f)),
                new Composition("pneumothorax", "Right lung lobes with selected lateral ribs, showing the case's anatomical region. Healthy reference geometry; no invented collapse or drain placement.",
                    r => r.id.StartsWith("MAL_R") || Has(r, "FMA8066", "FMA7957", "FMA7394"),
                    r => r.id == "MAL_RUL", new Vector3(-.5f, 1, -.1f)),
                new Composition("asthma", "Bronchial tree isolated at an anterior oblique angle. Major bronchi emphasised; no invented narrowing or alveolar segmentation.",
                    Airways, r => Has(r, "FMA31744", "FMA31746"), new Vector3(-.3f, 1, -.15f)),
                new Composition("coronary-disease", "Exposed healthy heart and source coronary vessels. Interface emphasis does not claim a lesion location.",
                    Heart, r => Has(r, "FMA50039", "FMA50040"), new Vector3(.45f, 1, -.2f)),
                new Composition("pneumonia", "Posterior-lateral view of the complete right lung lobes, with the lower lobe emphasised and bronchial meshes omitted. This is a healthy subject preview, not an infection map.",
                    r => r.id.StartsWith("MAL_R"),
                    r => r.id == "MAL_RLL", new Vector3(-.45f, -1f, .25f)),
                new Composition("pleural-effusion", "Right lung lobes, diaphragm and existing parietal pleura translated laterally as a layer relationship preview. No fluid volume is implied.",
                    r => r.id.StartsWith("MAL_R") || Has(r, "FMA13295", "REC_Right_Parietal_Pleura"),
                    r => r.id == "REC_Right_Parietal_Pleura", front,
                    r => r.id == "REC_Right_Parietal_Pleura" ? new Vector3(-.095f, .012f, 0) : Vector3.zero),
                new Composition("aortic-stenosis", "The three original aortic valve cusps, isolated and enlarged equally for recognition. Healthy source shapes, not a fabricated stenosis or treatment result.",
                    AorticCusps, r => r.id == "FMA7253", new Vector3(.12f, 1f, -.65f))
            };
        }
    }
}
