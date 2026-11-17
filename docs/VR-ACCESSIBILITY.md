# VR accessibility and first-session instructions

Before sign-in, **Before you begin** explains the controls and progression rules, and **VR comfort & text size** opens Quick setup. A learner who has not completed the controls introduction also sees the rules automatically in the lab, before the first activity. The rules remain available through **Menu → How to play & unlock levels**.

- Complete the physical controls introduction to unlock Easy.
- Pass an assessment with **at least 80%**, every essential step completed, no critical safety error and no answer hints.
- Pass Easy to unlock Medium, then Medium to unlock Hard **in the same unit and mode**. Lessons, practice and XP do not unlock levels.
- There is no time limit, and assessment retries are unlimited. Guest progress lasts only for the current session.

Quick setup brings text size, high contrast, movement style and speed, turning style, stick deadzone and dominant hand together. The reading sample previews size and contrast immediately; **Apply** saves the choices and returns to the previous screen. **Cancel** retains the saved preferences. Caption size is under **Learning & accessibility**, and height assistance remains under **VR comfort**.

New profiles start with text and captions at 115%. Existing saved reading preferences are retained. Interface labels use a minimum base size of 22 canvas units, wrap without automatic shrinking, and scroll within their panels. The footer and guide page counter reserve additional space for larger text. Grip the top bar to bring a panel closer or rotate it toward your view.

The left stick moves and the right stick turns; left dominant hand swaps these assignments. Smooth walking follows your horizontal viewing direction. The configurable radial deadzone ranges from 0.10 to 0.45, defaults to 0.20, and smoothly scales the remaining stick travel. Diagonal input cannot exceed the chosen walking speed. Snap turning performs one turn per side press and requires centering before another turn. Teleport uses a forward push and release. Pausing, panel capture, tracking loss, changing input source and applying settings cancel pending movement and require centering before a fresh input.

## Validation

The current changes pass **934 core assertions** and **74 camera-height/locomotion checks**. The locomotion harness compiles the actual `LearningControls.cs` against engine/XR doubles; it verifies input handling and cancellation, not headset rendering or physical comfort. The core suite retains the 80% boundary, essential-step, safety, assessment and mastery checks.

From the repository root, with .NET 10 on PATH:

```sh
dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json
dotnet run --project qa/floor-height/FloorHeightTests.csproj -p:BaseOutputPath=/tmp/anatome-accessibility/bin/ -p:BaseIntermediateOutputPath=/tmp/anatome-accessibility/obj/
```

Unity 6000.3.19f1 and headset checks have **not** run in the cloud environment. The existing simulator verifier now includes early guideline navigation, text preview at 85%/115%/150%, contrast preview, Apply/Cancel behavior, unchanged learning progress and Quick setup button coverage. Run **Anatome → Verify XR Device Simulator integration** with the pinned editor to exercise these checks. Before a player release, also check readability and scrolling at minimum/maximum text and caption sizes, seated/standing reach, both dominant-hand mappings, drift, one snap turn per press, teleport cancellation and resuming after panel use on Quest.
