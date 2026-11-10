# Anatome classroom release — 1.4.9

**Production-signed release for private school/classroom distribution.** Package **org.thoraxlab.learning**, version **1.4.9**, Android version code **17**, curriculum **2026.10.2**.

- APK: `builds/AnatomeSchool/Anatome-1.4.9-Production.apk`
- Classroom ZIP: `builds/Anatome-1.4.9-Production-Classroom.zip`
- Current release record: `qa/school-signed-release/verification-summary.json`
- APK SHA-256: `bb3d3bf8a82a5e1b150ffc4d9429fe0de4ebf0720697dd6bee269f1e95e47dbc`

## Acceptance and release scope

On 16 October 2026 (Asia/Shanghai), the user confirmed **“Both Quest 2 and Quest 3 — version 1.4.9”**, reported no problems, and confirmed login works. These are accepted user-reported functional results. The APK hash is associated with the project artifact for that version; the user did not independently provide a file hash.

The production APK contains the **same 1,346 application entries** as the tested classroom candidate, including its manifest, code, native libraries and resources. Only signing metadata changed. All **30 strict APK checks pass**, including the permanent non-debug signer and Android school/privacy policy. The underlying core, content, database, native, simulator, scene and release-build evidence remains in `qa/production-school-20261016`; its old pending-device/signing summary is preserved as history and superseded by the current release record.

The APK uses a permanent release certificate and a two-certificate Android V3 rotation proof from the previous debug certificate. Android's verifier accepts it for API 29–36 with installed-data capability enabled and old-key rollback disabled. This is the supported update mechanism for the app's minimum Android API 29. No headset was connected for an actual installation of this newly signed artifact. **Confirm one in-place update and retained progress before installing it across the classroom.** Your completed functional tests do not need to be repeated merely because the APK was signed.

General functional acceptance does not claim a measured FPS minimum, a timed thermal run or every offline/account recovery path. This release is prepared for private classroom distribution; no public store submission or clinical certification is claimed.

## Included improvements

- Refined case head, skin, vessel and tray presentation from 1.4.8 is retained, along with the four learning modes and source attribution.
- Automatic headset refresh requests the highest rate the runtime reports. **There is no fixed 72 FPS cap.** Actual frame rate depends on headset load and the selected graphics settings.
- Privacy and shared-headset guidance is available before login and in Account & saves.
- Android backup and device transfer exclude app data, including device-bound credentials and local saves. Account sync and explicit learner records remain available. Cleartext traffic is disabled.
- Build checks reject private/service-role keys and invalid or non-HTTPS account configuration. Release verification pins the signing certificate and validates the packaged policy.
- The signing pipeline preserves application contents, verifies its rotation chain, and refuses to overwrite an issued production APK.

## Install and update

1. Finish or pause the lesson, connect online and use **Sync now**. Confirm success. Export progress if a separate readable record is needed; exports are not automatic restore files.
2. Use Meta Quest Developer Hub to **update the existing app** with `Anatome-1.4.9-Production.apk`. It retains the package name and carries the verified signing rotation proof. Do not uninstall or clear app data.
3. Open the app and confirm version 1.4.9, the existing account and expected progress. Record the headset, OS, date and APK hash for this installation check, then deploy to the remaining headsets.
4. If Android refuses the update, stop and retain the installed app and its data. Preserve the installation error for diagnosis. Do not bypass it with uninstall, data reset or forced downgrade.

Keep this APK and ZIP with the school's release record. The old debug-signed candidate remains an archive, not a rollback installer after key rotation. A corrective release should have a higher version code and use the same permanent key and lineage.

## Shared headset routine

- Guest practice is session-only and requires no email. It does not create a persistent learning record.
- For saved learning, use a school-approved learner email. Enable trusted offline PIN access only where the school permits that email to appear on the headset's sign-in page.
- Finish/pause, connect online, **Sync now**, then sign out before handing the headset to another learner. Remove trusted offline access for accounts that should no longer appear.
- Offline PIN access retains local work. Reconnect and sync before switching devices; unsynced work cannot be recovered from cloud storage.
- Replace a forgotten PIN through fresh online email-code sign-in. Do not delete an account to solve a network issue.
- Account deletion affects cloud data and the current device. Other offline devices and previously exported files need separate cleanup. The school controls account eligibility and retention of exported records.

## Future releases

Use [VR-RELEASE-SIGNING.md](VR-RELEASE-SIGNING.md) for the permanent key, backup procedure and canonical build command. Keep private signing materials outside the project and classroom ZIP. Increase both the app version and Android version code before issuing a new application payload; retain its evidence separately.

The prior automated suite covered 896 core checks, 1,761 content checks, 263 local database checks, 105 native checks, 89 native-player checks, 2,411 full XR simulator checks, 1,048 isolated UI checks, 72 scene scenarios, 22 readiness regressions and 16 build-policy checks. Five live API probes were anonymous and sent no emails. The user confirmation supplies current login evidence; the anonymous probes are not relabeled as authenticated testing.
