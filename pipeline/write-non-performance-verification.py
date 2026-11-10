"""Bind completed software verification to current source and review artifacts."""
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[1]
out = root / "qa/non-performance-verification"

def read(path):
    return json.loads((root / path).read_text(encoding="utf-8-sig"))

def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def identity(relative):
    path = root / relative
    return {"path": relative, "bytes": path.stat().st_size, "sha256": sha(path)}

def require(condition, message):
    if not condition:
        raise RuntimeError(message)

def passed_checks(report, count):
    return len(report["checks"]) == count and all(c["passed"] for c in report["checks"])

live = read("qa/non-performance-verification/live-account-report.json")
recovery = read("qa/non-performance-verification/cloud-recovery-report.json")
runtime = read("qa/non-performance-verification/player/runtime-report.json")
regressions = read("qa/non-performance-verification/player/readiness-regressions.json")
xr = read("qa/vr-xr-simulator-report.json")
content = read("qa/vr-content-report.json")
backend = read("qa/learning-backend-report.json")
release = read("qa/vr-release-gate-report.json")
apk = read("qa/learning-apk-verification.json")
signing = read("qa/non-performance-verification/packaging/learning-apk-verification.json")
delivery = read("qa/vr-learning-delivery-manifest.json")
payload = read("qa/non-performance-verification/profile-payload-report.json")

require(live["status"] == "passed" and not live["errors"] and passed_checks(live, 38), "Live account checks incomplete")
require(recovery["status"] == "passed" and not recovery["errors"] and len(recovery["passed"]) == 9, "Cloud recovery incomplete")
require(runtime["sceneAttemptsPassed"] == runtime["variants"] == 72 and not runtime["errors"], "Scene verification failed")
require(regressions["passed"] and passed_checks(regressions, 22), "Player regressions failed")
require(xr["passed"] and passed_checks(xr, 74) and not xr["errors"] and not xr["runtimeQaBypass"], "Official simulator verification failed")
require(payload["passed"] and content["passed"] and content["checks"] == 1451 and backend["result"] == "pass" and backend["checks"] == 251, "Content/backend/payload verification failed")
require(release["passed"] and release["editorTransportHookAbsent"] and apk["failures"] == 0, "Release/package verification failed")
require(signing["releaseSigningRequired"] and signing["failures"] == 1 and [c["name"] for c in signing["checks"] if not c["passed"]] == ["Non-debug release signing certificate"], "Expected only the documented missing release-signing blocker")

core_text = (out / "core-tests.txt").read_text(encoding="utf-8-sig")
native_text = (out / "native-tests.txt").read_text(encoding="utf-8-sig")
require(re.search(r"^PASS 769 assertions:", core_text, re.M), "Full core curriculum run missing")
require("RESULT 81/81" in native_text, "Native regression result missing")

source_root = root / "unity/ThoraxLab/Assets"
review_root = root / "unity/ThoraxLabReview/Assets"
snapshot = []
for path in sorted(source_root.rglob("*")):
    if not path.is_file():
        continue
    relative = path.relative_to(source_root).as_posix()
    copy = review_root / relative
    require(copy.is_file(), "Review missing asset: " + relative)
    entry = {"path": relative, "sha256": sha(path), "reviewSha256": sha(copy)}
    entry["matchesReview"] = entry["sha256"] == entry["reviewSha256"]
    if not entry["matchesReview"]:
        require(relative == "ThoraxLab/Resources/Fonts/SourceSans3-SDF.asset", "Unexpected source difference: " + relative)
        pattern = r"(?ms)^  m_FontFeatureTable:\n.*?(?=^  \S|\Z)"
        main_text, copy_text = path.read_text(), copy.read_text()
        require(re.search(pattern, main_text) and re.search(pattern, copy_text) and re.sub(pattern, "", main_text) == re.sub(pattern, "", copy_text), "Font differs beyond generated feature cache")
        entry["allowedDifference"] = "Generated TMP font feature cache only; every other serialized field matches"
    snapshot.append(entry)
extras = [p.relative_to(review_root).as_posix() for p in review_root.rglob("*") if p.is_file() and not (source_root / p.relative_to(review_root)).is_file()]
require(not extras, "Unexpected review assets: " + str(extras))
(out / "source-assets-snapshot.json").write_text(json.dumps(snapshot, indent=2), encoding="utf-8")

for assembly, report in [(root / "builds/LearningWindows/ThoraxLab_Data/Managed/Assembly-CSharp.dll", release)]:
    require(sha(assembly) == report["assemblySha256"], "Release assembly changed after inspection")
qa_assembly = root / "builds/LearningVRTest/ThoraxLab_Data/Managed/Assembly-CSharp.dll"
for report in [runtime, regressions]:
    require(qa_assembly.stat().st_mtime <= datetime.fromisoformat(report["utc"].replace("Z", "+00:00")).timestamp(), "QA binary is newer than its report")
require(sha(root / "builds/LearningQuest/ThoraxLab.apk") == apk["artifactSha256"] == signing["artifactSha256"], "APK changed after inspection")
for artifact in delivery["artifacts"]:
    require(sha(Path(artifact["path"])) == artifact["sha256"], "Delivery artifact changed")
for build in ["StandaloneWindows64", "StandaloneWindows64-QA", "Android"]:
    text = (root / ("qa/learning-build-" + build + ".txt")).read_text()
    require(text.startswith("Succeeded\n0 errors\n0 warnings\n"), "Build not clean: " + build)

report_paths = [
    "qa/non-performance-verification/live-account-report.json",
    "qa/non-performance-verification/cloud-recovery-report.json",
    "qa/non-performance-verification/profile-payload-report.json",
    "qa/non-performance-verification/player/runtime-report.json",
    "qa/non-performance-verification/player/readiness-regressions.json",
    "qa/non-performance-verification/core-tests.txt",
    "qa/non-performance-verification/native-tests.txt",
    "qa/non-performance-verification/source-assets-snapshot.json",
    "qa/non-performance-verification/packaging/learning-apk-verification.json",
    "qa/vr-xr-simulator-report.json", "qa/vr-content-report.json",
    "qa/learning-backend-report.json", "qa/vr-release-gate-report.json",
    "qa/learning-apk-verification.json", "qa/vr-learning-delivery-manifest.json",
    "qa/learning-build-StandaloneWindows64.txt", "qa/learning-build-StandaloneWindows64-QA.txt",
    "qa/learning-build-Android.txt",
]
report = {
    "utc": datetime.now(timezone.utc).isoformat(), "project": "unity/ThoraxLab",
    "verificationProject": "unity/ThoraxLabReview", "unity": "6000.3.19f1",
    "status": "Software checks passed; release signing and remaining acceptance checks outstanding",
    "productionAccepted": False, "performance": "Excluded at user request",
    "tests": {"liveAccounts": 38, "liveCloudRecovery": 9, "sceneVariants": 72,
              "playerRegressions": 22, "officialXrSimulator": 74, "coreAssertions": 769,
              "contentAssertions": 1451, "postgresChecks": 251, "nativeChecks": 81,
              "sideloadApkChecks": 11, "releaseIsolationPassed": True,
              "strictReleaseSigningPassed": False},
    "accountCleanup": "Both approved temporary accounts reset and permanently deleted; normal account not used",
    "source": {"assetFiles": len(snapshot), "exactMatches": sum(e["matchesReview"] for e in snapshot),
               "allCodeAndAnatomyMatch": True, "allowedGeneratedCacheDifferences": [e for e in snapshot if not e["matchesReview"]],
               "unexpectedReviewAssets": extras, "qaAssemblySha256": sha(qa_assembly)},
    "artifacts": delivery["artifacts"], "reports": [identity(p) for p in report_paths],
    "fix": "Omit optional profile object on reads/deletes to prevent JsonUtility-created default profiles",
    "scope": {"physicalDeviceTested": False, "independentAuthenticatedSessionsOnOneWindowsComputer": True,
              "recipientInboxObserved": False, "codesObtainedFromSenderSentCopies": True,
              "timedUnredeemedCodeExpiryTested": False, "humanFirstTimeUsabilityAccepted": False},
    "remaining": delivery["limitations"],
}
(out / "verification-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({"status": report["status"], "tests": report["tests"], "assetFiles": len(snapshot)}, indent=2))
