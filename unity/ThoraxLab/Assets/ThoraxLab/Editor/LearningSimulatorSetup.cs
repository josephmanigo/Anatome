#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ThoraxLab.Editor
{
    // Uses Unity's installed XR Device Simulator and its unmodified sample controls.
    // Assets live under Editor and cannot enter a Windows or Android player build.
    [InitializeOnLoad]
    public static class LearningSimulatorSetup
    {
        const string Preference = "ThoraxLab.XRDeviceSimulator";
        const string Menu = "Anatome/XR Device Simulator (Editor only)";
        static LearningSimulatorSetup() { EditorApplication.playModeStateChanged += OnPlayMode; }
        [MenuItem(Menu)]
        static void Toggle() { EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true)); }
        [MenuItem(Menu, true)]
        static bool Validate() { UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Preference, true)); return true; }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Install();
        }
        public static void Install()
        {
            if (!EditorPrefs.GetBool(Preference, true) || Environment.GetCommandLineArgs().Contains("-selftest") ||
                !UnityEngine.Object.FindFirstObjectByType<DissectionSession>() ||
                UnityEngine.Object.FindFirstObjectByType<XRDeviceSimulator>() || InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid) return;
            var simulatorActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ThoraxLab/Editor/Simulator/XR Device Simulator Controls.inputactions");
            var controllerActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ThoraxLab/Editor/Simulator/XR Device Controller Controls.inputactions");
            var handActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/ThoraxLab/Editor/Simulator/XR Device Hand Controls.inputactions");
            if (!simulatorActions || !controllerActions || !handActions) { Debug.LogError("Anatome XR simulator controls were not imported."); return; }
            var go = new GameObject("XR Device Simulator - Editor testing only") { tag = "EditorOnly" };
            go.SetActive(false);
            var lifecycle = go.AddComponent<SimulatedDeviceLifecycleManager>();
            lifecycle.handTrackingCapability = false; // Anatome currently teaches controller interaction.
            var simulator = go.AddComponent<XRDeviceSimulator>();
            simulator.deviceSimulatorActionAsset = simulatorActions;
            simulator.controllerActionAsset = controllerActions;
            simulator.handActionAsset = handActions; // Required by the official component even in controller mode.
            simulator.cameraTransform = Camera.main ? Camera.main.transform : null;
            var refs = go.AddComponent<SimulatorReferenceCleanup>();
            var actions = simulatorActions.Concat(controllerActions).ToDictionary(a => Normalize(a.name), a => a);
            foreach (var property in typeof(XRDeviceSimulator).GetProperties())
            {
                if (property.PropertyType != typeof(InputActionReference) || !property.CanWrite || !property.Name.EndsWith("Action", StringComparison.Ordinal)) continue;
                if (!actions.TryGetValue(Normalize(property.Name.Substring(0, property.Name.Length - 6)), out var action)) continue;
                var reference = InputActionReference.Create(action); refs.references.Add(reference); property.SetValue(simulator, reference);
            }
            simulator.handControllerModeAction = null;
            go.SetActive(true);
            Debug.Log("Anatome: official XRI XR Device Simulator active. Keyboard and mouse drive simulated XR devices; desktop gameplay is disabled.");
        }
        static string Normalize(string value) => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
    public sealed class SimulatorReferenceCleanup : MonoBehaviour
    {
        public readonly List<InputActionReference> references = new List<InputActionReference>();
        void OnDestroy() { foreach (var reference in references) if (reference) Destroy(reference); }
    }
}
#endif
