import bpy,bmesh,json,numpy as np,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
production=bpy.data.collections['THORAX_PRODUCTION'];objects={o['id']:o for o in production.objects};catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];byid={r['id']:r for r in catalog}
# Author-selected clearance for the two reconstructed tissue layers, avoiding
# interpenetration due to their independently simplified regional surfaces.
for ident,ratio in [('REC_fat',.90/.97),('REC_fascia',.84/.945)]:
 o=objects[ident]
 if o.get('clearance_refined'):continue
 inverse=o.matrix_world.inverted();origin=Vector((0,-1.35,.935))
 for v in o.data.vertices:
  p=o.matrix_world@v.co-origin;p.x*=ratio;p.z*=ratio;v.co=inverse@(p+origin)
 o['clearance_refined']=True;byid[ident]['notes']+=' Additional author-selected layer clearance reduces overlap from independent surface simplification; spacing is a teaching adjustment.'
def geometry(o):
 o.data.calc_loop_triangles();return np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices]),np.array([t.vertices[:] for t in o.data.loop_triangles])
def winding(v,f,point):
 a,b,c=(v[f]-point).transpose(1,0,2);la=np.linalg.norm(a,axis=1);lb=np.linalg.norm(b,axis=1);lc=np.linalg.norm(c,axis=1);return float(np.arctan2(np.einsum('ij,ij->i',a,np.cross(b,c)),la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb).sum()/(2*np.pi))
ident='REGION_InteratrialSeptum'
if ident not in objects:
 left=geometry(bpy.data.objects['CAVITY_FMA9465']);right=geometry(bpy.data.objects['CAVITY_FMA11359']);lw=geometry(objects['FMA9531']);rw=geometry(objects['FMA9457']);lc=left[0].mean(0);rc=right[0].mean(0);samples=[];inside=[]
 for t in np.linspace(0,1,81):
  p=lc*(1-t)+rc*t;walls=[winding(*lw,p),winding(*rw,p)];cavities=[winding(*left,p),winding(*right,p)];valid=max(abs(x) for x in walls)>.9 and max(abs(x) for x in cavities)<.1;samples.append({'point_world_m':p.tolist(),'walls':walls,'cavities':cavities,'separating_tissue':bool(valid)})
  if valid:inside.append(p)
 # The atlas does not supply an independently segmented interatrial septum.
 # Keep this as an explicit approximate region annotation, never a filled slab.
 verified_line=bool(inside)
 if not inside:inside=[(lc+rc)*.5]
 center=Vector(np.mean(inside,axis=0));cu=bpy.data.curves.new(ident,'CURVE');cu.dimensions='3D';cu.bevel_depth=.0004;cu.bevel_resolution=1;cu.use_fill_caps=True
 for direction in [Vector((.004,0,0)),Vector((0,.004,0))]:
  sp=cu.splines.new('POLY');sp.points.add(1);sp.points[0].co=(*(center-direction),1);sp.points[1].co=(*(center+direction),1)
 o=bpy.data.objects.new('A_'+ident,cu);production.objects.link(o);cu.materials.append(bpy.data.materials['Tissue_cardiac']);bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 for v in o.data.vertices:v.co-=center
 o.location=center;o.hide_render=True
 r={'id':ident,'name':'Interatrial septal region','object_name':o.name,'system':'Cardiovascular','tissue':'cardiac','layer':6,'source_kind':'authored_region_annotation','mesh_ids':['FJ2438','FJ2439'],'notes':'Approximate region marker between the source atrial cavities. The center-line sample test did not identify a separate interatrial wall here, and full septal extent is not segmented. This marker is not tissue and cannot be cut or removed.','cutting_ready':False,'reference_region':True,'required_group':'cardiac_septa','source_record':'qa/reports/interatrial-region.json'}
 for key in ['id','name','system','tissue','layer','source_kind','cutting_ready']:o[key]=r[key]
 o['reference_region']=True;catalog.append(r)
 (ROOT/'qa/reports/interatrial-region.json').write_text(json.dumps({'samples':samples,'source_tissue_line_verified':verified_line,'anchor_world_m':list(center),'limitation':r['notes']},indent=2))
(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'));print('REGION_REFINEMENT_COMPLETE',len(catalog))
