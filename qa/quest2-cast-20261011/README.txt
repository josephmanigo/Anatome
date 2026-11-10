ANATOME 1.4.4 - QUEST FEEDBACK UPDATE
Historical results for 1.4.4. The subsequent settings/panel/chunk update is documented in SETTINGS-1.4.5.txt and verification-summary.json.
Package: org.thoraxlab.learning. Android version code: 12.

Changes
- Guided Dissection shows cut feedback above its instructions. Tool, side, stroke, and native-cut rejection messages no longer start below the initial visible panel area.
- A completed attempt without a pass says "Needs review". Active and interrupted attempts retain their actual status without being described as completed.
- Learning content, scoring rules, cut geometry protections, and saved settings are unchanged.

Verification
- Before the fix, the added viewport regression failed: cut feedback was below the visible panel.
- After the fix, all 31 focused chest/height checks passed.
- All 2,331 XR controller and interaction regression checks passed, with zero test errors and runtimeQaBypass=false.
- APK verification for this version is in apk/learning-apk-verification.json. Version 1.4.4 was installed during this session before the later settings/panel/chunk changes.

Observed on the connected Quest 2 before this update
- Installed 1.4.3 (version code 11) matched SHA-256 1591a24fe62855e685601ec28a38b2f6ec5e1d5eeb8b042e9421681aa7e3e99b.
- Head movement, tool selection, menu navigation, scrolling, and settings sliders responded.
- The user reported holding the index trigger while the scalpel moved over skin, with the removal meter at zero. This alone did not prove a cutting-engine defect: cut rejection feedback was below the visible panel, and removal also requires lifting and releasing a detached chunk.
- A progress entry displayed both Completed and In progress or revision.
- No Unity/Android crash was found in the inspected app-log samples. A 121-sample VrApi window averaged 71.69 FPS, with a minimum of 64 and maximum of 73. These samples are not a sustained-performance or comfort qualification.

Physical retest
1. Open Guided Dissection, Chest wall, Easy, Practice.
2. Select the bone saw and attempt a short skin stroke, then release the trigger. The instruction to select the scalpel must be readable without scrolling.
3. Select the scalpel. Make a short stroke entirely on the requested side, release the trigger, then grip, lift, and release the detached chunk. Confirm that removal credit changes only after a valid chunk removal.
4. Check Your progress: a completed non-passing attempt should say Needs review.

Install Anatome.apk as an update. Do not uninstall or clear app data to update.
This APK uses Android Debug signing for controlled sideload testing. Windows binaries were not rebuilt for this Quest update.
