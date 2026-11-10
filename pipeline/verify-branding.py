"""Record checks for the Anatome branding revision without relabeling prior QA."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
def read(path):
    return (root / path).read_text(encoding="utf-8-sig")
def report(path):
    return json.loads(read(path))
def sha(path):
    with (root / path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

runtime = report("qa/branding/player/runtime-report.json")
regressions = report("qa/branding/player/readiness-regressions.json")
apk = report("qa/learning-apk-verification.json")
email = report("qa/branding/email-templates.json")
kit = report("qa/branding/school-kit.json")
delivery = report("qa/vr-learning-delivery-manifest.json")
assert "PASS 775 assertions" in read("qa/branding/core-tests.txt")
assert runtime["sceneAttemptsPassed"] == runtime["variants"] == 72 and not runtime["errors"]
assert regressions["passed"] and len(regressions["checks"]) == 22
assert all(check["passed"] for check in regressions["checks"])
assert apk["failures"] == 0 and len(apk["checks"]) == 12
assert any(c["name"] == "Application display name" and c["passed"] and c["detail"] == "Anatome" for c in apk["checks"])
assert apk["package"] == "org.thoraxlab.learning"
assert sha(Path(apk["apk"])) == apk["artifactSha256"] == kit["apkSha256"]
assert kit["verified"] and sha(Path(kit["archive"])) == kit["archiveSha256"]
assert email["passed"] and not email["testEmailSent"]
assert read("builds/AnatomeWindows/Anatome_Data/app.info").splitlines() == ["Anatome", "Anatome"]
for platform in ("StandaloneWindows64", "StandaloneWindows64-QA", "Android"):
    assert read(f"qa/learning-build-{platform}.txt").splitlines()[:3] == ["Succeeded", "0 errors", "0 warnings"]
assert "Application will terminate with return code 0" in read("qa/anatome-final-compile.log")
for artifact in delivery["artifacts"]:
    assert sha(Path(artifact["path"])) == artifact["sha256"]

main = root / "unity/ThoraxLab/Assets"
review = root / "unity/ThoraxLabReview/Assets"
def files(folder):
    return {p.relative_to(folder).as_posix(): p for p in folder.rglob("*") if p.is_file()}
main_files, review_files = files(main), files(review)
assert main_files.keys() == review_files.keys()
differences = sorted(name for name in main_files if sha(main_files[name]) != sha(review_files[name]))
assert set(differences) <= {"ThoraxLab/Resources/Fonts/SourceSans3-SDF.asset"}
source_hashes = {name: sha(path) for name, path in main_files.items() if path.suffix == ".cs"}
result = {
    "utc": datetime.now(timezone.utc).isoformat(),
    "productName": "Anatome", "passed": True,
    "scope": "Branding revision; rebuilt Windows and Quest artifacts, save compatibility and local player checks",
    "coreAssertions": 775, "addedSaveCompatibilityChecks": 6,
    "sceneScenarios": 72, "playerRegressions": 22, "apkChecks": 12,
    "windowsCompanyAndProduct": ["Anatome", "Anatome"],
    "androidPackage": apk["package"], "androidApplicationLabel": "Anatome",
    "buildErrors": 0, "buildWarnings": 0,
    "emailTemplateReadback": email,
    "mainAndReviewAssetCount": len(main_files),
    "mainAndReviewAssetDifferences": differences,
    "assetDifferenceExplanation": "Only Unity-generated TextMeshPro font feature-cache serialization differs; source scripts, scene and other assets match.",
    "sourceSha256": source_hashes,
    "schoolKit": kit, "artifacts": delivery["artifacts"],
    "priorEvidenceNotRerun": {
        "xrSimulator": "qa/vr-xr-simulator-report.json",
        "liveAccounts": "qa/non-performance-verification/live-account-report.json",
        "cloudRecovery": "qa/non-performance-verification/cloud-recovery-report.json"
    },
    "physicalQuestTested": False, "performanceTested": False,
    "productionRelease": False, "signing": apk["signing"],
    "screenshot": "qa/branding/player/vr-signin.png"
}
(root / "qa/branding/verification-report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print("PASS Anatome branding: 775 core assertions, 72 scenarios, 22 regressions, 12 APK checks; both delivery archives verified.")
