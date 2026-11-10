# Anatome natural viewpoint and optional bare-hand input

Historical September 19 revision. The October 4 controller/UI repair restores visible controller proxies and replaces the button interaction path. Consult `docs/VR-CONTROLLER-UI-AUDIT.md` and the current test-kit BUILD-INFO.json for current evidence. Counts and behavior below describe the earlier revision.

This revision keeps controller input, removes the rendered hand/glove/controller models, and adds optional Quest bare-hand input through Unity XR Hands 1.8.1 with OpenXR Hand Tracking and Meta Hand Tracking Aim. The existing package identity and learner storage identity are preserved.

## View and input behavior

- Floor tracking preserves the headset's measured eye height in both seated and standing layouts, including leaning and crouching. The headset supplies its per-eye projection. Only the Editor simulator preview uses a 75-degree lens, and a physical HMD takes priority when both devices exist.
- The original startup regression used a stale Floor pose to compute a negative offset before a Device-relative pose arrived. The fixed path reads the actual origin, handles delayed origin/pose changes and uses a safe fallback only when a usable floor pose is unavailable.
- Controllers: trigger selects/uses, grip holds/releases. Existing controller locomotion remains available.
- Bare hands: index-thumb pinch selects/uses; curl middle/ring/little fingers to hold; opening releases. Pinch-drag scrolls. Object manipulation uses the palm, and pointing uses the runtime's independent aim pose.
- A thin ray appears over interactive targets. There are no rendered hands, gloves or controller models. Instruments still appear when used. Source assets and their notices are retained.
- Tracking loss, a system hand gesture or input-source switching cancels held actions. Release before acting again. Pending teleport or stale movement input cannot execute when controllers return.
- Bare-hand input does not introduce a locomotion gesture. Hand camera tracking, gesture comfort and reach must be checked on the physical Quest.

## Verification

The current machine-readable record is [verification-report.json](verification-report.json).

- `dotnet run --project qa/floor-height/FloorHeightTests.csproj`: **39** camera/locomotion checks using the actual `LearningControls.cs` with explicit Unity/XR doubles.
- `dotnet run --project qa/natural-vr/GestureTests.csproj`: **31** gesture and source-switch checks using the shipping pure state logic.
- `dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json`: **775** existing core/curriculum assertions.
- `LearningVRSimulatorVerification.Run` in Unity 6000.3.19f1: **83** passed checks and zero recorded errors. Actual XRI controller selection, orientation, instrument manipulation, assembly release, tracking recovery and the startup-height replay run in the real scene. The new bare-hand tests exercise the actual XR Hands subsystem, Meta aim devices and shipping input reader with explicitly synthetic joint samples. They do not validate headset cameras.
- The managed compiler checks in `compilation.json` are supplementary; the later actual Unity scene run supersedes their compile-only boundary.
- `initial-unity-report.json` preserves the first run's old test expectation that a held grip remained exposed after recovery. The assertion was updated to verify both the held physical input and the newly filtered logical input, while retaining the no-reacquisition/no-credit checks. The subsequent run passes.
- `old-apk-check/` records the deliberate rejection of the September 7 APK: it lacks the new native hand provider and manifest declarations. The packaging script requires the new Unity checks and APK verification before creating a current kit.

The initial Unity launch was blocked by licensing. A later retry resolved the entitlement and ran the Editor successfully; `natural-vr-unity-verified.log` in the parent QA folder records the successful run. Consult the current report for APK/ZIP build status. Old September 7 archives remain historical and do not include this revision.

## Sources and compatibility

- [Unity XR Hands 1.8 documentation](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.8/manual/index.html). The pinned 1.8.1 package's changelog fixes Meta aim updates for OpenXR 1.16+ and stale tracking data. This project uses OpenXR 1.18.
- [Unity Camera.fieldOfView](https://docs.unity3d.com/ScriptReference/Camera-fieldOfView.html): headset/runtime projection takes precedence in VR.
- The installed package's `Editor/OpenXR/ModifyAndroidManifest.cs` adds `com.oculus.permission.HAND_TRACKING` and the optional `oculus.software.handtracking` feature when hand tracking and Quest support are enabled. APK verification checks both declarations and the ARM64 provider library.

This evidence establishes the local software paths. It does not claim physical Quest acceptance, native headset performance or clinical validation.
