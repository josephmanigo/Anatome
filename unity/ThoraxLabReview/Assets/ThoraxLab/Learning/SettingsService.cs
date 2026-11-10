using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThoraxLab.Learning
{
    public sealed class SettingsService : IDisposable
    {
        readonly AtomicFileStore<SettingsCollection> store;
        UniversalRenderPipelineAsset pipeline;
        UnityEngine.Rendering.RenderPipelineAsset originalPipeline;
        public SettingsCollection Collection { get; private set; }
        public SettingsProfile Current { get; private set; }
        public string RecoveryNotice { get; private set; }
        bool recoveryReadOnly;
        public string Platform => Application.platform == RuntimePlatform.Android ? "Quest" : "Windows";
        public event Action Changed;
        public SettingsService(string root)
        {
            store = new AtomicFileStore<SettingsCollection>(Path.Combine(root, "settings.save"), value => JsonUtility.ToJson(value), JsonUtility.FromJson<SettingsCollection>, Validate);
            try { Collection = store.Load(() => new SettingsCollection()); }
            catch (InvalidDataException)
            {
                // Settings are recoverable preferences. Preserve both damaged files
                // before permitting a new save; progress and credentials are separate.
                Collection = new SettingsCollection();
                RecoveryNotice = "Your settings could not be read. Safe defaults are active; damaged copies have been preserved.";
                try
                {
                    string suffix = ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    foreach (string path in new[] { store.FilePath, store.FilePath + ".bak" })
                        if (File.Exists(path)) File.Copy(path, path + suffix, false);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    recoveryReadOnly = true;
                    RecoveryNotice = "Safe default settings are active. Free space or restore write access, then restart to save settings. Your original files are preserved.";
                }
            }
            if (store.RecoveredBackup) RecoveryNotice = "Settings recovered from your saved backup.";
            Current = Collection.profiles.FirstOrDefault(p => p.id == Collection.selectedId && p.platform == Platform) ?? Collection.profiles.FirstOrDefault(p => p.platform == Platform);
            if (Current == null) { Current = new SettingsProfile { name = Platform + " balanced", platform = Platform }; Current.SetPreset(GraphicsPreset.Medium); Collection.profiles.Add(Current); Collection.selectedId = Current.id; }
            originalPipeline = QualitySettings.renderPipeline;
            if (originalPipeline is UniversalRenderPipelineAsset original) { pipeline = UnityEngine.Object.Instantiate(original); QualitySettings.renderPipeline = pipeline; }
            ApplyRuntime(Current, false);
        }
        static void Validate(SettingsCollection collection)
        {
            if (collection == null || collection.schemaVersion != 1 || collection.profiles == null || collection.profiles.Count > 32) throw new InvalidDataException("Invalid settings collection.");
            if (collection.profiles.Select(p => p.id).Distinct().Count() != collection.profiles.Count) throw new InvalidDataException("Duplicate settings profile.");
            foreach (var profile in collection.profiles) profile.Validate();
        }
        public SettingsProfile Draft() => Clone(Current);
        public static SettingsProfile Clone(SettingsProfile source) => JsonUtility.FromJson<SettingsProfile>(JsonUtility.ToJson(source));
        public void Apply(SettingsProfile profile)
        {
            EnsureWritable();
            if (profile.platform != Platform) throw new InvalidOperationException("This profile belongs to another platform.");
            profile.Validate(); profile.updatedUtc = DateTime.UtcNow.ToString("o");
            var candidate = JsonUtility.FromJson<SettingsCollection>(JsonUtility.ToJson(Collection));
            int index = candidate.profiles.FindIndex(p => p.id == profile.id);
            if (index >= 0) candidate.profiles[index] = Clone(profile); else { if (candidate.profiles.Count >= 32) throw new InvalidOperationException("Delete a profile before adding more."); candidate.profiles.Add(Clone(profile)); }
            candidate.selectedId = profile.id;
            store.Save(candidate);
            Collection = candidate; Current = Collection.profiles.First(p => p.id == profile.id);
            ApplyRuntime(Current, true); Changed?.Invoke();
        }
        public void PreviewDisplay(SettingsProfile profile) { profile.Validate(); ApplyRuntime(profile, true); }
        public void RevertDisplay() => ApplyRuntime(Current, true);
        public SettingsProfile Duplicate(string name)
        {
            var value = Draft(); value.id = Guid.NewGuid().ToString("N"); value.name = name; Apply(value); return Draft();
        }
        public void Delete(string id)
        {
            EnsureWritable();
            if (Collection.profiles.Count(p => p.platform == Platform) <= 1) throw new InvalidOperationException("Keep at least one profile for this platform.");
            var candidate = JsonUtility.FromJson<SettingsCollection>(JsonUtility.ToJson(Collection));
            candidate.profiles.RemoveAll(p => p.id == id && p.platform == Platform);
            candidate.selectedId = Current.id == id ? candidate.profiles.First(p => p.platform == Platform).id : Current.id;
            store.Save(candidate);
            Collection = candidate; Current = Collection.profiles.First(p => p.id == candidate.selectedId);
            ApplyRuntime(Current, true); Changed?.Invoke();
        }
        void EnsureWritable() { if (recoveryReadOnly) throw new IOException(RecoveryNotice); }
        public void ApplySceneSettings() => ApplyRuntime(Current, false);
        public void Dispose()
        {
            if (!pipeline) return;
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = originalPipeline;
            UnityEngine.Object.Destroy(pipeline); pipeline = null;
        }
        void ApplyRuntime(SettingsProfile p, bool display)
        {
            AudioListener.volume = p.masterVolume;
            QualitySettings.globalTextureMipmapLimit = p.textureLimit;
            QualitySettings.vSyncCount = Platform == "Windows" && p.vsync ? 1 : 0;
            Application.targetFrameRate = Platform == "Quest" ? p.refreshRate : p.frameCap;
            if(Platform=="Quest"&&QuestRefreshFeature.Active&&!QuestRefreshFeature.Active.Apply(p.refreshRate))Debug.LogWarning("Requested headset refresh rate is unavailable; the runtime keeps its supported rate.");
            if (pipeline) { pipeline.renderScale = p.renderScale; pipeline.msaaSampleCount = p.msaa; pipeline.shadowDistance = p.shadows ? p.shadowDistance : 0; }
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.name == "Specimen examination light") { light.intensity = VRSpecimenPresentation.ExaminationIntensity * p.brightness; light.shadows = p.shadows ? LightShadows.Soft : LightShadows.None; }
            if (display && Platform == "Windows") Screen.SetResolution(p.width, p.height, p.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            if (LabRuntime.Instance && LabRuntime.Instance.Session) LabRuntime.Instance.Session.SetTableHeight(p.tableHeight);
            var controls=UnityEngine.Object.FindFirstObjectByType<LearningControls>();if(controls)controls.ApplyComfort();
        }
    }
}
