"""Export the five original MAL lobe surfaces in the existing teaching specimen's frame."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parents[1]
ATLAS = ROOT / "source/references/open-anatomy"
sys.path.insert(0, str(ATLAS))
from vtk_reader import read_vtk_polydata


def nearest_mm(points, vertices, triangles):
    tree = BVHTree.FromPolygons(vertices.tolist(), triangles.tolist(), all_triangles=True)
    distances = [tree.find_nearest(Vector(point))[3] * 1000 for point in points]
    return {"median": float(np.median(distances)), "p95": float(np.percentile(distances, 95)),
            "max": float(np.max(distances))}


def signed_volume(points, faces):
    a, b, c = (points[faces] - points.mean(axis=0)).transpose(1, 0, 2)
    return float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum() / 6)


def registered(vertices, registration, fit):
    bp = (registration["scale"] * (vertices * registration["ras_to_bp_basis"])
          @ np.array(registration["row_vector_rotation"]) + registration["translation_mm"])
    # Same supine table frame as build_anatomy.py and finalize_anatomy.py.
    world = np.column_stack((bp[:, 0], bp[:, 2] - 1212.5, -bp[:, 1])) * .001
    world += [0, -1.35, .935]
    center = np.array(fit["center_world_m"])
    return (world - center) * fit["proposed_uniform_scale"] + center + fit["proposed_translation_world_m"]


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "blender/Thorax_MASTER.blend"))
    production = {o["id"]: o for o in bpy.data.collections["THORAX_PRODUCTION"].objects}
    records = json.loads((ROOT / "exports/anatomy-catalog.json").read_text())["records"]
    registration = json.loads((ROOT / "docs/lung-registration.json").read_text())
    fit = json.loads((ROOT / "docs/lung-production-adaptation.json").read_text())
    output = ROOT / "unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/AtlasLungDetail.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    detail = []
    report = []
    for record in records:
        if record["source_kind"] != "registered_reference_mesh" or record["tissue"] != "lung":
            continue
        ident = record["id"]
        path = ATLAS / "models" / record["mesh_ids"][0]
        vertices, faces = read_vtk_polydata(path)
        world = registered(vertices, registration, fit)
        original = production[ident]
        original.data.calc_loop_triangles()
        low = np.array([(original.matrix_world @ v.co)[:] for v in original.data.vertices])
        low_faces = np.array([t.vertices[:] for t in original.data.loop_triangles])
        source_to_working = nearest_mm(world, low, low_faces)
        working_to_source = nearest_mm(low, world, faces)
        # A flipped axis, scale or pivot selects a different surface and must fail the export.
        if max(source_to_working["p95"], working_to_source["p95"]) > 2:
            raise ValueError(f"{ident} differs from the working lobe: {source_to_working}, {working_to_source}")
        if signed_volume(world, faces) * signed_volume(low, low_faces) < 0:
            faces = faces[:, ::-1]
        inverse = original.matrix_world.inverted()
        mesh = bpy.data.meshes.new(ident)
        mesh.from_pydata([inverse @ Vector(p) for p in world], [], faces.tolist())
        mesh.polygons.foreach_set("use_smooth", np.ones(len(faces), dtype=bool))
        mesh.update()
        model = bpy.data.objects.new(ident, mesh)
        bpy.context.scene.collection.objects.link(model)
        model.matrix_world = original.matrix_world.copy()
        detail.append(model)
        report.append({"id": ident, "source": str(path.relative_to(ROOT).as_posix()),
                       "sourceSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                       "sourceTriangles": len(faces), "workingTriangles": len(low_faces),
                       "sourceVerticesToWorkingSurfaceMm": source_to_working,
                       "workingVerticesToSourceSurfaceMm": working_to_source})
    if len(detail) != 5:
        raise ValueError(f"Expected five atlas lobes, found {len(detail)}")
    for obj in detail:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", bake_anim=False, use_custom_props=False)
    target = ROOT / "qa/organ-fidelity/lung-atlas-geometry.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps({"source": "Mauritanian Anatomy Laboratory thoracic atlas, January 2021",
        "method": "Full original segmentation triangles with smooth vertex normals, using the existing two shared transforms. Distances compare atlas vertices and working triangles in both directions within each lobe. These compare representations of the same segmentation, not anatomical accuracy across donors.",
        "lobes": report}, indent=2) + "\n", encoding="utf-8")
    print("Lung atlas triangles:", sum(row["sourceTriangles"] for row in report), flush=True)


if __name__ == "__main__":
    main()
