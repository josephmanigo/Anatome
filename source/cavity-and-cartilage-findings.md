# Source cavity and lower cartilage findings

## Airway and esophagus representation

All **102** selected airway/esophageal elemental meshes were sampled at four principal-axis planes. Every resulting section had one closed contour; all **408** contour-centroid samples had unit signed winding in the source mesh. No nested empty lumen contours were found. This provides consistent evidence that these originals are **solid enclosed envelopes/casts**, rather than walls enclosing open lumens at the tested sections.

Key source elements are FJ2541 (trachea), FJ2539 (right main bronchus proper), FJ2450 (left main bronchus), and FJ2563 (esophagus). The audit also covers all selected elements assigned to the five lobar bronchial-tree groups and the additional airway segment group.

The source identity does not specify whether its surface follows the luminal or external tissue boundary. Geometry alone does not resolve that semantic question. Hollowing, wall offsets, and thickness choices therefore require explicit authored-derivation records; they must not be represented as recovered source wall thickness. Preserve original bytes and source geometry separately from the authored cutting volumes.

`airway-lumen-audit.json` records every section plane, intersection-loop area, sample point, winding, and classification. Run `audit-airway-lumens.py` with Blender's bundled Python to reproduce. Section endpoint rounding to 0.0001 mm occurs only in diagnostic arrays. Finite samples do not certify every untested location.

## Heart cavities and septal tissue

The source chamber-related meshes are all selected: FJ2422–FJ2425 are the four cavity references, FJ2428 is the shared ventricular wall, and FJ2438/FJ2439 are the left/right atrial walls. The deeper all-header inventory found **no additional unselected genuine chamber wall**.

For FJ2438 and FJ2439, all 25 points confirmed inside each corresponding source atrial cavity were outside its tissue wall. Earlier FJ2428 checks likewise found all 25 points per ventricular cavity outside the shared ventricular wall. Thus the actual selected cardiac tissue meshes preserve the sampled voids and should not be replaced with filled organ primitives.

A line connecting the two ventricular cavity centroids was sampled at 61 positions. Sixteen samples were inside FJ2428 while outside both cavity meshes, providing evidence of separating tissue along that line. **The complete anatomical interventricular septum is not separately identified or segmented in this source.** No separately named interatrial septum mesh was found. Unrelated nasal/cerebral septa and septal vessel branches are not substitutes.

`heart-source-geometry-audit.json` and `chamber-wall-followup-audit.json` preserve the numerical evidence. Topology cleanup of the production wall copies remains necessary as previously recorded; source originals are unchanged.

## Costal cartilage inventory

Only 14 separately identified costal cartilage elements occur in the full archive headers and metadata:

| Rib number | Left source OBJ | Right source OBJ |
|---|---|---|
| 1 | FJ3239 | FJ3333 |
| 2 | FJ3242 | FJ3335 |
| 3 | FJ3245 | FJ3337 |
| 4 | FJ3248 | FJ3339 |
| 5 | FJ3251 | FJ3341 |
| 6 | FJ3254 | FJ3343 |
| 7 | FJ3255 | FJ3345 |

**No separate source IDs for cartilages 8–12 were found.** This does not establish that the lower rib meshes include cartilage, nor does it reveal a histological bone/cartilage boundary. Preserve that source gap when constructing any labeled lower-cartilage geometry. `chamber-and-costal-source-inventory.json` records each observed identity and selection status.
