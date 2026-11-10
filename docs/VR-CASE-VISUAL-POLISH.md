# Case Study visual polish — Anatome 1.4.8

The airway teaching station now uses a continuous sculpted head, jaw, neck and shoulder surface. The separate spherical cheeks, nose and chin have been replaced by integrated contours. Closed eyelids, nostrils, shallow lip detail and folded ears sit on the new warm silicone surface. A fitted graphite and satin-metal plinth supports the bust.

The pleural teaching inserts have rounded boundaries, a gently convex skin surface and continuous cut edges. Skin, adipose tissue, muscle, lung and bone reuse the existing tissue appearance assets. Vascular channels now have rounded visible bends while retaining their original interaction polylines. Geometry remains merged by material, with at most 34,936 triangles and 21 renderers per station. The side instruments and their tray implementation are unchanged.

The head and tissue inserts are illustrative teaching models. This change does not alter source patient anatomy, clinical objectives, tool working ends, target locations, protected-region definitions or authored routes.

## Refresh rate

Quest now defaults to **Auto · highest supported**. Each XR session requests the maximum rate enumerated by that headset, rather than assuming 72, 90 or 120 Hz. The separate Unity application frame target is unset on Quest; OpenXR controls display timing. Supported manual choices remain available in Settings.

On the first upgrade, an old Quest profile saved at the former 72 Hz default moves to Auto. Other preferences and higher custom refresh choices remain intact. A new per-field migration revision preserves later explicit manual choices without changing the existing save/cloud schema. Performance suggestions use the last accepted refresh request instead of a hardcoded 72 FPS target.

The runtime can reject a request, and rendering may run below the selected refresh rate. Automatic maximum is not proof of sustained 90 or 120 FPS. [Unity documents that the VR SDK controls frame rate](https://docs.unity.cn/6000.1/Documentation/ScriptReference/Application-targetFrameRate.html).

## Verification

Evidence is stored in `qa/case-premium-20261014`:

- `before`, `pass1`, `pass2`, `pass3` and `after` retain the visual review iterations.
- `after/report.json` checks all six case stations and compares all 38 equipment anchors and routes with the baseline. It also checks the collider inventory and visual budgets.
- The geometry check inspects 12,377 vertices and 23,456 triangles before upload: finite positions, unit normals and consistent triangle winding. It caught a miniature-scale normal calculation error during iteration.
- `player-final` contains the final compiled-player replay and actual scene captures; `player` retains the earlier visual pass.
- `xr-case-report.json` contains the focused official XR simulator check.
- `xr-refresh-report.json` checks automatic and manual refresh selection, unsupported and rejected requests, session cleanup, the settings selector, and repeated graphics transitions. Native request outcomes are simulated locally; they do not prove hardware acceptance. The first run hit a test-only Text/TMP component mismatch, corrected before the final run.
- Core tests include refresh defaults, old-save migration, preservation of other settings, and persisted manual choices.
- `apk` contains the exact 1.4.8 APK verification. `verification-summary.json` records the final outcomes and hashes.

To repeat the visual check, run `pipeline/run-unity.ps1 -Project ThoraxLab -Method ThoraxLab.Editor.CaseVisualVerification.Capture -Graphics -Log case-premium-review`. Run builds with `-Graphics` because preparation can regenerate source-anatomy menu artwork.

## Headset review

Update the existing app with `builds/AnatomeQuest/Anatome-1.4.8.apk`, preserving its data. In Anatomy Cases, open Asthma or Pneumonia and inspect the bust from the front and side. Check the mouthpiece target, then open Pneumothorax or Pleural Effusion and inspect the skin layers and access route. In Graphics & display, confirm Headset refresh rate shows Auto. Compare the FPS readout across presets and check sustained comfort on the physical headset.

Desktop renders and simulator checks do not confirm physical Quest appearance, sustained frame rate or comfort. The APK uses the existing Android Debug certificate for sideload review.
