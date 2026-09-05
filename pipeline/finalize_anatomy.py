"""Sync tested cutting flags, verify hollow points and record the exact FBX contract."""
import bpy,json,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
production=bpy.data.collections['THORAX_PRODUCTION'];catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];byid={r['id']:r for r in catalog};objects={o['id']:o for o in production.objects}
native=json.loads((ROOT/'qa/reports/anatomy-native-validation.json').read_text());accepted={x['id'] for x in native['checks'] if x['native_status']==2 and x['message']=='No material intersected.'}
for r in catalog:
 r['cutting_ready']=r['id'] in accepted and not r.get('reference_region');o=objects[r['id']];o['cutting_ready']=r['cutting_ready'];r['rest_transform_blender']={'position':list(o.location),'rotation_euler':list(o.rotation_euler),'scale':list(o.scale)};r['selection_anchor_local']=[0,0,0]
def winding(v,f,point):
 a,b,c=(v[f]-point).transpose(1,0,2);la=np.linalg.norm(a,axis=1);lb=np.linalg.norm(b,axis=1);lc=np.linalg.norm(c,axis=1)
 return float(np.arctan2(np.einsum('ij,ij->i',a,np.cross(b,c)),la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb).sum()/(2*np.pi))
points=[];cache={}
for mesh in json.loads((ROOT/'source/airway-lumen-audit.json').read_text())['meshes']:
 ident=mesh['selection_id'];o=objects[ident]
 if ident not in cache:
  o.data.calc_loop_triangles();v=np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices]);f=np.array([p.vertices[:] for p in o.data.loop_triangles]);cache[ident]=(v,f)
 v,f=cache[ident]
 for section in mesh['sections']:
  for sample in section['loops']:
   x,y,z=sample['sample_mm'];point=np.array([x*.001,(z-1212.5)*.001-1.35,-y*.001+.935]);w=winding(v,f,point);inside_region=np.all(point>=v.min(0)) and np.all(point<=v.max(0))
   points.append({'id':ident,'mesh':mesh['mesh_id'],'source_sample_mm':[x,y,z],'inside_region':bool(inside_region),'production_winding':w,'empty':abs(w)<.1})
tested=[s for s in points if s['inside_region']];lumen_report={'samples_in_region':len(tested),'empty_samples':sum(x['empty'] for x in tested),'samples':points,'limitation':'Point samples establish these lumen voids only. Offset thickness and endpoint closures are authored approximations.'}
(ROOT/'qa/reports/lumen-verification.json').write_text(json.dumps(lumen_report,indent=2))
for r in catalog:
 failed=[p for p in tested if p['id']==r['id'] and not p['empty']]
 if failed:r['cutting_ready']=False;objects[r['id']]['cutting_ready']=False;r['notes']+=' Lumen sample validation finding: cutting disabled until resolved.'
(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2))
contract=[]
for o in production.objects:
 o.data.calc_loop_triangles();v=np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices]);contract.append({'id':o['id'],'object_name':o.name,'triangles':len(o.data.loop_triangles),'world_min_m':v.min(0).tolist(),'world_max_m':v.max(0).tolist(),'materials':[m.name for m in o.data.materials]})
(ROOT/'qa/reports/export-contract.json').write_text(json.dumps({'meshes':contract,'triangles':sum(c['triangles'] for c in contract)},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in production.objects:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports/Thorax.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_custom_props=True)
print('LUMEN_SAMPLES',lumen_report['empty_samples'],lumen_report['samples_in_region'],'TRIANGLES',sum(c['triangles'] for c in contract),flush=True)
