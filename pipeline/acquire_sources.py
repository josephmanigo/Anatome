"""Acquire documented anatomy sources without modifying downloaded geometry.

Uses only the Python standard library. Run from any directory. Original bytes,
SHA256 checksums, URLs, and acquisition timestamps are retained under source/.
"""
from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import shutil
import ssl
import subprocess
import time
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "source"
BASE = "https://dbarchive.biosciencedbc.jp/data/bodyparts3d/LATEST/"
ATTRIBUTION = "BodyParts3D, © The Database Center for Life Science licensed under CC Attribution 4.0 International"
SOURCES = [
    ("bodyparts3d/archive/isa_BP3D_4.0_obj_99.zip", BASE + "isa_BP3D_4.0_obj_99.zip", "CC-BY-4.0", "mesh_archive"),
    *[("bodyparts3d/metadata/" + name, BASE + name, "CC-BY-4.0", "source_metadata") for name in (
        "isa_parts_list_e.txt", "isa_element_parts.txt", "isa_inclusion_relation_list.txt",
        "partof_parts_list_e.txt", "partof_element_parts.txt", "partof_inclusion_relation_list.txt",
        "README_e.html", "release_4.0_e.html", "coordinate_system.png")],
    ("bodyparts3d/metadata/license.html", "https://dbarchive.biosciencedbc.jp/en/bodyparts3d/lic.html", "CC-BY-4.0", "license_evidence"),
    ("references/gray/Gray806.png", "https://upload.wikimedia.org/wikipedia/commons/3/3c/Gray806.png", "public-domain", "historical_reference_image"),
    ("references/gray/Gray968.png", "https://upload.wikimedia.org/wikipedia/commons/0/01/Gray968.png", "public-domain", "historical_reference_image"),
    ("references/gray/Gray806-source.html", "https://commons.wikimedia.org/wiki/File:Gray806.png", "reference-page", "license_evidence"),
    ("references/gray/Gray968-source.html", "https://commons.wikimedia.org/wiki/File:Gray968.png", "reference-page", "license_evidence"),
    ("references/nlm/visible_human.html", "https://www.nlm.nih.gov/research/visible/visible_human.html", "reference-page", "dataset_documentation"),
    ("references/open-anatomy/mal-thoracic-atlas.html", "https://www.openanatomy.org/atlas-pages/atlas-mauritania-thorax.html", "reference-page", "dataset_documentation"),
    ("references/open-anatomy/slicer-license.html", "https://www.openanatomy.org/atlas-pages/slicer-license.html", "reference-page", "license_evidence"),
]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def acquire(item: tuple[str, str, str, str]) -> dict:
    relative, url, license_id, role = item
    path = SOURCE / relative
    record = {"path": "source/" + relative, "url": url, "license": license_id, "role": role}
    try:
        if not path.exists():
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary = path.with_suffix(path.suffix + ".partial")
            request = urllib.request.Request(url, headers={"User-Agent": "ANATOME-Source-Audit/1.0 (educational asset provenance)"})
            try:
                with urllib.request.urlopen(request, timeout=120) as response, temporary.open("wb") as target:
                    shutil.copyfileobj(response, target, length=1024 * 1024)
                    record["http_last_modified"] = response.headers.get("Last-Modified")
            except urllib.error.URLError as error:
                # Windows curl uses the OS trust store. Keep TLS verification on.
                if not isinstance(error.reason, ssl.SSLCertVerificationError):
                    raise
                subprocess.run(["curl.exe", "--fail", "--location", "--silent", "--show-error",
                                "--output", str(temporary), url], check=True)
                record["transport"] = "Windows curl with TLS verification"
            temporary.replace(path)
        record.update(status="acquired", bytes=path.stat().st_size, sha256=sha256(path),
                      verified_utc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
        if relative.startswith("bodyparts3d/"):
            record["attribution"] = ATTRIBUTION
        print(f"ACQUIRED {relative} {record['bytes']} bytes", flush=True)
    except Exception as error:
        record.update(status="failed", error=str(error))
        print(f"FAILED {relative}: {error}", flush=True)
    return record


def extract_archive() -> dict:
    archive = SOURCE / "bodyparts3d/archive/isa_BP3D_4.0_obj_99.zip"
    destination = SOURCE / "bodyparts3d/obj"
    if not archive.exists():
        return {"status": "unavailable"}
    destination.mkdir(parents=True, exist_ok=True)
    count = 0
    with zipfile.ZipFile(archive) as zipped:
        corrupt = zipped.testzip()
        if corrupt:
            raise ValueError(f"Archive CRC validation failed: {corrupt}")
        for member in zipped.infolist():
            if member.is_dir() or Path(member.filename).suffix.lower() != ".obj":
                continue
            filename = Path(member.filename.replace("\\", "/")).name
            target = destination / filename
            if not target.exists():
                with zipped.open(member) as incoming, target.open("wb") as outgoing:
                    shutil.copyfileobj(incoming, outgoing)
            count += 1
    print(f"EXTRACTED {count} OBJ entries into {destination}", flush=True)
    return {"status": "extracted", "obj_count": count, "path": "source/bodyparts3d/obj", "geometry_modified": False}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--metadata-only", action="store_true")
    args = parser.parse_args()
    SOURCE.mkdir(parents=True, exist_ok=True)
    items = [item for item in SOURCES if not args.metadata_only or item[3] != "mesh_archive"]
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        records = list(pool.map(acquire, items))
    manifest_path = SOURCE / "source-manifest.json"
    if args.metadata_only and manifest_path.exists():
        old = json.loads(manifest_path.read_text(encoding="utf-8"))
        current_paths = {record["path"] for record in records}
        records = [record for record in old.get("sources", []) if record["path"] not in current_paths] + records
    manifest = {
        "schema_version": 1,
        "acquired_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "policy": "Free redistributable sources; no share-alike source geometry. Original downloaded bytes retained.",
        "sources": records,
        "extraction": extract_archive() if not args.metadata_only else {"status": "not_requested"},
    }
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if any(record["status"] == "failed" for record in records):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
