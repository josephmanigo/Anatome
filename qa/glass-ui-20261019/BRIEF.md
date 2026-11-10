# ANATOME glass console

User request: revamp the in-game panels and buttons, make them premium and like 3D glass, keep buttons consistently sized without overlap, and add a working logout button for signed-in users.

Mode: Operate. Native Unity world-space UI for PC VR and Quest. Keep the existing learning modes, curriculum, controller ray input, panel movement, accessibility controls and account storage.

Direction: user-pinned smoked blue glass. A dimensional outer edge and vertically shaded, bevelled controls create material depth. Ivory Source Sans 3 text, sea-glass primary actions, and equal sibling button geometry preserve readability. The header separates the brand/grip area from equally sized Guide, Menu/Pause and signed-in Log out buttons. Main content scrolls independently of status and fixed settings actions. No animated camera motion or screen-space refraction.

Implementation: shared LabWidgets, LabUIVisuals, LabUI header/modal/keyboard, fixed guide/settings controls, and LabRuntime.LogOut. UI scaling changes control height and accommodates wrapped labels. High contrast disables decorative shading.

Logout: current-device session, using the existing AccountService.SignOut event flow. Save before logout; block during cutting, account requests or save failure. Retain account-isolated progress and existing PIN-protected offline access. No backend/schema changes or all-device revocation.

Verification: development-player layout captures at normal and maximum text sizes; real XR ray dispatch; synthetic offline account in isolated QA storage; save-failure and busy guards; logout, sign-in and checkpoint resume. Separate official XR simulator check and Android compilation. Physical-headset acceptance requires a wearer.

Source baseline is in source-before. Final Unity QA captures and report are in .impeccable/review/glass-ui-final; the earlier glass-ui folder retains the initial failed slider audit. The native UI is outside the HTML/CSS detector's scope. Existing source anatomy artwork is reused without edits.
