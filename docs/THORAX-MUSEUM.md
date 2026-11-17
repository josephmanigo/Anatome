# Thorax Museum

A fifth mode for examining the project's complete anatomy collection. Open **Visit the Thorax Museum** from the lab, including before orientation, or use the museum card in **Choose an activity**. Guest access is supported.

The museum includes all **249 entries** from the existing anatomy catalog: 243 tissue models and six reference annotations. It does not claim to add anatomical structures absent from that catalog. Galleries follow the eight catalog systems:

| Gallery | Exhibits |
| --- | ---: |
| Cardiovascular | 104 |
| Skeletal | 78 |
| Nervous | 28 |
| Respiratory | 18 |
| Muscular | 16 |
| Integumentary | 3 |
| Digestive | 1 |
| Lymphatic | 1 |

Each display contains up to six separate models on stands. Every label contains the structure's name and a brief description of its location or function; catalog IDs are not included on the labels. The four chamber cavities and two septal-region markers are explicitly described as reference annotations, rather than separate tissue walls. Labels remain visible in this mode even when ordinary activity labels are turned off. Text size and high contrast follow the accessibility settings.

Use **Choose a gallery / find a part** to switch systems or search names and descriptions across the entire collection. A result opens its system and display and selects that exhibit. **Previous** and **Next** visit every display without omitting entries.

Trigger selects an exhibit. Grip lifts and rotates it; release returns it to its stand. A on the right controller, or X with left dominant hand, opens the existing inspection view for the selected exhibit. Stick up/down enlarges/shrinks it and sideways rotates it while pointing away from the panel. Panel zoom buttons are also available. Display sizes are normalized for visibility, so exhibits are not presented at their relative anatomical scale.

The gallery has its own floor, walls and stands, using the current walking/teleport settings. Leaving restores the learner's prior lab position, heading and panel placement. Menu brings the controls beside the learner and pauses museum interactions; returning keeps the current gallery and selection. Gallery changes require release of held grip before grabbing a replacement model.

Museum copies share the original mesh/material assets but have independent transforms and grab components. They are not assessment anatomy targets and cannot be cut, submit evidence, earn XP or unlock levels. The museum has no graded scenario or saved attempt, so the four serialized assessment mode identities and server rubrics stay stable.

## Validation

The .NET learning tests cover exact catalog coverage, all galleries and pages, label/description completeness, search, reference labeling, duplicate identities and unchanged assessment mode values.

The Unity scene verifier adds coverage for every real exhibit and label, shared source meshes, XRI trigger/grip/release, selected-exhibit zoom, menu return, unchanged source geometry/progress and restoration of the lab position. Run Unity 6000.3.19f1 without `-quit` or `--learning-qa`:

```sh
Unity -batchmode -projectPath unity/ThoraxLab \
  -executeMethod ThoraxLab.Editor.LearningVRSimulatorVerification.Run \
  --thorax-museum-only
```

It also runs at the end of the full simulator verification. Unity is unavailable in the cloud environment, so the scene verifier and physical headset acceptance have not run here. On a headset, validate both handedness settings, grip/release while pausing, gallery changes while holding a model, zoom, walking/teleporting, label readability at large text sizes, reference-marker presentation and return to the lab.
