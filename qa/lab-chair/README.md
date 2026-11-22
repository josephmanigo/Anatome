# Laboratory chair at covered station C

The chair sits beside the covered teaching table, facing the work surface. Its moulded seat and lumbar back use the room’s washable green finish. The back has an open hand grip. A formed steel support, height lever, gas lift, foot ring and five twin-wheel casters are visible from learner height. The chair is fixed scenery with collision.

[Unity close-up](unity-chair.png) · [Unity station view](unity-station.png)

## Matched authoring views

| Station before | Station after | Back and lift |
| --- | --- | --- |
| [![Station before](before-station.png)](before-station.png) | [![Station after](after-station.png)](after-station.png) | [![Chair from behind](after-back.png)](after-back.png) |

The first two images use the same 45 mm Blender camera and room lighting at 1024 × 768. They show the actual editable masters before and after the change. The wider Unity view shows the chair in the saved lab scene. [Before camera and asset inventory](before-authoring.json) · [After cameras and asset inventory](after-authoring.json)

## Placement and geometry

| Measurement | Result |
| --- | --- |
| Footprint and overall height | 0.610 × 0.617 m on the floor, 1.025 m tall |
| Seat height | 0.540 m at its Blender centre, 0.541 m at the Unity collider sample |
| Floor contact | All five caster pairs touch the floor within 0.001 mm |
| Clearance | 0.193 m to the covered station, 1.092 m of clear passage between the chair and the wall rail |
| Tracked spawn | 5.189 m from the spawn centre to the nearest point on the chair’s floor bounding box |

The 110,380-byte chair FBX contributes 3,524 triangles. Three single-material meshes reuse Guard (716 triangles), Stainless (1,228) and Black (1,580). The complete room has 116,892 triangles, 21 renderer batches and 16 materials. The same three meshes supply the chair’s collision surfaces. [Blender FBX round-trip and clearance checks](../reports/environment-roundtrip.json) · [Unity measurements](unity.json)

## Asset and lighting checks

The editable master contains the original 685 objects and 41 new chair meshes under one placement pivot. Comparisons covered the original transforms, mesh geometry and UVs, material slots, curve geometry, camera and lights. The room FBX and its Unity copy preserved their pre-chair SHA-256. All 18 existing room mesh references still resolve to their original file IDs in both the scene and prefab.

The three chair renderers receive light and reflections from the room’s probes. The saved scene and prefab share their mesh and collider references. Unity loaded 120 light probes, two lightmaps and twelve ceiling lights. The existing lighting data, both directional lightmap pairs and reflection maps retained their pre-chair hashes. Physical Quest frame timing and appearance require an in-headset run. [Source and reference integrity](asset-audit.json) · [Unity verification](unity.json)

## Source

The full environment generator calls the standalone chair generator. A chair-only rebuild reads the current editable master, replaces its named chair collection, and writes both copies of the separate chair FBX. An isolated full build and a second chair-only update each passed all 24 geometry checks. Both reproduced the shipping chair’s geometry and UVs, with 41 editable mesh parts and one pivot.

The normal Unity room update applies these batches to the open lab scene and reusable room prefab through **Thorax Lab / Art / Update laboratory environment**. [Editable room master](../../blender/Laboratory_MASTER.blend) · [Chair generator](../../pipeline/build_lab_chair.py) · [Exported chair](../../exports/LaboratoryChair.fbx) · [Unity room integration](../../unity/ThoraxLab/Assets/ThoraxLab/Editor/LaboratoryFinishes.cs)
