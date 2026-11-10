using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ThoraxLab.Editor
{
    public static class LearningBuild
    {
        public static void Prepare()
        {
            PlayerSettings.companyName=Learning.LearningBrand.Name;
            PlayerSettings.productName=Learning.LearningBrand.Name;
            PlayerSettings.bundleVersion="1.4.9";
            PlayerSettings.Android.bundleVersionCode=Mathf.Max(PlayerSettings.Android.bundleVersionCode,17);
            var accountConfiguration=Resources.Load<TextAsset>("Learning/supabase-config");
            LearningAndroidPolicy.ValidateConfiguration(accountConfiguration?JsonUtility.FromJson<Learning.SupabaseConfiguration>(accountConfiguration.text):null);
            AssetDatabase.Refresh();
            PrepareHandTracking();
            VRSpecimenAssets.Prepare();
            LearningCardArt.Prepare();
            LearningCutMeshRepair.Prepare();
            PrepareGuidanceMaterials();
            if(!Resources.Load<TMP_Settings>("TMP Settings"))
            {
                var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
                AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath,"Package Resources/TMP Essential Resources.unitypackage"),false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            const string path="Assets/ThoraxLab/Resources/Fonts/SourceSans3-SDF.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(!font)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/ThoraxLab/Resources/Fonts/SourceSans3-Regular.otf");
                if(!source)throw new Exception("Missing bundled Source Sans 3 font.");
                font=TMP_FontAsset.CreateFontAsset(source,64,6,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
                font.name="Source Sans 3 SDF";AssetDatabase.CreateAsset(font,path);
                foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
                AssetDatabase.AddObjectToAsset(font.material,font);font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?'-/()[]%+×·&@=<>_\n");EditorUtility.SetDirty(font);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/ThoraxLab/Resources/Learning"}))
            {
                var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;if(!importer)continue;
                var sample=importer.defaultSampleSettings;
                if(sample.compressionFormat==AudioCompressionFormat.Vorbis&&Mathf.Approximately(sample.quality,.6f)&&sample.loadType==AudioClipLoadType.CompressedInMemory&&importer.forceToMono)continue;
                sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.6f;sample.loadType=AudioClipLoadType.CompressedInMemory;importer.defaultSampleSettings=sample;importer.forceToMono=true;importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var xr=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);if(xr){var refresh=xr.GetFeature<Learning.QuestRefreshFeature>();if(refresh){refresh.enabled=true;EditorUtility.SetDirty(refresh);}}
            // Quest 2 logs reproduce Adreno Vulkan image-view crashes during graphics changes.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
            if(xr){
                xr.optimizeBufferDiscards=false;
                var quest=xr.GetFeature<UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature>();
                if(quest){var settings=new SerializedObject(quest);settings.FindProperty("m_optimizeBufferDiscards").boolValue=false;settings.ApplyModifiedPropertiesWithoutUndo();}
                EditorUtility.SetDirty(xr);
            }
            AssetDatabase.SaveAssets();
        }
        public static void Windows()
        {
            Prepare();Build(BuildTarget.StandaloneWindows64,"builds/AnatomeWindows/Anatome.exe");
        }
        static void PrepareGuidanceMaterials()
        {
            // Explicit assets keep the transparent and unlit variants in player builds.
            // Shader.Find alone succeeds in the Editor but can be stripped in players.
            const string folder="Assets/ThoraxLab/Resources/Learning/";
            var socket=AssetDatabase.LoadAssetAtPath<Material>(folder+"GuidanceSocket.mat");
            if(!socket){socket=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(socket,folder+"GuidanceSocket.mat");}
            socket.SetFloat("_Surface",1);socket.SetFloat("_Blend",0);
            socket.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);socket.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            socket.SetFloat("_ZWrite",0);socket.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");socket.SetColor("_BaseColor",new Color(.2f,.85f,.77f,.16f));socket.renderQueue=3000;EditorUtility.SetDirty(socket);
            var line=AssetDatabase.LoadAssetAtPath<Material>(folder+"GuidanceLine.mat");
            if(!line){line=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(line,folder+"GuidanceLine.mat");}
            line.SetColor("_BaseColor",new Color(.45f,.94f,.86f));EditorUtility.SetDirty(line);AssetDatabase.SaveAssets();
            foreach(var name in new[]{"ControllerBody","ControllerFace"})
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(folder+name+".mat");
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,folder+name+".mat");}
                material.SetColor("_BaseColor",name=="ControllerBody"?new Color(.72f,.8f,.82f):new Color(.045f,.065f,.075f));material.SetFloat("_Smoothness",.45f);EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
        public static void WindowsQA()
        {
            Prepare();Build(BuildTarget.StandaloneWindows64,"builds/AnatomeVRTest/Anatome.exe",BuildOptions.Development);
        }
        public static void VerifyGlovedHands()
        {
            Prepare();LearningVRSimulatorVerification.Run();
        }
        public static void WindowsReviewAndRelease()
        {
            Prepare();
            Build(BuildTarget.StandaloneWindows64,"builds/AnatomeVRTest/Anatome.exe",BuildOptions.Development);
            Build(BuildTarget.StandaloneWindows64,"builds/AnatomeWindows/Anatome.exe");
        }
        public static void CraftWindowsQA()
        {
            Prepare();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            InstrumentVisualVerification.Capture();Build(BuildTarget.StandaloneWindows64,"builds/AnatomeVRTest/Anatome.exe",BuildOptions.Development);
        }
        [MenuItem("Anatome/Open VR learning scene")]
        public static void OpenVRLearning()
        {
            PrepareHandTracking();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/ThoraxLab/Scenes/ThoraxLab.unity");
            EditorPrefs.SetBool("ThoraxLab.XRDeviceSimulator",true);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        public static void PrepareHandTracking()
        {
            foreach(var target in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})
            {
                UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(target);
                var settings=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(target);
                if(!settings)continue;
                var hands=settings.GetFeature<UnityEngine.XR.Hands.OpenXR.HandTracking>();
                var aim=settings.GetFeature<UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim>();
                if(!hands||!aim)throw new InvalidOperationException("XR Hands 1.8.1 must resolve before preparing controller and bare-hand input.");
                hands.enabled=true;aim.enabled=true;
                EditorUtility.SetDirty(hands);EditorUtility.SetDirty(aim);EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }
        public static void OpenVRReview()
        {
            OpenVRLearning();
            var game=Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach(var window in game)if(window.titleContent.text=="Game"){window.maximized=true;window.Focus();break;}
            LearningEditorPlayMode.Request();
        }
        public static void Android()
        {
            Prepare();
            BuildQuest("builds/AnatomeQuest/Anatome.apk");
        }
        public static void AndroidRelease()
        {
            string Required(string name)=>Environment.GetEnvironmentVariable(name) is string value&&!string.IsNullOrWhiteSpace(value)?value:throw new InvalidOperationException("Set "+name+" privately before building the classroom release.");
            string keystore=Path.GetFullPath(Required("ANATOME_KEYSTORE"));
            string alias=Required("ANATOME_KEY_ALIAS"),storePassword=Required("ANATOME_KEYSTORE_PASSWORD"),keyPassword=Required("ANATOME_KEY_PASSWORD");
            if(!File.Exists(keystore))throw new FileNotFoundException("The configured release keystore does not exist.");
            string workspace=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(keystore.StartsWith(workspace,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Keep the signing key outside the project workspace.");
            bool custom=PlayerSettings.Android.useCustomKeystore;
            string previousStore=PlayerSettings.Android.keystoreName,previousAlias=PlayerSettings.Android.keyaliasName;
            string previousStorePassword=PlayerSettings.Android.keystorePass,previousKeyPassword=PlayerSettings.Android.keyaliasPass;
            try
            {
                Prepare();PlayerSettings.Android.useCustomKeystore=true;
                PlayerSettings.Android.keystoreName=keystore;PlayerSettings.Android.keyaliasName=alias;
                PlayerSettings.Android.keystorePass=storePassword;PlayerSettings.Android.keyaliasPass=keyPassword;
                BuildQuest("builds/AnatomeSchool/Anatome-"+PlayerSettings.bundleVersion+".apk");
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore=custom;PlayerSettings.Android.keystoreName=previousStore;PlayerSettings.Android.keyaliasName=previousAlias;
                PlayerSettings.Android.keystorePass=previousStorePassword;PlayerSettings.Android.keyaliasPass=previousKeyPassword;AssetDatabase.SaveAssets();
            }
        }
        static void BuildQuest(string output)
        {
            var xr=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var apis=PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if(apis.Length!=1||apis[0]!=UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3||xr&&xr.optimizeBufferDiscards)
                throw new Exception("Quest compatibility build requires OpenGLES3 with Vulkan buffer discards disabled.");
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../qa/learning-graphics-Android.txt")),"OpenGLES3 only\nVulkan buffer discards disabled\n");
            if(xr){xr.latencyOptimization=UnityEngine.XR.OpenXR.OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;EditorUtility.SetDirty(xr);AssetDatabase.SaveAssets();}
            Build(BuildTarget.Android,output);
        }
        static void Build(BuildTarget target,string relative,BuildOptions options=BuildOptions.None)
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));string output=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(output));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/ThoraxLab/Scenes/ThoraxLab.unity"},locationPathName=output,target=target,options=options});
            File.WriteAllText(Path.Combine(root,"qa","learning-build-"+target+(options.HasFlag(BuildOptions.Development)?"-QA":"")+".txt"),result.summary.result+"\n"+result.summary.totalErrors+" errors\n"+result.summary.totalWarnings+" warnings\n"+result.summary.totalTime);
            if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Learning release build failed: "+result.summary.result);
        }
    }
}
