# Anatome 1.3.1 — dissection layer cleanup

The reported extra body-shaped layers appeared after cutting, grabbing and releasing tissue. Replaying that sequence showed that accepted layers and their cut fragments stayed active beside the specimen. Continuing an activity also restored completed layers at a fixed offset, rebuilding the floating stack.

Guided dissection now retires each completed layer and all of its fragments only after a valid cut, lift and release has exposed the next target and the evaluator accepts the step. Cutting alone, releasing without a lift, or an unsuccessful exposure keeps the tissue available. The original meshes and cut geometry are retained; no source anatomy assets are modified.

Undo snapshots keep completed layers hidden. Restore and Continue reconstruct the completed checkpoint without offset shells. Starting a new activity restores one intact specimen and clears the old fragments. Free sandbox still keeps moved tissue available for inspection and Undo.

## Verification

`qa/dissection-duplicates/xr-red-report.json` records the failing cut-and-grab replay before the fix. The targeted XR replay uses actual simulated controller trigger/grip input, native cuts, and the normal assessment gate. Its final report and the full XR regression report are stored in the same directory. Screenshots after each accepted skin, fat and fascia lift show the remaining anatomy without a stack of completed shells.

The imported-mesh check separately verifies one original object per layer, no increase in total cut tissue volume, and one active cut renderer. Those checks distinguish completed tissue left in the work area from duplicated source objects or an intact display mesh rendered over a cut.

## Quest 3 retest

Install the **1.3.1** APK as an update using Meta Quest Developer Hub. Open Chest wall → Easy → Guided practice:

1. Point at skin, hold trigger and draw a short cut, then release the trigger. Skin must remain visible.
2. Grip and release without lifting. The step must remain unfinished.
3. Grip, move the cut tissue aside until the next layer is exposed, then release. The completed layer and its fragments should clear from the work area.
4. Repeat for subcutaneous tissue and fascia. The final view should show the muscles without floating body-shaped copies.
5. Repeat a partial activity using Undo, Restore activity position and Continue. Completed layers must stay cleared; a new activity must restore the intact specimen once.

This is a debug-signed school sideload build. Local Unity/player verification does not replace this physical-headset retest or establish sustained Quest frame rate.
