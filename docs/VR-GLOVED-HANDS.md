# First-person gloved hands

The avatar uses separate left and right rigged hand meshes from Unity's **XR Hands 1.6.1 / HandVisualizer** sample. This changes the equipment visible from the existing first-person XR camera. It does not replace the adult thorax specimen, add assessed anatomy, install a hand-tracking subsystem, or change clinical tools, colliders, procedure tips, or assessment rules.

## Visual source and provenance

- Official archive: <https://download.packages.unity.com/com.unity.xr.hands/-/com.unity.xr.hands-1.6.1.tgz>.
- Registry archive SHA-1: `ff73e7abcedb00c91c9323d447e83f233d142178`; the downloaded archive matched this value.
- Source meshes: `Samples~/HandVisualizer/Models/LeftHand.fbx` and `RightHand.fbx`.
- Left FBX SHA-256: `828996c1e01f710222a90caa3451a2099ec82f0651242a9b4847091a508ec521`.
- Right FBX SHA-256: `215bd9c9782a74c95b47e08e802bd2e37b9bd0e2205428543dbdef9da7990db3`.
- The FBX files are byte-for-byte copies. Their original importer settings are adapted in this project to disable animation clips, camera/light imports, colliders, material imports and bone optimization; project-specific asset GUIDs avoid conflicting with a future sample import.
- The package's `LICENSE.md` and `Third Party Notices.md` are retained in `Assets/ThoraxLab/Resources/Learning/Hands/Source`. The package uses the [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license), with project-integrated binary distribution. The source meshes are not a standalone redistributable asset pack.
- Unity documents these rigged models in its [hand visuals guide](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.6/manual/hand-data/xr-hand-visuals.html).

The pale teal nitrile material, subtle repeated equipment micrograin and small rolled cuff lip are original appearance additions. They represent avatar gloves, not a patient-derived hand model or a clinical claim. The source mesh topology and skin weights stay intact. One skinned renderer per hand deforms the real connected palm and finger mesh; the cuff adds one small static mesh using the same material.

## Asset preparation and runtime contract

Run `ThoraxLab.Editor.GlovedHandAssets.Prepare()` through the existing learning preparation workflow. Preparation verifies source hashes and emits `Resources/Learning/Hands/glove-provenance.json`, including imported dimensions and bone positions. Its version stamp avoids regenerating unchanged materials and prefabs.

Visual prefabs contain transforms and renderers only:

| Resource key | Hand | Finger bone prefix |
|---|---|---|
| `Learning/Hands/GloveLeft` | Left | `L_` |
| `Learning/Hands/GloveRight` | Right | `R_` |

The canonical prefab origin is the palm center. Fingers extend along **+Z**, the back of the hand faces **+Y**, and the palm faces **−Y**. The right thumb is on **−X**; the left thumb is on **+X**. Wrist position remains behind the palm along **−Z**. The original model child stores axis conversion; no negative scale mirrors either mesh.

The named joints are `Wrist`, `Palm`, and `Thumb`, `Index`, `Middle`, `Ring`, `Little` with the handedness prefix. Thumb joints use `Metacarpal`, `Proximal`, `Distal`, `Tip`; the other fingers additionally have `Intermediate`. All five have a named `Proximal` joint. The source rest pose already has a small natural curl, so controller-driven animation must add bounded local rotations to the captured neutral pose rather than overwrite it with Euler zero. Local curl axes should be derived from the actual child-joint direction toward the palm.

Controller movement remains the source of hand position and orientation, including the official XR Device Simulator. Trigger and grip can animate the avatar fingers while the existing input actions continue selecting UI and holding instruments. These are **controller-driven gloved avatar hands**, not camera-based bare-hand tracking.

## Verification scope

Preparation fails on changed source hashes, missing named bones, missing skinning, unexpected hand scale, or any collider/interaction script in the visual prefab. Software pose, visibility, selection and instrument handling are verified by the shared official simulator checks. Physical headset alignment, fit and frame timing remain hardware checks; a successful mesh import alone does not establish them.
