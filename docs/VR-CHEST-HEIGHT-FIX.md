# Chest cutting and accessible height — Anatome 1.4.2

Install the APK from the same test kit as this document as an update. Check version 1.4.2, Android version code 10 and the APK hash in BUILD-INFO.json. Curriculum remains 2026.10.2; this update does not reset learning progress or require a database migration.

## Fix and focused audit

The real controller regression reproduced zero skin-cut samples when the learner held trigger in empty space before aiming onto the skin. Selection previously happened only on the initial press. Cutting now selects the first valid surface reached by a held trigger. The current target, regional blade bounds, correct instrument and native mesh validation still apply. A cut does not complete a step until its detached chunk is lifted and released.

Cut errors such as using the bone saw on skin now appear in the learner panel. Processing and successful separation also have visible feedback. An interrupted stroke still cancels at a missed surface, obstruction, pause, tracking loss or input-source switch; release before drawing a fresh stroke. Tool evidence uses the instrument validated when the cut began, even if the toolbar changes while the mesh calculation finishes.

Automatic height assistance is enabled for existing and new preferences. On startup or explicit recenter, shorter and seated players are raised to a 1.65 m virtual eye height. Taller players retain their measured height. The shared rig moves the hands and viewpoint together. Continuous crouching and ordinary tracking recovery retain the calibrated offset.

A delayed valid floor pose replaces the temporary startup offset, including a 0.9 m seated reading. It cannot add the startup height a second time. The focused audit covers a single learner using either hand; saved height preferences use the existing validated profile serializer.

Open **Settings > VR comfort** for **Automatic height assistance** and **Extra player height (m)**, adjustable from 0 to 0.8 m. Apply the settings. Recenter while sitting or standing normally. Disable automatic assistance and set extra height to zero to use measured floor height. Table height remains independently adjustable.

## Physical retest

1. Start with a shorter or seated player. Confirm the chest, tools and panel are reachable. Try leaning and crouching. Adjust extra height and confirm both hands still line up with their controls.
2. Open **Guided Dissection > Dissect the chest wall** on Easy. Hold trigger while aiming away from the body, then move onto the requested skin region. Sweep a short continuous cut and release. Confirm a small piece separates from the stationary remainder.
3. Grip the detached piece, lift and release. Confirm the removal meter advances; the other side remains required. Complete both skin regions and continue to the next prepared station.
4. Select the bone saw while working on skin. Confirm the error says to use the scalpel and the skin stays intact. Restore the scalpel. Interrupt a stroke by moving off the surface, then release and retry.
5. Briefly interrupt tracking or open the pause menu while holding trigger. Confirm no cut resumes until release and a new press. Recheck immediate buttons, slider dragging and scroll controls.

Local engine tests, Unity simulator checks and APK verification do not establish physical Quest comfort, tracking, latency or performance. This kit uses Android Debug signing for controlled sideload testing. Record physical Quest 2 and Quest 3 results separately against this APK hash.
