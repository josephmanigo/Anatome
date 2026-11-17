# Try Anatome in VR

**New mode: Thorax Museum.** Choose **Visit the Thorax Museum** from the lab, or the museum card under **Choose an activity**. Explore all **249 anatomy entries** in eight body-system galleries, with six separate 3D exhibits per display. Each label shows the **structure name and a brief description**. Use **Choose a gallery / find a part** to search the collection; trigger selects an exhibit, grip lifts and turns it, and release returns it to its stand. A/X opens zoom for the selected exhibit. Museum visits are ungraded and available before the controls introduction. See [museum behavior and validation](THORAX-MUSEUM.md).

**Zoom works in all four learning modes, the Thorax Museum and the dissection sandbox.** Press **A** on the right controller, or **X** on the left with **Left dominant hand** enabled, to inspect the current 3D model. Point away from the panel and push that stick **up to zoom in**, **down to zoom out**, or **sideways to rotate**. Press A/X again to return to the activity. **Inspect / zoom model** also opens the view, with **Zoom in**, **Zoom out**, **Reset view** and **Return to activity** buttons. Zoom ranges from 50% to 300% of the fitted inspection size. While pointing at the panel, the stick scrolls; center it before zooming again. Activity interactions and movement pause during inspection. Current cuts, part positions, steps and progress are retained. See [implementation and validation](MODEL-INSPECTION.md).

**Identify & Enumerate now uses focused models:** heart activities show the heart and related vessels, lung activities show the lungs, and airway activities show the bronchial tree. Unrelated thorax anatomy is hidden. Internal targets such as valves use a labelled cutaway. This applies to lessons, practice and assessments; [focused-model details and validation](IDENTIFICATION-FOCUS.md).

Before your first activity, review **Before you begin** and open **VR comfort & text size**. The controls introduction unlocks Easy. Pass an assessment with **at least 80%**, every essential step, no critical safety error and no answer hints to progress: **Easy → Medium → Hard** in the same unit and mode. Lessons, practice and XP do not unlock levels; retries are unlimited. The lab displays these rules automatically before a new learner's controls introduction.

Quick setup previews larger text and high contrast before Apply. It also offers movement speed, Teleport/Smooth movement, Snap/Smooth turning, dominant hand and stick deadzone. Snap turns once per press; center the stick before turning again. In Teleport mode, push the movement stick forward and release to step ahead. Center both sticks after using a panel or returning from a pause. See [VR accessibility and validation](VR-ACCESSIBILITY.md).

Case controls retained in version 1.4.2: start with the patient briefing, then **Begin this case**. The current step names the tool and shows placement/rotation progress. Grip holds a tool; moving the working end completes contact, connection and route tasks. Use trigger only when the live instruction asks you to operate an active control. **Case and controls** explains the case and can return loose tools to the tray. **Show me this step** previews movement without awarding credit.

**References → Read source details** stays inside VR. **Open website in browser** saves the current step; closing the browser and returning focus restores that step. If the Horizon menu stays open, select Anatome and Resume. See [the case and browser retest](VR-CASE-CLARITY.md).

## Open the in-game guide

On Quest, open **Anatome**, then select **Guide** in the top navigation. It is available before sign-in and during all activities. **Next** and **Previous** navigate ten illustrated, narrated steps. **Close guide** returns to your exact screen and objective; reopening **Guide** remembers the page. The controller Menu shortcut also closes it. Use the thumbstick while pointing at the guide to scroll longer pages.

The guide covers setup, trigger selection, grip/release, orientation, all four learning modes, assessment, progress and troubleshooting. Generic controls help does not complete objectives or reveal assessment answers. Easy assembly and lessons/practice show placement outlines; Medium/Hard assessments use anatomical relationships without an outline.

All 341 offline narration clips use Microsoft Zira, a female synthetic voice at a calm default pace. The guide has **Pause / resume guide voice** and **Replay guide voice**. Opening the guide pauses playing lesson audio; closing restores it. A manually paused lesson stays paused. Set volume and speed in **Menu → Settings → Audio**. Focus loss pauses narration and respects manual pause on return.

Open **unity/ThoraxLab** in **Unity 6000.3.19f1**. The game is VR-only; Unity's official XR Device Simulator lets you test tracked-controller interaction in the Editor without a headset.

## Start the simulator

1. Choose **Anatome → Open VR learning scene**.
2. Confirm **Anatome → XR Device Simulator (Editor only)** is checked.
3. Press **Play**, then click the **Game** view so it receives input.
4. Aim the simulated controller ray at **Try your first VR lesson** and trigger it.
5. Choose **Learn the VR controls**. Trigger-select the skin, then hold grip, lift upward slightly and release. This introductory lift is limited to 12 cm and keeps the chest facing up. The game returns the skin and opens Easy. Turning your wrist alone does not count as a lift.

The installed official simulator bindings are:

| Action | Control |
|---|---|
| Headset view | Hold **right mouse**, move the mouse. |
| Right controller | Hold **Space** while moving it. |
| Left controller | Hold **left Shift** while moving it. |
| Translate the selected device | Mouse movement, or **W/A/S/D** and **Q/E**. |
| Rotate the selected device | **R** switches translation/rotation; **Control** temporarily rotates. |
| Trigger/select/use | **Left mouse** while manipulating a controller. |
| Grip/hold | Hold **G** while manipulating a controller; release G to let go. |
| Cycle devices | **Tab**. |
| Controller menu | **M**. |
| Cursor lock/unlock | **Backslash**. |

These inputs move simulated XR devices; they are not desktop anatomy controls. A physical headset takes priority. Turn the simulator menu option off before Play if you want it disabled for headset testing.

Your view is first person and follows your headset pose. Automatic height assistance raises shorter or seated players to a 1.65 m working eye height at calibration, while preserving leaning and crouching afterward. Taller players keep their measured height. Settings > VR comfort offers an additional 0–0.8 m boost; apply changes and recenter while sitting or standing normally. Disable assistance and set the boost to zero for measured floor height. The headset supplies its stereo field of view. Only the flat Editor simulator uses a 75-degree preview lens.

Both tracked controllers have visible controller proxies. A teal pointing ray remains visible when empty-handed; a small target marker appears over a panel or interactive anatomy. Squeeze trigger to activate a button immediately, then release before the next press. Use the thumbstick, scrollbar, or trigger-drag an empty panel margin to scroll. Buttons respond on press, so dragging must start outside a button. Grip holds anatomy or tools. Controller pointing uses OpenXR's aim pose; manipulation uses its grip pose. Tracking loss hides the controller and cancels pending actions.

Quest bare-hand input uses an index-finger/thumb pinch to select/use; curl the middle, ring and little fingers to hold, then open them to release. Pinch an empty panel margin or the scrollbar and drag to scroll. Hand tracking must be enabled on the headset; put down the controllers to switch, and open your hands before starting. Grabbing follows the palm pose; the runtime's separate aim pose points at remote targets. Controller proxies disappear in bare-hand mode. The system hand-menu gesture cancels game input. Changing input method or recovering tracking requires a release before another action.

The XR Device Simulator above tests controllers. It does not verify camera-based finger tracking. Bare-hand tracking is implemented for the Quest OpenXR hand/aim extensions and must be tested on the physical headset. Stick movement and turning remain controller controls; this revision does not add a hand locomotion gesture.

**Build revision:** `chest-height-20261008`, application **1.4.2**, curriculum **2026.10.2**. Use the APK from the same ZIP as this guide and check `BUILD-INFO.json`. You can hold trigger before bringing the ray onto skin, sweep a short continuous cut, then release. Real chunks separate from an anchored remainder; regional cut-and-lift evidence earns credit. Lost contact, obstruction, pause or tracking loss cancels the stroke until release. This revision retains immediate buttons, visible email editing, reliable case docking and persistent trays. Automated checks are local evidence; record this APK's physical headset results separately.

## Sign in inside the headset

Select **Email address**. Your text stays in the fixed display above the keys, including when you scroll the keyboard. **Left**, **Right** and **Delete** edit at the caret; **Clear** empties the field. **Back** and **Done** close the keyboard and keep your edits. Long addresses show the part around the caret. Select **Send email code**, then enter all six digits using the numeric VR keyboard. A leading zero is retained. **Back to email** lets you correct the address. The VR keyboard replaces the Android screen keyboard in this flow.

## Begin with one Easy activity

After orientation, choose **Start an Easy lesson** for a short chest-wall identification activity. Follow the single current step. **Show me this step** replays help in lessons/practice. Finish appears when the steps are complete.

**Choose an activity** opens four modes:

- **Identify & Enumerate:** point to the named source structure and trigger to place its numbered marker, one Easy target at a time. The set counter records unique accepted structures. Wrong or repeated selections cannot add extra markers.
- **Assemble Anatomy:** grip the nearby piece, bring it into the matching outline and release. Easy starts with the correct orientation and generous placement tolerance.
- **Guided Dissection:** each topic has its own prepared station and full anatomy at all difficulties. Point at the named tissue, hold trigger and sweep a short continuous cut, then release. Grip the detached chunk, lift clear and release. Follow the regional removal meter; the uncut remainder stays anchored. The bone saw equips automatically for ribs. Saw, scalpel and undo are visible above the instruction. Both tools reach the first surface within three metres; missing the surface or pausing cancels a stroke until a fresh trigger press.
- **Anatomy Cases:** inspect the finding, hold side grip to pick up a mechanism cutaway, lower it to the patient-plan dock and release grip when the dock says **Release grip to place**. The dock changes colour when the held choice is within reach, including an overlapping edge. An out-of-dock release returns the choice to its starting pose. The instrument tray remains visible throughout the workspace. Continue with procedural instruments and a predicted-response model choice. Wrong choices explain the misconception. Lesson/practice help marks the correct model and dock without answering; assessment hides answer guidance. Patients and plans are fictional teaching scenarios.

**Pass that unit/mode's Easy assessment to open Medium, then Medium to open Hard.** Passing requires at least 80%, every essential objective and no essential preservation failure. Lessons/practice prepare you; they do not unlock the next difficulty by themselves. There is no mandatory timer, and retries are unlimited. Guest progress lasts for this session only.

## Get comfortable

Use trigger on UI buttons. Stick-scroll while pointing at a scroll panel; grip the panel's top edge to move it. The Menu panel offers **Move panel in front of me** and **Settings**.

Opening Pause or losing tracking during orientation returns held skin to the table without completing the lesson. Release grip before trying again after tracking returns. During assembly, the part remains linked to its anatomical reference while held; the guide updates as you move and rotate it. Practice gives forgiving placement feedback. An incorrect assessment release remains in your recorded attempt after correction or Restore.

Start with **VR comfort** for seated layout, table height, recentering, movement and turning. **Learning & accessibility** contains text/caption sizing, labels, contrast and reduced motion. **Graphics & display** contains Low, Medium, High, Very High and Extreme. Performance suggestions wait for your choice. **Profiles** can name, duplicate and save a setup. Apply keeps your settings; Cancel discards the draft.

For Windows VR, connect a headset through its OpenXR runtime and run `builds/AnatomeWindows/Anatome.exe`, or enter Play mode in the Unity scene. The standalone Quest review package is `builds/AnatomeQuest/Anatome.apk`. The simulator is not included in either player. Missing XR tracking pauses gameplay.

Email codes use the configured school Google Workspace sender through Supabase. Earlier builds verified Unity sign-in and account recovery with temporary test accounts. This revision verifies VR email/code entry locally; it does not repeat live email delivery or authenticated physical Quest testing. Use your complete recipient address (the school domain ends in `hcdc.edu.ph`). Guest practice is available without saved account progress. [LEARNING-SETUP.md](LEARNING-SETUP.md) contains technical setup and [LEARNING-ACCEPTANCE.md](LEARNING-ACCEPTANCE.md) identifies verification limits.

After updating scripts in an already running Unity Play session, stop Play and start it again to load the complete updated application state.

See [the current dissection retest](VR-DISSECTION-OVERHAUL.md) for bilateral coverage, chunk handling and fourth-rib saw checks.
