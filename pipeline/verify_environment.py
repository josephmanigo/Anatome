import bpy,json,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(ROOT/'exports/Laboratory.fbx'))
objects=[o for o in bpy.data.objects if o.type=='MESH'];triangles=0;all_vertices=[]
for o in objects:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles);all_vertices.extend([(o.matrix_world@p.co)[:] for p in o.data.vertices])
points=np.asarray(all_vertices);size=points.max(0)-points.min(0);expected=json.loads((ROOT/'qa/reports/environment-build.json').read_text())
checks={'batch_count':len(objects)==expected['renderers'],'triangle_count':triangles==expected['triangles'],'finite_geometry':bool(np.isfinite(points).all()),'metre_scale_room':bool(11.9<size[0]<12.3 and 8.9<size[1]<9.3 and 2.95<size[2]<3.35),'adjustable_table_batches':any(o.name.startswith('Lab_MainTable_') for o in objects),'material_assignments':all(len(o.data.materials)>0 for o in objects)}
report={'checks':checks,'passed':all(checks.values()),'objects':len(objects),'triangles':triangles,'bounds_size_m':size.tolist(),'names':[o.name for o in objects]};(ROOT/'qa/reports/environment-roundtrip.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2));assert report['passed']
