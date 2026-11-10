# Anatome 1.2.0 — guide, female narration and thorax completeness audit

Revision: `guide-female-voice-20261004`; curriculum remains `2026.10.1`. This update continues the controller repair and advanced procedural learning work. The current machine-readable verdict is `qa/guide-voice-audit/verification-report.json`; do not treat a previous build's report as acceptance of this APK.

## Delivered behavior

The top navigation has **Guide** on every normal screen, including before sign-in. Ten illustrated pages explain setup, pointing, grip/release, orientation, learning phases, identification, assembly, dissection, cases, progress and troubleshooting. **Next**, **Previous** and **Close guide** remain outside the scrolling content. The controller Menu action also closes the guide. Reopening remembers its page within the running session.

The guide overlays the current screen and blocks specimen interaction. Closing preserves the current objective, attempts, score and page state. Generic controls help does not mark objectives complete or reveal assessment answers. Medium/Hard assembly assessment instructions explicitly explain that placement outlines are absent.

All 321 bundled WAV clips use **Microsoft Zira Desktop**, an installed female English voice, at synthesis rate −1. The tone is a calm synthetic educational guide, requested as nurse-style narration; no human nurse performance or clinical qualification is claimed. Guide audio comes from the same text students see. Voice identity, pace and transcript enter each cache hash, so former male recordings cannot survive a valid cache hit. The guide can pause/resume or replay its voice; lesson audio resumes on closing only if it was playing when the guide opened. Focus loss pauses audio without overriding a learner's manual pause on recovery. Volume and playback speed follow saved settings.

## Anatomy coverage and limits

All **152 selected source records** resolve into the shipping catalog. The **249 catalog entries** comprise **243 tissue entries and six reference annotations**. Required groups include 24 ribs, 24 costal cartilages, 12 thoracic vertebrae, 11 discs between those vertebrae, five lung lobes, main/lobar airways, pleura, diaphragm, chamber walls and regions, four valve groups, great/coronary/pulmonary vessels, chest-wall layers, thoracic nerves and duct. Group counts are recorded in `assets-report.json`.

The runtime audit checks exact one-to-one scene membership, nonempty meshes, valid surface materials, tissue collision/grab components, and reference exclusions. The source audit checks identity and provenance records and every selected source's presence. Existing native cutting, preserved-cavity and FBX round-trip evidence remains in `qa/reports/`; the current scenario run tests the shipped activity paths. These checks are distinct from inspecting every possible cut or anatomical variant.

This is a gross-thorax teaching specimen. Its catalog is complete against the selected source inventory; it does not model the whole body or every subdivision. Four cardiac cavity annotations represent empty space; the two septal annotations locate regions in existing source walls and are not separately segmented septal tissue. Fascia/fat, membranes and some nerve/vessel paths are documented approximations. The original source meshes and source records are retained. No new speculative anatomy was introduced to inflate completeness.

The cage checklist was cross-checked against the publisher's description of ribs, costal cartilage and thoracic vertebrae. Heart and respiratory references clarify that a complete biological structure is more detailed than this finite teaching inventory. Sources: [OpenStax thoracic cage](https://openstax.org/books/anatomy-and-physiology-2e/pages/7-4-the-thoracic-cage), [heart anatomy](https://openstax.org/books/anatomy-and-physiology-2e/pages/19-1-heart-anatomy), [respiratory structures](https://openstax.org/books/anatomy-and-physiology-2e/pages/22-1-organs-and-structures-of-the-respiratory-system). These sources support the anatomical relationships, not a claim that they reviewed this application.

## Verification and review

`qa/guide-voice-audit/assets-report.json` records 1,907 asset, source-inventory, female-voice cache and guide/transcript checks. `xr-final-report.json` contains the official Input System/XRI controller run with ordinary tracking gates, including the existing controller/ray/button suite and new guide navigation, audio, interruption and assessment checks. `player/runtime-report.json` records all 72 scenario fixtures and rendered screenshots; that development player uses an explicit QA tracking bypass. The normal release's exclusion of that bypass is checked separately in `vr-release-gate-report.json`.

Independent standards review found no actionable defect. Requirement review found one P2: the initial assembly help promised a placement outline during advanced assessments. The visible text and corresponding narration were corrected and re-reviewed with zero remaining findings. The first new XR run stopped on a test-fixture error: recreating the guest erased its orientation unlock before the guide's activity test. The corrected fixture retains the real controller-completed orientation. Its original failure report is preserved as `xr-fixture-initial-failure.json`.

A subsequent startup-audio regression reproduced a real edge case: a new guide AudioSource retained default volume/speed until Update, allowing a muted guide to start at full volume for its first frame. `LessonNarrator.Play` now applies saved audio settings before playback. `guide_respects_mute_from_first_sample` inspects the actual guide AudioSource immediately, before Update; the failing evidence is preserved as `xr-muted-startup-failure.json`. Final build and runtime results must postdate this correction.

The curriculum and scoring migration hashes remain identical to the already deployed October rubrics. This UI/audio revision does not require another scoring migration. Current learner sign-in and cross-device persistence remain separate acceptance work.

## Release boundary

The user confirmed the physical Quest 3 is unavailable. This revision therefore cannot establish physical headset readability, reach, comfort, narration intelligibility, controller/hand transitions or Android persistence. Sustained native 72 Hz performance needs an on-device run of at least 15 minutes. The sideload APK uses Android Debug signing; a public release needs publisher-owned signing. Current authenticated learner sign-in, save/reopen, offline recovery and Windows/Quest synchronization also remain open.

Use the exact ZIP and SHA-256 recorded in `BUILD-INFO.json` and the final verification report. The included school result form records physical acceptance without labelling untested checks as passed. Passing local checks does not justify a zero-bug or production-ready claim.
