"""Inspect this VR learning APK; does not claim a physical Quest run."""
import argparse, hashlib, json, re, struct, subprocess, zipfile
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--apk", type=Path, default=root / "builds/AnatomeQuest/Anatome.apk")
parser.add_argument("--expected-label", default="Anatome")
parser.add_argument("--expected-package", default="org.thoraxlab.learning")
parser.add_argument("--expected-certificate-sha256")
parser.add_argument("--require-school-policy", action="store_true")
parser.add_argument("--expected-version")
parser.add_argument("--expected-version-code", type=int)
parser.add_argument("--output-dir", type=Path, default=root / "qa")
parser.add_argument("--require-release-signing", action="store_true")
args = parser.parse_args()
if args.expected_certificate_sha256 and not re.fullmatch(r"[0-9a-fA-F]{64}", args.expected_certificate_sha256):
    parser.error("Expected certificate SHA-256 must contain 64 hexadecimal characters.")
if args.require_school_policy and (not args.require_release_signing or not args.expected_certificate_sha256):
    parser.error("School release verification requires release signing and an expected certificate SHA-256.")
apk = args.apk.resolve()
args.output_dir.mkdir(parents=True, exist_ok=True)
android = Path("C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data/PlaybackEngines/AndroidPlayer")
checks = []
def check(name, passed, detail=None):
    checks.append({"name": name, "passed": bool(passed), "detail": detail})

with zipfile.ZipFile(apk) as archive:
    check("Archive integrity", archive.testzip() is None)
    names = archive.namelist()
    libraries = [name for name in names if name.startswith("lib/") and name.endswith(".so")]
    check("ARM64 libraries only", libraries and all("/arm64-v8a/" in name for name in libraries), libraries)
    check("XR Hands ARM64 provider", "lib/arm64-v8a/libUnityOpenXRHands.so" in libraries)
    data = archive.read("lib/arm64-v8a/libthoraxcut.so")
    check("Native cutting AArch64", data[:4] == b"\x7fELF" and data[4] == 2 and struct.unpack_from("<H", data, 18)[0] == 183)
    check("Chunk extraction native entry point", b"tl_extract_chunk\0" in data)
    check("Current chunk extraction native version", b"ThoraxCut/1.1 chunk-extraction" in data, hashlib.sha256(data).hexdigest())
    phoff = struct.unpack_from("<Q", data, 32)[0]
    size, count = struct.unpack_from("<HH", data, 54)
    alignments = [struct.unpack_from("<Q", data, phoff + i * size + 48)[0] for i in range(count) if struct.unpack_from("<I", data, phoff + i * size)[0] == 1]
    check("16 KB native load alignment", alignments and all(a >= 16384 for a in alignments), alignments)
    for license_name in ("ATTRIBUTION.txt", "MAL-Atlas-License.txt", "Manifold-Apache-2.0.txt",
                         "Unity-XR-Hands-License.txt", "Unity-XR-Hands-Third-Party-Notices.txt"):
        check("Packaged " + license_name, any(name.endswith("/" + license_name) for name in names))

badging = subprocess.run([str(android / "SDK/build-tools/36.0.0/aapt.exe"), "dump", "badging", str(apk)], capture_output=True, text=True)
check("Learning package identity", badging.returncode == 0 and ("name='" + args.expected_package + "'") in badging.stdout)
check("Application display name", badging.returncode == 0 and ("application-label:'" + args.expected_label + "'") in badging.stdout.splitlines(), args.expected_label)
if args.expected_version:
    check("Application version", badging.returncode == 0 and ("versionName='" + args.expected_version + "'") in badging.stdout, args.expected_version)
if args.expected_version_code is not None:
    check("Application version code", badging.returncode == 0 and ("versionCode='" + str(args.expected_version_code) + "'") in badging.stdout, args.expected_version_code)
check("Hand tracking permission", "uses-permission: name='com.oculus.permission.HAND_TRACKING'" in badging.stdout)
check("Optional hand tracking preserves controller support", "uses-feature-not-required: name='oculus.software.handtracking'" in badging.stdout)
signer = subprocess.run([str(android / "OpenJDK/bin/java.exe"), "-jar", str(android / "SDK/build-tools/36.0.0/lib/apksigner.jar"), "verify", "--verbose", "--print-certs", str(apk)], capture_output=True, text=True)
check("APK signature verifies", signer.returncode == 0)
debug_certificate = "CN=Android Debug" in signer.stdout
non_debug_signer = signer.returncode == 0 and "certificate DN:" in signer.stdout and not debug_certificate
if args.require_release_signing:
    check("Non-debug release signing certificate", non_debug_signer)
fingerprint = re.search(r"Signer #1 certificate SHA-256 digest: ([0-9a-fA-F]{64})", signer.stdout)
certificate = fingerprint.group(1).lower() if fingerprint else None
if args.expected_certificate_sha256:
    check("Expected publisher certificate", signer.returncode == 0 and certificate == args.expected_certificate_sha256.lower(), certificate)
if args.require_school_policy:
    manifest = subprocess.run([str(android / "SDK/build-tools/36.0.0/aapt.exe"), "dump", "xmltree", str(apk), "AndroidManifest.xml"], capture_output=True, text=True)
    check("Android manifest readable", manifest.returncode == 0)
    for attribute in ("allowBackup", "fullBackupContent", "usesCleartextTraffic"):
        check("Android " + attribute + " disabled", bool(re.search(r"A: android:" + attribute + r"\([^\n]*=\(type 0x12\)0x0\b", manifest.stdout)))
    check("Android debugging disabled", not bool(re.search(r"A: android:debuggable\([^\n]*=\(type 0x12\)0xffffffff", manifest.stdout)))
    check("Explicit Quest 2 and Quest 3 support", '"quest2|questpro|quest3|quest3s"' in manifest.stdout)
    # Release resource optimization shortens XML paths; follow the compiled resource table.
    resources = subprocess.run([str(android / "SDK/build-tools/36.0.0/aapt.exe"), "dump", "--values", "resources", str(apk)], capture_output=True, encoding="utf-8")
    resource = re.search(r'resource (0x[0-9a-f]+) [^\n]*:xml/anatome_data_extraction_rules:[^\n]*\n\s*\(string8?\) "([^"]+)"', resources.stdout)
    reference = re.search(r'android:dataExtractionRules\([^)]*\)=@(0x[0-9a-f]+)', manifest.stdout)
    linked = resources.returncode == 0 and resource and reference and resource.group(1) == reference.group(1) and resource.group(2) in names
    check("Android extraction rules linked", linked, resource.group(2) if resource else None)
    rules = subprocess.run([str(android / "SDK/build-tools/36.0.0/aapt.exe"), "dump", "xmltree", str(apk), resource.group(2) if resource else "res/xml/anatome_data_extraction_rules.xml"], capture_output=True, text=True)
    domains = ("root", "file", "database", "sharedpref", "external", "device_root", "device_file", "device_database", "device_sharedpref")
    sections = re.split(r"E: (cloud-backup|device-transfer)\b[^\n]*", rules.stdout)
    for mode in ("cloud-backup", "device-transfer"):
        block = sections[sections.index(mode) + 1] if mode in sections else ""
        excluded = re.findall(r'E: exclude[^\n]*\n\s*A: domain="([^"]+)"[^\n]*\n\s*A: path="\."', block)
        check(mode + " excludes device credentials and saves", rules.returncode == 0 and set(excluded) == set(domains))
report = {"verifiedUtc": datetime.now(timezone.utc).isoformat(), "apk": str(apk), "artifactSha256": hashlib.file_digest(apk.open("rb"), "sha256").hexdigest(), "bytes": apk.stat().st_size, "package": args.expected_package, "checks": checks, "failures": sum(not row["passed"] for row in checks), "physicalDeviceTested": False, "releaseSigningRequired": args.require_release_signing, "schoolPolicyRequired": args.require_school_policy, "certificateSha256": certificate, "nonDebugSigner": non_debug_signer, "signing": "Android Debug certificate; sideload review, not a store release" if debug_certificate else "Non-debug certificate; compare the recorded fingerprint with the school's signing record"}
(args.output_dir / "learning-apk-signature.txt").write_text(signer.stdout + signer.stderr)
(args.output_dir / "learning-apk-verification.json").write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
assert report["failures"] == 0
