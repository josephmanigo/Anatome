# Anatome 1.3.0 — VR usability retest

These changes and their original evidence belong to 1.3.0. The current **1.3.1** kit retains them and adds [dissection layer cleanup](VR-DISSECTION-LAYER-FIX.md); use that kit for the next school retest.

This revision responds to the school Quest 3 report: most buttons reacted late while the view stayed smooth; email could not be clearly seen or edited; the scalpel needed ray reach; a selected case choice could not be placed; and the case tray disappeared without a held tool.

## Changed behavior

- Buttons retain activation on trigger/pinch press, zero colour-fade delay, and capture until release. Holding or releasing does not repeat the action.
- Sign-in puts the email field near the top. Its VR keyboard has a fixed text display, caret editing, Clear, Back and Done. Scrolling keys never scrolls the entered text away. Draft email survives returning to the page. The numeric code keypad preserves leading zeroes and enforces six digits. Input fields suppress the Android keyboard in this VR flow. Duplicate async submissions are guarded.
- In Guided Dissection, the scalpel follows the first physical surface hit by a controller ray, up to three metres. Hold trigger and move the ray; release to apply the stroke. Native blade depth, mesh validation, fragment limits and protected-contact monitoring remain in force. A gap, occluder, excessive jump or UI interruption cancels the stroke. A trigger held through pause cannot resume cutting. The bone saw remains hand mounted.
- Case choice pickup preserves pose and its workbench parent. Placement uses the choice's visible grab volume at dock height, so a reasonable overlapping edge counts. The dock highlights and says “Release grip to place” before submission. Hovering, unheld choices and interrupted grips cannot answer. Invalid releases restore the original position and rotation; wrong decisions retain their rationale and grading.
- The case instrument tray stays visible for the whole workspace, independent of the current tool and whether anything is held. Tools remain parented to their workspace during grabs, including pause.
- Original ringless controller meshes replace the capsule/cube proxy assembly: smooth shell, faceplate, thumbstick, buttons, side grip and animated index trigger. Geometry is merged by material; the tracked pose is not smoothed or delayed.
- The dissection guide and its bundled female narration describe the new ray controls.

## Verification

`qa/vr-usability/before.json` preserves failing baseline checks for the scrolling preview, missing keyboard Back, hidden tray and unstable choice-grab settings. The implementation was then iterated against actual Unity Input System/XRI controller events.

`qa/vr-usability/xr-final-report.json` records **2,102 passing checks, zero errors**, without the runtime tracking bypass. This includes **558 rendered button targets** activating in the pointer-down frame, normal and maximum text-size email editing, long and empty input, code entry, all case decision labels, wrong/correct/outside/interrupted drops, assembly, identification, guides, tracking transitions, and a validated native ray cut from 70 cm. An occluder and a pause cancel the pending cut; the three-metre reach is bounded.

The learning-core run passes **826 assertions** for scoring, evidence, ordering, mastery, idempotence, isolation and durable save recovery. Content validation passes **1,669 checks**. Compiled-player scenario, readiness, Android build, APK signature and packaged hash results are recorded in `qa/vr-usability/verification-report.json` and the kit's BUILD-INFO.json after artifact verification.

## School retest boundary

The current build needs a physical Quest 3 retest. No headset was connected during this local run. Local simulation cannot establish headset readability, grip alignment, comfort, perceived latency, sustained FPS, thermal behavior or Android account persistence. Current VR email/code editing is verified; live email delivery and authenticated physical Quest sign-in were not repeated. Earlier backend evidence is explicitly retained separately.

Install **Anatome.apk from the current 1.3.1 kit** as an update and use its included TEST-RESULTS.txt. Retest dissection layer cleanup, email/Back, remote cutting, case choice placement and the always-visible tray. Then complete an Easy activity in each mode and a saved-progress reopen. The app remains a debug-signed school sideload build.
