# VR accessibility and first-session instructions

Before sign-in, **Before you begin** explains the controls and progression rules, and **VR comfort & text size** opens Quick setup. A learner who has not completed the controls introduction also sees the rules automatically in the lab, before the first activity. The rules remain available through **Menu → How to play & unlock levels**.

The opening screen invites players with astigmatism to try **Astigmatism reading mode**. Open **VR comfort & text size**, turn the switch on, and use **Reading brightness (%)** to preview the lights, tissue and menu at the new brightness. The starting setting is 80%. Move it toward 100% if tissue detail looks too dark, or toward 65% if bright surfaces still feel uncomfortable. Choose **Apply** to save. **Cancel** returns to the previous brightness.

The same switch is available during play under **Settings** in **Learning & accessibility**. It gives menus a solid background, removes the glass shading, and uses at least 135% text and 150% caption size. Larger personal sizes still apply. Choose high contrast separately. Turning the mode off restores the player's usual game brightness, sizes and panel appearance.

Glare, hard-to-read contrast, straining to see in very dim light and long periods of screen use can contribute to eyestrain. Use the lighting that makes tissue and labels easiest to distinguish. If your eyes feel sore, take off the headset and rest. Astigmatism affects how the eye focuses light. Use your usual prescribed correction. [Mayo Clinic on eyestrain](https://www.mayoclinic.org/diseases-conditions/eyestrain/symptoms-causes/syc-20372397) · [National Eye Institute on astigmatism](https://www.nei.nih.gov/eye-health-information/eye-conditions-and-diseases/astigmatism) · [Meta headset safety](https://www.meta.com/legal/quest/health-and-safety-warnings/)

- Complete the physical controls introduction to unlock Easy.
- Pass an assessment with **at least 80%**, every essential step completed, no critical safety error and no answer hints.
- Pass Easy to unlock Medium, then Medium to unlock Hard **in the same unit and mode**. Lessons, practice and XP do not unlock levels.
- There is no time limit, and assessment retries are unlimited. Guest progress lasts only for the current session.

Quick setup brings text size, high contrast, movement style and speed, turning style, stick deadzone and dominant hand together. The reading sample previews size and contrast immediately; **Apply** saves the choices and returns to the previous screen. **Cancel** retains the saved preferences. Caption size is under **Learning & accessibility**, and height assistance remains under **VR comfort**.

New profiles start with text and captions at 115%. Existing saved reading preferences are retained. Interface labels use a minimum base size of 22 canvas units, wrap without automatic shrinking, and scroll within their panels. The footer and guide page counter reserve additional space for larger text. Grip the top bar to bring a panel closer or rotate it toward your view.

The left stick moves and the right stick turns; left dominant hand swaps these assignments. Smooth walking follows your horizontal viewing direction. The configurable radial deadzone ranges from 0.10 to 0.45, defaults to 0.20, and smoothly scales the remaining stick travel. Diagonal input cannot exceed the chosen walking speed. Snap turning performs one turn per side press and requires centering before another turn. Teleport uses a forward push and release. Pausing, panel capture, tracking loss, changing input source and applying settings cancel pending movement and require centering before a fresh input.

## Validation

For version 1.6.6, Unity's XR simulator passed 126 settings checks, including the opening note, applying or cancelling a change, caption and text sizes, and restoring the player's preferences. At 65% reading brightness, a ceiling light measured 65.2% of its full-brightness luminance and the Apply button measured 65.0% in the rendered images. Physical headset comfort still needs player testing. [Simulator checks](../qa/astigmatism-reading-mode/xr-settings.json) · [Rendered brightness measurements](../qa/astigmatism-reading-mode/rendered-brightness.json)

The core suite passed 2,161 assertions on this version. The earlier locomotion verification passed 74 camera-height and movement checks using the production `LearningControls.cs` with simulated engine and XR input. [Core test result](../qa/astigmatism-reading-mode/learning-tests.txt)

The final full run passed all 4,154 XR simulator checks, including scalpel contact, the case menu, the operating-room timer and assembly walking after earlier runs stopped at those checks. The Quest sideload APK passed 20 package checks. Inspection of its compiled data confirmed the opening note, the brightness control and the dimming shader parameter. [Full simulator report](../qa/astigmatism-reading-mode/xr-full.json) · [APK verification](../qa/astigmatism-reading-mode/learning-apk-verification.json) · [Packaged setting](../qa/astigmatism-reading-mode/packaged-setting.json)

From the repository root, with .NET 10 on PATH:

```sh
dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json
dotnet run --project qa/floor-height/FloorHeightTests.csproj -p:BaseOutputPath=/tmp/anatome-accessibility/bin/ -p:BaseIntermediateOutputPath=/tmp/anatome-accessibility/obj/
```

Run **Anatome → Verify XR Device Simulator integration** with Unity 6000.3.19f1 to exercise guideline navigation, text and contrast previews and the Quick setup controls. On a physical Quest, check reading brightness in both eyes, tissue detail, scrolling at minimum and maximum sizes, seated and standing reach, both dominant-hand mappings, turning, teleport cancellation and resuming after panel use.
