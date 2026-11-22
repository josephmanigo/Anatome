# Laboratory environment audit

[View the four matched before-and-after images](comparison.jpg)

| Area | What the audit found | Change in the playable lab |
| --- | --- | --- |
| Room lighting | Startup applied one dark blue color to the walls, cabinets, and linen. Ceiling panels were clipped white. | The material colors survive startup. Twelve baked rectangular lights sit beneath the corresponding diffusers. A sealed, tiled ceiling and a room reflection probe give the steel a neutral indoor reflection. |
| Work surfaces | The imported floor stretched one small color map across the room. Steel, vinyl, and cloth responded similarly to light. | Surfaces use metre-scale UVs. Brushed stainless, woven linen, welded sheet vinyl, glazed tile, washable coating, ceiling tile, and oak each have a finish-specific roughness response. |
| Wash stations and equipment | The two sinks were flat plaques. The pails had solid tops, and rails protruded through the covered stations. | Both bowls have depth, rims, and strainers. All three pails have an open interior and rolled lip. The sheets hang over all three covered stations. Cabinet glass reveals the stacked linen. |
| Examination table | The examination drape intersected the metal top. | The full woven drape sits 1.4 mm above the tabletop at each tested height and follows the specimen. It has no interaction collider. |

## What was measured

- The room export has 113,368 triangles across 18 renderer batches, up from 112,408 across 16. The Blender round-trip checks cover both basins, three pails, three covered table rails, UV scale, and the room dimensions. [Geometry checks](../reports/environment-roundtrip.json)
- Unity loaded two 512 × 512 directional lightmaps, 120 light probes, and the room reflection. All twelve baked lights aligned with the luminaire meshes. The drape cleared the table at offsets of −20, 0, and +25 cm. [Play Mode measurements](runtime-audit.json)
- All seven room textures are 512 × 512, mipmapped, and configured for Android ASTC 6×6. Unity compiled 20 vertex and fragment checks for the opaque, transparent, and emissive material variants across OpenGL ES 3 and Vulkan. The Android project targets OpenGL ES 3. [Shader results](android-shaders.json)
- In the room overview, median pixel brightness rose from 26 to 134 on a 0–255 scale. The fraction of pixels with all three channels at 250 or higher fell from 2.41% to 0.19%. These are measurements of the saved camera images.
- The full Unity XR Device Simulator run passed 4,044 checks with zero errors, including cabinet transparency, diffuser emission, and drape clearance before and after a height adjustment. The learning-logic suite passed 2,116 assertions. [XR simulator report](../vr-xr-simulator-report.json)

## How to read the captures

All eight PNGs came from the Unity main camera in the Career lab at 1280 × 800. Each pair uses the same lens and pose. The first three pairs hide the floating interface to show the surfaces. The fourth keeps the learner console visible. Full-resolution views are available for the [room](after-overview.png), [examination table](after-table.png), [wash stations](after-wash-station.png), and [learner console](after-learner.png).

| View | Camera position, metres (x, y, z) | Look-at point, metres (x, y, z) | Vertical field of view |
| --- | --- | --- | --- |
| Room | −3.83, 1.67, 3.80 | 0.30, 1.24, −1.53 | 69° |
| Table | 1.03, 1.55, 2.27 | −0.14, 0.93, 1.13 | 60° |
| Wash stations | −2.72, 1.58, −1.80 | −1.05, 1.45, −4.15 | 66° |
| Learner console | 1.25, 1.65, 0.70 | −0.24, 1.58, 0.58 | 65° |

Physical Quest frame time and in-headset appearance remain unmeasured.
