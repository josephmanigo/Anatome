import bpy,json,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
contract=json.loads((ROOT/'qa/reports/export-contract.json').read_text());bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'exports/Thorax.fbx'),use_custom_props=True)
objects={o.name:o for o in bpy.data.objects if o.type=='MESH'};results=[]
for expected in contract['meshes']:
 o=objects.get(expected['object_name'])
 if not o:results.append({'id':expected['id'],'passed':False,'reason':'missing object'});continue
 o.data.calc_loop_triangles();v=np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices]);error=max(np.max(np.abs(v.min(0)-expected['world_min_m'])),np.max(np.abs(v.max(0)-expected['world_max_m'])));tris=len(o.data.loop_triangles)
 materials=[m.name for m in o.data.materials];uv_ok=len(o.data.uv_layers)>0 and all(np.isfinite(loop.uv[:]).all() for loop in o.data.uv_layers.active.data);normals_ok=all(np.isfinite(p.normal[:]).all() and p.normal.length>.99 for p in o.data.polygons)
 # Region wire annotations have no rendered texture and do not require UVs.
 region=expected['id'].startswith('REGION_') or bool(o.get('reference_region',False));uv_ok=uv_ok or region
 results.append({'id':expected['id'],'passed':error<1e-5 and tris==expected['triangles'] and o.get('id')==expected['id'] and uv_ok and normals_ok and materials==expected['materials'],'max_bound_error_m':float(error),'triangles':tris,'stable_id':o.get('id'),'uv_layers':len(o.data.uv_layers),'uv_valid':uv_ok,'normals_valid':normals_ok,'materials':materials})
unexpected=sorted(set(objects)-{m['object_name'] for m in contract['meshes']});report={'expected':len(contract['meshes']),'imported':len(objects),'passed':sum(r['passed'] for r in results),'unexpected':unexpected,'checks':results}
(ROOT/'qa/reports/fbx-roundtrip.json').write_text(json.dumps(report,indent=2));print('ROUNDTRIP',report['passed'],report['expected'],'unexpected',unexpected)
if report['passed']!=report['expected'] or unexpected:raise RuntimeError('FBX transport validation failed')
