ANATOME 1.4.7 - QUEST FACING, WALKING AND GRAPHICS RETEST

APK: Anatome-1.4.7.apk
Package: org.thoraxlab.learning
Android version code: 15
Curriculum: 2026.10.2

Load this APK manually through Meta Quest Developer Hub as an update to the
existing ANATOME app. If using the ZIP, extract it first. Keep the existing app
data. This work does not install or launch the update on your headset.

WHAT CHANGED

The Quest build uses OpenGL ES 3 to avoid the Vulkan driver path implicated in
four recorded 1.4.6 graphics-change crashes. All presets remain available and
apply immediately. The actual crash fix requires the headset check below; a
passing desktop test does not establish that result.

Lower graphics may keep the same FPS at the headset's selected refresh limit.
The captured run showed 72/72 FPS with scale factor 0.72. The update explains this
in Settings and does not automatically raise your chosen refresh rate.

Startup uses the measured headset heading so the first view faces the workstation
even when the room's tracking origin faces another direction. It preserves the
clear aisle starting position, height assistance and ordinary head movement.

Pointing either controller at a panel no longer blocks walking. In lessons, the
movement stick remains reserved for movement, while the other stick can scroll
the panel. Deliberate clicks, held drags, scrolling and panel grips still suspend
movement. After those interactions, release the stick before moving again.

CONTROLS

Default: left stick walks, right stick turns; right stick up/down scrolls a panel.
Left dominant hand: right stick walks, left stick turns/scrolls.
Sign-in, menus, Settings and modal dialogs still block walking. Start or resume a
lesson first. Settings > VR comfort > Movement must be Smooth for continuous
walking. A deliberately saved Teleport preference is preserved.

HEADSET CHECKLIST

1. Confirm version 1.4.7 after loading the APK. Restart the app while facing a
   different real-world direction. The initial view should face the workstation.
2. Start a lesson. Release both sticks, then walk forward, backward and sideways
   using the movement stick. Repeat while each controller points at the panel.
3. Grip the panel header, move it and rotate your wrist. Joystick movement must
   remain blocked during the grab. Release grip, release the stick, then walk.
4. Check movement in Identify & Enumerate, Anatomy Cases, Assemble Anatomy and
   Guided Dissection. Pause or interrupt tracking: movement should resume only
   after releasing the stick. Verify your saved player/table height still works.
5. In Settings, apply High, Extreme, Low and Medium several times. Confirm no
   crash, both eyes render correctly, and your chosen values remain saved.
   Compare FPS in the same lesson and viewpoint; note the selected refresh rate.

EVIDENCE

verification-summary.json identifies the exact APK hash and completed checks.
baseline-repeat.json records the reproduced 1.4.6 failures; fixed-focused.json
records the same regression after repair. full-xr.json drives the official XR
devices through the shipping interaction path without the QA tracking bypass.
The separate compiled-player scene checks use isolated QA state and do not prove
physical controller behavior. APK verification checks packaging and signature.

Physical acceptance of this exact APK still requires the checklist above.
The APK uses the existing Android Debug certificate for controlled sideload
testing. Publisher release signing and sustained headset performance are not
established by this handoff.
