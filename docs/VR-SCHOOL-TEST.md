# Anatome: school Quest 2 and Quest 3 test

You can prepare with Unity's XR Device Simulator at home and borrow the school's Quest 3 for the physical test. The school computer only needs Meta Quest Developer Hub (MQDH) to install the existing APK; Unity is not required for this route.

## Arrange before the test

Ask the instructor or lab technician to reserve the Quest 3 with both controllers and confirm that they can install your local APK. Have the headset administrator enable Developer Mode and authorize the test computer for USB debugging. A managed school headset may restrict these options; the administrator should handle its account and device settings.

Bring the school test ZIP on a USB drive, a USB-C data cable, and access to the email inbox you will use if testing saved progress. Arrange Wi-Fi for first sign-in. Guest practice is available without signing in. Charge the headset and controllers before the session. Reserve 30–45 minutes for setup and the first functional test; this is a suggested schedule, not a performance benchmark.

Official setup guide: https://developers.meta.com/horizon/documentation/unity/ts-mqdh-getting-started/

## Install at school

1. Extract the test ZIP to a folder on the school computer.
2. Open Meta Quest Developer Hub and sign in with the developer account approved by the headset administrator. If it is not installed, use the Windows download linked from the official setup guide above.
3. Connect the Quest 3 with a USB-C data cable. Put on the headset and accept the USB debugging prompt for this authorized computer.
4. In MQDH, open Device Manager and confirm that the Quest 3 is connected.
5. Under Apps, click Add Build and select Anatome.apk from the extracted test folder. Alternatively, drag the APK into the connected device's Apps area.
6. Wait for installation. In the headset, open Library, choose Unknown Sources, and launch Anatome.

Official deployment instructions: https://developers.meta.com/horizon/documentation/unity/ts-mqdh-deploy-build/

The supplied APK has Android Debug signing for local sideload review. A production release keystore is not needed for this test. It is not a store release. The Quest 3 runs the installed app itself; the computer is used for installation and optional capture/logging.

## First test: start small

Select **Guide** in the top navigation to open the ten-step illustrated guide. Use **Next**, **Previous**, and **Close guide**; open it again and check that it returns to the same guide page. During an activity, closing must restore the same objective without accidental progress or object movement. Narration is a bundled female synthetic voice. Verify its clarity, pronunciation and comfortable volume on the headset.

Use the version **1.4.2** kit whose BUILD-INFO.json records `chest-height-20261008`. Install its APK as an update over the previous school build. Check the assisted working height and both visible controllers. Its curriculum remains **2026.10.2**; previous results remain compatible. Quest 2 and Quest 3 each require a physical retest.

Retest **CHEST-HEIGHT-FIX.md** first: hold trigger before aiming onto skin, cut and lift both requested skin regions, and check the automatic height assistance with a shorter or seated player. Settings > VR comfort also offers up to 0.8 m of extra player height. Apply changes and recenter while sitting or standing normally.

Retain the **CASE-CLARITY.md** checks: the anatomy source link, closing the browser back to the same activity, the case briefing, tool names, natural contact/route movement, and the live alignment instruction. Active operations still use trigger; movement/contact steps do not require an extra trigger hold.

Retest the reported issues first: email remains visible above the VR keys, Back preserves edits, a ray-directed scalpel cuts the visible surface, choices can be released into the highlighted dock, and the case instrument tray stays visible without holding a tool. Follow **VR-QUICKSTART.md** for the exact controls. This build retains button activation on trigger press.

Start with the Quest controllers: index trigger selects/uses, and side grip holds an object. Both controllers have visible proxies and teal aim rays. A small target marker identifies interactive surfaces. Squeeze trigger to activate a button immediately; release before the next press. Holding or releasing must not repeat an action. Scroll with the thumbstick, scrollbar, or trigger-drag starting on an empty panel margin. Sliders still follow trigger-drag.

Then enable hand tracking in the headset settings and put down the controllers. Open your hands, point and pinch index finger to thumb to select/use. Curl the middle, ring and little fingers to hold; open them to release. Pinch-drag an empty panel margin or the scrollbar to scroll. Pick the controllers back up to switch back, releasing the buttons before acting. Repeat orientation and an Easy activity with each input method. Camera-based hands need a physical Quest; the Editor's XR Device Simulator checks controllers only.

1. Choose Try your first VR lesson, then Learn the VR controls. Select the skin, grip it, lift slightly and release. The skin should return to the table and Easy should open.
2. Choose Start an Easy lesson. Check that you can read the instruction, hear narration, select the target and continue without help from the developer.
3. Try an Easy assembly practice. Grab the nearby piece and release it in the outline. Check whether the reach and placement feel comfortable.
4. Try an Easy guided dissection practice. Point the ray at the requested layer, hold trigger and move the ray along the opening, then release. Test from a comfortable distance. Grip the small detached chunk, lift it clear and release. Both sides are required where named. A missed surface or pause must cancel the stroke until a new trigger press.
5. Try an Easy case practice. After inspecting the finding, hold side grip to move one of three mechanism cutaways onto the patient-plan dock. Release when it says **Release grip to place**. Try a wrong choice, read its feedback, then correct it. The tray must stay visible before pickup and after release. Repeat with the predicted response near the end. In practice, **Show me this step** marks the correct model and dock.
6. Open Settings. Adjust seated mode/table height and text size if needed. Check that panels remain readable and reachable.

These four modes use different actions: selecting and marking anatomy, spatial assembly, protected layer dissection, and clinical reasoning with instruments. PCI, TAVI, chest drainage and aspiration rehearse procedural anatomy; asthma and pneumonia rehearse treatment reasoning. Cutaways are enlarged schematics and outcomes are time-compressed. They do not establish clinical competence. For the medical-student test, also check the following:

- At maximum text size, all three model labels and the dock remain readable and separated.
- A dropped, unheld or out-of-dock model cannot submit an answer. Pause/focus/tracking loss requires a fresh grip and release.
- Hard and assessment instrument steps offer alternatives. A wrong instrument cannot advance the objective.
- In pleural aspiration assessment, choosing to continue or dismiss new chest discomfort prevents a pass even after correction.
- Debrief explains each reasoning choice. XP is awarded once per unassisted assessment objective; repeating an already credited answer cannot farm XP. Lessons and guided attempts cannot unlock mastery.

Medium and Hard begin locked. Passing the corresponding Easy assessment opens Medium; passing Medium opens Hard. Passing needs at least 80 percent, all essential objectives and no critical preservation failure. Guest progress is only for the current session.

## Saved-progress test

For this separate test, the administrator must first confirm the two October scoring/content migrations are applied and the configured backend is reachable. Then sign in with your own learner email and enter the six-digit code. Complete orientation and one Easy assessment, wait for Synced, close and reopen the app, and check the score and unlock. Sign out when finished using the shared headset. Guest practice remains available while connected release setup is pending. The two temporary automated QA aliases were deleted; they are not school login accounts.

If checking cross-platform progress, sign in with the same learner account in the main Unity project on your computer, let synchronization complete, then compare the completed assessment and unlocked difficulty. This behavior has not yet been verified across physical Windows and Quest devices.

## Record useful findings

Use TEST-RESULTS.txt in the ZIP. For each issue, write the mode, difficulty, phase, last action, expected behavior and actual behavior. Record whether it happens again. MQDH can cast or capture the headset view and inspect logs if available. A short recording of a confusing step is useful; keep email codes and credentials out of recordings.

Earlier software checks apply to earlier builds. Current simulator checks and APK identity are recorded in BUILD-INFO.json and the clinical learning audit. Physical Quest results must be recorded for this APK hash. Record actual results rather than marking untried checks as passed. Production acceptance needs at least a 15-minute representative headset session with on-device performance evidence. Headset readability, reach, controller alignment, Android secure storage and cross-platform recovery remain checks to establish at school.

## Prepare at home without a headset

Open unity/ThoraxLab in Unity 6000.3.19f1. Choose Anatome > Open VR learning scene. Enable Anatome > XR Device Simulator (Editor only), press Play and click the Game view. Hold Space to control the right hand; left mouse is trigger and G is grip. Hold right mouse to look around. See VR-QUICKSTART.md for the complete controls.

The ordinary Windows executable requires an OpenXR headset. The no-headset simulator runs inside the Unity Editor.

See [the current dissection retest](VR-DISSECTION-OVERHAUL.md) for bilateral coverage, chunk handling and fourth-rib saw checks.
