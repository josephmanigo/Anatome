"""Check the packaged FOV/guide code; does not run the APK on a headset."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from zipfile import ZipFile

root = Path(__file__).resolve().parents[2]
apk = root / "builds/AnatomeSchool/Anatome-1.5.1-Production.apk"
metadata_file = root / "unity/ThoraxLab/Library/Bee/artifacts/Android/il2cppOutput/data/Metadata/global-metadata.dat"
checks = []

def check(name, passed):
    checks.append({"name": name, "passed": bool(passed)})

with ZipFile(apk) as archive:
    check("Every APK entry passes CRC", archive.testzip() is None)
    metadata = archive.read("assets/bin/Data/Managed/Metadata/global-metadata.dat")
    check("Packaged metadata matches the fresh Unity Android build", metadata == metadata_file.read_bytes())
    for marker in ("simulatorFov", "comfortFov", "UpdateDissectionGuide", "TryGetCutGuide",
                   "Dissection visual guide - no collision or scoring", "Hold trigger and sweep", "Grip and lift"):
        check("Packaged " + marker, marker.encode() in metadata)
    for marker in ("FovGuideRuntime", "LearningVRSimulatorVerificationRunner"):
        check("Excluded QA fixture " + marker, marker.encode() not in metadata)

report = {
    "verifiedUtc": datetime.now(timezone.utc).isoformat(),
    "apk": str(apk), "sha256": hashlib.file_digest(apk.open("rb"), "sha256").hexdigest(),
    "method": "Read packaged IL2CPP metadata and ZIP CRC; compare with fresh build metadata",
    "passed": all(c["passed"] for c in checks), "physicalDeviceTested": False, "checks": checks,
}
(Path(__file__).parent / "apk-feature-verification.json").write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
assert report["passed"], "Packaged feature verification failed"
