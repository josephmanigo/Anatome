# Focused models for Identify & Enumerate

Identify & Enumerate automatically shows the anatomy for the selected unit instead of the whole thorax. It applies to lessons, practice and assessments at every difficulty.

| Unit | Visible model |
| --- | --- |
| Heart & coronary anatomy | Heart walls, valves, coronary vessels and the unit's attached vessels; lungs, chest wall, pericardium and unrelated organs are hidden. |
| Pleura & lungs | Both lungs and their lobes. Pleural coverings are hidden until a pleural surface is requested; the lungs are hidden for diaphragm identification. |
| Airways | Trachea and the branching bronchial tree, without lungs or overlying organs and vessels. |
| Chest wall | Sternum, ribs and costal cartilage, without internal organs or outer soft-tissue coverings. |
| Great vessels & valves | Heart context and the unit's great vessels. Valve steps open an internal cutaway. |
| Mediastinal relationships | The unit's esophagus, nerves, duct and related structures; overlying organs are hidden, including the trachea for esophagus identification. |

Heart valve steps hide the chamber walls and overlying great/pulmonary vessels while retaining the valve leaflets and other internal cardiac context. Returning to a coronary step restores the heart walls. The interface labels the focused model and internal cutaway. Models retain their original geometry and positions, and visible structures can still be gripped and inspected.

The model includes related non-answer structures, even on Easy, so the learner must still identify the requested part. Preparing a view grants no selection, cut, orientation or assessment credit. References depicting empty cavities remain excluded from selectable tissue. Progress, the 80% pass threshold and unlock rules are unchanged. Other learning modes retain their existing visibility rules; restoring an activity reapplies its focused model, while leaving the activity restores the intact specimen.

## Validation

The current core suite passes **1,270 assertions**. Focused visibility checks cover all **18 identification variants and 96 target steps**, topic context, hidden coverings, internal valves, pleura/diaphragm transitions, restoration of heart walls and exclusion of reference regions.

Run from the repository root with .NET 10:

```sh
dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json
```

Unity scene and headset validation have **not** run here because the pinned editor/license is unavailable. The existing **Anatome → Verify XR Device Simulator integration** runner now checks real object visibility for heart, valve, lung and airway transitions, preservation of meshes and learning evidence, and restoration of dissection/intact anatomy. Its `--identification-focus-only` option runs the focused checks. Before release, inspect these views on the headset and select/grab the required surfaces, including coronary and valve targets, then restore the activity and switch units.
