"""Apply reviewed native mesh fixes to fresh Blender exports, then refresh their QA contracts."""
import json
import shutil
import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace

import numpy as np

PIPELINE = Path(__file__).resolve().parent
sys.path.insert(0, str(PIPELINE))
import native_model_repair as repair
from fbx_arrays import child, geometry_by_model, read_tree


def apply(project):
    exports = project / "exports"
    reports = project / "qa/reports"
    guards = PIPELINE / "repair-guards.json"
    spec = json.loads(guards.read_text())["levels"][0]
    addon = repair.find_addon()
    args = SimpleNamespace(models=exports, guards=guards, source_match=None, fbx_addon=addon)
    contract = json.loads((reports / "export-contract.json").read_text(encoding="utf-8-sig"))
    lods = json.loads((reports / "lods.json").read_text(encoding="utf-8-sig"))
    rows = {row["object_name"]: row for row in contract["meshes"]}
    display = {row["level"]: row for row in lods}
    with tempfile.TemporaryDirectory(dir=exports) as temp:
        stage = Path(temp)
        repair.generate(args, stage)
        pending = {}
        for level, stem in enumerate(repair.STEMS):
            path = stage / (stem + ".fbx")
            meshes = geometry_by_model(read_tree(path, addon))
            counts = {}
            for name, mesh in meshes.items():
                raw = np.asarray(child(mesh, b"PolygonVertexIndex").props[0])
                if len(raw) % 3 or not (raw[2::3] < 0).all():
                    raise ValueError(f"{stem}/{name} contains an unexpected polygon")
                counts[name] = len(raw) // 3
            if set(counts) != set(rows):
                raise ValueError(f"{stem} and the catalog contain different models")
            if level:
                display[level].update(objects=len(counts), triangles=sum(counts.values()))
            else:
                for name in (repair.BRONCHUS, repair.PULMONARY):
                    local = np.asarray(child(meshes[name], b"Vertices").props[0]).reshape(-1, 3)
                    world = np.asarray(spec["world"][name])
                    coords = local @ world[:3, :3].T + world[:3, 3]
                    rows[name].update(triangles=counts[name], world_min_m=coords.min(0).tolist(),
                                      world_max_m=coords.max(0).tolist())
                if any(row["triangles"] != counts[name] for name, row in rows.items()):
                    raise ValueError("The anatomy contract disagrees with an untouched mesh")
                contract["triangles"] = sum(counts.values())
            if repair.digest(path) != repair.digest(exports / path.name):
                pending[exports / path.name] = path
        for name, document in (("export-contract.json", contract), ("lods.json", lods)):
            target = reports / name
            if document != json.loads(target.read_text(encoding="utf-8-sig")):
                staged = stage / name
                staged.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
                pending[target] = staged
        backups = {}
        for target in pending:
            backup = stage / (target.name + ".previous")
            shutil.copy2(target, backup)
            backups[target] = backup
        changed = []
        try:
            for target, staged in pending.items():
                staged.replace(target)
                changed.append(target)
        except OSError:
            for target in reversed(changed):
                backups[target].replace(target)
            raise
    print("REVIEWED_EXPORTS", len(rows), contract["triangles"], [d["triangles"] for d in lods],
          "files updated", len(changed), flush=True)


if __name__ == "__main__":
    apply(PIPELINE.parent)
