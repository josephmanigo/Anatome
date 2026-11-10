# ANATOME 1.5.0: glass panels and logout

Signed Quest APK: `builds/AnatomeSchool/Anatome-1.5.0-Production.apk` (version code 18).

The native console now uses smoked-blue glass shading, bevelled edges, a dimensional panel rim, bold labels and sea-glass primary actions. Header navigation sits below the brand/grip row. Sibling controls share width and grow together for wrapped labels. Guide and settings actions retain reserved space, and slider handles stay within their travel area. Maximum text size and high contrast remain supported.

**Log out** appears in the header only for a signed-in account. It saves the current lesson, ends the current-device session and returns to sign-in. It blocks while cutting, while an account request runs, or when saving fails. Account-isolated progress and existing PIN-protected offline access remain. The isolated account test verifies logout, sign-in and checkpoint resume. No backend or scoring changes were made.

## Verification

- Core learning suite: 105 assertions passed.
- Development player: 1,014 checks, zero errors, 17 captures at normal/maximum text size and high contrast.
- Official XR Input System simulator: 1,555 checks, zero errors, no runtime tracking bypass. Includes pose checks and same-frame controller button callbacks.
- Windows development and Android release builds: zero errors and zero warnings.
- Existing classroom signing certificate reused. Strict package checks, certificate lineage, 16 KB alignment and all 1,346 application-entry hashes verified.
- Signing permissions check: eight assertions. The helper now verifies already-correct owner/SYSTEM-only access before attempting an unnecessary permission reset.
- Independent finish review: all four findings resolved; disposition `ship` within the review's stated scope.

`verification-summary.json` records source hashes and the final APK hash. `release/` contains only this release's copied verification receipts. `glass-ui-report.json`, `xr-ui-audit.json`, `windows-build.txt`, `android-build.txt` and `finish-verdict.md` contain detailed evidence. Final rendered images are in `.impeccable/review/glass-ui-final/`.

Earlier evidence is retained: `.impeccable/review/glass-ui/` contains the initial slider failures; `xr-initial-report.json` contains the hidden-Editor missed-click failure. The final XR audit restores focus and verifies the queued controller pose before clicking. No production input behavior was changed for that fixture issue.

## Device handoff

Install the signed 1.5.0 APK with the existing Quest sideload workflow. Keep the existing app installed if checking an update that preserves progress. In the headset, sign in, start a lesson, use **Log out**, sign back in and check **Continue your activity**.

Physical installation/update preservation, headset comfort/performance and live cloud sign-in were not retested. Software captures and signing verification do not establish those results. Logout affects the current device session; it does not revoke every device's session or remove existing offline enrollment.

APK SHA-256: `247963317147078fe18fde7084458d4cebefa7a21d3bff8cdafa00a3e6d93188`.
