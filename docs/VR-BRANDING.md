# Anatome branding

The learning application is now named **Anatome**. The game header, welcome text, Unity editor menu, app company/product display metadata, legacy scene sign, first-party attribution heading, email-code templates and school test kit use the new name.

Open the existing `unity/ThoraxLab` project and choose **Anatome → Open VR learning scene**. The simulator toggle is **Anatome → XR Device Simulator (Editor only)**. The internal project folder, scene paths, namespaces and stable anatomy IDs are retained so references remain valid.

New build outputs are `builds/AnatomeWindows/Anatome.exe`, `builds/AnatomeVRTest/Anatome.exe` and `builds/AnatomeQuest/Anatome.apk`. Earlier ThoraxLab-named files are historical builds. Use the newly packaged Anatome school kit for the school test.

## Existing account and save compatibility

Supabase project identity, public configuration, database tables, user IDs, account deletion route, Android package ID `org.thoraxlab.learning`, encryption format and Android Keystore alias stay the same. The app remains an update to the same Android package.

Unity derives its Windows persistent directory from company/product metadata. `LearningBrand.DataRoot` explicitly retains the established `LocalLow/Thorax Lab/Thorax Lab/Learning-v1` directory when the app is branded Anatome. No learner files are moved or reset. Separate verification product names continue to use isolated storage. Other platforms keep Unity's normal package-based data path.

Six added core checks cover old/new Windows paths, isolated QA storage, Android storage, reading an actual legacy atomic save through the renamed app's resolved path and avoiding a second empty profile directory. The full core suite passes 775 assertions.

## Email branding

The existing live Supabase project has both Confirm sign up and Magic link or OTP templates set to **Your Anatome sign-in code**, with **Sign in to Anatome** in the body. The six-digit token placeholder and ten-minute explanation are retained. Both saved templates were read back after reloading the dashboard. The configured sender display name is already Anatome. SMTP credentials and the sending mailbox were not changed.

## Verification and review

`qa/branding` contains the core test output, fresh player captures, scenario results and branding verification record. The renamed Windows player's `Anatome_Data/app.info` identifies both company and product as Anatome. The APK inspector checks the Android launcher label explicitly, alongside the stable package identifier and existing archive/native/signature checks.

This is a branding revision of the review candidate. Physical Quest testing and production signing remain outstanding; the school APK is debug-signed for sideload testing. The earlier non-performance report records the prior functional/account verification and is not rewritten as a new live-account test.
