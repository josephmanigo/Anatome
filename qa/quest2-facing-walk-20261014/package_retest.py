"""Package only verified local artifacts; never install or launch on a headset."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import re
import zipfile

root = Path(__file__).resolve().parents[2]
qa = Path(__file__).resolve().parent
apk = root / "builds/AnatomeQuest/Anatome-1.4.7.apk"
archive_path = root / "handoff/Anatome-1.4.7-Quest-Retest.zip"

def read(relative):
    return json.loads((qa / relative).read_text(encoding="utf-8-sig"))

def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

xr = read("full-xr.json")
focused = read("fixed-focused.json")
graphics = read("graphics-fixed.json")
player = read("player/runtime-report.json")
readiness = read("player/readiness-regressions.json")
apk_check = read("apk/learning-apk-verification.json")
baseline = read("baseline-repeat.json")
for report in (xr, focused, graphics):
    assert report["passed"] and not report["errors"] and not report["runtimeQaBypass"]
    assert all(check["passed"] for check in report["checks"])
assert player["sceneAttemptsPassed"] == player["variants"] == 72 and not player["errors"]
assert readiness["passed"] and all(check["passed"] for check in readiness["checks"])
assert apk_check["failures"] == 0 and apk_check["artifactSha256"] == digest(apk)
builds = {name: (qa / filename).read_text().strip() for name, filename in (
    ("android", "android-build-summary.txt"), ("windowsQA", "windows-build-summary.txt"))}
assert all(value.startswith("Succeeded\n0 errors\n") for value in builds.values())
assert (qa / "android-graphics.txt").read_text().startswith("OpenGLES3 only")
signature = (qa / "apk/learning-apk-signature.txt").read_text()
certificate = re.search(r"certificate SHA-256 digest: ([0-9a-f]+)", signature).group(1)
previous_signature = (root / "qa/game-mode-audit-20261011/apk-1.4.6/learning-apk-signature.txt").read_text()
assert certificate == re.search(r"certificate SHA-256 digest: ([0-9a-f]+)", previous_signature).group(1)

source_paths = [
    "Assets/ThoraxLab/Learning/LearningControls.cs",
    "Assets/ThoraxLab/Learning/LabUI.cs",
    "Assets/ThoraxLab/Learning/LabUI.Settings.cs",
    "Assets/ThoraxLab/Scripts/LabHandInteractor.cs",
    "Assets/ThoraxLab/Editor/LearningBuild.cs",
    "Assets/ThoraxLab/Editor/LearningVRSimulatorVerification.Facing.cs",
    "Assets/ThoraxLab/Editor/LearningVRSimulatorVerification.Settings.cs",
    "Assets/XR/Settings/OpenXR Package Settings.asset",
    "ProjectSettings/ProjectSettings.asset",
]
summary = {
    "verifiedUtc": datetime.now(timezone.utc).isoformat(),
    "status": "LOCAL_CHECKS_PASSED_MANUAL_HEADSET_RETEST_REQUIRED",
    "version": "1.4.7", "versionCode": 15, "package": "org.thoraxlab.learning",
    "apk": str(apk), "sha256": digest(apk), "bytes": apk.stat().st_size,
    "runtimeFixes": ["Measured startup heading", "Passive UI hover no longer captures walking"],
    "graphicsWorkaround": "OpenGLES3 only; Vulkan-only buffer discards disabled",
    "baseline": {"report": "baseline-repeat.json", "failedChecks": sum(not c["passed"] for c in baseline["checks"])},
    "xr": {"report": "full-xr.json", "checks": len(xr["checks"]), "errors": 0, "runtimeQaBypass": False},
    "focused": {"report": "fixed-focused.json", "checks": len(focused["checks"]), "errors": 0},
    "graphics": {"report": "graphics-fixed.json", "checks": len(graphics["checks"]), "presetTransitions": 15, "hardwareCrashReproducedInDesktopTest": False},
    "player": {"report": "player/runtime-report.json", "sceneAttemptsPassed": 72, "variants": 72, "errors": 0, "trackingBypass": True, "readinessChecks": len(readiness["checks"]), "readinessReport": "player/readiness-regressions.json"},
    "apkChecks": {"report": "apk/learning-apk-verification.json", "checks": len(apk_check["checks"]), "failures": 0},
    "builds": builds,
    "signing": apk_check["signing"],
    "signingCertificateSha256": certificate, "sameSignerAs1_4_6": True,
    "compiledPlayerAssemblySha256": digest(root / "builds/AnatomeVRTest/Anatome_Data/Managed/Assembly-CSharp.dll"),
    "installedOnHeadset": False, "launchedOnHeadset": False, "physical1_4_7Accepted": False,
    "deviceEvidence": {"report": "graphics-diagnosis.json", "observedVersion": "1.4.6", "nativeVulkanCrashes": 4, "savedPreset": "Low", "savedRefreshRate": 72, "latest30Samples": "72/72 FPS"},
    "limits": [
        "The full XR run precedes the later Android graphics API configuration and explanatory UI text. Separate graphics tests and final compiled-player checks cover those edits locally.",
        "An earlier full XR run lost interaction during a new hover test; its failed report is retained as full-xr-first.json. The diagnostic repeat passed all 2376 checks with normal focus and tracking gates.",
        "No physical confirmation that the graphics workaround prevents the reported crash; the user requested manual Developer Hub installation.",
        "No matched High versus Low benchmark, sustained thermal test, new cloud test or publisher release signing.",
    ],
    "sourceSha256": {path: digest(root / "unity/ThoraxLab" / path) for path in source_paths},
}
(qa / "verification-summary.json").write_text(json.dumps(summary, indent=2))

files = {
    "Anatome-1.4.7.apk": apk,
    "README-1.4.7.txt": qa / "README-1.4.7.txt",
    "verification-summary.json": qa / "verification-summary.json",
    "controls.md": root / "docs/VR-SETTINGS-GRAB-FIX.md",
}
for relative in ("baseline-repeat.json", "fixed-focused.json", "full-xr.json", "graphics-baseline.json", "graphics-fixed.json", "graphics-diagnosis.json", "android-build-summary.txt", "windows-build-summary.txt", "android-graphics.txt", "runtime-fix.patch", "player/runtime-report.json", "player/readiness-regressions.json", "apk/learning-apk-verification.json", "apk/learning-apk-signature.txt"):
    files[relative] = qa / relative
manifest = {name: {"sha256": digest(path), "bytes": path.stat().st_size} for name, path in files.items()}
archive_path.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for name, path in files.items():
        archive.write(path, name)
    archive.writestr("MANIFEST.json", json.dumps(manifest, indent=2))
with zipfile.ZipFile(archive_path) as archive:
    assert archive.testzip() is None
    for name, expected in manifest.items():
        with archive.open(name) as stream:
            assert hashlib.file_digest(stream, "sha256").hexdigest() == expected["sha256"], name
handoff = {"zip": str(archive_path), "sha256": digest(archive_path), "bytes": archive_path.stat().st_size, "entries": len(files)+1, "allEmbeddedHashesVerified": True, "apkSha256": digest(apk)}
(qa / "handoff-verification.json").write_text(json.dumps(handoff, indent=2))
print(json.dumps(handoff, indent=2))
