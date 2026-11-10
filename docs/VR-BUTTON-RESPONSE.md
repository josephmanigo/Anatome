# Quest 3 button response repair — October 7, 2026

The school report was that most buttons reacted late while the headset view stayed smooth. The affected project is `unity/ThoraxLab`. The replacement school build is application version 1.2.1, Android version code 5, revision `immediate-buttons-20261007`. The curriculum remains 2026.10.1.

## Cause and change

The shared world-space pointer sent a button click only when the trigger or pinch was released. A learner holding the trigger therefore waited for an action even though tracking and rendering continued. The first regression run reproduced this through an actual queued XR controller state: the confirmation callback count remained zero while pressed.

Buttons now invoke their normal Unity click event on the press edge. Pointer state clears before the callback so page replacement and modal dismissal cannot repeat the click. A held UI press remains captured when the ray leaves the panel, preventing that same press from activating anatomy or a cutting tool. Release restores ordinary world interaction. Tracking, focus, source-switch, and disabled-button protection remain in place.

The 100 ms button and 120 ms activity-card color fades are removed. Sliders keep their drag lifecycle. Scroll with the thumbstick, scrollbar, or by dragging an empty panel margin. Starting a press on a button activates it immediately; it is no longer a way to begin scrolling. The guide text and its bundled female narration now explain immediate activation.

## Reproduce and verify

From the repository root, the focused real Unity/XRI regression is:

```powershell
.\pipeline\run-unity.ps1 -Project ThoraxLab -Method ThoraxLab.Editor.LearningVRSimulatorVerification.Run -Log button-response-focused -Graphics -NoQuit -ExtraArgs @('--button-response-only')
```

The wrapper starts Unity asynchronously. Wait for it to exit and inspect `qa/vr-xr-simulator-report.json`. Do not start another Editor on the same project while it runs.

The full controller and UI check uses `ThoraxLab.Editor.LearningBuild.VerifyGlovedHands` with the same wrapper and without the extra argument. The full sweep replaces button callbacks temporarily with local counters so it can test the real raycast and Unity event route without invoking account deletion, network requests, or other unrelated actions. Separate navigation, keyboard, confirmation, guide, and learning checks execute their real callbacks.

Evidence is stored under `qa/button-response`:

- `before.json`: the original release-wait regression, with zero callbacks while held.
- `after-focused.json`: the first passing focused reproduction.
- `xr-final-report.json`: the final complete XR integration run, including same-frame button activation, no repeated action, input capture, scrolling, sliders, focus, and tracking.
- `player/`: the newly compiled development player's scenario and widget checks.
- `verification-report.json`: final build, APK, archive, source hashes, and verification results.

The new scroll fixture originally moved only once, which began the ScrollRect drag without a subsequent movement. It now drives both movement stages. The new button fixture also restores the virtual headset/controller poses before the existing assembly tests. Original failing reports are retained; neither repair weakens the interaction assertions.

## School retest

Install the APK from `Anatome-Responsive-Buttons-Quest3-Test-Kit` as an update. The package and signing identity remain compatible with the existing school sideload. Confirm the version and APK SHA-256 in `BUILD-INFO.json`.

Press Menu, Guide, Next, a keyboard key, and an activity button with each controller. The action should occur before releasing. Hold for two seconds and release: exactly one action should occur. Check sliders, the scrollbar, thumbstick scrolling, and dragging an empty margin. Move a held UI press toward the specimen: it must not select or cut anatomy. Then release and confirm a fresh anatomy action works.

No physical Quest is connected during this repair. Input-frame verification in Unity does not measure headset motion-to-photon latency, sustained FPS, thermals, or hand-camera tracking. The new APK still needs the school headset retest; zero device latency is not claimed.
