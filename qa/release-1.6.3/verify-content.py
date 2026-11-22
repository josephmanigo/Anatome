"""Check the submitted Android player for this release's room, surfaces and interaction code."""
import argparse
import hashlib
import json
import re
import struct
import zipfile
from datetime import datetime, timezone
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    source = here.parents[1] / "unity/ThoraxLab/Assets/ThoraxLab"
    prefix = "assets/bin/Data/"
    groups = {}
    with zipfile.ZipFile(args.apk) as archive:
        names = {p[len(prefix):] for p in archive.namelist() if p.startswith(prefix)}
        serial = {p: archive.read(prefix + p) for p in names
                  if "/" not in p and not p.endswith((".resource", ".resS")) and ".resS.split" not in p}
        levels = {p: data for p, data in serial.items() if p.startswith("level0")}
        metadata = archive.read(prefix + "Managed/Metadata/global-metadata.dat")

        def checked(group, label, matches):
            groups.setdefault(group, {})[label] = sorted(matches)

        def serialized_name(group, label, containers):
            word = label.encode("utf-8")
            unity_string = struct.pack("<I", len(word)) + word
            checked(group, label, (p for p, data in containers.items() if unity_string in data))

        def asset(group, path):
            meta = path.with_name(path.name + ".meta").read_text(encoding="utf-8")
            guid = re.search(r"^guid: ([0-9a-f]{32})$", meta, re.M)[1]
            checked(group, path.relative_to(source).as_posix(),
                    (p for p in names if p == guid or p.startswith(guid + ".split")))

        for finish in ("Black", "Guard", "Stainless"):
            serialized_name("lab scene objects", "Lab_Chair_" + finish, levels)
        learn = source / "Resources/Learning"
        for directory in ("CaseSkin", "InstrumentFinishes"):
            for path in sorted((learn / directory).iterdir()):
                if path.suffix in (".mat", ".png"):
                    asset(directory, path)
        for tissue in ("lung", "cardiac", "valve", "artery", "vein", "airway", "esophagus", "membrane"):
            for side in ("Tissue", "Interior"):
                path = learn / "Specimen" / (side + "_" + tissue + ".mat")
                asset("specimen materials", path)
        for relative in ("AtlasLungDetail.fbx", "Museum/Gallery.prefab", "Museum/Plinth.prefab"):
            asset("museum", learn / relative)
        asset("museum", source / "Resources/Fonts/SourceSans3-SDF.asset")
        for name in ("ThoraxLab/Museum finish", "ThoraxLab/Organ Surface"):
            serialized_name("shaders", name, serial)
        for field in ("CaseIncisionSurface", "CaseIncisionStroke", "LabHandInteractor", "ProcedureTool", "CaseAnatomyWorkbench"):
            checked("compiled player metadata", field, ["Managed/Metadata/global-metadata.dat"] if field.encode() in metadata else [])
    missing = [f"{group}: {name}" for group, items in groups.items() for name, entries in items.items() if not entries]
    output = {"utc": datetime.now(timezone.utc).isoformat(), "apk": args.apk.name,
              "sha256": hashlib.file_digest(args.apk.open("rb"), "sha256").hexdigest(),
              "checks": sum(map(len, groups.values())), "missing": missing, "evidence": groups}
    destination = args.output or here / "packaged-content.json"
    destination.write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(str(output["checks"]) + " package content checks; missing " + str(len(missing)))
    for item in missing:
        print(item)
    return int(bool(missing))


if __name__ == "__main__":
    raise SystemExit(main())
