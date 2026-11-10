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

The implementation creates 23 instrument variants with a cached mesh per variant.
All details are merged by surface into one renderer per instrument, using six shared
finishes across the kit: cutting steel, satin steel, ivory polymer, teal polymer,
graphite elastomer and translucent polymer. Thin wire and scaffold details use
bounded polygonal tubes; no refraction, screen-copy effects or per-detail renderers
are used. Only the required instrument remains visible during a normal activity.

`InstrumentGeometry.Create(parent, id)` creates visual children only. The caller
retains its original interaction collider, working-end transform and rigid-body
configuration. Dissection scalpel and saw variants retain the existing cutting-plane
orientation and nominal visual extents.
