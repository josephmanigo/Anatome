# XR Device Simulator integration verification

The Editor entry point is `ThoraxLab.Editor.LearningVRSimulatorVerification.Run`.
It can also be started from **Thorax Lab → Verify XR Device Simulator integration**.
Start outside Play Mode. For a command-line run, supply `-projectPath` and
`-executeMethod` with graphics enabled. **Do not pass `-quit` or `--learning-qa`.**
The command-line method exits the Editor itself after writing its result. A menu
run leaves the Editor open and exits Play Mode. An Editor watchdog fails a run
that does not finish within 180 seconds.

The fixture uses the official XR Interaction Toolkit simulator installed by
`LearningSimulatorSetup`. It first checks that the simulator contains its
simulator, controller and required hand sample assets, that its keyboard/mouse
sample actions are enabled, and that it creates the HMD and two handed
controllers in controller mode. It then disables only the keyboard/mouse writer.
In the installed XRI version, its separate `SimulatedDeviceLifecycleManager`
retains those exact devices. The fixture never creates replacement devices.

The remaining input consists of `InputSystem.QueueStateEvent` calls using
`XRSimulatedHMDState` and `XRSimulatedControllerState`. It does not call the UI ray
handler, runtime selection/completion methods, XRI selection methods, or Unity
button callbacks. The normal camera/controller adapter, controller ray handler,
UI event dispatch, physics ray cast, XRI grab/release events, and orientation
guards must produce the observed results.

The checks cover:

- HMD and controller poses reaching their actual scene transforms.
- Left/right gloved hands with five skinned digits, no avatar colliders and no visible controller mesh.
- Independent trigger and partial/full grip animation measured from the rendered finger bones.
- Loss of one controller hiding its own hand, and loss of HMD tracking hiding both hands.
- Loss of HMD tracking blocking button presses and panel dragging even when a controller remains tracked; a held trigger does not replay a press after recovery.
- The gloved avatar leaving the tracked camera and interaction origins unchanged.
- Trigger press/release activating the guest and orientation UI buttons.
- Trigger selection hitting the original skin collider.
- Controller Menu opening a pause screen without discarding orientation, blocking
  a grip/movement attempt, and returning to the same step on the next Menu press.
- Untracked HMD state blocking a grip attempt and anatomical movement.
- Restored tracking allowing the same controller to grip the skin.
- Stationary grip preserving the skin pose, and a stationary release granting no
  orientation completion.
- An 80-degree wrist turn preserving the torso's table pose during orientation.
- A 35 cm hand lift being limited to a 12 cm specimen lift.
- Tracking loss or Pause returning held orientation skin without completion;
  a grip held through recovery cannot reacquire it until released.
- Skin following the held controller while orientation remains incomplete.
- The actual XRI release completing orientation and restoring the skin.
- The interface remaining a world-space Unity canvas.
- Real assembly pickup retaining the specimen parent used by the guide,
  independent rotation while held, alignment feedback and placement on release.
- Normal guarded entry into an Easy case, followed by actual trigger selection of its anatomical finding.
- The current procedure instrument's original collider being reachable by a real controller ray.
- XRI grip preserving the instrument body, working end and rotation at stationary pickup.
- Body and working-end motion following controller translation and a 22-degree turn around the dynamic grip.
- Actual grip release retaining the released instrument pose, with handling alone granting no clinical credit.

The report is written to `qa/vr-xr-simulator-report.json`. A successful run also
captures the actual camera view to `qa/vr-xr-simulator-world-ui.png`. The JSON
records device IDs/layouts, each assertion, Editor startup and runtime exceptions, and whether the
general runtime QA bypass was active; that bypass must be false.
The readiness revision contains 74 checks. The current JSON is authoritative for
the timestamp, pass count and errors.

The Editor error ledger begins before the Play request and survives the Play
domain reload through SessionState. The report also records whether
`UserSettings/Search.index` existed when verification started and before leaving
Edit mode. Automatic review and verification use the same deferred Play helper,
allowing Unity's startup callback batch to create its default Search database.
The missing-index regression and its failing/passing evidence are documented in
`qa/search-index-repair/README.md`. A timed-out verification cancels any deferred
Play request.

The gloved-hands integration also captures `qa/vr-gloved-hands-first-person.png`, `qa/vr-gloved-hands-trigger.png` and `qa/vr-gloved-hands-grip.png` using the actual HMD camera with both official simulated devices held in front of the learner. It restores the controller states before the original anatomy/procedure checks. `ThoraxLab.Editor.LearningBuild.VerifyGlovedHands` prepares the source-derived glove assets before entering that same simulator test.

`pipeline/run-unity.ps1 -Project ThoraxLab -Method ThoraxLab.Editor.LearningBuild.VerifyGlovedHands -Target Win64 -Log vr-gloved-hands-xr -Graphics -NoQuit` reproduces the current integration run. The earlier tools/cards/UI run caught a missing CanvasRenderer on the vector icons in the Editor clipping path. Each icon now explicitly requires and constructs that renderer before registering with the mask. That initial failure remains in `qa/vr-craft-xr-initial-failure.json` for traceability; use the current report for the final outcome.

This is an automated simulator-device integration check. Its injected XR states
do not exercise a human's keyboard/mouse manipulation of the sample simulator
controls, physical headset/controller tracking, comfort, or Quest frame timing.
Those need separate interactive or physical-device checks. The harness is under
`Assets/ThoraxLab/Editor` and does not enter player builds.
