# Anatomy grabbing and joystick walking — Anatome 1.4.3

Historical 1.4.3 report. For the current 1.4.5 update, see [settings, panel and chunk controls](VR-SETTINGS-GRAB-FIX.md). In 1.4.5, guided and free dissection keep whole tissue anchored and grip only detached chunks. The inspection behavior below still describes the other learning modes.

Use the 1.4.3 APK, Android version code 11, from the matching grab-and-walk test kit. The package remains `org.thoraxlab.learning` and curriculum remains `2026.10.2`. Install it as an update to preserve local learning data. No curriculum or database migration is required.

## Controls in every mode

Point at visible anatomy and hold either controller's grip to pick it up. Move the controller to lift the part and turn your wrist to inspect it. Release grip to let go. Identify & Enumerate, Anatomy Cases, Assemble Anatomy, and Guided Dissection use the same grab path. The existing controller orientation exercise retains its limited introductory lift.

Inspection returns a part to its pickup pose on release, including when tracking is lost or the game is paused. Inspection never grants placement, identification, or tissue-removal credit. The current assembly piece retains its normal placement behavior. In Guided Dissection, only a correctly cut, lifted, and released detached chunk can count as removal; lifting the original tissue cannot skip cutting. Hidden structures and reference annotations cannot be grabbed, and native cutting still requires the target to be released.

Use the left joystick to walk forward, backward, or sideways. Use the right joystick to turn. The Left dominant hand setting swaps these controls. Aim both controller rays away from the interface while walking; pointing at the interface captures the sticks for its controls. Release the joystick to neutral after pause, tracking loss, or input-source changes before moving again.

Smooth walking is the default for new profiles. Older settings profiles adopt this default once when loaded; a separate movement revision records the upgrade while retaining the cloud-compatible settings schema version 1. Other preferences remain intact. To use teleport again, open **Settings > VR comfort > Movement**, choose **Teleport**, and apply. This saved choice survives later loads. Movement speed, snap/smooth turning, vignette, automatic height assistance, and extra player height remain available.

## Audit and regression coverage

The original controller regression failed when gripping anatomy in Identify & Enumerate because the shared manipulation policy rejected it. The movement harness also reproduced drift below the joystick deadzone. Walking now applies the deadzone and bounds diagonal speed.

Real Unity scene testing found that a floor-based collision capsule was being raised with the assisted rig, causing ceiling collision to lower the viewpoint. Floor alignment then exposed overlapping equipment at the previous start position and repeated floor collision recovery. The player now starts in a clear aisle, independent of the headset's initial horizontal tracking coordinates. The capsule follows the headset horizontally, remains above the floor's collision clearance, and cannot automatically step onto furniture. The regression checks walking without losing calibrated height, including maximum extra height.

Automated checks drive the official XR simulator's controller states through `LearningXRInput`, `LabHandInteractor`, XRI grab/release, and the actual scene collision controller. They cover all four modes, deliberate rotation, stationary pickup, return on release, carrying while walking, pause/tracking interruption, assembly context inspection, movement directions, handedness, and settings upgrade/round-trip. Existing full-suite checks retain real assembly placement, bounded cuts, detached-chunk credit, immediate buttons, slider dragging, and height recovery. See `VERIFICATION.json` in the kit (source: `qa/grab-walk/verification-summary.json`) for the completed run counts, build results, and artifact identity.

## Physical Quest retest

1. Install the matching 1.4.3 APK as an update. Confirm the version, then enter an Easy activity in each of the four modes.
2. Grip visible anatomy with each controller. Lift, rotate, walk while holding it, and release. Inspection parts must return without advancing the task. Briefly interrupt tracking and pause while holding a part; confirm release and a fresh grip are required afterward.
3. In Assemble Anatomy, inspect another visible part, then place the requested piece into its outline. In Guided Dissection, inspect whole tissue, release it, cut a small piece, and lift/release only that detached chunk to earn removal credit.
4. Walk forward, backward, and sideways. Turn with the other stick. Try left-handed controls, a seated/short player, and extra height. Check viewpoint height, controller alignment, furniture collision, stick drift, and neutral-stick recovery after pause.
5. Select Teleport in VR comfort and apply. Restart and confirm that choice persists. Record comfort, sustained performance, and input reliability separately for Quest 2 and Quest 3 against the exact APK hash.

The test kit is Android Debug-signed for controlled sideload testing. Local simulator, player, and build results do not establish physical Quest tracking, comfort, sustained performance, or production release readiness.
