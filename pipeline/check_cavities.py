import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
o=next(o for o in bpy.data.collections['THORAX_PRODUCTION'].objects if o['id']=='FMA13884');o.data.calc_loop_triangles()
vertices=np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices]);faces=np.array([t.vertices[:] for t in o.data.loop_triangles])
def winding(point):
 a,b,c=(vertices[faces]-point).transpose(1,0,2);la=np.linalg.norm(a,axis=1);lb=np.linalg.norm(b,axis=1);lc=np.linalg.norm(c,axis=1)
 return float(np.arctan2(np.einsum('ij,ij->i',a,np.cross(b,c)),la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb).sum()/(2*np.pi))
source=json.loads((ROOT/'source/heart-source-geometry-audit.json').read_text());out=[]
for cavity in source['cavity_tests']:
 for sample in cavity['samples']:
  x,y,z=sample['point_mm'];point=np.array([x*.001,(z-1212.5)*.001-1.35,-y*.001+.935]);w=winding(point)
  out.append({'cavity_mesh':cavity['cavity_mesh_id'],'point_mm':sample['point_mm'],'production_wall_winding':w,'outside_tissue':abs(w)<.1})
atrial=json.loads((ROOT/'source/chamber-wall-followup-audit.json').read_text())['atrial_cavity_tests']
for wall_mesh,ident in [('FJ2438','FMA9531'),('FJ2439','FMA9457')]:
 o=next(o for o in bpy.data.collections['THORAX_PRODUCTION'].objects if o['id']==ident);o.data.calc_loop_triangles()
 vertices=np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices]);faces=np.array([t.vertices[:] for t in o.data.loop_triangles])
 for sample in atrial[wall_mesh]['samples']:
  x,y,z=sample['point_mm'];point=np.array([x*.001,(z-1212.5)*.001-1.35,-y*.001+.935]);w=winding(point)
  out.append({'cavity_mesh':atrial[wall_mesh]['cavity_mesh'],'wall_id':ident,'point_mm':sample['point_mm'],'production_wall_winding':w,'outside_tissue':abs(w)<.1})
report={'tested':len(out),'passed':sum(x['outside_tissue'] for x in out),'samples':out,'limitation':'Confirms these source-derived cavity samples survived production repair; does not establish all thin trabeculae or chamber boundaries.'}
(ROOT/'qa/reports/heart-cavity-preservation.json').write_text(json.dumps(report,indent=2));print('CAVITY_CHECK',report['passed'],report['tested'])
if report['passed']!=report['tested']:raise RuntimeError('Source cavity samples were filled by production repair')
