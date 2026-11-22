# Museum descriptions and case instruments

## Unity captures

| Subject | Earlier capture | Revised capture |
| --- | --- | --- |
| Museum exhibit plaque | [Earlier plaque](before-plaque.png) | [Revised plaque](after-plaque.png) |
| Museum reading panel | [Earlier panel](before-museum-panel.png) | [Revised panel](after-museum-panel.png) |
| Scalpel | [Earlier scalpel](before-scalpel.png) | [Revised scalpel](after-scalpel.png) |
| Dissection saw | [Earlier saw](before-dissection-saw.png) | [Revised saw](after-dissection-saw.png) |
| Needle holder | [Earlier needle holder](before-suture.png) | [Revised needle holder](after-suture.png) |
| Spacer | [Earlier spacer](before-spacer.png) | [Revised spacer](after-spacer.png) |
| Pneumothorax workbench | [Earlier workstation](before-station.png) | [Revised workstation](../case-premium-20261014/after/pneumothorax.png) |

The case simulator captured the skin insert at 50% of a stroke made with a gripped scalpel. [Live Unity incision](live-incision.png)

## Offline visual studies

| View | What it shows |
| --- | --- |
| [Museum descriptions](museum-typography-proof.png) | The superior left lung lobe before and after selection, its reading panel, and the longest exhibit name at 150% high contrast. The proof uses the shipped Source Sans 3 outlines and catalog copy. [Font measurements](museum-typography-check.json) |
| [Case instruments](offline-instrument-review.png) | Six instruments and two close-ups rendered in Blender from the application's generated meshes, UVs and authored finish maps. [Geometry and material measurements](offline-instrument-review.json) |
| [Working skin insert](case-incision-preview.png) | The same insert at 0%, 50% and 100% of a virtual stroke. Blender rendered vertices exported from the compiled surface code, the authored scalpel mesh and the case material maps. [Measured states and source hashes](case-incision-preview.json) |

The offline previews use their own lighting and camera positions. The Unity images above show the application's presentation.

## XR verification on 21 November 2026

The complete simulator run for this snapshot passed **4,074 of 4,074 checks**, with zero errors and the runtime QA bypass off. It predates the final 1.6.4 skin and Back-button changes. [Full simulator report](../release-1.6.3/full-xr.json)

An earlier run stopped on the second orientation Menu press. Its screenshot showed the pause screen, and its report lacked the input and focus values needed to identify the cause. The Editor fixture now checks for neutral controller input and focus before sending the press and records the button state. A hidden batch window that has lost focus receives the same focus callback used by the other focused-input tests. The player menu still uses its existing rising-edge and focus guards. In the passing run, focus was already present, the menu opened the workspace on press, and release retained the selected skin at orientation step one with zero grabs and no completion credit. [Earlier menu result](../release-1.6.3/full-xr-before-menu-fix.json) · [Recorded input and orientation state](../release-1.6.3/full-xr.json)

- **Case interactions passed 98 of 98 checks.** The official XRI simulator gripped the scalpel with the hand inside its collider while its aiming ray missed, then picked it up again by ray. Hovering, rolling the edge away and jumping between endpoints left the skin closed. After a 50% stroke, the hand could resume only at the visible cut. The practice run completed all 16 pneumothorax objectives. A held pointer registered chart contact without a trigger, and returning from the references browser restored the same step. Unity 6000.3.19f1 recorded zero errors with the runtime QA bypass off. [Case simulator report](../release-1.6.3/case-xr.json)
- **One stationary contact now records one mistake.** A focused C# harness ran the production gesture condition and protected-contact recorder for 12 frames against one protected collider. The tool and its tip stayed still, grip stayed held and the step required no trigger. Before the correction, the recorder emitted 12 events. After it, the recorder emitted one. In the Unity assessment fixture, the swab's tip remained on the protected region for 12 frames during the chart step. The simulator verified one event, one mistake, the critical-failure flag and the same incomplete chart objective. [Case simulator report, `case_stationary_protected_contact_once`](../release-1.6.3/case-xr.json)
- **Guided dissection passed 156 of 156 checks.** The first full run found that changing from the scalpel to the saw on the release frame misreported a complete stroke as too short. The corrected controller discards samples when the tool changes and gives the tissue-specific instruction. Selecting the saw through the workspace and pressing the trigger on skin now shows “Select the scalpel for soft tissue.” The blade stays at rest and release preserves the mesh. Selecting the scalpel permits a fresh cut. Across seven specimens, the simulator verified contact at the displayed working end, bounded tissue removal and separate lift evidence. On both tested bones, the saw blade oscillated on contact and stayed still when reduced motion was enabled. The focused rerun recorded zero errors with the runtime QA bypass off. [Original failure](../release-1.6.3/full-xr-before-feedback-fix.json) · [Corrected dissection simulator report](../release-1.6.3/dissection-layers-xr.json)
- **Museum interactions passed 911 of 911 checks.** Unity checked every exhibit's model, display size and label, every page directory, and the five detailed lung lobes. The simulator selected, gripped, released and enlarged exhibits, paused and resumed the museum, and restored the lab position on exit. The source specimen and assessment progress were preserved. Unity recorded zero errors with the runtime QA bypass off. [Museum simulator report](../release-1.6.3/museum-xr.json)

## Verification

- The learning suite passed **2,153 assertions**. It replayed the 72 authored variants through the scoring engine. The three pneumothorax difficulty levels used measured scalpel evidence in those replays. The contact model rejected hovering, deep or lateral displacement, an incorrect blade angle, the blunt side, holding still and jumping between the ends. Equivalent one-second movement produced the same result at 30, 72 and 120 updates per second. These rates are test inputs. [Learning test output](../release-1.6.3/learning-tests.txt)
- All **2,988 measured museum text layouts** fit their available line widths across 249 exhibits. The measurements cover four text sizes, both plaque states and the reading panel.
- Unity checked **23 instruments plus the radial band**, seven shared finishes and the oxygen mask's unobstructed seal, and wrote 18 instrument images. The busiest model contained 4,494 triangles. The saw uses two renderers for independent movement of the blade and grip. [Unity instrument report](../release-1.6.3/instruments.json)
- Unity built all six case workbenches and checked their anchors. A separate geometry check validated 12,377 vertices and 23,456 triangles across the airway manikin, both ears and a tissue band. The skin insert opened and reset at each of the three tested sampling rates. [Workbench report](../release-1.6.3/workbenches.json)
- A static dependency check started at the museum and plinth prefabs, the detailed lung FBX, the two case-skin materials and the seven instrument finish materials. All 55 referenced asset GUIDs resolved across the 37 nonempty asset files in that dependency set.
- The Windows library shipped with ThoraxLab processed seven meshes from the master anatomy file. Its two entry points each passed 28 trials covering shallow, curved, grazing and missed strokes. These tests used the native library directly. [Cut results](native-cut.json) and [fragment extraction results](native-fragments.json)
- Unity 6000.3.19f1's compiler built the current learning core, runtime and editor sources with the project's editor and player response files. It reported zero errors and five editor warnings about the existing deprecated XR Device Simulator API.

## Scope of the simulation

The pneumothorax case now requires a contacting, correctly oriented cutting edge and a completed surface mark. Lifting keeps the current mark available for continuation. The fitted inhaler cap is the part the learner holds and removes. The oxygen mask meets the manikin at the open seal. Bony dissection uses the oscillating saw. The six authored clinical cases have no bone-cutting objective.

The instrument review used the American College of Surgeons' educational instrument descriptions. The pleural case was reviewed against the British Thoracic Society's statement. The case uses an enlarged teaching insert and a simplified tissue response. Professional validation is pending in the project's review record. [ACS instrument module](https://www.facs.org/media/wgcmalet/common_surgical_instruments_module.pdf) · [BTS pleural procedures](https://www.brit-thoracic.org.uk/clinical-resources/clinical-statements/pleural-procedures/) · [Professional review record](../../docs/professional-validation/ANATOME-Professional-Validation.md)

## Physical verification

Quest controller feel, stereo text readability, comfort and device frame timing require a headset run. The professional review record tracks clinical teaching validation separately.

The earlier offline QA copy remains in `%TEMP%\anatome-case-ui-ca3ff9ec`. Its original audit reported 2.29 GiB and an automatic approval rejection for recursive cleanup.
