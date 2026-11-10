ANATOME 1.4.6 - ALL-MODE AUDIT AND REPAIRS
Package: org.thoraxlab.learning. Android version code: 14. Curriculum: 2026.10.2.

Use Anatome-1.4.6.apk in Meta Quest Developer Hub to update the existing app.
Extract the ZIP first if using the complete retest package.
Keep the installed app and its data; do not uninstall or clear its data.
The agent has not installed or launched this update on the headset.

Additional fixes found by the audit
- Applying Practice hints off hides existing clinical-choice hint cues on the current decision.
- Turning hints back on does not automatically expose an answer. An explicit practice hint still works and does not submit a choice.
- The panel header has enough height for its brand and grip instruction at maximum text scale.
- Dissection instructions and cut feedback now describe anchored whole layers and detached-chunk pickup consistently.
- The settings Apply/Cancel footer, player/table height, graphics/audio controls, movable and rotatable panel, single-chunk selection and earlier cutting fixes remain included.

Local verification
- Official XR devices: 2,349 checks passed, no failed checks or captured runtime errors, runtimeQaBypass=false.
- Repaired compiled-player scenes: 72/72 activities completed, zero reported errors or text overflow; 22/22 readiness regressions passed.
- All four modes passed six Easy, six Medium and six Hard scene attempts each. The Anatomy Cases group includes pneumothorax, asthma, coronary disease, pneumonia, pleural effusion and aortic stenosis.
- Core learning: 889 assertions passed. Content: 1,761 checks passed. Native cutting: 105/105 passed. Local PostgreSQL: 263 checks passed, including all 72 server rubrics and account isolation.
- The Windows QA build passed with zero errors or warnings. Android build, APK validation, exact hashes and signer identity are recorded in verification-summary.json.
- The scene fixture uses an isolated QA tracking bypass. It proves scene/scoring behavior, not physical VR acceptance. The separate official XR suite uses normal tracking gates.

Evidence
- audit-1.4.5.json records the three initial findings. The original scene run completed all 72 activities but reported 12 header text-bound overflows.
- settings-baseline.json reproduces the clinical hint and instruction defects before repair.
- xr-final.json, player-fixed/runtime-report.json and player-fixed/readiness-regressions.json record repaired behavior.
- verification-summary.json binds the tests to the exact APK and its manual-installation state.

Physical retest
1. Load 1.4.6 through Developer Hub. Test the standalone ANATOME app.
2. Change settings in each category and Apply. Confirm visible/audible effects and saved values after reopening. Include Extra player height and Table height offset.
3. Grip the panel header, move it, turn your wrist, and release. Apply an unrelated setting and check that its position remains.
4. In Guided Dissection, cut and release the trigger, then grip one detached chunk, lift and release it. Point across other anatomy while holding it and confirm no extra objects join the selection.
5. In Anatomy Cases practice, reveal a choice hint, turn Practice hints off and Apply. Confirm both cue dots disappear without answering the step.
6. Check maximum text size and clinical tool-name readability from normal working positions. Long instrument names looked crowded in one oblique desktop QA capture and still need headset review.

Limits
No physical acceptance of this exact APK has occurred. Controller feel, comfort, sustained headset performance, actual supported refresh rates, Android Keystore behavior and live email/cloud operations remain unverified. The APK uses Android Debug signing for controlled sideload review; publisher/store signing is separate.
