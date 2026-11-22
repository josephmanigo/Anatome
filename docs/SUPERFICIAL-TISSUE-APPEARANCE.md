# Chest-wall tissue appearance

The lab, museum and chest-wall teaching insert use the same four specimen tissue profiles. Each profile has an intact surface and an exposed section. The removable silicone insert has a separate surface material and an explicit chart for the depth of its opening.

## Anatomical references

| Teaching cue | Reference and interpretation |
| --- | --- |
| Thin skin and dermis | The intact surface shows shallow furrows and sparse follicular openings. The exposed dermis shows irregular connective-tissue bundles. The selected pigmentation is illustrative. The Minnesota slide shows human thin skin with epidermis, dermis and follicles. Masuda and colleagues measured surface relief on cheek replicas, so their study supports the kinds of visible detail rather than a chest-specific measurement. [University of Minnesota human thin skin](https://histologyguide.com/slideview/MH-090-thin-skin/11-slide-1.html) · [Masuda et al., surface microtopography](https://pubmed.ncbi.nlm.nih.gov/24267349/) |
| Subcutaneous fat | Rounded warm yellow lobules meet along finer, paler connective partitions. The visible units represent groups of adipocytes. Their pattern and scale are authored. [Human adipose lobule architecture and fibrous septa, figure 1](https://www.nature.com/articles/s41467-019-09992-3.pdf) |
| Fascia | The surface is a pale collagen sheet with gently changing grain. Its section has a softer reflection and warmer tone. Stecco and colleagues examined the pectoral fascia in the anterior chest and described its thin layer of undulating collagen and elastic fibers. [Stecco et al., pectoral fascia](https://pubmed.ncbi.nlm.nih.gov/19524850/) |
| Skeletal muscle | The red-brown surface carries elongated fascicle groups and fine connective partitions. The exposed map shows shorter bundle profiles. The source has no measured fascicle-direction field, so the map illustrates tissue identity across cut angles. Individual sarcomeres require microscopic magnification. [Columbia University skeletal muscle slides](https://www1.columbia.edu/itc/hs/medical/sbpm_histology_2009/lab/lab04_skeletal.html) |

## Geometry and scale

The outer shape of FMA7163 comes from the source skin mesh. Its nominal 1.8 mm inward wall is an existing authored thickness, locally clamped. The thoracic inlet and inferior closure follow the regional mesh boundary. REC_fat and REC_fascia are reconstructed teaching envelopes. Their surfaces do not supply donor-specific pigmentation, lobule locations, sheet thickness or histology. The pleural workstation is an enlarged cross-section with a visible fascial layer and a synthetic working insert. [Skin wall provenance](skin-wall.json)

Each specimen surface samples one 512 × 512 texture in three object-space planes. RGB stores colour. Alpha modulates smoothness and supplies a filtered gradient for shallow relief. The tile periods below refer to the standard specimen frame. Magnifying a specimen magnifies the pattern with it. The case imports the profiles onto its metre-scale meshes with the corresponding coordinate conversion. The silicone chart has a 62.5 mm lateral repeat and maps the existing 3.5 mm opening depth across its cut-face texture.

| Profile | Surface tile | Section tile |
| --- | ---: | ---: |
| Skin | 64 mm | 55 mm |
| Fat | 83 mm | 70 mm |
| Fascia | 91 mm | 74 mm |
| Skeletal muscle | 103 mm | 80 mm |

## Reproduction

1. With NumPy and Pillow installed, run `python pipeline/build_superficial_tissues.py` from the repository root. The generator deterministically writes eight packed specimen maps and four silicone colour/normal maps under the existing asset identifiers.
2. Run `python pipeline/build_superficial_tissues.py --check` to compare every decoded pixel against a fresh generation.
3. Run **Anatome → Prepare VR tissue appearance** in the Unity Editor. This assigns both specimen materials for each tissue, verifies map alpha, scale, mipmaps and Android ASTC 6×6 settings, and updates the source-mesh channel audit. A normal player build uses the checked-in assets.
4. Compare the intact models, the exposed faces of native mesh fragments and the open training insert at matched camera positions. [Unity visual verification](../qa/skin-tissue/README.md)
