# Zoom during an activity

Use zoom in Identify & Enumerate, Assemble Anatomy, Guided Dissection, Anatomy Cases, the dissection sandbox or the Thorax Museum. The current instruction, captions and tools stay in the workspace. Trigger and grip still act on the anatomy at the chosen magnification.

| Action | Controller or panel |
| --- | --- |
| Open or close zoom | Press A on the right controller, or X with left-hand dominance. The workspace also has an **Inspect / zoom** button. |
| Enlarge or shrink | Point away from the panel and move the dominant stick up or down. The limits are 50% and 300%. |
| Rotate the view | Move the same stick sideways. |
| Scroll the panel | Point at the panel and move the stick. Center it before adjusting zoom again. |
| Restore the normal view | Choose **Reset view** to keep the controls open, or **Close zoom** to exit. |

The view centers on visible anatomy, a case model or the selected museum exhibit. The tracked eyes and hands move in the same coordinate frame. The original tissue, fragment colliders, placement sockets and instrument paths retain their physical dimensions. The other stick still controls locomotion. Menu navigation or loss of tracking restores the normal view.

In Assemble Anatomy, the lesson draws a guide line from the piece to its destination. Grip the solid piece, align it with the faint placement outline and release. **Show me this step** replays the guide at your current zoom.

## Verification

The .NET tests cover zoom limits, input timing, handedness and the neutral-stick requirement. The scene verifier runs controller selections, assembly placement, native cutting and narration through the live workspace. Run it with Unity 6000.3.19f1 and the XR Device Simulator.

```sh
Unity -batchmode -projectPath unity/ThoraxLab \
  -executeMethod ThoraxLab.Editor.LearningVRSimulatorVerification.Run \
  --model-inspection-only
```

Headset speakers, stereo comfort and controller reach require a physical Quest check.
