# Quest settings, panel and walking update — Anatome 1.4.7

Package `org.thoraxlab.learning`, Android version code 15, curriculum `2026.10.2`. Update the existing app; do not uninstall it or clear its data. This update retains the settings, panel, chunk and all-mode audit fixes from 1.4.6 and corrects startup facing and joystick walking during panel hover.

## Facing and walking

The first tracked headset pose now aligns the learner's horizontal gaze toward the workstation, regardless of the room's initial tracking heading. The head still starts in the clear aisle, with physical pitch, roll, subsequent head movement and calibrated height preserved.

Inside a lesson, the left stick walks and the right stick turns. **Left dominant hand** swaps those roles. Merely pointing either controller at the panel no longer blocks walking. The movement stick remains reserved for walking; use the other stick vertically to scroll the panel. Clicking, dragging, scrolling and gripping the panel still capture locomotion. Release the stick to neutral after active panel interaction, pause or tracking loss before moving again.

Sign-in, menus, Settings and modal dialogs continue to block locomotion. Start or resume a lesson to walk. Smooth walking is the default; an explicitly saved Teleport preference remains respected under **Settings > VR comfort > Movement**.

## Settings

The 1.4.7 Quest compatibility build selects OpenGL ES 3 and disables the Vulkan-only buffer-discard option. Four captured 1.4.6 native crashes ended in `vulkan.adreno.so` at `vkCreateImageView`; this change avoids that driver path without removing presets or deferring settings until restart. Desktop preset cycling cannot prove the Quest driver workaround. Repeat High, Extreme and Low changes on the exact updated headset build before marking the crash resolved.

The graphics panel explains that lower graphics can reduce GPU workload without increasing FPS when the headset is already at its selected refresh rate. The headset's saved profile was read back as Low, render scale 0.72, 2x MSAA, shadows off and 72 Hz. The corresponding running process reported 72/72 FPS with scale factor 0.72. This is a current sample, not sustained performance acceptance or a measured High-versus-Low comparison. No refresh-rate preference is changed automatically.

Apply and Cancel remain visible in a fixed footer while the category scrolls. Sliders and toggles edit a draft; Apply commits it, activates the settings and returns to the previous screen. Cancel keeps the previously saved values.

The Android quality level uses the default graphics pipeline. Settings now clone that active pipeline even when no quality-level override exists, so render scale, anti-aliasing and shadows take effect. Table height changes made during mesh processing are queued and applied when processing finishes. Assembly guidance and case equipment follow the changed table height. Practice hint changes refresh the current teaching aids. A resumed OpenXR session requests the saved refresh rate instead of always requesting 72 Hz.

Turning practice hints off also hides clinical-choice cues already revealed on the current decision. Turning the preference back on does not automatically expose the answer; the learner can explicitly request another practice hint. No choice or score is submitted by changing this setting.

Height tests drive the real sliders and Apply button, then measure the camera, specimen and table. Automatic height assistance preserves floor tracking and physical crouching. Extra player height adds the chosen offset. Seated fallback height applies only when floor height is unavailable and automatic assistance is off; it does not override valid floor tracking.

## Moving the panel

Point at the top bar labelled **Grip to move / rotate**, hold grip, move the controller and turn your wrist. Release grip to leave the panel in place. The top bar works on menus and settings as well as the workspace. Menu's recenter action brings the panel back in front of the learner. Applying unrelated settings preserves the chosen panel pose. Its height now accommodates both header text rows at the maximum text scale.

## Grabbing a cut piece

In Guided Dissection and the free dissection sandbox, whole layers remain anchored. Cut a small chunk, release the trigger to commit the cut, then grip that detached piece, lift it and release it. One grip retains one selected item; sweeping the pointer cannot accumulate more anatomy. Direct proximity selection ignores tissue that cannot be grabbed, and the cutting ray no longer overwrites the proximity grab target. Other learning modes retain temporary anatomy inspection. Removal credit still requires the correct detached chunk to be lifted and released; cutting alone is insufficient.

## Evidence and physical acceptance

The 1.4.5 fixes and their evidence remain under `qa/quest2-cast-20261011`. The subsequent all-mode audit and 1.4.6 verification are under `qa/game-mode-audit-20261011`. The original audit records 72 completed activities but 12 large-text header overflow reports, alongside reproduced clinical hint and instruction failures. The final verification summary records the exact repaired APK hash, full XR result, scene results, build result, APK validation and installation state. Simulator inputs use the official XR devices and the shipping interaction path without a tracking bypass; the fixture disconnects its own Editor XR loader so a connected Quest Link session cannot override those devices. The separate compiled-player scene fixture uses its isolated QA tracking bypass and does not prove physical headset acceptance.

Physical acceptance requires testing the exact APK on the headset. Check player and table height after Apply, a moved and rotated panel, and a real detached-chunk pickup. Account/cloud operations, sustained performance and publisher release signing are separate from these local regression checks. This package is signed for controlled sideload testing.

The facing and hover regressions are recorded under `qa/quest2-facing-walk-20261014`. The repeated 1.4.6 baseline reproduced incorrect facing at three nonzero initial headings and blocked walking for both pointing hands with either handedness setting. The actual OpenXR Oculus Touch layout correctly routed both joystick axes; the failure was in scene alignment and UI capture. Consult the verification summary for the final run counts and exact APK hash. These checks do not establish physical acceptance of 1.4.7.

The user requested manual installation through Meta Quest Developer Hub. Version 1.4.7 is delivered as a local APK; it is not automatically installed or launched on the headset.
