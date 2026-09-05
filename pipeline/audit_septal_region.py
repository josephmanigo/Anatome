"""Place the interatrial region from explicitly transformed cavity source points."""
import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
objects={o['id']:o for o in bpy.data.collections['THORAX_PRODUCTION'].objects};catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records']
def geometry(o):
 o.data.calc_loop_triangles();return np.array([(o.matrix_basis@v.co)[:] for v in o.data.vertices]),np.array([t.vertices[:] for t in o.data.loop_triangles])
def winding(v,f,p):
 a,b,c=(v[f]-p).transpose(1,0,2);la=np.linalg.norm(a,axis=1);lb=np.linalg.norm(b,axis=1);lc=np.linalg.norm(c,axis=1);return float(np.arctan2(np.einsum('ij,ij->i',a,np.cross(b,c)),la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb).sum()/(2*np.pi))
left=geometry(bpy.data.objects['CAVITY_FMA9465']);right=geometry(bpy.data.objects['CAVITY_FMA11359']);lw=geometry(objects['FMA9531']);rw=geometry(objects['FMA9457']);lc=left[0].mean(0);rc=right[0].mean(0);samples=[];inside=[]
for t in np.linspace(0,1,121):
 p=lc*(1-t)+rc*t;walls=[winding(*lw,p),winding(*rw,p)];cavities=[winding(*left,p),winding(*right,p)];valid=max(abs(x) for x in walls)>.9 and max(abs(x) for x in cavities)<.1;samples.append({'point_world_m':p.tolist(),'walls':walls,'cavities':cavities,'separating_tissue':bool(valid)})
 if valid:inside.append(p)
center=min(inside,key=lambda p:np.linalg.norm(p-(lc+rc)*.5)) if inside else (lc+rc)*.5
r=next(r for r in catalog if r['id']=='REGION_InteratrialSeptum');o=objects[r['id']];o.location=Vector(center)
r['notes']=('Region anchor within the sampled source atrial wall between the source atrial cavities. ' if inside else 'Approximate region marker between source atrial cavities; the line test did not find separating tissue. ')+'Full interatrial septal extent is not independently segmented. This annotation is not tissue and cannot be cut; inspect the surrounding atrial wall. See qa/reports/interatrial-region.json.'
r['source_kind']='source_region_annotation' if inside else 'authored_region_annotation';o['provenance_notes']=r['notes'];o['source_kind']=r['source_kind']
report={'samples':samples,'source_tissue_line_verified':bool(inside),'verified_points':len(inside),'anchor_world_m':center.tolist(),'transform_method':'matrix_basis explicitly includes local source placement despite hidden dependency-graph collection','limitation':r['notes']}
(ROOT/'qa/reports/interatrial-region.json').write_text(json.dumps(report,indent=2));(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'));print('SEPTAL_REGION',len(inside),center,flush=True)
