# Thorax Lab asset and demonstration handoff

This package contains editable source-backed anatomy, a modular laboratory and working dissection software. Its evidence is the actual exported files and machine-readable reports under `qa/reports`. It is a development handoff for the planned Quest 3 teaching game. The supplied anatomy includes documented reconstructions and approximations; do not describe every mesh as a direct segmentation from one specimen.

## Inventory and anatomical boundaries

The final catalog contains 249 entries: 137 refined source meshes, one source skin envelope with an authored dermal wall, 10 source meshes with authored lumens, five registered MAL lobe meshes, 90 authored reconstructions and six source-region annotations. This gives 243 cuttable tissues. Every entry has a stable ID, name, source/reconstruction record, anatomical group and rest placement. The CSV inventory maps all requested teaching groups.

The six annotations identify four empty cardiac cavities and two septal regions. All four source chamber walls/voids are represented, but the complete septal surfaces are not separately segmented as removable meshes. Both region anchors have sampled tissue support after explicit source-coordinate correction. Use the surrounding atrial/ventricular wall for cutting. This is a limitation of the provided segmentation and region interaction, not a claim of an independently authored complete septal model.

The MAL atlas is a different male specimen. Its five lobes retain their relative arrangement through one common registration and a second uniform 0.916 scale plus small common translation. The additional adaptation places all 16,260 tested lobe vertices within the measured cage envelope with the chosen clearance. It does not prove patient-specific bronchial, vascular, fissural or diaphragmatic registration. Original MAL surfaces remain separate. The geometric sternum-centroid residuals must not be presented as manually verified anatomical landmarks.

The pleural layers are thin envelope reconstructions with simplified fissural/hilar reflections. Pericardium is an authored envelope. Vagus, phrenic, sympathetic, intercostal and thoracic-duct paths are gross reconstructions with enlarged teaching diameters and simplified branches. Lower costal cartilages are documented reconstructions; ribs 8–10 connect toward the preceding cartilage and 11–12 terminate freely. Fat/fascia spacing is an author-selected visibility adjustment. These simplifications require art/anatomy refinement if more detailed teaching objectives are added.

Airway and esophageal source meshes were solid casts/envelopes of uncertain luminal-versus-exterior boundary. Explicit authored wall thickness and local lumen-clearance adjustments create hollow cutting volumes. Source endpoint closures and regional terminations are retained; continuous branch-wall junction anatomy is approximate. `docs/authored-lumens.json` records the initial operation; `qa/reports/lumen-wall-adjustments.json` and `geometry-normalization.json` record subsequent changes and take precedence for final geometry. Do not interpret thicknesses as measured histology. Subdivision or voxel repair is not recovered anatomical detail.

## Implemented interaction

Explore offers direct/pointed selection, labels, catalog search/browsing, system visibility, isolation, removal, rotation and return to recorded pose. Guided mode advances through ordered layers with deterministic opening motions. These are prepared rigid opening animations of layer structures, not physically simulated tissue flaps or suture attachments. Free mode samples tool motion into swept Boolean volumes. Bone requires the saw; other tissues use the scalpel. Pieces keep their parent identity. Mode changes and reset restore the original session state.

The native adapter performs Boolean operations and preserves the ownership of enclosed negative-volume cavity surfaces. A sealed cavity is not returned as a detached inverted tissue fragment. Native work and collider cooking run off the main thread; Unity mesh creation and the final replacement run on the main thread. Rejected and stale results preserve the previous/intact specimen. Ten undo frames, 32 detached pieces and 100,000 triangles per parent lineage are enforced. Cut-face materials distinguish tissue families; the bone shader has an authored approximately 1 mm cortical appearance band and cancellous pattern. That appearance band is not a segmented cortex. Soft handling adds small damped volume strain rather than physiological mechanics.

## Verification evidence

| Evidence | Result and meaning |
|---|---|
| `anatomy-native-validation.json` | 243/243 tissue volumes accepted by the native topology gate; six annotations excluded from tissue cutting |
| `fbx-roundtrip.json` | 249/249 entries survive clean re-import with IDs, triangle counts, placement tolerance, UV validity, normals and material assignments |
| `heart-cavity-preservation.json` | 100/100 source-derived points across all four chambers remain outside production tissue |
| `lumen-verification.json` | 408/408 sampled airway/esophagus lumen points remain empty |
| `actual-anatomy-cuts.json` | 28/28 representative real-anatomy cuts, including hollow structures, pass the scripted checks |
| Native fixture log | 74/74 synthetic geometry contract assertions pass |
| `runtime-asset-demo.json` | Actual player-run interaction/cutting checks and Windows rendering observations; inspect the current count and failure field |
| `asset-demo-build-StandaloneWindows64.json`, `asset-demo-build-Android.json` | Isolated demo Unity build results, error/warning totals and bytes; distinguish them from runtime/headset evidence |
| `apk-verification.json` | 10/10 packaging checks: signature, package identity, ARM64 libraries, native ELF alignment, licenses and ZIP integrity |

FBX tolerance checks do not prove microscopic anatomical accuracy. Point samples do not establish every lumen junction, trabeculation or membrane reflection. Runtime tests include direct XRI selection/release event checks for both configured hand interactors, but they do not emulate the headset's physical controllers or tracking.

The anatomy LODs contain 625,424 / 300,171 / 144,599 triangles in total, with the environment at 112,408 triangles in 16 material batches. Actual visibility and LOD selection determine rendered cost. The 600,000-triangle and 200-draw-call budgets are engineering targets; consult measured Windows counters and then profile the exact APK on Quest. Zero-valued unavailable counters are not evidence of zero rendering cost. Do not present an all-LOD0 total or a desktop frame interval as Quest frame-rate proof.

The runtime combines unselected intact structures into material batches using LOD1. Individual colliders and catalog identities remain active. Selection/grabbing restores the individual detailed renderer; cut structures and detached pieces stay separate. Guided opening temporarily uses individual renderers while structures move. Reset/undo invalidate the display batches. This reduces draw calls without merging the authoritative anatomical objects or the cutting meshes.

The final Windows harness passed **59/59 checks**. Its forced offscreen 1440 × 1000 captures on an RTX 3080 measured 42 draw calls / 320,374 triangles in the open view and 46 draw calls / 369,978 triangles with all layers visible. These are two sampled views, not a worst-case walkthrough. An Android build was compiling concurrently, and the open-view 95th-percentile frame interval was approximately 35.9 ms; do not use these timing samples as a frame-rate acceptance result. The rendering counters establish the sampled triangle/draw budgets only. Physical Quest timing remains pending.

Both final Unity builds succeeded with zero errors. The Windows build reports one warning and Android reports two; the APK is 94,364,054 bytes, package `org.thoraxlab.education`, version 1.0, and development-signed. Its SHA-256 is `f03def0192a31d4093f6cd6ed1fea838a55c34efa302bd189c72c359007959af`. The packaged native adapter is ELF64 AArch64 with 16 KB load-segment alignment. No authorized ADB device was connected, so installation and execution on Quest were not performed.

## Remaining device work and scope limits

Professional evaluation is not a completion requirement. Physical Quest 3 verification remains pending: both hands, tiny/occluded structures, stereo appearance, table height, seated/standing reach, locomotion comfort, blade contact, repeated cuts, fragment-limit behavior and 15-minute thermal performance. Target 72 FPS with approximately 13.9 ms CPU/GPU frame intervals. Record dropped frames, memory and temperature/thermal behavior against the exact APK hash. Rendering and timing on the Windows computer cannot substitute for that evidence.

The environment uses an assumed 12 × 9 × 3 m room, original modular geometry, shared inexpensive textures, baked illumination, probes and a single examination light. The image is a style/layout reference rather than a measured architectural survey. The covered background forms are non-interactive teaching-room props. Near-field texture richness and authored membrane/nerve detail are simplified for the standalone target.

The asset/dissection task does not deliver an account system, progression, formal assessment, multiplayer, bleeding or physiological tissue mechanics. Concurrent learning/account work remains preserved in `unity/ThoraxLab`. The reproducible handoff is isolated in `unity/ThoraxAssetDemo`, which excludes those unfinished scripts and is the target of the final builds and runtime checks.

Keep the full source notices, especially the MAL Section B license, with this document and distributed portions. `ATTRIBUTION.txt` describes actual incorporated sources and modifications. Build warnings from Unity package analytics, the installed read-only SDK metadata and Gradle deprecations should be distinguished from compiler, packaging or runtime failures.
