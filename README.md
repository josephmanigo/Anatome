# Anatome

**Anatome** is a VR anatomy learning game built for medical students studying the human chest. Set inside a virtual operating room, students use their Meta Quest controllers to explore a fully layered 3D thorax, identify anatomical structures, assemble organs, and work through simulated clinical cases. English narration and captions guide every lesson, and completing an activity unlocks its next difficulty — from Easy through Hard.

## About the Game

Anatome places you inside a fully realized virtual dissection lab. A source-attributed 3D thorax specimen sits at the center — built from BodyParts3D and original reconstructions covering 243 tissue structures and six cardiac annotations. The lab is designed around focused, one-step-at-a-time learning: compact repositionable panels, restrained charcoal and teal UI, and guided narration keep you on task without overwhelming detail.

Six clinical cases drive the anatomy into clinical context: **pneumothorax** with chest drain, **asthma** with inhaler and spacer, **coronary artery disease** with balloon and stent, **pneumonia** with prescribed care, **pleural effusion** with aspiration, and **aortic stenosis** with catheter valve replacement. Each case includes 3D mechanism matching, response prediction, and instrument interaction — not arbitrary completion buttons. Wrong decisions produce specific rationales; an unsafe safety decision blocks an assessment pass.

Completed activities turn green. To pass an assessment, score at least 80%, complete every essential step without answer hints, and avoid critical safety errors. Learners can retry at their own pace.

## Watch the advertisement

[![Watch the 60-second Anatome advertisement](docs/media/anatome-advertisement.jpg)](docs/media/Anatome-Advertisement.mp4)

See the rib cage come together, explore six anatomy units and six patient cases, and visit the Thorax Museum. The 1080p film includes English captions and a generated female voice that pronounces Anatome like “anatomy.” [Download the MP4](https://github.com/josephmanigo/Anatome/releases/download/v1.6.9/Anatome-Advertisement.mp4) · [Editable film project](https://github.com/josephmanigo/Anatome/releases/download/v1.6.9/Anatome-Advertisement-source.zip)

## Play on Meta Quest

[Download Anatome 1.6.9 for Quest](https://github.com/josephmanigo/Anatome/releases/download/v1.6.9/Anatome-1.6.9.apk) · [Release notes and corrected models](https://github.com/josephmanigo/Anatome/releases/tag/v1.6.9)

Sideload the APK on Quest 2 or Quest 3 with SideQuest or Android Debug Bridge (ADB). This test build uses Android debug signing. Physical headset checks for this version are pending. For the Windows build and production signing workflow, see [Build and download the APK](docs/GITHUB-APK.md).

Version 1.6.9 updates six anatomical structures across the three detail levels and cleans the laboratory geometry. It includes the green activity-completion controls and Astigmatism reading mode. [Model review](docs/ANATOMY-MODEL-REVIEW.md) · [VR accessibility settings](docs/VR-ACCESSIBILITY.md)

## Game Modes

| Mode | What you do |
|---|---|
| Anatomy Cases | Examine six simulated conditions, predict responses, and practice treatment decisions using virtual instruments. |
| Identify & Enumerate | Locate and name structures and identify how they relate to nearby anatomy. |
| Assemble Anatomy | Pick up and place anatomical parts to reconstruct regions of the thorax. |
| Guided Dissection | Reveal tissue layers and practice cuts with virtual instruments. Free Dissection provides an unscored sandbox. |

The [Thorax Museum](docs/THORAX-MUSEUM.md) provides a space to inspect anatomy exhibits at your own pace.

## Classroom Release

Production-signed classroom release **1.4.9 / code 17**, curriculum **2026.10.2**, preserves package `org.thoraxlab.learning`. The user confirmed 1.4.9 works on **Quest 2 and Quest 3**, including login. The release has the same 1,346 application entries as that tested candidate, a permanent release key and a verified Android key-rotation proof. All **30 strict APK checks pass**. Learner privacy guidance, shared-headset instructions, backup/transfer exclusions, the head/skin visual refresh and automatic highest-supported refresh remain; there is **no fixed 72 FPS cap**. Get the exact production APK and ZIP from [the classroom release guide](docs/CLASSROOM-RELEASE.md). The new signing transition needs one in-place installation check before a fleet rollout; no headset was connected for that check.

The Quest build retains OpenGL ES compatibility, startup facing, movable panels and joystick walking while merely pointing at the panel. Deliberate panel interaction captures movement. Dissection holds whole layers in place and grabs one detached chunk at a time; other learning modes retain anatomy inspection. Earlier audits in `docs` and `qa` are historical; acceptance applies only to the exact APK tested.

## Run the Game in Unity

Open `unity/ThoraxLab` in Unity **6000.3.19f1**, choose **Anatome → Open VR learning scene**, then press Play. The Editor automatically enables the **XR Device Simulator** when no headset is connected. Start with **Try your first VR lesson** and complete the controller orientation. The Windows player requires PC VR. See the [VR quickstart](docs/VR-QUICKSTART.md), [learning setup](docs/LEARNING-SETUP.md), and [verification reports](docs/LEARNING-ACCEPTANCE.md).

The separate asset demonstration is retained for the original asset handoff. Open `unity/ThoraxAssetDemo` in **Unity 6000.3.19f1**. The scene is `Assets/ThoraxLab/Scenes/ThoraxLab.unity`. Run `builds/AssetDemo/Windows/ThoraxLab.exe` for the desktop demonstration. The Quest-targeted package is `builds/AssetDemo/Android/ThoraxLab.apk`; consult `qa/reports/asset-demo-build-Android.json` and the handoff report for the verified result. The separate `unity/ThoraxLab` folder retains concurrent learning/account development and is not the frozen asset-demo handoff.

Portable archives are in `handoff/`. Extract the asset/source and Unity archives into the same folder to retain the complete authoring and build pipeline. The Windows archive contains the complete player directory. `handoff/SHA256.json` records file sizes and checksums; `qa/reports/apk-verification.json` records the signed APK checks.

## Repository Contents

| Location | Contents |
|---|---|
| `blender/Thorax_MASTER.blend` | Editable production anatomy and separate hidden immutable source collections |
| `blender/Laboratory_MASTER.blend` | Original modular room and furniture geometry |
| `exports/` | FBX models, two display LODs, shared textures and anatomy catalog |
| `unity/ThoraxAssetDemo/Assets/ThoraxLab/Prefabs/` | Reusable anatomy and laboratory prefabs |
| `source/` | Original archives, metadata, source hashes, MAL surfaces/segmentation and reference evidence |
| `docs/structure-inventory.csv` | Every structure's name, stable ID, source/reconstruction record, group and cutting status |
| `docs/reconstructions.json` | Authored anatomy and cited references |
| `native/` | Manifold adapter, tests and exact upstream revision |
| `qa/reports/` | Actual topology, export, cavity, cutting, build and runtime results |
| `qa/renders/` | Anatomical review views and rendered laboratory/station |

The catalog contains **249 entries: 243 tissue structures and six annotations of cardiac cavities/septal regions**. All 243 tissue volumes pass the native topology gate. A cardiac cavity annotation depicts empty space; the surrounding wall is the cuttable tissue. Septa are identified within sampled source walls; their complete extent is not independently segmented as removable anatomy.

## Asset Demo Controls

These controls belong to the separate `ThoraxAssetDemo` application. The revised learning game uses the VR controller and Editor simulator controls in [VR-QUICKSTART.md](docs/VR-QUICKSTART.md).

| Desktop | Action |
|---|---|
| WASD / Q / E | Move / down / up |
| Hold right mouse | Look |
| Left mouse | Select, drag in Explore, or draw a cutting stroke in Free |
| Shift + drag | Rotate the held structure |
| 1 / 2 / 3 | Explore / Guided / Free mode |
| N / B / Z / R | Next guided layer / switch scalpel-saw / undo / reset |
| Sidebar | Catalog search, inspect, hide, isolate, return, system visibility and table height |

On Quest, use the controller trigger to point/select, press world-space buttons, or draw a tool stroke; grip grabs a nearby/pointed structure. The left stick moves and the right stick turns in 30-degree increments. The control panel supplies modes, catalog browsing, tool selection, undo/reset, isolation, return, system visibility and table height. Physical controller, reach, stereo, comfort and frame-time verification remain pending on the headset.

Select the scalpel for soft tissue and the saw for bone. Release a held structure before cutting it. Only one selected target is processed at a time. Mesh Boolean work and collision cooking run on a worker. Results are validated and committed together; rejected cuts preserve the previous state. The session retains ten undo frames, at most 32 detached pieces and at most 100,000 triangles for a parent structure and its fragments. Changing modes restores the specimen. Grab strain is a small damped handling effect, not physiological tissue mechanics.

## Sources and Scope

BodyParts3D is the primary assembly. The five MAL lung lobes are from another specimen and share a documented common registration and subsequent cage-fit adaptation. Missing tissue layers, membranes, nerves, neurovascular bundles and thoracic duct are authored reconstructions with explicit records. This is a gross-anatomy teaching presentation with simplified fine structure and tissue appearance.

Read [ATTRIBUTION.txt](ATTRIBUTION.txt), [source research](docs/source-research.md), [implementation and limitations](docs/HANDOFF.md), and [reproduction instructions](docs/REPRODUCING.md). Original source files are preserved. Professional review is not a completion requirement for this project. A successful desktop test or APK build does not establish 72 FPS, comfort, controller behavior or thermal stability on Quest 3.
