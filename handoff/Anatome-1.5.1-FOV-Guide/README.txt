ANATOME 1.5.1 - FOV controls and visual dissection guide
Android version code: 19
Package: org.thoraxlab.learning

INSTALL AS AN UPDATE
Use Anatome-1.5.1-Production.apk from this folder. Connect the Quest to Meta Quest Developer Hub and install this APK over the existing Anatome app. Keep app data: do not uninstall Anatome or clear its storage. With an authorized ADB connection, the equivalent command is:
  adb install -r Anatome-1.5.1-Production.apk
The package uses the existing classroom release certificate and verified signing lineage. Actual retained-progress installation has not been tested for this version.

NEW CONTROLS
Settings > VR comfort > Comfort vignette: On > Comfort view (degrees).
Range 40-100, default 55. Higher values leave a wider clear area while moving. Select Apply to save. This is a movement comfort mask; it does not change Quest lenses or runtime projection.
The Unity Editor XR Device Simulator also has Simulator FOV, range 50-110, default 75. That control is intentionally absent on a physical headset.

VISUAL DISSECTION GUIDE
Enable Settings > Learning & accessibility > Practice hints, then Apply.
Start Guided Dissection in Lesson or Practice. At an exposure step, begin at the ring, hold the trigger, sweep along the arrow, then release. After a valid cut creates a detached chunk, follow the Grip and lift cue; lift clear and release.
The guide never cuts or awards credit automatically. It hides during assessments and when hints are off. Whole tissue layers remain anchored; only detached chunks earn removal credit after a valid lift/release.

VERIFICATION
See TEST-RESULTS.json for exact build and test outcomes.
Local checks include Unity's official XR simulator, core settings/scoring tests, controls tests, the signed APK, fresh packaged IL2CPP metadata, and release tracking gates.
No physical Quest was connected during verification. This package is ready for controlled device retesting, not proof of stereo comfort or sustained headset performance.

QUEST RETEST
1. Update without clearing data; launch and confirm existing login/progress remain. Restart once and confirm settings persist.
2. Compare Comfort view 40, 55, and 100 during walking/turning; verify fade-out, Off, Apply, and Cancel.
3. Follow a real trigger cut, grip, lift, and release. Verify removal credit and Undo; inspect ring, arrow, and text in stereo and at large text/high contrast.
4. Verify anatomical side/occlusion, hints Off, assessment hiding, pause, and activity exit. Check sustained frame timing and temperature on each target Quest model.

APK SHA-256
7d033d45040065acc6d2669abd70c531aff9b8d7352f722a68e0e384dd48af14
