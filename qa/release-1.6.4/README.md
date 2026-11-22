# ANATOME 1.6.4 for Quest

[Download the updated APK](../../builds/Anatome-1.6.4.apk) · [Skin and tissue comparison](../skin-tissue/comparison.jpg) · [Updated panel](../panel-back/page-normal.png) · [Lab chair](../lab-chair/unity-chair.png)

| Package | Result |
| --- | --- |
| Application | `org.thoraxlab.learning` |
| Android version | 1.6.4, code 24 |
| Size | 116,982,927 bytes, 111.56 MiB |
| SHA-256 | `5128da8af20b83aad8dd027b8d3a26ee0ddb01ba7d9ea27224912de294b3cf36` |
| Build | Unity 6000.3.19f1, Android ARM64, IL2CPP, OpenGLES3, 0 errors and 0 warnings |
| Upgrade | The APK passes Android v2 signature and ZIP alignment checks. The package and the local sideload signing certificate match the verified 1.6.2 and 1.6.3 APKs. The Android manifest has debugging disabled. |

### Changes visible to a learner

- **Skin and tissues.** Skin, subcutaneous fat, thoracic fascia and skeletal muscle each have separate exterior and cut materials. These profiles reach 19 source structures, including 16 muscles. The museum shares each exhibit’s surface materials with its corresponding lab structure. There are nine matched render pairs using the actual intact meshes, native cut fragments and silicone trainer. [Tissue review](../skin-tissue/README.md) · [Anatomical references](../../docs/SUPERFICIAL-TISSUE-APPEARANCE.md)
- **Case study.** The chest-wall trainer shows separate dermis, fat, fascia and sectioned muscle. The silicone insert opens smoothly and receives shadows from nearby objects. Direct tool pickup, scoring a single sustained protected contact once, and tissue-specific tool feedback passed through the simulator’s normal interaction path. The six clinical stations retained their recorded targets, routes and collider counts. [Case study and instrument audit](../case-instruments/README.md) · [Six-station report](workbenches.json)
- **Panel navigation.** Back sits in the fixed header and retains a fully selectable controller target. It measures 115 × 58 canvas units at the standard text size and 157 × 72 at 150% with high contrast. The keyboard keeps the draft during dismissal. [Before](panel-keyboard-before.png) · [After](../panel-back/keyboard-normal.png) · [Navigation checks](../panel-back/README.md)
- **Laboratory.** The chair at covered Station C has a contoured seat, back grip, steel foot ring and five caster pairs. The chair is fixed scenery with collision. Unity measured 0.541 m to the seat and the layout leaves 1.092 m of passage alongside the wall. [Chair measurements and matched views](../lab-chair/README.md)

### Verification

| Check | Result |
| --- | --- |
| [Complete Unity XR run](full-xr.json) | 4,111 passed, zero errors, normal tracking gate active. Includes all 249 museum models, five detailed lung meshes, Back navigation, the case workflow, and the dissection saw. |
| [Focused panel run](panel-xr.json) | 69 passed, including 33 navigation checks. |
| [Learning and scoring](learning-tests.txt) | 2,153 assertions passed. |
| [Native cuts](../case-instruments/native-cut.json) and [fragment extraction](../case-instruments/native-fragments.json) | 28 of 28 trials passed for each entry point on seven anatomy meshes. |
| [Texture regeneration](texture-regeneration.txt) and [render comparison](../skin-tissue/render-comparison.json) | All 12 authored maps reproduced pixel for pixel. The original and updated Unity views have matching geometry counts. |
| [Android build](unity-build.txt) and [APK verification](learning-apk-verification.json) | Build succeeded. All 20 archive, architecture, native plugin, attribution, manifest and signing checks passed. |
| [Packaged content](packaged-content.json) and [appearance changes inside the APK](packaged-appearance.json) | All 47 required content checks and all 22 comparisons against 1.6.3 passed. The eight specimen materials, eight specimen colour maps and both case surface colour maps changed inside the APK. |
| [Upgrade compatibility](update-compatibility.json) | All eight checks passed, including signature compatibility, file integrity and archive alignment. |

No Quest was attached during this audit. In-headset appearance, stereo readability, frame timing, controller feel and installation remain unverified. The skin wall has authored thickness and the fat and fascia layers are teaching reconstructions. Professional clinical sign-off is pending. [Review record](../../docs/professional-validation/ANATOME-Professional-Validation.md)
