# Quest startup viewpoint below the floor

The school tester reported that Anatome starts under the floor immediately on a physical headset. The exact headset log is unavailable. The shipped `LearningControls` default seated flow independently reproduces this symptom with delayed XR origin data.

## Reproduction

Run `dotnet run --project qa/floor-height/FloorHeightTests.csproj` from the repository root. This compiles the actual runtime `LearningControls.cs` with explicit engine/XR test doubles. It does not claim a Unity scene or physical Quest run.

Before the fix (`repro-before.txt`), a floor-relative 1.65 m head pose establishes a seated camera height of 1.25 m and requests Device tracking. The rig offset is -0.40 m. When the eye-relative 0 m Device pose arrives in the next frame, the existing code leaves that negative offset in place: the camera is 0.40 m below the floor.

## Fix

- Request Floor tracking for both seated and standing layouts; seated layout adjusts application height without switching the runtime to Device mode.
- Read actual subsystem mode, handle unsupported/rejected Floor requests and subscribe to tracking-origin updates.
- Recalibrate with the subsequent tracked pose and hold the last valid camera pose during a reference change.
- Recover from delayed pose jumps, including runtimes which deliver the pose after their origin notification.
- Preserve normal head movement and crouching; applying unrelated preferences no longer resets height.
- Recenter view and height together from the VR comfort settings.
- Emit a concise `[Anatome XR] Height aligned` record for future headset diagnosis.

Unity distinguishes floor-relative and device-relative coordinates: [TrackingOriginModeFlags](https://docs.unity3d.com/ScriptReference/XR.TrackingOriginModeFlags.html). The installed XR Core Utils source likewise reads actual origin mode and listens for `trackingOriginUpdated`.

## Evidence and delivery status

- `repro-before.txt`: original negative camera-height reproduction.
- `repro-after.txt`: 28 checks of the actual camera component using engine/XR doubles.
- The existing 775 core assertions also pass.
- `compilation.json`: managed compilation against installed Unity references for runtime, Editor verification and Android, using Unity's bundled Roslyn compiler. Existing XR Device Simulator deprecation warnings are separate from the fix.
- The official XR simulator verification now includes a startup height regression. It has **not run yet**: Unity exits with code 198, "No valid Unity Editor license found." See `../floor-height-repro.log`.
- **No replacement APK or ZIP has been built.** The September 7 school ZIP still contains the reported bug. Do not redistribute it as fixed.
- After Unity license activation, run the simulator verification, build Android, inspect the resulting APK, package a new school kit, and record its new hash before asking for a physical retest.

Main and isolated review projects both contain the source fix. No learner progress, accounts or saved preferences were changed by the regression harness.
