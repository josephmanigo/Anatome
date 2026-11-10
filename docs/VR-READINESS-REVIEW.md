# VR interaction and release-readiness review

This review responds to the screenshot of the chest being lifted toward the learner during orientation. It applies to `unity/ThoraxLab`, Unity 6000.3.19f1, with the official XR Device Simulator. The user's open Editor was left running; verification and builds use an isolated `unity/ThoraxLabReview` copy of the same source.

## Reproduced interaction defect

A stationary grip followed by an 80-degree wrist turn rotated the entire skin shell by 80 degrees and displaced it by 0.305 m. This explains why the orientation could bring the chest into the learner's face. The previous test only translated the controller, so it did not expose this behavior.

- `qa/readiness-review/baseline/rotation-reproduced.json` records the failure.
- `qa/readiness-review/baseline/rotation-reproduced.png` is the actual Unity frame.
- `qa/vr-xr-simulator-report.json` contains the subsequent regression results.

Orientation now uses a constrained upward lift, limited to 12 cm. Wrist turns do not rotate the chest. A deliberate upward lift of at least 4 cm followed by release completes orientation. Stationary release, opening Pause, or losing tracking returns the skin without granting completion. After an interruption, grip must be released before it can acquire anatomy again. Normal assembly and instrument rotation remain available.

## Corrected behavior

| Area | Defect and resulting behavior |
|---|---|
| Held anatomy | XRI detached a held structure from its parent while the assembly guide dereferenced that parent. Anatomy now retains its specimen parent during pickup; real grab, rotation, alignment feedback and release are covered by the simulator check. |
| Assembly scoring | Out-of-tolerance releases returned before assessment evaluation. Incorrect assessment releases now remain recorded after correction and Restore. Practice retains forgiving correction feedback. |
| Dissection preservation | Only the selected mesh's completed cut was inspected for protected-structure injury. An independent blade-overlap and swept-contact monitor now records contact with protected geometry, including when that geometry becomes selected. Undo and Restore retain the violation. Duplicate contacts are bounded. Saturated physics queries cancel the stroke and request a shorter retry. |
| Enumeration | Accepted selections place numbered markers at the actual selected surface. Completed objective IDs determine the set count. Markers rebuild from saved local contact positions; duplicate selections cannot inflate the set. |
| Interrupted controls | A grip held through tracking/menu recovery could reacquire anatomy. It now requires a release first. Cutting-tool visuals and their automatic hand curl are limited to an active cutting step. |
| Cloud resume | A newer checkpoint with an already-cached attempt ID replaced the checkpoint but left the resume attempt stale. Reconciliation now updates inactive attempts with newer, append-only evidence while protecting active work, completed records, identity and reset generation. This also permits same-device backup recovery. |
| Damaged settings | Two unreadable settings copies prevented startup. Defaults now permit startup, both damaged files are preserved, and the next successful save restores a valid backup. |
| Failed settings save | Apply/Delete changed the in-memory profile before a failed disk write. A candidate collection is now saved before it becomes current; Cancel cannot apply a failed draft. |
| Lighting settings | Presentation initialization overwrote saved brightness and shadows. Scene settings are reapplied after lighting exists. Temporary pipeline instances are disposed when their owner is destroyed. |
| Profile deletion | A local Delete appeared to remove an account profile. Device and account copies now have distinct, explicit deletion controls. |
| UI | The keyboard uses centered key labels, including letters, numbers and action keys. Orientation includes the actual simulator trigger/grip keys. Uninitialized UI refreshes no longer dereference missing runtime services after a script reload. Restart Play after script updates to reconstruct the full application state. |
| Tissue appearance | Strong cyan selection tint turned the skin green. Selection now preserves the tissue palette with a small brightness cue and a named selection status. |

## Visual reference and limits

The reference reviewed was [Human Anatomy VR 2025 by Virtual Medicine](https://www.youtube.com/watch?v=F9zJNhHAmVI), linked from the developer's [official anatomy bundle page](https://www.medicinevirtual.com/news/anatomy-vr-bundle-get-human-and-animal-anatomy-together). The trailer sequences inspected include the isolated heart, close-up vascular relationships, and sectioned/cadaver imagery. Its presentation supports a central specimen, surrounding controls, clear structure identification and progressive layer exploration. Those interaction and composition principles informed this review; its assets were not copied and this project is not represented as having its scanned cadaver or DICOM capabilities.

The dark console, Source Sans 3, first-person gloves, source specimen, one-step learning flow, four modes and Easy/Medium/Hard gates are retained. The original meshes and attribution remain intact. A normals-only skin experiment made no measurable normal changes and was excluded; it is not reported as a visual improvement. This pass does not establish a subjective “10/10” rating.

## Verification artifacts

- `qa/vr-xr-simulator-report.json`: real official simulator devices and normal XRI routing, without the runtime assessment bypass.
- `qa/readiness-review/player/runtime-report.json`: all 72 scenario variants exercised through scene fixtures in the Windows development player; captures include large text, keyboard, cards, layers and activities.
- `qa/readiness-review/player/readiness-regressions.json`: corruption recovery, failed-save behavior, scene lighting, assembly errors, enumeration markers and protected contacts.
- `qa/readiness-review/release-review.json`: final artifact hashes, test counts, source snapshot and release limitations.
- `qa/vr-release-gate-report.json`: compiled Windows release keeps headset tracking required and compiles the Editor simulator and QA bypass gates to false.
- `qa/learning-apk-verification.json`: packaged Quest ARM64/native libraries, attribution and signature inspection. This is not a physical Quest run.

The final JSON reports are authoritative for pass/fail and timestamps. Prior reports are retained as historical evidence and must not be presented as a current-build performance result.

## Release boundary

The subsequent verification excluding VR performance is recorded in [VR-NON-PERFORMANCE-VERIFICATION.md](VR-NON-PERFORMANCE-VERIFICATION.md). Actual Unity email-code sign-in, two independent authenticated sessions, account isolation, offline access on Windows, reset and account deletion now pass. These sessions ran on one computer; no physical Quest or cross-platform session is claimed. Recipient Inbox placement was not observed because test codes came from sender Sent copies. VR performance was explicitly excluded from that verification. The Quest artifact still uses a debug certificate, and the user confirmed there is no release keystore yet. Release signing, physical interaction/storage checks and the user's visual/ease-of-use acceptance remain before a production-release claim.

Live testing also found that Unity serializes a null optional settings profile as a newly constructed default object. Reading or deleting cloud profiles therefore created extra profiles. Separate read/delete request shapes now omit the profile field entirely. The retained failing and passing payload reports, live repeated reads/deletes, and three new player regressions demonstrate the correction. The fresh verification report supersedes this page's earlier artifact identities for current delivery.

The six cases retain the researched processes and documented abstractions in `VR-CASE-SOURCES.md`. No new professional-validation approval gate is imposed. Software tests and citations do not claim clinical certification or patient-specific treatment fidelity.
