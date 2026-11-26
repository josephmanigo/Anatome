# Anatome 1.6.9 model update

The review covered all 249 thorax catalog entries at each of the three detail levels. Six structures received corrections. Unity imported all 747 anatomical meshes and all 35 laboratory mesh groups.

![Released and corrected left main bronchus and right pulmonary artery](media/anatomy-model-fixes.jpg)

- **Left main bronchus.** One misplaced vertex formed a 52.54 mm spike. Repositioning it at the centre of its neighboring vertices restores the local airway contour. All 408 sampled airway and oesophageal lumen points remained empty.
- **Right pulmonary artery.** Two source fragments lay over 100 mm outside the registered lung surfaces. The teaching model now omits those two and retains the other 53 components. The original source records are preserved.
- **Valve and muscle detail.** Eleven small surfaces collapsed to pairs of triangles during simplification. Nine now use the corresponding intact finer surfaces. Two remnants of a previously identified internal-intercostal artifact were removed.
- **Laboratory.** The export omits 3,062 zero-area triangles. All 178,513 visible triangles kept their positions, UV coordinates and material assignments.

The annotated review includes the source measurements, known microscopic surface contacts, and two views of every catalog entry. [Download the corrected models and full audit](https://github.com/josephmanigo/Anatome/releases/download/v1.6.9/Anatome-Corrected-Models.zip) · [Unity import](../qa/quest-1.6.9/unity-import.json) · [Native mesh checks](../qa/quest-1.6.9/native-health.json) · [Laboratory surfaces](../qa/quest-1.6.9/room-surfaces.json)
