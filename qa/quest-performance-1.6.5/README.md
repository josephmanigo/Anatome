# ANATOME 1.6.5 for Quest 2 and Quest 3

The ARM64 OpenXR APK targets both headsets and uses the same sideload signing certificate and package ID as 1.6.4. Android build 25 is 250,481,035 bytes. [Quest APK](../../builds/Anatome-1.6.5.apk) ? [Android package verification](learning-apk-verification.json) ? [Device targets and file hash](android-device-targets.json)

## Measured changes

| Measurement | Before | After |
| --- | ---: | ---: |
| Initialize 249 interactive anatomy structures | 23.43 ms | 2.33 ms |
| Run the resting tissue update for 206 structures | 0.112 ms | 0.024 ms |
| Retained CPU copies of display-only anatomy meshes | 498 | 0 |

Timings are Unity Editor main-thread medians on the same Windows machine and operating-room source. Initialization used seven samples after warm-up. The idle measurement used five sets of 120 updates across all 206 components, including those normally inactive. The build stores precomputed volumes for all 249 original structures. New fragments and the alternate validated working mesh retain their existing calculation. Glove joints also skip identical poses while the pointer continues to update. [Benchmark data and methods](performance.json)

The two display levels contain 23,822,316 bytes (22.72 MiB) of vertex and index data. Both now discard their CPU copies. All 249 full-detail working meshes retain CPU access. Preserving source vertex precision also increased the buffers of three chair meshes and five museum lung meshes by 331,428 bytes in total. Overall headset memory was not sampled. [Android mesh audit](android-meshes.json) ? [Vertex precision comparison](vertex-precision.json) ? [Unity mesh storage](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/mesh/isreadable)

## Visual and interaction checks

All 747 packaged anatomy meshes match 1.6.4 byte for byte in vertex buffers, index buffers, vertex layout and submesh ranges. The 249 package-baked volumes also match the pre-optimization meshes at double precision. The inspected scene, materials, authored textures, baked lighting, quality assets and 346 audio files matched the source baseline. The new operating room contributes 18 4K scene textures to the APK. [Packaged room textures](android-room-textures.json)

The three fixed-camera, 1440 ? 900 editor views differed at 51 of 3,888,000 pixels (99.9987% identical). The operating room, specimen and observation room differed at 27, 10 and 14 pixels, respectively. The clock was fixed, and hands and UI were hidden to compare the room and specimen. [Before-and-after views](visual-comparison.jpg)

At the maximum 80 cm reach adjustment, the test learner's collision capsule previously expanded from 1.65 m to 2.45 m. It now stays at 1.65 m while the camera rises. The tested walk preserved the calibrated vertical position. An initial pause-and-grip check failed once. It passed on subsequent focused and full runs. [Grab and walking checks](xr-grab-walk.json)

The Android build reported zero errors and zero warnings. The final full Unity XR run passed 4,137 checks with the normal tracking gate active. The focused grab-and-walk run passed 113 checks. The learning and scoring suite passed 2,153 assertions. [Full XR report](xr-full.json) ? [Learning tests](learning-tests.txt) ? [Android build](unity-build.txt)

No headset was attached at the final ADB check. Installation, stereo appearance, sustained frame rate and thermals on physical Quest 2 and Quest 3 hardware remain unmeasured.
