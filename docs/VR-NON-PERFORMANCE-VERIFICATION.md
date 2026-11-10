# Verification excluding VR performance

The 2026-09-07 local verification covers the current `unity/ThoraxLab` source and the fresh Windows/Quest review artifacts. VR performance is **excluded at the user's request**, not marked passed. The machine-readable result is `qa/non-performance-verification/verification-report.json`; `qa/vr-learning-delivery-manifest.json` identifies the delivered artifacts. The user's open Editor and normal learner account were preserved.

## Live account results

**38 live checks passed**, using the actual Unity `AccountService`, sign-in input fields and button handlers in the isolated `ThoraxLabReview` Editor. Only `j.oginam2+thorax-qa-a@gmail.com` and `j.oginam2+thorax-qa-b@gmail.com` were used. The user explicitly authorized their reset and permanent deletion; both operations completed.

- Email-code sign-in for both accounts and a distinct second authenticated session for account A.
- Incorrect/malformed code rejection and the 60-second client resend restriction.
- Windows DPAPI-protected storage, incorrect PIN rejection, offline unlock after service restart, online refresh, and fresh-email requirements for resetting a PIN.
- Completed assessment persistence, mastery, repeated sync without duplicate rewards, concurrent sessions retaining separate attempts, and safe checkpoint updates.
- Real account backup recovery, settings-profile save/read/delete, export without authentication secrets, sign-out, offline resume and account switching.
- RLS hiding account A's records from B, client/server rejection of foreign-account writes, and preservation of the other account's progress.
- Reset generations preventing an independent stale session from restoring deleted progress; account deletion clearing local access and rejecting the old account and refresh token.

**Nine additional live recovery checks passed**, covering actual interrupted Unity web requests, pending-save preservation, reconnection retry, sign-out cancellation, server reset generation, stale-client protection and repeated profile reads/deletes. Evidence: `live-account-report.json` and `cloud-recovery-report.json` in the verification directory.

These were independent authenticated sessions on one Windows computer, not two physical devices. The code emails were visible in the school sender's Gmail Sent folder and successfully authenticated in Unity. Recipient Inbox placement was not observed. A newly issued, unredeemed code was not left for ten minutes to time its live expiration boundary. No OTPs, passwords or session tokens are included in the reports.

## Bug found and fixed

Reading or deleting account profiles created unwanted **My settings** entries. Unity `JsonUtility` serialized the nullable profile field as a new default object, including a generated profile ID. The server correctly treated that object as a profile to save.

Read requests now serialize as `{}`. Delete requests contain only `delete_id`. Save requests retain the explicitly supplied profile. The failing serialized payload, the successful replacement payload, the original live failure and the successful rerun are retained. Three targeted regressions run in the real Unity player as well as the Editor. No server-data cleanup was applied to the user's normal account.

## Software and interaction verification

| Check | Result | Evidence |
|---|---|---|
| Windows scene scenarios | 72/72, no reported errors | `player/runtime-report.json` |
| Player regressions | 22/22 | `player/readiness-regressions.json` |
| Official XR Device Simulator | 74/74, no runtime tracking bypass | `qa/vr-xr-simulator-report.json` |
| Core scoring, progression and persistence | 769 assertions | `core-tests.txt` |
| Curriculum/source-contract validation | 1,451 assertions | `qa/vr-content-report.json` |
| Local PostgreSQL policies and rubrics | 251 checks | `qa/learning-backend-report.json` |
| Native cutting library | 81/81 | `native-tests.txt` |
| Windows release isolation | Passed compiled-IL inspection | `qa/vr-release-gate-report.json` |

The simulator check exercises normal XRI routing, first-person gloves, orientation wrist turns, bounded lifting, tracking/menu interruption, grip rearming, assembly movement and instrument handling. Scene fixtures exercise all 72 variants; they are automated positive-path assessments, not a human completion of every lesson and activity. Negative-path checks are provided by the targeted core, database and player regressions. The player captures include large text, captions, keyboard, cards, exposed layers and case workstations. Direct inspection of the large-text keyboard and mode cards confirmed centered keys and scrollable content; this does not establish physical stereo readability.

The compiled normal Windows player requires XR tracking and excludes Editor simulation, runtime verification bypasses and the Editor-only network-interruption hook. QA fixtures and the local verification portal do not ship as an Editor assembly in that player.

## Remaining release work

The Quest package is a **debug-signed sideload review artifact**. The user confirmed that no publisher release keystore exists. Normal APK inspection verifies the archive, ARM64/native library, packaged notices and signature; strict `--require-release-signing` correctly rejects the debug certificate. [VR-RELEASE-SIGNING.md](VR-RELEASE-SIGNING.md) gives the publisher-owned key setup and verification procedure.

No physical Quest was connected during this pass. Android Keystore operation, real headset stereo readability, reach, controller alignment, comfort and cross-platform resume therefore remain unverified independently of performance. Recipient Inbox placement, a timed unredeemed-code expiry test, and first-time human usability/visual acceptance also remain. These are not replaced by passing automated tests.

Research-led content retains the source-backed processes and explicit model/mechanical simplifications in [VR-CASE-SOURCES.md](VR-CASE-SOURCES.md). No professional-validation approval gate was added. This verification does not claim clinical certification, complete real-world procedure fidelity, a subjective 10/10 rating or unconditional production readiness.
