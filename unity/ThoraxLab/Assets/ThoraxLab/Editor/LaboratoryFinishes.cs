using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ThoraxLab.Editor
{
    public static class LaboratoryFinishes
    {
        const string Root = "Assets/ThoraxLab/";
        const string RoomPrefab = Root + "Prefabs/Laboratory.prefab";

        public static void PrepareMaterials()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Laboratory" }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                bool normal = importer.assetPath.EndsWith(" normal.png", StringComparison.OrdinalIgnoreCase);
                bool screen = importer.assetPath.EndsWith("/Theatre monitors.png", StringComparison.Ordinal);
                bool contact = importer.assetPath.EndsWith("/Theatre floor contact.png", StringComparison.Ordinal);
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var wrap = screen || contact ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                bool legacy = importer.assetPath.EndsWith("/Welded sheet flooring.png", StringComparison.Ordinal) ||
                    importer.assetPath.EndsWith("/Washable coating.png", StringComparison.Ordinal);
                int size = contact ? 1024 : legacy ? 512 : 4096;
                var android = importer.GetPlatformTextureSettings("Android");
                if (importer.maxTextureSize == size && importer.filterMode == FilterMode.Trilinear && importer.anisoLevel == 4 && importer.mipmapEnabled &&
                    importer.wrapMode == wrap && importer.textureType == type && importer.sRGBTexture == !(normal || contact) &&
                    !importer.alphaIsTransparency && android.overridden && android.maxTextureSize == size &&
                    android.format == TextureImporterFormat.ASTC_6x6) continue;
                importer.maxTextureSize = size;
                importer.textureType = type;
                importer.sRGBTexture = !(normal || contact);
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = false;
                importer.wrapMode = wrap;
                android.overridden = true;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.maxTextureSize = size;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            Finish("Floor", "Theatre resin", Color.white, .65f, normal: "Theatre resin normal", relief: .17f);
            Finish("Wall", "Theatre ceramic panels", Color.white, 1 / 1.2f, normal: "Theatre ceramic normal", relief: .72f);
            Finish("Stainless", "Brushed stainless", Color.white, 2, .83f, normal: "Brushed alloy normal", relief: .29f);
            Finish("InstrumentSteel", "Brushed stainless", new Color(.98f, .99f, 1f), 3, .94f, .86f, "Brushed alloy normal", .18f);
            Finish("DarkSteel", "Brushed stainless", new Color(.42f, .47f, .52f), 2, .74f, normal: "Brushed alloy normal", relief: .23f);
            Finish("Blue", "Surgical nonwoven", Color.white, 1.45f, normal: "Surgical textile normal", relief: .52f);
            Finish("Cloth", "Woven linen", new Color(.97f, .985f, 1), 4, normal: "Woven linen normal", relief: .40f);
            Finish("Cushion", null, new Color(.165f, .180f, .203f), smoothness: .43f);
            const string coating = "Medical equipment coating", coatingNormal = "Medical equipment coating normal";
            Finish("EquipmentBlue", coating, new Color(.17f, .42f, .58f), 2.4f, .12f, .91f, coatingNormal, .24f);
            Finish("CylinderGreen", coating, new Color(.20f, .51f, .45f), 2.4f, .14f, 1f, coatingNormal, .22f);
            Finish("Display", null, new Color(.065f, .113f, .14f), smoothness: .64f);
            Finish("MonitorAtlas", "Theatre monitors", Color.white);
            Finish("Ceiling", "Sealed ceiling tile", new Color(.85f, .91f, .97f), 1 / .6f, normal: "Sealed ceiling tile normal", relief: .22f);
            Finish("VentFace", "Perforated diffuser", Color.white, 2, .29f);
            Finish("Guard", coating, new Color(.55f, .65f, .71f), 2.4f, .08f, .84f, coatingNormal, .29f);
            Finish("Ceramic", "Glazed scrub tile", new Color(.88f, .94f, 1f), 1 / .6f, normal: "Glazed scrub tile normal", relief: .50f);
            Finish("White", coating, new Color(.94f, .96f, .98f), 2.4f, .035f, .95f, coatingNormal, .25f);
            Finish("Black", coating, new Color(.13f, .155f, .18f), 2.4f, 0, .59f, coatingNormal, .24f);
            Finish("Sign", null, new Color(.10f, .155f, .19f), smoothness: .28f);
            Finish("Red", coating, new Color(.69f, .24f, .18f), 2.4f, .075f, .90f, coatingNormal, .24f);
            Finish("Glass", null, new Color(.81f, .90f, .97f, .115f), smoothness: .87f);
            Finish("Light", null, new Color(.83f, .92f, 1f), smoothness: .44f);
        }

        static void Finish(string name, string map, Color tint, float repeat = 1, float metal = 0,
            float smoothness = 1, string normal = null, float relief = 1)
        {
            string path = Root + "Materials/" + name + ".mat";
            bool screen = name == "MonitorAtlas";
            var shader = Shader.Find("Universal Render Pipeline/" + (screen ? "Unlit" : name == "Floor" ? "Complex Lit" : "Lit"));
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            Texture2D texture = null;
            if (map != null)
            {
                string relative = map.Contains("/") ? map : "Laboratory/" + map;
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + relative + ".png");
                if (!texture) throw new InvalidOperationException("Missing laboratory finish " + map);
            }
            bool glass = name == "Glass", lamp = name == "Light";
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one * repeat);
            material.SetColor("_BaseColor", tint);
            if (screen)
            {
                EditorUtility.SetDirty(material);
                return;
            }
            var bump = normal == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Laboratory/" + normal + ".png");
            if (normal != null && !bump) throw new InvalidOperationException("Missing operating room normal map " + normal);
            material.SetTexture("_BumpMap", bump);
            material.SetFloat("_BumpScale", relief);
            CoreUtils.SetKeyword(material, "_NORMALMAP", bump);
            material.SetFloat("_Cull", (float)(name == "Blue" ? CullMode.Off : CullMode.Back));
            material.doubleSidedGI = name == "Blue";
            if (name == "Floor")
            {
                var contact = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Laboratory/Theatre floor contact.png");
                if (!contact) throw new InvalidOperationException("Missing baked operating room floor contact map");
                material.SetTexture("_DetailAlbedoMap", contact);
                material.SetTextureScale("_DetailAlbedoMap", new Vector2(1 / (repeat * 17.3f), 1 / (repeat * 9.2f)));
                material.SetTextureOffset("_DetailAlbedoMap", new Vector2(11.2f / 17.3f, .5f));
                material.SetFloat("_DetailAlbedoMapScale", 1);
                CoreUtils.SetKeyword(material, "_DETAIL_MULX2", true);
                material.SetFloat("_ClearCoat", 1);
                material.SetFloat("_ClearCoatMask", .85f);
                material.SetFloat("_ClearCoatSmoothness", .94f);
                CoreUtils.SetKeyword(material, "_CLEARCOAT", true);
            }
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            bool packedSmoothness = texture && name != "InstrumentSteel";
            material.SetFloat("_SmoothnessTextureChannel", packedSmoothness ? 1 : 0);
            CoreUtils.SetKeyword(material, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", packedSmoothness);
            material.SetFloat("_Surface", glass ? 1 : 0);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_BlendModePreserveSpecular", 1);
            BaseShaderGUI.SetupMaterialBlendMode(material);
            material.SetShaderPassEnabled("ShadowCaster", !glass);
            material.SetColor("_EmissionColor", lamp ? new Color(.75f, .87f, 1f) : Color.black);
            CoreUtils.SetKeyword(material, "_EMISSION", lamp);
            // The matching baked area lights supply the diffuser's illumination once.
            material.globalIlluminationFlags = lamp ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.SetColor("_Color", tint);
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", Vector2.one * repeat);
            EditorUtility.SetDirty(material);
        }

        static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        public static void ApplyRoom(GameObject room)
        {
            var modelPath = Root + "Models/Laboratory.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            // Blender's centimetre-based FBX children have scale 100 in the scene. Using 1 collapses their lightmap charts.
            if (importer && !Mathf.Approximately(importer.secondaryUVMinObjectScale, 100f))
            {
                importer.secondaryUVMinObjectScale = 100f;
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var chair = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/LaboratoryChair.fbx");
            if (!source || !chair) throw new InvalidOperationException("Missing operating room or chair model");
            var table = room.transform.Find("Adjustable main table");
            if (!table) throw new InvalidOperationException("Missing adjustable laboratory table");
            if (!room.TryGetComponent(out ThoraxLab.Learning.ScrubLobbyDoor door)) door = room.AddComponent<ThoraxLab.Learning.ScrubLobbyDoor>();
            door.southLeaf = Child(room.transform, "Scrub entry south leaf");
            door.northLeaf = Child(room.transform, "Scrub entry north leaf");
            var clock = Child(room.transform, "Digital operating room timer");
            clock.SetLocalPositionAndRotation(new Vector3(.82f, 2.34f, -4.331f), Quaternion.identity);
            if (!clock.TryGetComponent(out ThoraxLab.Learning.OperatingRoomClock display)) clock.gameObject.AddComponent<ThoraxLab.Learning.OperatingRoomClock>();
            var parts = source.GetComponentsInChildren<MeshFilter>().Concat(chair.GetComponentsInChildren<MeshFilter>()).ToArray();
            foreach (var part in parts)
                if (!part.sharedMesh || part.sharedMesh.vertexCount == 0)
                    throw new InvalidOperationException("Empty imported operating room mesh: " + part.name);
            var names = parts.Select(part => part.name).ToHashSet();
            foreach (var parent in new[] { room.transform, table, door.southLeaf, door.northLeaf })
            foreach (var child in parent.Cast<Transform>().ToArray())
            {
                if (!child.name.StartsWith("Lab_", StringComparison.Ordinal) || names.Contains(child.name)) continue;
                if (!child.TryGetComponent(out MeshFilter oldMesh) || !child.TryGetComponent(out MeshRenderer oldRenderer)) continue;
                // Removed FBX submeshes can already be null after Unity reimports the model.
                if (oldMesh.sharedMesh && AssetDatabase.GetAssetPath(oldMesh.sharedMesh) != AssetDatabase.GetAssetPath(source)) continue;
                var oldMaterial = oldRenderer.sharedMaterial;
                if (oldMaterial && AssetDatabase.GetAssetPath(oldMaterial) == Root + "Materials/" + oldMaterial.name + ".mat" &&
                    (child.name == "Lab_" + oldMaterial.name || child.name == "Lab_MainTable_" + oldMaterial.name ||
                     child.name == "Lab_ScrubSouth_" + oldMaterial.name || child.name == "Lab_ScrubNorth_" + oldMaterial.name))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            foreach (var mesh in parts)
            {
                bool adjustable = mesh.name.StartsWith("Lab_MainTable_", StringComparison.Ordinal);
                bool chairPart = mesh.name.StartsWith("Lab_Chair_", StringComparison.Ordinal);
                bool south = mesh.name.StartsWith("Lab_ScrubSouth_", StringComparison.Ordinal);
                bool north = mesh.name.StartsWith("Lab_ScrubNorth_", StringComparison.Ordinal);
                var parent = adjustable ? table : south ? door.southLeaf : north ? door.northLeaf : room.transform;
                var target = parent.Find(mesh.name) ?? room.transform.Find(mesh.name);
                if (!target) target = Child(parent, mesh.name);
                target.SetParent(parent, false);
                target.SetLocalPositionAndRotation(mesh.transform.localPosition, mesh.transform.localRotation);
                target.localScale = mesh.transform.localScale;
                if (!target.TryGetComponent(out MeshFilter filter)) filter = target.gameObject.AddComponent<MeshFilter>();
                if (!target.TryGetComponent(out MeshRenderer renderer)) renderer = target.gameObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh.sharedMesh;
                string prefix = adjustable ? "Lab_MainTable_" : south ? "Lab_ScrubSouth_" : north ? "Lab_ScrubNorth_" : chairPart ? "Lab_Chair_" : "Lab_";
                string finish = mesh.name.Substring(prefix.Length);
                var collider = target.GetComponent<MeshCollider>();
                bool needsCollider = !(adjustable && finish == "Blue") && finish != "MonitorAtlas" && finish != "InstrumentSteel";
                if (needsCollider)
                {
                    if (!collider) collider = target.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                }
                else if (collider) UnityEngine.Object.DestroyImmediate(collider);
                var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + finish + ".mat");
                if (!material) throw new InvalidOperationException("Missing laboratory material " + finish);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = finish == "Glass" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                if (adjustable || south || north)
                    GameObjectUtility.SetStaticEditorFlags(target.gameObject, StaticEditorFlags.ReflectionProbeStatic);
                else
                {
                    var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
                    // Letter strokes and small dark fittings are narrower than a lightmap texel.
                    bool useLightmap = !chairPart && finish != "Glass" && finish != "Sign" && finish != "Black" &&
                        finish != "Light" && finish != "MonitorAtlas" && finish != "InstrumentSteel";
                    if (useLightmap) flags |= StaticEditorFlags.ContributeGI;
                    renderer.receiveGI = useLightmap ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
                    GameObjectUtility.SetStaticEditorFlags(target.gameObject, flags);
                }
            }
        }

        public static void ConfigureLights()
        {
            var ceiling = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.name.StartsWith("Ceiling illumination", StringComparison.Ordinal))
                .OrderBy(l => l.name, StringComparer.Ordinal).ThenBy(l => l.transform.GetSiblingIndex()).ToArray();
            int index = 0;
            foreach (float x in new[] { 4.5f, 1.65f, -1.65f, -4.5f })
            foreach (float z in new[] { -3f, 0f, 3f })
            {
                var light = index < ceiling.Length ? ceiling[index] : new GameObject().AddComponent<Light>();
                light.name = "Ceiling illumination " + (++index).ToString("00");
                light.transform.SetPositionAndRotation(new Vector3(x, 2.969f, z), Quaternion.Euler(90, 0, 0));
                light.type = LightType.Rectangle;
                light.areaSize = new Vector2(.31f, 1.55f);
                light.color = new Color(.86f, .94f, 1f);
                light.intensity = 42f;
                light.range = 9;
                light.shadows = LightShadows.Soft;
                light.lightmapBakeType = LightmapBakeType.Baked;
                EditorUtility.SetDirty(light);
            }
            var room = GameObject.Find("Laboratory").transform;
            var annexPositions = new[] { 3.55f, 1.72f, .138f, -1.258f };
            for (int i = 0; i < annexPositions.Length; i++)
            {
                var fixture = Child(room, "Scrub and observation light " + (i + 1));
                if (!fixture.TryGetComponent(out Light light)) light = fixture.gameObject.AddComponent<Light>();
                fixture.SetPositionAndRotation(new Vector3(7.65f, 2.96f, annexPositions[i]), Quaternion.Euler(90, 0, 0));
                light.type = LightType.Rectangle;
                light.areaSize = new Vector2(.44f, .99f);
                light.color = new Color(.95f, .98f, 1);
                light.intensity = 31;
                light.range = 6;
                light.shadows = LightShadows.Soft;
                light.lightmapBakeType = LightmapBakeType.Baked;
                EditorUtility.SetDirty(light);
            }
            foreach (float z in new[] { 2.64f, -1.10f })
            {
                var fixture = Child(room, z > 0 ? "Staff circulation light south" : "Staff circulation light north");
                if (!fixture.TryGetComponent(out Light light)) light = fixture.gameObject.AddComponent<Light>();
                fixture.SetPositionAndRotation(new Vector3(10.08f, 2.96f, z), Quaternion.Euler(90, 0, 0));
                light.type = LightType.Rectangle;
                light.areaSize = new Vector2(.35f, .96f);
                light.intensity = 27f;
                light.range = 5;
                light.lightmapBakeType = LightmapBakeType.Baked;
            }
            var grid = GameObject.Find("Laboratory light probes")?.GetComponent<LightProbeGroup>();
            if (grid)
            {
                var positions = grid.probePositions.Where(point => point.x <= 6f).ToList();
                foreach (float x in new[] { 6.53f, 7.65f, 8.80f, 10.08f })
                foreach (float z in new[] { -2.12f, -.19f, 1.72f, 3.67f })
                foreach (float y in new[] { .47f, 1.45f, 2.48f }) positions.Add(new Vector3(x, y, z));
                grid.probePositions = positions.ToArray();
                EditorUtility.SetDirty(grid);
            }
            var annex = Child(room, "Scrub and observation reflection");
            annex.position = new Vector3(7.65f, 1.5f, .92f);
            if (!annex.TryGetComponent(out ReflectionProbe annexProbe)) annexProbe = annex.gameObject.AddComponent<ReflectionProbe>();
            annexProbe.mode = ReflectionProbeMode.Baked;
            annexProbe.boxProjection = true;
            annexProbe.size = new Vector3(3.33f, 3.28f, 7.45f);
            annexProbe.resolution = 128;
            var heads = new[] { new Vector3(.52f, 2.29f, .95f), new Vector3(-.52f, 2.25f, .57f) };
            for (int i = 0; i < heads.Length; i++)
            {
                string name = "Operating field illumination " + (i + 1);
                var fixture = GameObject.Find(name) ?? new GameObject(name);
                fixture.transform.SetParent(room, true);
                if (!fixture.TryGetComponent(out Light light)) light = fixture.AddComponent<Light>();
                light.transform.SetPositionAndRotation(heads[i], Quaternion.LookRotation(new Vector3(0, .89f, 1.35f) - heads[i]));
                light.type = LightType.Spot;
                light.spotAngle = 94;
                light.innerSpotAngle = 52;
                light.range = 3.4f;
                light.intensity = i == 0 ? 1.35f : 7.5f;
                light.color = new Color(.94f, .98f, 1f);
                light.shadows = i == 0 ? LightShadows.None : LightShadows.Soft;
                light.lightmapBakeType = i == 0 ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
                EditorUtility.SetDirty(light);
            }
            var probe = GameObject.Find("Laboratory reflection")?.GetComponent<ReflectionProbe>();
            RenderSettings.reflectionIntensity = .92f;
            if (probe)
            {
                probe.boxProjection = true;
                probe.hdr = true;
                probe.resolution = 256;
                probe.size = new Vector3(12.3f, 3.3f, 9.2f);
                probe.intensity = 1.5f;
                EditorUtility.SetDirty(probe);
            }
            var settings = Lightmapping.lightingSettings;
            if (settings)
            {
                settings.ao = true;
                settings.aoMaxDistance = .42f;
                settings.aoExponentIndirect = 1f;
                settings.aoExponentDirect = .4f;
                settings.lightmapResolution = 20;
                settings.directSampleCount = 64;
                settings.indirectSampleCount = 128;
                EditorUtility.SetDirty(settings);
            }
        }

        [MenuItem("Thorax Lab/Art/Update laboratory environment")]
        public static void UpdateEnvironment()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != Root + "Scenes/ThoraxLab.unity")
                throw new InvalidOperationException("Open the ThoraxLab scene in Edit mode.");
            PrepareMaterials();
            ApplyRoom(GameObject.Find("Laboratory"));
            var prefab = PrefabUtility.LoadPrefabContents(RoomPrefab);
            try { ApplyRoom(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, RoomPrefab); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            ConfigureLights();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
