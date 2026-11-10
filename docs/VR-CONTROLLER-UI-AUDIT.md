# Anatome controller and UI audit — October 4, 2026

The school test reported invisible controllers and unreliable button activation. This repair applies to `unity/ThoraxLab` and its isolated `ThoraxLabReview` copy. The artifact revision is `controller-ui-repair-20261004`, application version 1.0.1, Android version code 2. The exact APK and ZIP hashes are recorded in `qa/controller-ui-audit/verification-report.json` and the supplied kit's `BUILD-INFO.json`.

## Findings and repairs

1. **Controllers were deliberately invisible.** `LabHandInteractor.Start` removed the glove component, and the prior scene had no controller model. Both tracked controllers now have compact original controller proxies, a persistent teal aim ray while empty-handed, and a target marker over interactive surfaces. The proxies have no active colliders. They disappear on tracking loss or when bare-hand input owns that side. Controller materials are explicit bundled assets so player shader stripping cannot remove them.
2. **Natural hand movement canceled clicks.** Every content button inherited its surrounding ScrollRect as a drag handler. The pointer code disabled drag thresholds and started a drag after movement greater than 0.1 screen pixel. A real Input System controller test reproduced the failure: move 2 mm while holding trigger over the guest button, release, and the application remains at sign-in. The repair measures deliberate movement in panel coordinates, tolerates small movement, retains immediate slider dragging, and sends pointer-up when scrolling cancels a button press. Clicks require release over the original target.
3. **UI hit testing used the headset camera as a mouse.** Controller rays were converted to screen coordinates and passed to a regular GraphicRaycaster. The repair uses the installed XRI `TrackedDeviceGraphicRaycaster` and `TrackedDeviceEventData`, so hit testing starts with the actual 3D controller or hand aim ray. Unity's mask and Graphic filtering still apply. Pointer state and reusable hit buffers are independent for each controller.
4. **Controller pointing used the grip pose.** OpenXR provides independent grip and aim poses. The shipping input reader now uses the actual handed OpenXR Input System controller, its trigger/grip controls, and its separate pointer pose. The grip pose still drives anatomy and instruments. The legacy adapter remains available with analog fallback. Meta Aim and HandInteraction devices remain on the bare-hand path. A synthetic fixture using Unity's actual OpenXR Touch layout checks these bindings; it does not establish physical controller alignment.
5. **Input interruption and UI lifetime needed hardening.** Page replacement immediately disables old controls before deferred destruction. Modals block the underlying page. Pointer state is cleared before click callbacks, preventing reentrant release. Panel dragging belongs to the initiating pointer, so the other controller cannot cancel it. Tracking/source changes cancel pending actions and require release before reacquisition. The final report includes application-focus interruption tests.

The performance monitor now persists its existing measurements when Android pauses, stops sampling while paused, and skips the resume transition frame. This repairs measurement persistence; it does not establish a headset frame rate.

API reference: [Unity's Tracked Device Graphic Raycaster](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/tracked-device-graphic-raycaster.html) supports canvas hit testing from 3D tracked devices. The implementation was checked against this project's installed XRI 3.6.0 source.

## Verification and boundaries

The final machine-readable report is `qa/controller-ui-audit/verification-report.json`. It records the fresh simulator report, player scenarios, core/curriculum/database/native checks, build results, APK inspection, source parity, archive identity, and open production gates. Use its current timestamps and hashes. Earlier reports remain historical evidence.

The controller/button reproduction lives in `LearningVRSimulatorVerification.VerifyButtonJitter`. Expanded target auditing sends actual queued controller events through the shipping input reader and interactor, checks pointer-down on each rendered button, then cancels without invoking unrelated network, account deletion, export, or external-link actions. Separate actual click tests exercise guest entry, orientation, menus, keyboard, confirmations and both controllers. Scenario fixtures exercise all 72 learning variants in the development player with an explicitly disclosed tracking bypass; the separate XR integration run has no runtime tracking bypass.

The first player run passed 72/72 scenarios but failed its widget fixture because it aimed at a slider clipped below the viewport. The native tracked raycaster correctly rejected that target. A focused actual-controller test passed slider movement from 0.20 to 0.80, release stopping, and thumbstick scrolling. The development fixture now scrolls its target into view before dragging. `player-initial-report.json` retains the initial failure; the final player report records the corrected full rerun.

The Windows development player runs without a desktop OpenXR runtime or connected headset. Its startup log reports `XR_ERROR_RUNTIME_UNAVAILABLE`; its disclosed QA mode then exercises the learning scenarios and world-space UI. That run does not establish desktop or Quest headset operation. Unity's final player BuildReports contain zero errors and warnings, while the Android Editor compilation log retains obsolete `XRDeviceSimulator` warnings for the existing Editor test fixture. These are separate observations, not a claim that every log line is warning-free.

Run the checks from the workspace root:

```powershell
rtk dotnet run --project qa/floor-height/FloorHeightTests.csproj
rtk dotnet run --project qa/natural-vr/GestureTests.csproj
rtk dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json
rtk proxy node qa/vr-content-tests.mjs
rtk proxy node qa/backend-tests/test.mjs
.\pipeline\run-unity.ps1 -Project ThoraxLab -Method ThoraxLab.Editor.LearningBuild.VerifyGlovedHands -Log controller-ui-xr -Graphics -NoQuit
```

`VerifyGlovedHands` is the existing integration entry point; the current test checks controller proxies and optional bare-hand input. After it passes, build Windows release/development players and Android with the existing `LearningBuild` methods. Run the development player's `--learning-qa --qa-output` suite. Verify the normal Windows tracking gate with `pipeline/verify-vr-release-gate.ps1`. Verify the APK with `pipeline/verify-learning-apk.py --expected-version 1.0.1` before `pipeline/package-school-test.ps1`.

## Production acceptance remains open

The old school build failed a physical test. The repaired APK needs a new physical Quest 3 result bound to its hash: cold startup, both visible controllers, all relevant screens, natural click/scroll behavior, controller/hand switching, tracking and focus recovery, readable text, comfortable reach, and save/reopen. A representative headset session of at least 15 minutes and an on-device profiler must establish the sustained native 72 FPS target and thermal behavior.

The sideload APK uses the existing Android Debug signing identity so it can update the school installation. Production distribution requires the publisher's backed-up release key and expected public certificate fingerprint. `--require-release-signing` must pass against the final distribution APK. A new signing identity can prevent an installed debug build from accepting updates; preserve learner progress before changing distribution identity.

Local database, encrypted persistence and scenario checks do not establish fresh email delivery, concurrent real-account synchronization, or secure-storage behavior on the school's Quest. Those connected acceptance checks remain explicit. Source-backed educational content retains its documented reconstruction limits and formative-use boundary. Automated checks do not establish clinical validity or convert this learning application into a medical device.
