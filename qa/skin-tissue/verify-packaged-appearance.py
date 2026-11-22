"""Compare skin and tissue assets inside two installed-format Unity APKs."""
import argparse
import hashlib
import json
import re
import zipfile
from pathlib import Path


def asset_bytes(archive, path):
    meta = path.with_name(path.name + ".meta").read_text(encoding="utf-8")
    guid = re.search(r"^guid: ([0-9a-f]{32})$", meta, re.M)[1]
    base = "assets/bin/Data/" + guid
    parts = sorted(n for n in archive.namelist() if n == base or n.startswith(base + "."))
    if base not in parts:
        raise ValueError(f"{path.name} ({guid}) missing in {archive.filename}")
    return b"".join(archive.read(part) for part in parts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("previous", type=Path)
    parser.add_argument("updated", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = Path(__file__).resolve().parents[2] / "unity/ThoraxLab/Assets/ThoraxLab"
    checks = []
    with zipfile.ZipFile(args.previous) as old, zipfile.ZipFile(args.updated) as new:
        def compare(relative, expected_change):
            previous = hashlib.sha256(asset_bytes(old, source / relative)).hexdigest()
            updated = hashlib.sha256(asset_bytes(new, source / relative)).hexdigest()
            checks.append({"asset": relative, "previous": previous, "updated": updated,
                           "expectedChange": expected_change, "passed": (previous != updated) == expected_change})

        for tissue in ("skin", "fat", "fascia", "muscle"):
            for face in ("Tissue", "Interior"):
                for suffix in (".mat", "_Albedo.png"):
                    compare(f"Resources/Learning/Specimen/{face}_{tissue}{suffix}", True)
        for side in ("Surface", "CutFace"):
            compare(f"Resources/Learning/CaseSkin/{side}_Albedo.png", True)
        compare("Resources/Learning/InstrumentFinishes/Brushed relief.png", False)
        compare("Museum/Honed terrazzo.png", False)
        version_asset = "Resources/Learning/Specimen/appearance-version.txt"
        current = (source / version_asset).read_text(encoding="utf-8").strip().encode()
        if current not in asset_bytes(new, source / version_asset) or current in asset_bytes(old, source / version_asset):
            raise ValueError("The installed and source appearance revision must match and advance.")
        for name in ("lib/arm64-v8a/libil2cpp.so", "assets/bin/Data/Managed/Metadata/global-metadata.dat"):
            previous = hashlib.sha256(old.read(name)).hexdigest()
            updated = hashlib.sha256(new.read(name)).hexdigest()
            checks.append({"asset": name, "previous": previous, "updated": updated, "expectedChange": True, "passed": previous != updated})
    passed = all(c["passed"] for c in checks)
    result = {"previous": args.previous.name, "updated": args.updated.name, "appearanceRevision": current.decode(),
              "passed": passed, "checks": checks}
    args.output.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"{sum(c['passed'] for c in checks)}/{len(checks)} package comparisons passed; appearance {current.decode()}")
    for check in checks:
        if not check["passed"]:
            print(check["asset"])
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
