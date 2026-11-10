"""Verify that signing has not changed any application entry in an accepted APK."""
import argparse
import hashlib
import json
import re
import tempfile
import zipfile
from pathlib import Path


def payload(path):
    with zipfile.ZipFile(path) as archive:
        if archive.testzip() is not None:
            raise ValueError("Corrupt APK archive")
        names = [entry.filename for entry in archive.infolist() if not entry.is_dir()]
        if len(names) != len(set(names)):
            raise ValueError("Duplicate APK entries")
        return {name: hashlib.sha256(archive.read(name)).hexdigest() for name in names
                if not re.fullmatch(r"META-INF/(?:MANIFEST\.MF|[^/]+\.(?:SF|RSA|DSA|EC))", name, re.I)}


def verify(source, signed):
    original, release = payload(source), payload(signed)
    return {"passed": bool(original) and original == release, "entries": len(original),
            "changed": sorted(name for name in original.keys() | release.keys() if original.get(name) != release.get(name)),
            "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "signedSha256": hashlib.sha256(signed.read_bytes()).hexdigest(),
            "method": "SHA-256 comparison of every ZIP entry except JAR signing metadata; includes manifest, code, resources and native libraries"}


def self_test():
    with tempfile.TemporaryDirectory() as folder:
        first, second = Path(folder) / "first.apk", Path(folder) / "second.apk"
        def write(path, code, signature):
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("classes.dex", code)
                archive.writestr("META-INF/CERT.RSA", signature)
                archive.writestr("META-INF/service.properties", "keep this")
        write(first, "same code", "old signature")
        write(second, "same code", "new signature")
        assert verify(first, second)["passed"]
        write(second, "changed code", "new signature")
        assert not verify(first, second)["passed"]
        with zipfile.ZipFile(second, "w") as archive:
            archive.writestr("classes.dex", "same code")
        assert not verify(first, second)["passed"]
    print("PASS 3 APK payload equivalence checks")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--signed", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
    else:
        if not all((args.source, args.signed, args.output)):
            parser.error("--source, --signed and --output are required")
        report = verify(args.source, args.signed)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2))
        if not report["passed"]:
            raise SystemExit(1)
