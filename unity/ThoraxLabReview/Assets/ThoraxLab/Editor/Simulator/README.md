# Official XR Device Simulator input assets

These three input action assets are copied without modification from XR Interaction Toolkit 3.6.0, `Samples~/XR Device Simulator`, installed in this Unity project. The package copyright and Unity Companion License notice are retained in LICENSE.md.

They are used only by `LearningSimulatorSetup` in Unity Editor to configure the installed `XRDeviceSimulator` component. The actual simulated HMD and controllers come from Unity's `SimulatedDeviceLifecycleManager`. This folder is excluded from Windows and Android player builds because it is under Editor.

Hold right mouse to manipulate the headset; hold Space for the right controller or left Shift for the left. Left mouse sends the controller trigger; G sends grip. R changes translation/rotation, Control temporarily rotates, Tab cycles devices, M sends menu, and backslash toggles cursor lock. These are the official sample bindings, not desktop anatomy gameplay.
