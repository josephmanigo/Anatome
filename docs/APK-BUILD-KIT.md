# Build Anatome 1.6.0 on the existing Windows PC

To start the build from GitHub and download the APK from Releases, follow [Build and download the APK on GitHub](GITHUB-APK.md). The workflow downloads the project automatically after the Windows build PC is connected.

For a Git checkout, pull the current branch, run `git lfs pull`, then double-click `Build-New-APK.cmd`. The launcher checks for unresolved asset pointers before building the current checkout. ZIP deliveries additionally verify every file against the included build-kit manifest.

This download is a source/build kit, not an APK. It contains the current draft's museum, controller zoom, focused identification models and accessibility updates. It targets **version 1.6.0 / Android code 20**, package **org.thoraxlab.learning**.

1. Extract the entire ZIP into a new, short folder, such as `C:\Anatome-1.6.0`. Keep this separate from the previous project and APKs.
2. Use the same Windows account used for the previous Anatome production build. Unity **6000.3.19f1** must already be activated, with Android Build Support, SDK/NDK, OpenJDK and SDK build-tools **36.0.0**. Python **3.9+** must be available. Connect to the internet so Unity can restore its packages.
3. Double-click **Build-New-APK.cmd**. Keep its console open. The initial import can take time.
4. When it prints **APK READY**, the output folder opens with `builds\AnatomeSchool\Anatome-1.6.0-Production.apk` selected.

The launcher verifies every included file, checks the existing signing identity, prepares the Unity project, runs the new identification/zoom/museum simulator checks, then builds and signs the APK. It verifies the Android package/version, ARM64 native libraries, signing certificate, rotation lineage, payload equivalence and 16 KB alignment. It refuses to overwrite an existing production APK.

The release identity is read from `%LOCALAPPDATA%\Anatome\Signing` on that PC. No private key or password is included in this download, and no replacement key is generated. The original certificate is required to update the installed app while retaining its data.

Build results are recorded in `qa\current-apk\build-result.json`. Simulator results are in that same folder, Unity logs are `qa\current-apk-*.log`, and signing evidence is under `qa\school-signed-release`. If the build stops, retain those logs for diagnosis; it has not produced a verified new APK. Do not uninstall the previous app as a workaround.

To specify a Python executable or check requirements without building, run from a PowerShell window in the extracted folder:

```powershell
.\pipeline\build-current-apk.ps1 -Python 'C:\Path\To\python.exe' -PreflightOnly
```

Cloud validation covers the learning tests, movement harness, C# syntax, launcher parsing and archive integrity. Unity compilation, simulator checks, Android packaging and signing must execute on the Windows PC. A successful build still needs a physical Quest test.
