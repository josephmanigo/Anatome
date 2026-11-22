# Build and download the APK on GitHub

The **Build downloadable APK** workflow downloads the project onto the existing Windows build PC, runs Unity checks, builds and signs the APK, then adds it to **GitHub Releases**. A missing project folder is fine: the workflow checks out the project and downloads the model assets automatically.

The PC supplies its existing Unity installation and private signing identity. GitHub supplies the build controls and APK download. No new APK exists until a run succeeds.

## Connect the Windows PC once

1. Sign into the same Windows account used for the previous Anatome production build. Close Unity.
2. Confirm Unity **6000.3.19f1** is activated and has Android Build Support, SDK/NDK, OpenJDK and SDK build-tools **36.0.0**. Install Python **3.9+**, Git and Git LFS if they are missing. Run `git lfs install` once in PowerShell.
3. Open [Anatome → Settings → Actions → Runners → New self-hosted runner](https://github.com/josephmanigo/Anatome/settings/actions/runners/new). Choose **Windows**, **x64**.
4. Follow GitHub's displayed download and configuration commands in PowerShell, using a short folder such as `C:\AnatomeRunner`. When configuration asks for **additional labels**, enter **`anatome-apk`**. Keep the default `self-hosted`, `Windows` and `X64` labels.
5. Choose **No** if asked to run as a service. Run **`.\run.cmd`** from the runner folder under the same Windows account. Leave this window open and the PC awake. Interactive execution gives Unity access to the desktop/GPU and the existing Windows-protected signing password.
6. GitHub's Runners page should show this PC as **Idle**. This is the PC's connection to GitHub; this chat cannot start software on your PC by itself.

The original signing files are expected in `%LOCALAPPDATA%\Anatome\Signing`: `identity.json`, `anatome-release.p12`, `password.dpapi`, `certificate.der` and `debug-to-release.lineage`. Deleting the project folder does not delete this private folder. A backup in Documents can be used for recovery if needed; its exact file names and password protection must be checked before restoring it. Keep the backup outside the project. [Signing and backup details](VR-RELEASE-SIGNING.md).

## Build the APK

1. Open [Actions → Build downloadable APK](https://github.com/josephmanigo/Anatome/actions/workflows/build-apk.yml).
2. Click **Run workflow**, choose **main**, then click the green **Run workflow** button.
3. Keep the Windows PC running until both jobs finish. Initial Unity import and Android compilation can take time.
4. Open [Releases](https://github.com/josephmanigo/Anatome/releases) after the workflow finishes. Select the release for the version you built and download its **Production.apk** under **Assets**.

Successful runs also retain the APK under the run's **Artifacts** for 30 days. If publishing fails after a successful build, that artifact is still available; rerun the failed publishing job to retry. Build diagnostics are retained for 7 days.

Each run creates a separate prerelease tag and records the exact source commit, APK checksum and certificate fingerprint. The existing release is not replaced. The new APK needs physical headset testing: install it with SideQuest or `adb install -r`, confirm existing account/progress still works, then test the Museum, controller zoom and focused identification.

## If it does not start or fails

- **Queued / waiting for a runner:** run `.\run.cmd` on the Windows PC and verify the `anatome-apk` label. No APK has been built while the job is waiting.
- **Missing Unity or Android tools:** add the listed components through Unity Hub. The build currently uses `C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor`.
- **Missing signing identity / cannot open credential:** use the original Windows account and inspect the private signing folder or recovery backup. A signing password alone cannot replace the keystore file. Do not put either into GitHub source or chat.
- **Failed Unity or APK check:** open the failed step and download the build logs from Artifacts. The workflow publishes only after all checks pass.

The workflow is manually triggered and only builds `main`. The Windows build job has read-only repository access; a separate GitHub-hosted job uploads the finished APK to Releases.
