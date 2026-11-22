# Thorax Museum environment audit

**21 November 2026 · ThoraxLab · Unity 6000.3.19f1 · Android target**

The entrance, exhibits and rear gallery sign are visible together in the redesigned room. The original gallery used five untextured cubes for the shell and a sixth cube shape repeated for stands. Its open top exposed the grey camera background. Two cartilage specimens on the first display occupied about 2 cm each and were hard to distinguish from the floor.

| View | Before | After |
| --- | --- | --- |
| Skeletal gallery from the entrance | [Original room](before-gallery.png) | [Finished room](after-gallery.png) |
| Stands and physical labels from the side | [Original stands](before-exhibits.png) | [Finished stands](after-exhibits.png) |

The paired captures use the same display, camera positions, 62° vertical field of view and 1280 × 800 output. The comparisons hide the simulator hands and floating workspace to expose the architecture. The normal entry capture keeps them enabled. [Entry with controllers](entry-with-controls.png) · [Respiratory gallery, display 2](respiratory-gallery.png)

## Findings and changes

| Finding | Change | Verification |
| --- | --- | --- |
| The floor, walls and plinths all used runtime-created URP/Lit materials with a flat tint and identical smoothness. | Added seamless honed terrazzo, mineral plaster and quarter-sawn oak. The museum shader reads surface colour and smoothness from one texture and uses vertex colours for interior lighting. Oak grain runs along the ceiling rails. | All three 512 × 512 maps have ten mip levels, trilinear filtering, 4× anisotropy and Android ASTC 6×6 import settings. |
| The floor was almost black and the room had no visible ceiling or gallery identifier. | Added a coffered ceiling with opal strips, oak-lined arched bays, bronze edging and the current system and page on the rear wall. Two local shadowless lights illuminate the exhibits. | The same unobstructed floor patch rose from 8.7 to 153.3 on a 0–255 image brightness scale. All 45 page signs displayed the correct system and page within the mounted width. |
| The display scale calculation clamped small source bounds before dividing. Several cartilage exhibits appeared as dots. | Normalize each nonempty museum copy to a 0.24 m bounding radius. The copy reuses its source mesh and tissue materials. | All 249 display-size checks passed, alongside the source geometry and progress checks. |
| The old labels pivoted on the front face of each cube. A side view could push the text into the stand. | Position each label outside the face nearest the viewer and tilt it toward a standing or seated reader. A small contact shadow grounds each beveled stand. | All 249 labels passed the text check. At 150% text size and high contrast, all four plaque corners cleared the stand from the front, rear, both sides and a diagonal. |

The floor measurement is median weighted sRGB luma in pixels x = 1050–1199, y = 720–769 of the matched entrance captures. It describes the images. Headset panel brightness depends on the device.

## Rendering cost

Counts cover the shell and six occupied stands. Anatomy, labels, the directory text, controllers and the workspace are excluded.

| Measure | Original | Revised |
| --- | ---: | ---: |
| Architectural mesh renderers | 11 | 7 |
| Architectural triangles | 132 | 10,136 |
| Material slots summed across visible architectural meshes, before batching | 11 | 18 |
| Museum lights | 1 | 2 |
| Shadow-casting museum lights | 0 | 0 |

The shell uses 5,840 triangles. Each stand uses 716 triangles and two material slots, one for its opaque finishes and one for its small floor contact shadow. The three ASTC maps total about 464 KiB of compressed blocks including mipmaps. Actual draw calls, GPU frame time and residency depend on the headset and render backend.

## Verification

- The official XR Device Simulator passed **885 checks with zero failures or errors**. It exercised eight galleries, 45 display pages, selection, grip, lift, release, zoom, pause, floor collision, and return to the lab. The run used tracked simulator inputs with the runtime QA bypass off. [Simulator report](simulator.json)
- The project targets OpenGL ES 3 on Android. Unity's Android shader compiler returned success for vertex and fragment requests for mono, stereo instancing and multiview on OpenGL ES 3 and Vulkan. The compiler report records the byte count Unity returned for each request. [Shader compiler results](shader-android.json)
- The existing .NET learning suite passed **2,114 assertions**. The live renders came from the Windows Unity Editor.
- A physical Quest was unavailable. Device frame time, stereo readability and comfort remain open. A six-stand view looking diagonally across the gallery shows the most simultaneous surfaces in these captures.

## Editable sources

The runtime loads the gallery and plinth prefabs from `Resources/Learning/Museum`. The finish maps are original procedural artwork. To regenerate them, run `python pipeline/build_museum_finishes.py` with NumPy and Pillow installed. In Unity, choose **Anatome > Art > Rebuild museum architecture**. Choose **Anatome > Art > Verify museum in XR simulator** to repeat the focused checks. [Finish map generator](../../pipeline/build_museum_finishes.py) · [Architecture generator](../../unity/ThoraxLab/Assets/ThoraxLab/Editor/MuseumGalleryAssets.cs) · [Museum shader](../../unity/ThoraxLab/Assets/ThoraxLab/Museum/MuseumFinish.shader)
