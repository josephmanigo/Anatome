# Thoracic organ appearance and geometry

The updated rendering covers 117 catalog structures across eight tissue profiles. The close-ups use the actual museum meshes and shared materials in a neutral Unity preview. Each pair uses the same camera, scale, exposure and lights. [Before and after](comparison.jpg) · [Respiratory gallery](after-respiratory-gallery.png) · [Assembled lab specimen](lab-organs.png)

| Specimen | What changed | Matched renders |
| --- | --- | --- |
| Anterior tricuspid leaflet | All 11 native leaflets and cusps have a pale connective-tissue surface and a distinct cut material. | [Original](before-FMA7238.png) · [Updated](after-FMA7238.png) |
| Left superior lung lobe | Selection displays the full atlas surface. The pleural-facing surface has a continuous sheen. | [Original](before-MAL_LUL.png) · [Updated](after-MAL_LUL.png) |
| Ventricular wall | Myocardium has its own warm material. Its surface shading follows the mesh across UV seams. | [Original](before-FMA13884.png) · [Updated](after-FMA13884.png) |
| Right inferior pulmonary vein | A muted violet-brown wall replaces the conventional blue. The museum label gives flow direction and oxygenation. | [Original](before-FMA49911.png) · [Updated](after-FMA49911.png) |
| Ascending aorta | The arterial wall uses a warm tissue colour and spatially varying smoothness. | [Original](before-FMA3736.png) · [Updated](after-FMA3736.png) |
| Esophagus | The wall has its own surface profile. This gross model uses one appearance along its length. | [Original](before-FMA7131.png) · [Updated](after-FMA7131.png) |

## Geometry and source limits

The five selected museum lobes display 14,214–31,510 original CT triangles apiece. Each working lobe has 6,500 triangles. Only the selected exhibit displays its higher resolution mesh. For every lobe, 95% of the atlas vertices were within 0.10 mm of the corresponding working surface. The largest individual distance was 1.268 mm. These distances compare two representations of the same lobe after the existing fit. The lab combines the atlas lobes with airways and vessels from another donor, so their alignment at the lung roots remains approximate. [Bidirectional geometry measurements](lung-atlas-geometry.json) · [Atlas publisher](https://www.openanatomy.org/atlas-pages/atlas-mauritania-thorax.html)

## Runtime verification

- All 117 affected structures used their assigned material on every source LOD. All 11 valve parts used separate exterior and interior connective-tissue materials. Undo restored the source mesh after an actual lung cut. Reset restored the organ materials. [Runtime material results](runtime-materials.json)
- A simulated scalpel stroke in the ungraded dissection lab separated the left superior lobe into two pieces. The detached fragment contained 229 exterior triangles and 123 cut-face triangles. Its two materials use independent textures. The cut texture samples in object space so its orientation can follow any blade face. The fragment's straight edges come from the simulated tool volume. Its fine grain is illustrative. [Detached tissue render](native-lung-cut.png) · [Native cut result](native-cut.json)
- All 905 checks passed in the official Unity XR Device Simulator with the runtime QA bypass disabled. All 36 Android vertex and fragment compilation checks passed across OpenGLES3 and Vulkan. These cover the organ colour pass, shadows, depth and the stock muscle material. The learning suite passed 2,116 assertions. [XR report](../museum-environment/simulator.json) · [Android shader results](android-shaders.json)
- All 243 original tissue meshes passed the existing UV and tangent-channel audit. All 16 learning cards were rendered again. Their provenance manifest records the same SHA-256 digest as the installed source thorax FBX. [Mesh audit](../vr-specimen-assets.json) · [Card artwork manifest](../../unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/CardArt/card-art-provenance.json)

The study materials use authored colours and gloss. The separately displayed pleural and pericardial envelopes are teaching reconstructions. Professional anatomical sign-off and physical Quest visual and performance checks remain pending. [Anatomy reference review](../../docs/THORACIC-ANATOMY-REFERENCE-REVIEW.md) · [Professional review status](../../docs/professional-validation/ANATOME-Professional-Validation.md)
