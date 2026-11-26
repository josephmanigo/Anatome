# Reproducing the handoff

## Open and rebuild the supplied project

Install Unity **6000.3.19f1**, Android Build Support, the matching SDK/NDK/OpenJDK, and Blender **5.2.0 LTS**. Open `unity/ThoraxAssetDemo`. Unity packages are pinned in its `Packages/manifest.json` and `packages-lock.json`: URP 17.3.0, OpenXR 1.18.0, XR Interaction Toolkit 3.6.0 and Input System 1.19.0. The project uses Android ARM64 IL2CPP, Vulkan and OpenXR single-pass rendering. The Windows test build uses Mono x64. The build wrapper defaults to this isolated demo; `-Project ThoraxLab` explicitly targets the separate ongoing learning/account project.

From the project root in PowerShell:

```powershell
& ./pipeline/run-unity.ps1 -Method ThoraxLab.Editor.BuildProject.WindowsPreview -Log unity-windows -Graphics -Target StandaloneWindows64
& ./pipeline/run-unity.ps1 -Method ThoraxLab.Editor.BuildProject.BuildAndroid -Log unity-android -Graphics -Target Android
```

These commands start Unity and print its process ID. **Wait for one build to finish before starting the next**; inspect the named log and `qa/reports/asset-demo-build-*.json`. Never open two Editors on the same project. The wrapper supplies a short project-local Java socket directory because Java's Windows Unix-domain socket path length can prevent Gradle from starting. See [Oracle's socket-directory documentation](https://docs.oracle.com/en/java/javase/16/core/networking-properties.html). This is a process-local setting, not a modification of the machine's networking configuration.

`BuildProject.ConfigureAndBake` rebuilds the scene from exports, creates materials/prefabs/XR settings and bakes lighting. `PolishScene` applies the cabinet glass and runtime lighting adjustments and rebakes. `FinalWindows` applies the review camera, syncs exports and builds Windows. Ordinary changes to scripts do not require regenerating source geometry. `SyncAnatomy` updates existing objects; use `ConfigureAndBake` if adding/removing catalog entries.

Run the compiled desktop acceptance harness:

```powershell
& './builds/AssetDemo/Windows/ThoraxLab.exe' -selftest -testOutput "$PWD/qa/reports/runtime-asset-demo.json" -logFile "$PWD/qa/runtime-asset-demo.log"
```

The JSON and adjacent PNG are written after actual player execution. The program exits with a nonzero code if a check fails. Rendering counters and frame intervals are Windows observations, not headset performance. An unavailable local OpenXR runtime is expected without a headset; the desktop fallback remains usable.

## Native cutting library

Restore the exact Manifold source:

```powershell
git clone --depth 1 --branch v3.5.2 https://github.com/elalish/manifold.git native/vendor/manifold
git -C native/vendor/manifold rev-parse HEAD
```

Require revision `11235e6b8ebea2dbed8aec4285685aafd3d95667`. Run `pipeline/build-native.ps1` for Windows and `pipeline/build-native.ps1 -Android` for Android. The Windows script uses Visual Studio Community's MSVC tools and project-local official Windows SDK packages acquired by `pipeline/get-windows-sdk.ps1`; adjust tool paths for another machine. Android uses Unity's NDK and CMake. The wrapper links the serial Manifold backend and exports the same C ABI on both platforms. Android uses 16 KB ELF segment alignment.

Windows synthetic contract tests execute as part of the native build. They cover swept cuts, curved strokes, open-mesh rejection, enclosed void ownership, output limits and repeated allocation/free. Source `native/tests.cpp` is the test definition. The full upstream Apache license is distributed with the project and builds.

## Asset verification and editing

The final geometry build applies the reviewed bronchus, pulmonary branch and display-detail corrections after exporting all three anatomy FBXs. `pipeline/apply_reviewed_models.py` checks their mesh data and transforms before editing and updates the geometry contracts. Running this stage again on the corrected exports preserves the meshes. The laboratory generator removes zero-area triangles from the room FBX after export. Both operations use Blender’s bundled NumPy and FBX reader.

The supplied Blender masters are the editable starting point. `THORAX_PRODUCTION` is separate from `IMMUTABLE_SOURCE_REFERENCE`, `MAL_IMMUTABLE_SEPARATE_SPECIMEN` and `CAVITY_REFERENCES`. Do not edit the original archive or substitute these hidden reference objects into the game export. Blender uses metres and a supine specimen; the source-to-production transform is documented in the source research and registration records.

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --factory-startup -b --python-exit-code 1 --python pipeline/verify_final_assets.py
& 'C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe' pipeline/catalog_inventory.py
```

This verifies native topology, records the exact LOD0 export contract, exports FBX and LODs, re-imports to a clean Blender scene, exercises real anatomy cuts, and checks the source-derived heart void samples. The finalizer also verifies 408 airway/esophagus lumen samples. It never treats filling an anatomical cavity as a passing repair. A failed topology/void gate disables cutting until repaired.

The source-generation and refinement scripts preserve the authoring history. Several are **one-time transformations**, not an idempotent build system: `author_lumens.py`, `thin_lumens.py`, `normalize_anatomy.py` and the initial assembly/refinement scripts must not be applied blindly to an already-refined master. The current masters plus final verification path reproduce the game exports. Consult the successive JSON modification records before reconstructing every historical stage from raw source.

## Git and distribution

Git LFS tracks authored Blender/FBX/texture binaries and original large data. Use `git lfs install` and `git lfs pull` after cloning. Generated Unity Library/Temp, native build/toolchain folders, Java scratch files and player build directories are excluded. Restore Manifold at its pinned revision before a native rebuild.

Distribute a complete player directory for Windows, not the EXE alone. Android APKs are development packages signed for local testing. They are not store submissions. Preserve `ATTRIBUTION.txt` and complete license notices with every asset/project/build package. The user photograph is a reference, not a redistribution grant for that photograph.

On an authorized Quest 3, enable developer mode, allow the USB debugging connection, install with `adb install -r builds/AssetDemo/Android/ThoraxLab.apk`, and launch `org.thoraxlab.education`. Record the APK hash and run the 15-minute interaction/performance procedure in the handoff document. No headset was available for those measurements during this build.
