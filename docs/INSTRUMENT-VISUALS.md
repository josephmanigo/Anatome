# Instrument visual revision

The instrument set uses brand-neutral, authored geometry. No manufacturer geometry,
logos, names or device dimensions have been copied into the game. These are readable
training representations, not engineering drawings or a claim of compatibility with
particular real devices.

Appearance references checked on 6 September 2026:

- [Swann-Morton, No. 3 handle range](https://www.swann-morton.com/product_range/3.php): flattened stainless handles, grip detail, distinct blade fitment and graduation placement.
- [Swann-Morton, No. 3 blade range](https://www.swann-morton.com/product_range/19.php): curved and pointed cutting-edge silhouettes.
- [BD, Anatomy of the Plastipak syringe](https://www.bd.com/content/dam/bd-assets/na/medication-delivery-solutions/documents/white-paper/phaseal-plunger-rod-contamination-white-paper.pdf): barrel, finger flange, plunger ribs, stopper, thumb press and connection components.
- [Intersurgical, connectors and accessories catalogue](https://media.intersurgical.com/global/documents/intersurgical-product-catalog-us-edition-10.pdf): recognizable airway connector, mouthpiece, polymer housing and mask component vocabulary.

These references informed component recognition. They did not change treatment
instructions, scoring, working-end position, contact tolerance or tool interactions.
Graduations are decorative divisions without volume or dose values.

The case and dissection tools use 23 named instrument models. The coronary case
also has a radial compression band. Each model shares cached meshes across instances.
The saw uses two renderers so its blade can oscillate about the spindle while the
grip stays steady. Each other model, including the band, uses one renderer. Only
finishes present on a mesh get a material slot.

Seven shared URP/Lit materials distinguish honed steel, satin stainless, ivory
polymer, teal polymer, graphite elastomer, clear polymer and absorbent fibre.
The metals share a directional stainless texture. The rigid plastics and rubber
share a mould-grain texture with different roughness and relief. The gauze and
swab use the woven surface. The six opaque finishes use normal maps. The clear
shell uses alpha blending so the plunger or fittings can be seen through it.
These material assets live in `Resources/Learning/InstrumentFinishes`. Tool meshes
use metre-scale UVs, so the grain stays attached as instruments turn in the hand.

`InstrumentGeometry.Create(parent, id)` creates the visual model. The caller owns
the grab collider, rigid body and contact checks. The scalpel has a separate
ground bevel and an open tang slot. The saw has a slotted plate and a curved tooth
row. The scalpel point and middle saw tooth lie 108 mm and 129 mm along local +Z,
respectively, with the saw at rest. The saw blade rotates about its local X axis at
the spindle.

The oxygen mask presents its broad open rim 67 mm along local +Z. The shell and
nose clip sit in front of that plane, and the elastic runs beside the cheeks and
around the back of the training head.

`InstrumentVisualVerification.Capture()` renders the original eight comparison
views with their fixed orthographic camera. It also renders the working ends in
close-up and the cloth and clear parts under the laboratory reflection. Its JSON
report records tool-tip alignment, UV scale, tangent frames, material references,
renderer counts and triangle counts. The radial band has its own entry alongside
the 23 instrument IDs. The mask check measures the rim plane and tests the center
and four surrounding points for obstructions. Headset appearance and controller
feel require a device run.
