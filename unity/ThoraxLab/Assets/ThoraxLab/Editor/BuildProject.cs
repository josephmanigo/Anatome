using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace ThoraxLab.Editor {
public static class BuildProject {
 const string Base="Assets/ThoraxLab";
 static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
 static T Asset<T>(string name) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(Base+"/"+name);
 static Material MakeMaterial(string name,Color color,float metal=0,float smooth=.4f){
  var path=Base+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}if(name=="Interior_bone")m.shader=Shader.Find("ThoraxLab/BoneCut");m.color=color;m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);
  var texture=Asset<Texture2D>("Textures/"+name+"_BaseColor.png");if(texture){m.mainTexture=texture;m.color=Color.white;}return m;
 }
 public static void Configure(){
  foreach(var folder in new[]{"Scenes","Prefabs","Settings","Models","Materials","Textures"})Directory.CreateDirectory(Base+"/"+folder);
  var modelFiles=new[]{"Laboratory.fbx","LaboratoryChair.fbx","Thorax.fbx","Thorax_LOD1.fbx","Thorax_LOD2.fbx"};
  foreach(var f in modelFiles)File.Copy(Path.Combine(Root,"exports",f),Base+"/Models/"+f,true);
  File.Copy(Path.Combine(Root,"exports/anatomy-catalog.json"),Base+"/anatomy-catalog.json",true);
  if(Directory.Exists(Path.Combine(Root,"exports/textures")))foreach(var file in Directory.GetFiles(Path.Combine(Root,"exports/textures"),"*.png"))File.Copy(file,Base+"/Textures/"+Path.GetFileName(file),true);
  AssetDatabase.Refresh();
  foreach(var f in modelFiles){var importer=(ModelImporter)AssetImporter.GetAtPath(Base+"/Models/"+f);importer.isReadable=!f.StartsWith("Thorax_LOD",StringComparison.Ordinal);importer.globalScale=1;importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.generateSecondaryUV=f=="Laboratory.fbx";importer.SaveAndReimport();}
  foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Base+"/Textures"})){var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();}
  var renderer=Asset<UniversalRendererData>("Settings/LabRenderer.asset");if(!renderer){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Base+"/Settings/LabRenderer.asset");}
  var pipeline=Asset<UniversalRenderPipelineAsset>("Settings/LabURP.asset");if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,Base+"/Settings/LabURP.asset");}pipeline.msaaSampleCount=4;pipeline.renderScale=1;pipeline.supportsHDR=false;pipeline.shadowDistance=3;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
  foreach(var guid in AssetDatabase.FindAssets("",new[]{"Assets/Plugins"})){var path=AssetDatabase.GUIDToAssetPath(guid);if(AssetImporter.GetAtPath(path) is PluginImporter plugin){plugin.SetCompatibleWithAnyPlatform(false);bool android=path.EndsWith(".so");plugin.SetCompatibleWithEditor(!android);plugin.SetCompatibleWithPlatform(android?BuildTarget.Android:BuildTarget.StandaloneWindows64,true);if(android)plugin.SetPlatformData(BuildTarget.Android,"CPU","ARM64");else{plugin.SetEditorData("CPU","x86_64");plugin.SetEditorData("OS","Windows");}plugin.SaveAndReimport();}}
  PlayerSettings.companyName="Anatome";PlayerSettings.productName="Anatome";PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"org.thoraxlab.education");PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android,ApiCompatibilityLevel.NET_Standard);PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=1000;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
  PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
  var ps=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var handling=ps.FindProperty("activeInputHandler");if(handling!=null){handling.intValue=1;ps.ApplyModifiedPropertiesWithoutUndo();}
  ConfigureXR();CreateScene();AssetDatabase.SaveAssets();
 }
 static void ConfigureXR(){
  var all=Asset<XRGeneralSettingsPerBuildTarget>("Settings/XRSettings.asset");if(!all){all=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(all,Base+"/Settings/XRSettings.asset");}EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,all,true);
  foreach(var target in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone}){
   if(all.SettingsForBuildTarget(target)==null)all.CreateDefaultSettingsForBuildTarget(target);var settings=all.SettingsForBuildTarget(target);settings.InitManagerOnStart=true;
   if(!settings.Manager){var manager=ScriptableObject.CreateInstance<XRManagerSettings>();manager.name=target+" XR Manager";AssetDatabase.AddObjectToAsset(manager,all);settings.Manager=manager;}
   XRPackageMetadataStore.AssignLoader(settings.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",target);
   var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(target);if(xr){xr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;foreach(var feature in xr.GetFeatures<OpenXRFeature>())if(feature.GetType().Name.Contains("OculusTouchControllerProfile")||feature.GetType().Name.Contains("MetaQuestFeature")||feature.GetType().Name.Contains("MetaQuestTouchPlusControllerProfile"))feature.enabled=true;EditorUtility.SetDirty(xr);}
  }
  EditorUtility.SetDirty(all);
 }
 static void CreateScene(){
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.5f,.51f);RenderSettings.reflectionIntensity=.8f;
  var env=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>("Models/Laboratory.fbx"));PrefabUtility.UnpackPrefabInstance(env,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);env.name="Laboratory";
  var table=new GameObject("Adjustable main table");table.transform.SetParent(env.transform,false);
  LaboratoryFinishes.PrepareMaterials();LaboratoryFinishes.ApplyRoom(env);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>("Models/Thorax.fbx"));model.name="ThoraxSpecimen";
  var lodModels=new[]{Asset<GameObject>("Models/Thorax_LOD1.fbx"),Asset<GameObject>("Models/Thorax_LOD2.fbx")};
  var catalog=JsonUtility.FromJson<AnatomyCatalog>(Asset<TextAsset>("anatomy-catalog.json").text);
  var tissueColors=new System.Collections.Generic.Dictionary<string,Color>{{"bone",new(.73f,.67f,.52f)},{"cartilage",new(.68f,.75f,.71f)},{"muscle",new(.37f,.115f,.1f)},{"skin",new(.52f,.34f,.24f)},{"cardiac",new(.4f,.16f,.14f)},{"artery",new(.47f,.13f,.115f)},{"vein",new(.18f,.24f,.34f)},{"airway",new(.64f,.51f,.39f)},{"lung",new(.55f,.31f,.30f)},{"membrane",new(.73f,.63f,.46f)},{"nerve",new(.73f,.58f,.19f)},{"fat",new(.70f,.53f,.23f)},{"fascia",new(.70f,.67f,.58f)},{"duct",new(.51f,.65f,.43f)}};
  foreach(var r in catalog.records){
   var t=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==r.object_name);if(!t)throw new Exception("Missing exported anatomy: "+r.object_name);
   var color=tissueColors.TryGetValue(r.tissue,out var c)?c:Color.gray;t.GetComponent<Renderer>().sharedMaterial=MakeMaterial("Tissue_"+r.tissue,color,0,.36f);
   var structure=t.gameObject.AddComponent<AnatomyStructure>();structure.record=r;structure.interiorMaterial=MakeMaterial("Interior_"+r.tissue,Color.Lerp(color,new Color(.6f,.32f,.24f),.24f),0,.25f);
   var collider=t.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=t.GetComponent<MeshFilter>().sharedMesh;
   var rigid=t.gameObject.AddComponent<Rigidbody>();rigid.isKinematic=true;rigid.useGravity=false;
   if(!r.reference_region){var grab=t.gameObject.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.Instantaneous;grab.throwOnDetach=false;}
   if(r.tissue!="bone")t.gameObject.AddComponent<SoftHandling>();
   var levels=new System.Collections.Generic.List<LOD>{new LOD(.20f,new[]{t.GetComponent<Renderer>()})};
   for(int level=0;level<2;level++){var source=lodModels[level].GetComponentsInChildren<MeshFilter>().FirstOrDefault(m=>m.name==r.object_name);if(!source)throw new Exception("Missing LOD: "+r.id);var child=new GameObject("LOD"+(level+1));child.transform.SetParent(t,false);child.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=t.GetComponent<Renderer>().sharedMaterial;levels.Add(new LOD(level==0?.07f:.005f,new[]{renderer}));}
   var lod=t.gameObject.AddComponent<LODGroup>();lod.SetLODs(levels.ToArray());lod.RecalculateBounds();t.gameObject.SetActive(!r.reference_region&&r.layer>=3&&r.tissue!="membrane");
  }
  var manager=new GameObject("XR Interaction Manager");manager.AddComponent<XRInteractionManager>();
  var session=new GameObject("Dissection session").AddComponent<DissectionSession>();session.specimen=model.transform;session.table=table.transform;
  var rig=new GameObject("Player rig");var capsule=rig.AddComponent<CharacterController>();capsule.radius=.18f;capsule.height=1.7f;capsule.center=new Vector3(0,.85f,0);var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";cameraObject.transform.SetParent(rig.transform);var camera=cameraObject.AddComponent<Camera>();camera.nearClipPlane=.02f;camera.farClipPlane=20;camera.fieldOfView=58;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.4f,.43f,.46f);cameraObject.AddComponent<AudioListener>();cameraObject.AddComponent<UniversalAdditionalCameraData>();
  Bounds bounds=new Bounds(model.GetComponentsInChildren<Renderer>()[0].bounds.center,Vector3.zero);foreach(var r in model.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
  rig.transform.position=bounds.center+new Vector3(.68f,.60f,-.8f);camera.transform.LookAt(bounds.center);
  var controls=rig.AddComponent<LabControls>();controls.session=session;controls.view=camera;controls.rig=rig.transform;
  foreach(var side in new[]{XRNode.LeftHand,XRNode.RightHand}){
   var hand=new GameObject(side.ToString());hand.transform.SetParent(rig.transform,false);var interactor=hand.AddComponent<LabHandInteractor>();interactor.hand=side;interactor.session=session;
   var line=hand.AddComponent<LineRenderer>();line.positionCount=2;line.startWidth=.0015f;line.endWidth=.0008f;line.material=MakeMaterial("Pointer",new Color(.08f,.55f,.48f));
  }
  var examLight=new GameObject("Specimen examination light");examLight.transform.position=bounds.center+new Vector3(.25f,.8f,-.2f);var kl=examLight.AddComponent<Light>();kl.type=LightType.Point;kl.range=2.6f;kl.intensity=1.8f;kl.shadows=LightShadows.None;
  var probes=new GameObject("Laboratory light probes").AddComponent<LightProbeGroup>();var positions=new System.Collections.Generic.List<Vector3>();for(int x=-5;x<=5;x+=2)for(int z=-4;z<=4;z+=2)foreach(float y in new[]{.4f,1.1f,1.8f,2.6f})positions.Add(new Vector3(x,y,z));probes.probePositions=positions.ToArray();
  var probe=new GameObject("Laboratory reflection").AddComponent<ReflectionProbe>();probe.transform.position=new Vector3(0,1.5f,0);probe.size=new Vector3(12,3,9);probe.mode=ReflectionProbeMode.Baked;probe.resolution=128;
  var board=new GameObject("Dissection controls");board.transform.position=bounds.center+new Vector3(-.65f,.15f,.05f);board.transform.rotation=Quaternion.Euler(0,145,0);
  Text("ANATOME",board.transform,new Vector3(.19f,.34f,0),.012f);
  var panel=Text("Select a structure",board.transform,new Vector3(.19f,.22f,0),.006f);controls.panelText=panel;panel.fontSize=48;
  string[] actions={"Explore","Guided","Free","Next","Scalpel","Saw","Undo","Reset","Hide","Isolate","Return","All","Previous","Browse","Systems","Height +","Height -","Open chest"};for(int i=0;i<actions.Length;i++){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=actions[i];b.transform.SetParent(board.transform,false);b.transform.localPosition=new Vector3((i%3)*.19f,-(i/3)*.09f,0);b.transform.localScale=new Vector3(.18f,.073f,.009f);b.GetComponent<Renderer>().sharedMaterial=MakeMaterial("UIButton",new Color(.045f,.12f,.14f));var a=b.AddComponent<LabButton>();a.action=actions[i];a.session=session;Text(actions[i],board.transform,b.transform.localPosition+new Vector3(0,0,-.008f),.008f);}
  PrefabUtility.SaveAsPrefabAsset(model,Base+"/Prefabs/Thorax.prefab");PrefabUtility.SaveAsPrefabAsset(env,Base+"/Prefabs/Laboratory.prefab");
  var lighting=Asset<LightingSettings>("Settings/Lighting.asset");if(!lighting){lighting=new LightingSettings();AssetDatabase.CreateAsset(lighting,Base+"/Settings/Lighting.asset");}lighting.name="Laboratory baked lighting";lighting.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;lighting.lightmapResolution=12;lighting.lightmapMaxSize=1024;lighting.bakedGI=true;lighting.realtimeGI=false;lighting.indirectSampleCount=32;lighting.directSampleCount=16;lighting.environmentSampleCount=32;Lightmapping.lightingSettings=lighting;
  LaboratoryFinishes.ConfigureLights();EditorSceneManager.SaveScene(scene,Base+"/Scenes/ThoraxLab.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Base+"/Scenes/ThoraxLab.unity",true)};
  File.WriteAllText(Path.Combine(Root,"qa/reports/unity-scene.json"),JsonUtility.ToJson(new SceneReport{structures=catalog.records.Length,anatomyCenter=bounds.center,anatomySize=bounds.size,renderers=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length},true));
 }
 [Serializable] class SceneReport{public int structures,renderers;public Vector3 anatomyCenter,anatomySize;}
 static TextMesh Text(string content,Transform parent,Vector3 local,float size){var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localRotation=Quaternion.Euler(0,180,0);var text=go.AddComponent<TextMesh>();text.text=content;text.characterSize=size;text.fontSize=48;text.color=new Color(.45f,.78f,.78f);text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;return text;}
 public static void Bake(){EditorSceneManager.OpenScene(Base+"/Scenes/ThoraxLab.unity");bool ok=Lightmapping.Bake();if(!ok)throw new Exception("Light bake failed");Lightmapping.BakeReflectionProbe(UnityEngine.Object.FindFirstObjectByType<ReflectionProbe>(),Base+"/Settings/LaboratoryReflection.exr");EditorSceneManager.SaveOpenScenes();}
 public static void ConfigureAndBake(){Configure();Bake();}
 public static void BuildWindows(){Build(BuildTarget.StandaloneWindows64,"builds/Windows/ThoraxLab.exe");}
 public static void SyncAnatomy(){
  File.Copy(Path.Combine(Root,"exports/Thorax.fbx"),Base+"/Models/Thorax.fbx",true);File.Copy(Path.Combine(Root,"exports/anatomy-catalog.json"),Base+"/anatomy-catalog.json",true);AssetDatabase.Refresh();
  foreach(var f in new[]{"Thorax_LOD1.fbx","Thorax_LOD2.fbx"})File.Copy(Path.Combine(Root,"exports",f),Base+"/Models/"+f,true);AssetDatabase.Refresh();
  EditorSceneManager.OpenScene(Base+"/Scenes/ThoraxLab.unity");var records=JsonUtility.FromJson<AnatomyCatalog>(Asset<TextAsset>("anatomy-catalog.json").text).records.ToDictionary(r=>r.id);
  foreach(var s in UnityEngine.Object.FindObjectsByType<AnatomyStructure>(FindObjectsInactive.Include,FindObjectsSortMode.None)){s.record=records[s.record.id];s.GetComponent<MeshCollider>().sharedMesh=s.Filter.sharedMesh;EditorUtility.SetDirty(s);}
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
 }
 public static void WindowsPreview(){SyncAnatomy();BuildWindows();}
 public static void FinalWindows(){
  SyncAnatomy();var session=UnityEngine.Object.FindFirstObjectByType<DissectionSession>();var camera=Camera.main;var center=new Vector3(0,1.04f,1.35f);camera.transform.parent.position=center+new Vector3(.70f,.60f,.78f);camera.transform.LookAt(center);
  var board=GameObject.Find("Dissection controls");var facing=camera.transform.position-board.transform.position;facing.y=0;board.transform.rotation=Quaternion.LookRotation(facing);
  PrefabUtility.SaveAsPrefabAsset(session.specimen.gameObject,Base+"/Prefabs/Thorax.prefab");EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();Render();BuildWindows();
 }
 public static void PolishScene(){
  SyncAnatomy();
  LaboratoryFinishes.UpdateEnvironment();
  // Re-save reusable anatomy prefab with the same catalog and native cutting flags.
  var session=UnityEngine.Object.FindFirstObjectByType<DissectionSession>();PrefabUtility.SaveAsPrefabAsset(session.specimen.gameObject,Base+"/Prefabs/Thorax.prefab");
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();Bake();Render();
 }
 public static void BuildAndroid(){Build(BuildTarget.Android,"builds/Android/ThoraxLab.apk");}
 static void Build(BuildTarget target,string relative){var path=Path.Combine(Root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Base+"/Scenes/ThoraxLab.unity"},locationPathName=path,target=target,options=BuildOptions.Development});File.WriteAllText(Path.Combine(Root,"qa/reports/build-"+target+".json"),"{\"result\":\""+report.summary.result+"\",\"errors\":"+report.summary.totalErrors+",\"warnings\":"+report.summary.totalWarnings+",\"bytes\":"+report.summary.totalSize+"}");if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+target);}
 public static void Render(){EditorSceneManager.OpenScene(Base+"/Scenes/ThoraxLab.unity");var camera=Camera.main;var rt=new RenderTexture(1440,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1440,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1000),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Root,"qa/renders/unity-station.png"),texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
}
}
