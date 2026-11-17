"""Check publication guards using fixture bytes, without claiming an Android build."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SOURCE = Path(__file__).resolve().parents[2]
CERTIFICATE = "36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910"


class GithubApkPackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="anatome-publish-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "pipeline").mkdir()
        shutil.copyfile(SOURCE / "pipeline/package-github-apk.ps1", self.root / "pipeline/package-github-apk.ps1")
        self.apk = self.root / "builds/AnatomeSchool/Anatome-1.6.0-Production.apk"
        self.apk.parent.mkdir(parents=True)
        self.apk.write_bytes(b"publication guard fixture; this is not an APK")
        self.digest = hashlib.sha256(self.apk.read_bytes()).hexdigest()
        self.result = {
            "version": "1.6.0", "versionCode": 20, "apk": str(self.apk), "sha256": self.digest,
            "createdUtc": "2026-11-18T00:00:00Z", "simulatorChecksPassed": True,
            "releaseSigningVerified": True, "physicalHeadsetTested": False,
        }
        self.receipt = {
            "passed": True, "zipAlignment16Kb": True, "apkSha256": self.digest,
            "certificateSha256": CERTIFICATE,
        }
        self.write_json("qa/current-apk/build-result.json", self.result)
        self.write_json("qa/school-signed-release/signing-receipt.json", self.receipt)
        for check in ("identification", "inspection", "museum"):
            self.write_json(f"qa/current-apk/{check}.json", {"passed": True})
        self.git("init", "-q")
        self.git("add", "pipeline/package-github-apk.ps1")
        self.git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-qm", "Fixture")
        self.commit = self.git("rev-parse", "HEAD").strip()
        self.env = {**os.environ, "GITHUB_SHA": self.commit}
        self.output = self.root / "builds/github-apk"

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.root), *args], text=True)

    def write_json(self, relative, data):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding="utf-8")

    def run_package(self, succeeds=False):
        shell = os.environ.get("ANATOME_TEST_POWERSHELL") or shutil.which("pwsh") or shutil.which("powershell")
        self.assertTrue(shell, "Install PowerShell or set ANATOME_TEST_POWERSHELL")
        result = subprocess.run(
            [shell, "-NoProfile", "-NonInteractive", "-File", str(self.root / "pipeline/package-github-apk.ps1")],
            capture_output=True, text=True, env=self.env,
        )
        if succeeds:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, "An unverified package was accepted")
            self.assertFalse(self.output.exists(), "A rejected build created a download")

    def test_verified_package_contains_only_download_files(self):
        self.run_package(succeeds=True)
        self.assertEqual({p.name for p in self.output.iterdir()}, {self.apk.name, self.apk.name + ".sha256", "build-info.json"})
        self.assertEqual((self.output / self.apk.name).read_bytes(), self.apk.read_bytes())
        self.assertEqual((self.output / (self.apk.name + ".sha256")).read_bytes(), f"{self.digest}  {self.apk.name}\n".encode("ascii"))
        info = json.loads((self.output / "build-info.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(info["sourceCommit"], self.commit)
        self.assertEqual(info["sha256"], self.digest)
        self.assertFalse(info["physicalHeadsetTested"])
        self.assertNotIn(str(self.root), json.dumps(info))

    def test_changed_apk_is_rejected(self):
        self.apk.write_bytes(b"different payload after verification")
        self.run_package()

    def test_incomplete_build_is_rejected(self):
        self.result["releaseSigningVerified"] = False
        self.write_json("qa/current-apk/build-result.json", self.result)
        self.run_package()

    def test_failed_museum_check_is_rejected(self):
        self.write_json("qa/current-apk/museum.json", {"passed": False})
        self.run_package()

    def test_other_release_certificate_is_rejected(self):
        self.receipt["certificateSha256"] = "0" * 64
        self.write_json("qa/school-signed-release/signing-receipt.json", self.receipt)
        self.run_package()

    def test_receipt_for_previous_apk_is_rejected(self):
        self.receipt["apkSha256"] = "0" * 64
        self.write_json("qa/school-signed-release/signing-receipt.json", self.receipt)
        self.run_package()

    def test_different_checkout_is_rejected(self):
        self.env["GITHUB_SHA"] = "0" * 40
        self.run_package()

    def test_unexpected_apk_path_is_rejected(self):
        self.result["apk"] = str(self.root / "another.apk")
        self.write_json("qa/current-apk/build-result.json", self.result)
        self.run_package()

    def test_invalid_version_is_rejected(self):
        self.result["version"] = "../other"
        self.write_json("qa/current-apk/build-result.json", self.result)
        self.run_package()


if __name__ == "__main__":
    unittest.main()
