# Skin and tissue in ANATOME 1.6.4

[Compare four intact tissues and their native cut faces](comparison.jpg) · [Inspect the skin up close](after/skin-close.png) · [Open the silicone case insert](after/case-detail.png)

| Surface | Change visible in Unity |
| --- | --- |
| Skin | A quiet surface at whole-torso distance. Shallow furrows, small follicular impressions and variation in reflected light become visible up close. The cut face has its own dermal fibre pattern. |
| Subcutaneous tissue | Yellow fat lobules have irregular boundaries and paler connective partitions. The exterior and exposed face use separate textures. |
| Thoracic fascia | A pale sheet shows a preferred fibre direction with subtle variations. The model uses the same sheet in the lab and museum. |
| Skeletal muscle | Longitudinal bundles distinguish the external muscle surface from the texture of the exposed section. The comparison shows the right sternocostal part of pectoralis major. |
| Case trainer | The enlarged chest wall has distinct skin, exposed dermis, subcutaneous tissue, fascia and intercostal muscle. The silicone training pad has its own material and a depth-mapped inner face. |

The matched images use the same Unity camera, lighting and mesh for each pair. Each right-hand pair shows a fragment taken from the corresponding mesh by the native cutting library. Triangle counts for the original meshes and exposed faces match before and after. The runtime museum uses the lab's mesh materials. [Before measurements](before/report.json) · [Updated measurements](after/report.json) · [Pleural case station](../case-premium-20261014/after/pneumothorax.png)

The original silicone pad cast a triangular shadow pattern into its own groove. Disabling shadow casting on the thin pad removed it. The pad still receives external shadows. In the isolated final close-up the same pad renders identically across the opening with the two review lights' shadow casting enabled or disabled. [Before the shadow fix](case-self-shadow-before.png) · [Final close-up](after/case-detail.png) · [Lighting control](after/case-detail-unshadowed.png)

Human thin-skin microscopy, adipose lobule architecture, an anterior thoracic fascia dissection study and skeletal muscle teaching slides guided the appearance. Pigmentation and fibre directions are illustrative. The specimen includes an authored skin wall and reconstructed fat and fascia envelopes. The clinical review record remains open. [Clinical sources and reconstruction details](../../docs/SUPERFICIAL-TISSUE-APPEARANCE.md) · [Professional review record](../../docs/professional-validation/ANATOME-Professional-Validation.md)
