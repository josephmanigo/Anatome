---
name: Anatome learning interface
description: A floating smoked-blue glass console with equal tactile controls for native Unity VR anatomy learning.
colors:
  ground: "rgb(3.5% 6.5% 9.5%)"
  surface: "rgb(10.5% 17% 21.5%)"
  field: "rgb(1.8% 3.1% 4.3%)"
  ink: "rgb(95% 97% 98%)"
  muted: "rgb(72% 82% 87%)"
  accent: "rgb(48% 88% 81%)"
  divider: "rgb(19% 24% 28%)"
typography:
  display:
    fontFamily: "Source Sans 3"
    fontSize: "40px"
    fontWeight: 700
  headline:
    fontFamily: "Source Sans 3"
    fontSize: "40px"
    fontWeight: 700
  title:
    fontFamily: "Source Sans 3"
    fontSize: "31px"
    fontWeight: 700
  card-title:
    fontFamily: "Source Sans 3"
    fontSize: "28px"
    fontWeight: 700
  card-description:
    fontFamily: "Source Sans 3"
    fontSize: "21px"
    fontWeight: 400
  body:
    fontFamily: "Source Sans 3"
    fontSize: "22px"
    fontWeight: 400
  label:
    fontFamily: "Source Sans 3"
    fontSize: "24px"
    fontWeight: 700
  caption:
    fontFamily: "Source Sans 3"
    fontSize: "20px"
    fontWeight: 400
  status:
    fontFamily: "Source Sans 3"
    fontSize: "18px"
    fontWeight: 400
rounded:
  control: "15px"
  panel: "15px"
  square: "0px"
spacing:
  row: "10px"
  column: "12px"
  scroll-gap: "14px"
  control-inline: "20px"
  scroll-padding: "28px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.ground}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "12px 20px"
    height: "76px"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "12px 20px"
    height: "76px"
  input:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "14px"
    height: "68px"
  activity-rail:
    backgroundColor: "{colors.ground}"
    textColor: "{colors.ink}"
    rounded: "{rounded.panel}"
    padding: "{spacing.scroll-padding}"
  narration-caption:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.caption}"
    rounded: "{rounded.panel}"
    padding: "16px"
  anatomy-card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.card-title}"
    rounded: "{rounded.panel}"
    padding: "16px 20px 16px 16px"
    height: "148px"
---

# Design System: Anatome VR learning interface

## Overview

**Creative North Star: "A floating glass instrument console"**

The specimen leads. A floating smoked-blue glass console teaches one action at a time beside the learner's hands. Polished edges, stationary vertical shading, cool ivory Source Sans 3 text and sea-glass primary controls give the interface a dimensional material. Source anatomy pictures make learning choices recognizable before the learner reads their descriptions. Neutral tissue lighting carries anatomical form separately from the UI palette.

The user-pinned 3D glass direction governs version 1.5.0 and supersedes seed 4eb97167. The build remains code-led (.impeccable/config.json sets buildPath to code). Equal sibling button geometry, separated navigation and reserved controls support operation inside VR; material effects remain subordinate to readable instructions.

**Key Characteristics:**

- One practical action at a time
- Smoked-blue glass with stationary bevels and a dimensional outer edge
- Equal sibling controls that grow with wrapped labels
- Native controller input and reserved status, captions and settings actions
- Source anatomy artwork with recorded provenance

This record describes `unity/ThoraxLab`, the VR-only learning application. `unity/ThoraxAssetDemo` is a separate handoff. The current implementation authority is `LabWidgets.cs`, `LabUIVisuals.cs`, and the layout and page composition in `LabUI.cs`, `LabUI.Settings.cs` and `LabUI.Guide.cs`, with account state in `LabRuntime.cs`; `PRODUCT.md` supplies product constraints. Current glass captures and report are in .impeccable/review/glass-ui-final; the earlier .impeccable/review/glass-ui pass is retained. Read the tested revision and result before asserting acceptance. Component heights in the frontmatter are default baselines; native layout grows them as specified below. The CSS and HTML in `.impeccable/design.json` are documentation-only translations, not a web application or a second gameplay interface. Token `px` values represent Unity canvas units at text scale 1; they do not establish physical headset legibility.

### First-time flow

Sign-in offers **Try your first VR lesson** before account setup. The guest hub offers one short orientation: point, select, grip and release. Completion is recorded only after the learner's actions. Easy opens after orientation; each mode/unit's Medium requires a passing current-version Easy assessment and Hard requires Easy and Medium. Guest use follows the same gates.

The hub recommends a first Easy lesson and puts the four-mode chooser on a second page. Mode and unit choices use source anatomy cards with a title, one short description, optional availability text, and an arrow or lock symbol. The whole card is one controller target. Unit selection starts at Easy with one primary **Learn step by step** action. Practice/assessment and difficulty are separate pages. Free dissection is inside Guided Dissection.

When an active attempt exists, **Continue your activity** is the hub's sole primary action and **Choose an activity** becomes secondary. Without an active attempt, the activity chooser is primary.

## Colors

Smoked blue anchors the console; cool ivory supplies clear text and sea-glass green identifies the current action. Frontmatter values are source color components expressed as CSS percentages; surface shading changes rendered pixels.

### Primary

- **Sea-glass accent** (accent): primary actions, selection and progress. Primary labels use the dark ground color.

### Neutral

- **Smoked-blue ground** (ground): main console and primary-action text.
- **Raised blue glass** (surface): secondary controls, instruction surfaces and cards.
- **Recessed input navy** (field): typed-value fields.
- **Cool ivory** (ink) and **mist blue** (muted): primary reading and supporting text.
- **Slate divider** (divider): quiet separators.

High contrast substitutes black main panels, white default/muted text and white border tint. LabGlassTint skips its decorative vertical gradient. The shared shape masks remain in use.

## Typography

Source Sans 3 is bundled and rendered through TextMeshPro. Page headings use display/headline; workspace and current-objective headings use title. Card titles and descriptions have separate roles. Practical instructions use 25 canvas units; brand text uses 28, and the grip instruction uses 16. All button labels are bold. Ordinary text follows TextScale, with normal wrapping and TMP line spacing of 3; narration uses its independent caption scale.

**The Read the whole instruction Rule.** Use preferred text heights and scroll overflow; never overlap essential text.

## Layout

The canvas is 700 × 860 units at scale 0.00085, approximately 0.60 × 0.73 metres. Recenter projects the headset's forward direction onto the horizontal plane, then places the panel 0.95 metres forward and 0.55 metres left. Its centre starts 0.02 metres below eye height. When the skin renderer is available, the centre is clamped to at least its minimum world Y plus half the panel height plus 0.04 metres; this keeps the lower edge above the skin's support level. Looking down at the specimen therefore does not place the panel inside the table. The panel faces the viewer with a vertical up axis. Head pose must be applied before recentering.

The header is a continuous raycast surface labelled **Grip to move / rotate**. A fresh grip captures its initial position and rotation relative to that controller; hand movement and wrist rotation move the panel until release, even when the ray leaves its graphics. Only one hand owns the panel at a time. Tracking or focus loss cancels the hold. Recenter remains available through Menu. Applying unrelated preferences preserves a custom panel pose. The camera remains a full VR view; there is no desktop anatomy viewport split and no automatic camera fitting in ordinary play.

The header separates a brand/grip row from a navigation row. The first row contains a 30-unit square layers symbol, **Anatome** in 28-unit bold type and the 16-unit **Grip to move / rotate** instruction. Guide and Menu/Pause occupy equal-width buttons beneath it; signed-in users also receive **Log out**. Symbol geometry uses the smaller rectangle dimension, so layout changes cannot stretch a symbol. Header height is ceil(48 × textScale + 28) + 58 × textScale + 34 (168 at scale 1). Ordinary footer height is max(68, 32 × textScale + 40) (72 at scale 1). The content rail keeps 10-unit horizontal insets and 8-unit gaps to the header and footer. A single scrolling region sits between navigation and save/tracking status.

The workspace displays a step counter, a quiet five-unit progress line, and a rounded instruction surface with 22-unit padding. The current target is bold; the instruction beneath it is quieter. **Show me this step** appears in guided use. Tools, restore/undo, reference details and early finish are on another page. Normal finish appears after the objectives are complete.

Narration occupies a separate reserved caption band, outside scrolling controls. Caption size is independent of ordinary text size. Pausing and menus block specimen manipulation. Essential state is also written in words; color alone cannot carry correctness or availability.

Settings shows one category at a time, initially VR comfort. A category chooser replaces the old two-pane dense settings page. Apply and Cancel occupy a fixed footer outside the scrolling controls; its height includes their controls and the save/tracking status. Per-category reset, named profiles and account controls retain their behavior. Platform-specific values remain separate. Seated fallback height is labelled explicitly: floor-tracked headsets preserve actual height, while the fallback applies without floor height and with automatic assistance off.

The guide keeps Close guide in a fixed top row. Previous, the page counter and Next share a fixed footer; the body scrolls between them. Guide controls use the shared control height. Settings reserves ControlHeight + 28 × textScale + 30 units for Apply/Cancel and status. Keyboard keys scroll independently of the typed value, caret controls and return actions. Text scaling reallocates space; the world-space canvas is not enlarged to conceal overflow.

**The One current action Rule.** Keep advanced tools and mode changes on separate pages.

## Elevation & Depth

Depth comes from a shared vertex gradient, bevelled nine-slice masks and an offset outer panel edge. LabGlassTint multiplies surface vertices from bottom (0.62, 0.72, 0.82) to top (1.36, 1.33, 1.28). The rounded fill mask darkens its perimeter; the outline brightens toward the top. The main backdrop uses ground at 0.97 alpha over a separate edge surface offset by (5, -7, 7) canvas units. This produces a dimensional glass impression without runtime blur, screen-space refraction, a shadow shader or animated camera motion. No physical transparency or optical-glass simulation is claimed.

**The Stationary glass Rule.** Build material depth from the shared gradient, bevel masks and outer edge; keep readability and high contrast intact.

## Shapes

Rounded surfaces use reusable 64 × 64 antialiased masks with a 15-unit source radius and 17-unit nine-slice borders. The narrow inset outline and darkened fill perimeter form the bevel. Source mask dimensions describe native sprite construction, not a universal browser-radius calculation. Slider tracks, progress strips and scrollbars remain rectangular functional primitives. Authored vector icons keep square proportions based on the smaller rectangle dimension.

## Components

### Buttons, inputs and sliders

The shared LabWidgets.Button supplies primary and secondary controls with bold labels, 20-unit horizontal and 12-unit vertical padding, a 12-unit icon gap and optional 24-unit vector symbols. Base height is ceil(48 × textScale + 28), or 76 at scale 1. LabButtonHeight measures the longest wrapped label among active button siblings and gives those siblings the same required height. Flexible width with zero minimum/preferred width makes sibling buttons share horizontal space; explicit keyboard sizing remains local to the keyboard. A long label grows the group instead of overlapping its neighbors.

Primary buttons use the accent with dark ground-colored text. Secondary buttons use the raised surface with ink-colored text. Hover and selection multiply the image by (0.8, 1, 1); press uses (0.62, 0.8, 0.78); disabled uses (0.43, 0.48, 0.50). State tint changes are immediate (fadeDuration = 0). Labels retain their own colors. Native ray hover, focus/selection, press and disabled state remain distinguishable without animated transforms.

Text fields retain their recessed field color, 14-unit inset, native caret and selection. Height is max(64, 40 × textScale + 28), or 68 at scale 1. Input text does not wrap. On/Off toggles and value cycles use the same button primitive, so labels stay explicit and live values participate in height measurement.

Sliders reserve a 44-unit row below an explicit label/value caption. The track has 10-unit vertical insets. The 20-unit glass handle moves inside a separate travel parent with 10-unit horizontal and 7-unit vertical insets, preserving its bounds at both ends. ScrollRect uses preferred-height content, a clipped viewport and a visible scrollbar when needed.

### Navigation and account state

Log out is visible in the header only when Account.UserId exists and calls the shared LabRuntime.LogOut path. That path saves first and blocks logout during a cut, an account request or a save failure. Successful logout uses AccountService.SignOut, closes the active session and returns to sign-in. Account-isolated progress and PIN-protected offline access remain; logout affects the current device session, not every device. Signed-in notices reflect online/offline account state instead of retaining the guest-session notice. No backend or schema change follows from this control.

Icons are authored vector geometry from `LabGlyph`, with a consistent stroke and square proportions. Layers, target, assembly, case, tools, settings, mail, hand, navigation, play/pause, check, lock and logout symbols support text labels. Symbols are not anatomical illustrations and never carry an essential state alone.

### Cards and containers

Learning cards have a 148-unit minimum height and grow with wrapped text. Their layout uses 16-unit top/bottom/left padding, 20 on the right, and 20 between image and copy. Preview frames are 144 × 104 units, reducing width to 112 when textScale > 1.2; images fit proportionally without stretching or cropping. A 24-unit arrow or lock supplies a navigation cue. Hero previews are proportionally fitted too, with page-specific heights including the guide's 140-unit image. Card hover/selected, pressed and disabled image multipliers are (0.77, 0.96, 0.94), (0.55, 0.77, 0.75) and (0.55, 0.58, 0.61), with immediate tint changes.

The current instruction uses 22-unit padding and 12-unit internal spacing. Narration stays in a separate band of 112 × captionScale units outside scrolling controls. Confirmation surfaces use the same glass language and retain named actions and Cancel.

Per-controller ray dispatch retains hover, press, drag, release and scroll. A simulator mouse click is converted through the actual simulated controller; a desktop UI event module must not independently click the same button.

In 1.4.7, passive panel hover consumes world selection without capturing locomotion. During lessons, the movement hand's stick remains reserved for walking; the other stick can scroll the panel. Presses, held drags, actual scrolling and header grabs still capture movement. Menus, modal dialogs, pause and tracking loss retain their existing interaction gates. Startup aligns the measured horizontal headset gaze toward the workstation independently of the tracking origin's heading.

### Specimen and teaching presentation

The first view shows intact skin. Authored fat/fascia and the existing source muscles are retained underneath. Guided dissection requires a committed native incision and physical lift before exposure credit, and verifies that the next layer is visible. Undo removes the incision state; it does not erase assessment mistakes.

Easy assembly introduces one movable part with the correct orientation, an outline of its actual source mesh and visible socket corners. Placement guides have no colliders and never award credit. Correct identity, position, orientation and physical release remain part of the recorded interaction.

Derived tissue materials are separate from source assets. Appearance detail is illustrative, not segmented histology. Mesh boundaries, colliders, source coordinates and provenance remain authoritative. Surface/lighting changes require inspection of actual Unity renders at the learner's working distance; texture count is not evidence of quality.

### Source anatomy artwork

The card set has exactly sixteen static 512 × 320 images: four modes, six anatomy units and six cases. `LearningCardArt.Prepare()` renders temporary Unity URP preview-scene instances of the imported `Thorax.fbx`, after the derived tissue materials are prepared. The game loads the PNG-derived textures from `Resources/Learning/CardArt`; it creates no thumbnail cameras or render textures during play. Windows uses BC7 and Android uses ASTC 6×6, with mipmaps and trilinear filtering.

The images retain complete source mesh geometry. A composition may omit a covering structure or translate a component in an exploded view; those choices are recorded rather than represented as new anatomy. Case cards identify the relevant healthy anatomical region. They do not invent lesions, collapse, measured fluid volume, or a treatment result. Teal is interface emphasis. The card-art provenance is `unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/CardArt/card-art-provenance.json`; it records every depicted anatomy ID, source kind/reference, triangle count, translation, view direction, image hash and original FBX hash. That generated manifest identifies the actual rendered revision.

### First-person controllers and VR input

Version 1.3.0 uses original ringless controller meshes with a smooth pale shell, charcoal faceplate, distinct stick/buttons, side grip and animated index trigger. The shell is generated once and merged by material. It follows the tracked grip pose directly, with no smoothing latency or interaction colliders. A teal ray uses the separate OpenXR aim pose; a small target marker identifies the first interactive surface. Lost tracking hides the affected controller. Optional bare-hand input hides these controller meshes; earlier glove artwork is retained as historical material, not the current presentation.

Version 1.3.1 retires completed guided-dissection tissue from the work area after the evaluator accepts a cut, lift and release. The original and every fragment of that layer become inactive, with the underlying source geometry preserved. Existing Undo snapshots retain that hidden state; checkpoint restore retires completed layers without offsetting intact shells beside the body. Cutting alone and invalid releases cannot retire tissue. Free sandbox keeps moved pieces available.

Email entry has a fixed readable display and caret controls above scrollable keys. Back and Done preserve edits. The six-digit code uses a numeric keypad. At large text sizes, keys can scroll while the entered text and return actions stay fixed. The sign-in field remains near the top of the main page.

Case choices provide a neutral placement dock that highlights any held choice in the capture area, without revealing its correctness. A release at dock height submits the decision; invalid placement returns the original pose. The instrument tray stays visible throughout the case workspace. Guided dissection places the scalpel at the first ray-hit surface, keeping blade depth and protected-contact checks unchanged. A missed surface or interrupted press cannot bridge a cut.

### Review and evidence

The current glass revision uses .impeccable/review/glass-ui-final for final player captures and glass-ui-report.json. The task contract is qa/glass-ui-20261019/BRIEF.md. The preceding .impeccable/review/glass-ui pass remains in place. Earlier tools/cards/UI captures remain in .impeccable/vr-craft; the gloved-hand verification remains in .impeccable/gloved-hands, with first-person captures in qa/vr-gloved-hands-*.png. .impeccable/vr-review is the first VR revision; other older .impeccable/review content and the old Windows benchmark are historical evidence. They do not establish acceptance of this revision. The official Input System simulator report is qa/vr-xr-simulator-report.json; check its timestamp and tested revision before reusing its result.

A screenshot is evidence of the rendered view; it does not prove physical stereo comfort. Scripted action fixtures are software checks, not human usability or clinical validation. The user reviews the running Unity application to judge the requested visual quality and ease of play. No numeric “10/10” or universal 60 FPS claim is inferred from a successful build.

Version 1.4.0 replaces incision-only progression with complementary chunk extraction. The cut remainder stays anchored; only the swept chunk can be gripped in guided dissection. The active saw/scalpel and Undo are visible above the instruction. Bone objectives equip the saw automatically. Topic-specific prepared stations cover the same complete anatomy at each difficulty, and bilateral chest regions have separate required removal meters. Accepted regional strokes rebuild the opening on resume; a completed objective cannot be credited again by restoring or grabbing its old tissue.

Version 1.4.1 retained the then-current palette and console layout. Case lessons begin with the patient summary and controls. Live tool handling feedback sits inside the current-step surface; instrument labels and working-end markers connect the text with the 3D object. Guided routes have numbered points and a direction guide. A translucent tool preview never collides or scores. Source details stay inside VR until the learner explicitly opens the website; return restores the retained activity after both focus and pause recover.

## Do's and Don'ts

- Do use shared glass surfaces and button sizing instead of page-specific copies.
- Do keep navigation, status, captions and fixed actions outside the scrolling content they control.
- Do show difficulty locks, availability and account state in words.
- Do preserve source anatomy proportions, provenance and actual controller press, hold, drag, release and scroll.
- Do separate source checks, player captures, simulator checks and physical-headset acceptance.

- Don't let wrapped text or slider handles draw over adjacent controls.
- Don't add runtime blur, screen-space refraction or animated panel motion to simulate glass.
- Don't restore the desktop anatomy gameplay path or let camera fitting move the learner's headset.
- Don't award credit from a UI finish button or a teaching aid.
- Don't infer comfort, clinical validity or headset FPS from screenshots or simulator tests.
