# VR view controls and visual dissection guide

Verified locally on 22 October 2026 (Asia/Shanghai). This change adds an adjustable simulator view, a movement comfort aperture, and a visual guide for the current Guided Dissection exposure step. It extends the existing Unity interface and preserves its shared settings controls, typography, and colors.

## Use the controls

1. Open **Settings > VR comfort**. In the Unity Editor's active XR Device Simulator, set **Simulator FOV (degrees)** between **50 and 110**; the default is **75**. This control changes the computer preview's vertical camera field of view and appears only for the simulator.
2. On Quest or PC VR, enable **Comfort vignette**, then set **Comfort view (degrees)** between **40 and 100**; the default is **55**. Higher values leave a wider clear area. The peripheral mask fades in during smooth walking or smooth turning and fades out when movement stops. It does not change the headset lenses or runtime projection.
3. Select **Apply** to save and use the draft settings. **Cancel** keeps the saved settings. **Reset this category** restores the VR comfort defaults in the draft; select Apply to retain them. Profiles from before this change receive the new defaults without a schema migration; `schemaVersion` remains `1`.

Unity defines `Camera.fieldOfView` as a vertical angle; horizontal coverage also depends on the viewport aspect ratio. VR runtimes can supply their own camera projection and ignore camera FOV assignments. Accordingly, this implementation assigns camera FOV only in the simulator and changes a separate mask for headset comfort. See [Unity's Camera.fieldOfView reference](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/camera/fieldofview).

## Follow the dissection guide

1. Open a Guided Dissection learning or practice activity. Enable **Settings > Learning & accessibility > Practice hints**, then Apply.
2. At the current exposure step, locate the start ring on the named tissue. Hold the tool trigger, sweep along the arrow, and release. The route indicates a possible surface stroke; the learner still performs the native cut.
3. After a successful cut creates the current objective's chunk, follow **Grip and lift**. Grip the marked chunk, lift it clear, and release. A cut or displayed guide alone earns no removal credit.
4. Use Undo to restore the current sweep stage. If the guide asks for a clear tissue surface, move around the specimen until the named region is visible.

The guide has no colliders and submits no scoring evidence. It hides when hints are off, during assessment, while interaction is paused or blocked, and while the session is busy. Leaving the activity clears its objects and materials. The surface search checks the actual target collider, anatomical left/right region, and covering geometry; it does not draw a route through a covering layer.

The final route is a front-facing lateral stroke of approximately 30 mm. A distinct start ring and arrow show direction, while a dark backing keeps the instruction readable over tissue and the room. Text scale and high contrast use the existing preferences. The ring's private material disables face culling to keep its loop complete; depth testing remains enabled.

The search tries at most eight eligible route starts and performs at most 480 raycasts per probe. It caches a stable route and rechecks observer movement at most every 0.75 seconds. Objective, mesh, and target-position changes invalidate the cached route. These bounds limit work; they do not establish Quest frame timing.

## Verification and exact scope

| Evidence | Result | What it establishes |
| --- | --- | --- |
| [Core tests](../qa/fov-guide/core-tests-release.txt) | 905 assertions passed in the release verification run | Core settings validation and existing scoring, persistence, mastery, and account-isolation checks passed. |
| [Controls tests](../qa/fov-guide/controls-tests-release.txt) | 65 checks passed in the release verification run | The actual `LearningControls.cs` passed checks with engine/XR doubles, including camera and locomotion behavior. This does not test physical controllers. |
| [Compilation report](../qa/fov-guide/compilation.json) | Seven compilation targets passed; no errors | Unity Roslyn compiled the Editor, Android, and player source targets against installed cached references. Five existing `XRDeviceSimulator` deprecation warnings remain in the Editor verification sources. This is not a Unity asset import or full build. |
| [Copied-player report](../qa/fov-guide/player-verified/report.json) | 25 checks passed; `errors` is empty | Fresh managed code exercised settings, comfort aperture, layout, route sides and occlusion, a real native cut, lift-stage transition, Undo, hiding, teardown, and six sampled tissue surfaces in a copied PC development player. |
| [Focused XR simulator report](../qa/fov-guide/xr-simulator-report.json) | 49 checks passed; no errors; runtime QA bypass disabled | Unity Editor ran official XRI simulated devices through the normal input path, including FOV Apply, legacy profiles, comfort aperture, native cutting along the guide, lift-stage transition, Undo, and assessment hiding. |
| [Full XR regression report](../qa/fov-guide/xr-simulator-full-report.json) | 2,976 checks passed; no errors; runtime QA bypass disabled | Existing menus, orientation, controller and synthetic-hand input, settings, cutting, grabbing, walking, and graphics transitions passed in the official XR simulator. |
| [Android build](../qa/fov-guide/release/build-report.txt) and [signed APK checks](../qa/fov-guide/release/learning-apk-verification.json) | Fresh build succeeded with zero errors/warnings; 30 APK checks passed | Version 1.5.1/code 19 is signed by the existing publisher identity, with ARM64 cutting/XR libraries and release privacy settings validated. |
| [Packaged feature checks](../qa/fov-guide/apk-feature-verification.json) | 11 checks passed | The signed APK passes ZIP CRC, contains the FOV and dissection guide code, matches fresh Android IL2CPP metadata, and excludes the added QA fixtures. |
| [Release tracking gate](../qa/fov-guide/release-tracking-gates.json) | Passed | The fresh Android release assembly retains the XR tracking gate, disables simulator and verification bypasses, and excludes the Editor transport hook. |
| Independent source and visual review | Sampled source flows reviewed; final visual-fix verdict was `ship` | The visual verdict covers the scored start-ring, direction, and contrast fixes in the final PC captures. It is not complete device acceptance. |

The copied-player check placed fresh managed DLLs in an isolated copy of an existing Unity Mono development player at `.tmp/fov-guide-player`. It used cached scene/assets and an explicit QA tracking bypass. QA data stayed within the test output. The reported surface-probe timings are measurements on this PC, not Quest benchmarks.

Final captures: [settings](../qa/fov-guide/player-verified/settings.png), [large text and high contrast](../qa/fov-guide/player-verified/settings-large-contrast.png), [sweep stage](../qa/fov-guide/player-verified/sweep.png), and [lift stage](../qa/fov-guide/player-verified/lift.png).

The initial Editor runs failed because the saved Unity license had no currently valid entitlement and the Editor could not refresh it without an access token. After the user completed Unity Hub sign-in, the focused XR test and fresh Android build succeeded. The Android build reported zero errors and zero warnings. Signing used the existing classroom identity and preserved all 1,346 application entries; the signed APK passed 30 strict package/signature checks and signing-lineage checks for Android API 29–36.

The new artifact is **Anatome 1.5.1, Android version code 19**, package `org.thoraxlab.learning`, at [Anatome-1.5.1-Production.apk](../builds/AnatomeSchool/Anatome-1.5.1-Production.apk). The [handoff ZIP](../handoff/Anatome-1.5.1-FOV-Guide.zip) contains that same APK, update instructions, and a test summary. APK SHA-256: `7d033d45040065acc6d2669abd70c531aff9b8d7352f722a68e0e384dd48af14`. Earlier issued APKs remain unchanged. No Quest was connected through ADB during verification, so physical operation, update/progress retention, stereo comfort, controller usability, and sustained device performance remain unverified for this version.

## Next verification

To repeat the focused simulator check, run this command from the repository root:

```powershell
./pipeline/run-unity.ps1 -Project ThoraxLab -Method ThoraxLab.Editor.LearningVRSimulatorVerification.Run -Log fov-guide-xr -Graphics -NoQuit -ExtraArgs '--fov-guide-only'
```

Install the new APK as an update without uninstalling or clearing app data, then complete these four checks on each target Quest:

1. Confirm Simulator FOV is absent. Compare Comfort view at 40, 55, and 100 degrees during smooth walking and turning; check fade-out, vignette Off, Apply, Cancel, and restart persistence.
2. Perform a real trigger sweep, grip, lift, and release on the current objective. Confirm removal credit requires the real action and Undo restores the appropriate guide stage.
3. Check left/right routes and occlusion while changing viewpoint. Confirm hints Off, assessment, pause, busy state, and activity exit hide or clear the guide. Inspect the ring, arrow, and text in stereo at normal and large text/high contrast.
4. Record sustained frame timing and thermal behavior while cutting and moving around the specimen. Observe route updates and comfort-mask behavior with the actual controllers; retain the report and headset/build details.

Implementation authority: `SettingsProfile.cs`, `SettingsService.cs`, `LearningControls.cs`, `ComfortVignette.cs`, `LabUI.Settings.cs`, `GuidedInteractionCoach.Dissection.cs`, and `VRSpecimenPresentation.cs` under `unity/ThoraxLab/Assets/ThoraxLab/Learning` (`SettingsProfile.cs` is under `Core`).
