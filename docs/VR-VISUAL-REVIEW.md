# Tools, anatomy cards and VR console revision

This revision responds to the requested improvement in tool, card and interface detail in `unity/ThoraxLab`. It retains the four learning modes, research-led curriculum, source specimen, Easy mastery gates and one-step teaching flow. The separate `unity/ThoraxAssetDemo` project is outside this revision.

## Visible changes

The 21 case instruments and two dissection tools now use authored component shapes rather than the old stacked primitive visuals. Examples include scalpel tang and blade edge, grooved handles, saw teeth, ring-handle needle holder, syringe stopper and plunger, inhaler mouthpiece, translucent spacer, drain fenestrations and wire stent scaffold. Steel, polymer and elastomer finishes are shared. The original collider bounds and working-end coordinates remain authoritative. Dynamic attachment preserves the instrument pose on pickup.

The treatment stations have recessed graphite bases, satin trim, fasteners, inset monitoring equipment, shaped pleural cut edges, visible adipose lobules, rib cut ends, distinct neurovascular elements, and a finished instrument tray. Vascular teaching channels have open lumens and continuous bends. Airway support uses a shaped training manikin. These are enlarged illustrative training models; additional visual detail does not establish patient-specific anatomy or complete biomechanics.

Sixteen anatomy images are rendered from actual imported specimen meshes for the four modes and twelve units. Temporary presentation offsets and source IDs are recorded in `Assets/ThoraxLab/Resources/Learning/CardArt/card-art-provenance.json`. Images are static, compressed per platform, and displayed with their aspect ratio preserved. The source FBX is unchanged. Case previews show the relevant healthy source anatomy; they are not claimed to show patient pathology.

The world-space console has fine-edged rounded surfaces, consistent vector icons, image-backed activity cards, stronger title/instruction hierarchy and larger slider hit areas. Card height grows with wrapped content. The keyboard and settings use the same controls. Recentring uses horizontal head direction and clearance above the specimen support, preventing a downward glance from putting the lower console inside the table. The panel remains movable by the learner.

## Measured scope

`qa/instrument-visual-verification.json` records all 23 instrument meshes: one renderer each, zero added colliders, at most six material passes and at most 4,494 triangles. Case station geometry counts in the current player log include all constructed states, including hidden drapes, dressings and deployed devices; they are not active-frame draw-call measurements. The tray adds three merged renderers and 2,880 authored triangles. The new visuals add no per-frame geometry preparation or card cameras.

Current player captures and scene verification belong in `.impeccable/vr-craft`; `first-pass` retains the first inspection. Detailed instrument captures are in `.impeccable/instrument-review`. The official XR integration report is `qa/vr-xr-simulator-report.json`. Its procedure pickup, translation, rotation and release checks use actual simulator Input System devices and XRI events without the runtime QA bypass.

`qa/vr-learning-delivery-manifest.json` identifies the packaged artifacts and current reports. `LEARNING-ACCEPTANCE.md` distinguishes this visual revision's checks from earlier content, database and cutting regressions. Screenshot inspection and geometry budgets do not establish physical Quest comfort, sustained frame timing, or the user's visual acceptance. The requested “10/10” remains a review goal.

## Try it in Unity

Open `unity/ThoraxLab` in Unity 6000.3.19f1, choose **Thorax Lab → Open VR learning scene**, enable **XR Device Simulator (Editor only)**, enter Play mode and focus the Game view. Follow `VR-QUICKSTART.md` for the official sample controls. Choose **Try your first VR lesson**, complete the short trigger/grip orientation, then open **Choose an activity** to inspect the four cards. Anatomy Cases contains the detailed treatment workstations. Easy opens first; passing its assessment unlocks Medium for the same unit and mode.

Instrument appearance sources and simplifications are in `INSTRUMENT-VISUALS.md`. Anatomy provenance and clinical teaching sources remain in the other `VR-*-SOURCES.md` documents.
