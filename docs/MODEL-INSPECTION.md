# 3D model inspection

Available from Identify & Enumerate, Assemble Anatomy, Guided Dissection, Anatomy Cases, Thorax Museum, and the dissection sandbox. In the museum, inspection shows the selected exhibit with its name and description. The initial controls introduction remains unchanged.

| Action | Controller or panel |
| --- | --- |
| Open / return | Right A; left X when Left dominant hand is enabled |
| Enlarge / shrink | That controller's stick up / down, pointing away from the panel |
| Rotate | That controller's stick sideways |
| Panel alternative | Inspect / zoom model, Zoom in +, Zoom out − |
| Restore fitted size and position | Reset view |

Inspection shows a 3D copy of the currently visible anatomy. Focused identification models retain their cutaways. Assembly keeps its current parts and positions; dissection uses the current meshes, including cut surfaces and visible fragments. Cases prefer their current access model, response model or mechanism choices when present, falling back to the specimen during anatomy steps. Inactive anatomy and answer guidance are not added to the inspection view.

The view fits the visible model in front of the learner at 100%, with bounded zoom from 50% to 300%. It stays in place while the learner looks around. The original meshes, materials, collider geometry, transforms and assessment tolerances are not scaled or replaced. Temporary objects have mesh filters/renderers only. Original rendering is restored when closing, navigating away, changing activities, losing tracking/focus or disabling the inspection component. Copies share source meshes/materials and never destroy those shared assets.

Activity input, locomotion and playing lesson narration pause during inspection. XRI releases held parts before the snapshot, with activity evidence disabled. Opening or closing cannot add answers, placement evidence, cut/removal credit or hints. A fresh A/X press is required after a menu, tracking loss, input-source change or handedness change. Pointing at UI routes the stick to scrolling; centering it is required before zoom resumes. Panel controls also work with the existing bare-hand pointer.

## Validation

The .NET learning suite exercises enlargement/shrink direction, reversible zoom, bounds, 72/90 Hz consistency, invalid input, long frame recovery, fresh button presses and the neutral-stick requirement after UI use. Existing movement checks cover the shared activity-input gate.

`LearningVRSimulatorVerification.Inspection.cs` adds real scene checks for all four modes, case mechanism models, unchanged geometry/progress/player position, collision-free copies, controller zoom, panel scrolling, A/X handedness, reset, held buttons, tracking loss, navigation cleanup, sandbox and the orientation guard. Run Unity 6000.3.19f1 without `-quit`:

```sh
Unity -batchmode -projectPath unity/ThoraxLab \
  -executeMethod ThoraxLab.Editor.LearningVRSimulatorVerification.Run \
  --model-inspection-only
```

Do not add `--learning-qa`: this suite uses the official simulator and normal tracking gate. It also runs as part of the full simulator verifier.

Unity is not installed in the cloud environment, so the new scene checks and physical Quest controls remain unverified there. Before shipping, run the verifier and inspect a heart cutaway, a partially assembled model, a real cut with fragments, and the case access/response models on a headset. Check both handedness settings, minimum/maximum zoom, panel placement/readability, return while holding grip/trigger, and tracking recovery. Confirm that the magnified view remains comfortable at the chosen scale and that each activity resumes at its prior step.
