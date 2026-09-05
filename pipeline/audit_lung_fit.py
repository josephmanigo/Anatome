"""Read-only cross-specimen fit audit against a conservative thoracic cage envelope."""
import bpy,bmesh,json,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];records={r['id']:r for r in catalog}
objects={o['id']:o for o in bpy.data.collections['THORAX_PRODUCTION'].objects}
def verts(o):return np.array([(o.matrix_world@v.co)[:] for v in o.data.vertices])
lobes=[o for k,o in objects.items() if k.startswith('MAL_')];points=np.concatenate([verts(o) for o in lobes]);center=(points.min(0)+points.max(0))/2
bones=[o for k,o in objects.items() if records[k].get('required_group') in ('ribs','sternum','costal_cartilages')]
if not bones:bones=[o for k,o in objects.items() if 'rib' in records[k]['name'] and records[k]['tissue']=='bone']
cloud=np.concatenate([verts(o)[::5] for o in bones]);bm=bmesh.new()
for p in cloud:bm.verts.new(p)
bmesh.ops.convex_hull(bm,input=list(bm.verts));bm.normal_update();planes=[]
for face in bm.faces:
 n=np.array(face.normal);p=np.array(face.verts[0].co)
 # The lung apices normally project above the first rib; no superior closing plane.
 if n[1]<.65:planes.append([*n,float(n@p)])
bm.free();planes=np.array(planes);n=planes[:,:3];d=planes[:,3]
# Unique hull planes avoid repeated nearly coplanar source triangles.
planes=np.unique(np.round(planes,5),axis=0);n=planes[:,:3];d=planes[:,3]
sample=points[::4]
def audit(param,full=False):
 p=(points if full else sample)-center;p=p*param[0]+center+param[1:]
 distances=np.max(p@n.T-d,axis=1)+.003
 return distances
def objective(param):
 dist=np.maximum(audit(param),0)
 return np.mean(dist**2)*40+np.max(dist)**2+(1-param[0])**2*.00003+np.sum(param[1:]**2)*.002
best=np.array([1.,0.,0.,0.]);score=objective(best)
for steps in [np.array([.05,.01,.01,.01]),np.array([.01,.002,.002,.002]),np.array([.002,.0005,.0005,.0005])]:
 for iteration in range(60):
  changed=False
  for axis in range(4):
   for sign in [-1,1]:
    p=best.copy();p[axis]+=steps[axis]*sign
    if not .65<=p[0]<=1.01 or np.any(np.abs(p[1:])>.08):continue
    s=objective(p)
    if s<score:best=p;score=s;changed=True
  if not changed:break
before=audit(np.array([1.,0.,0.,0.]),True);after=audit(best,True)
report={'method':'Read-only conservative cage-envelope fit proposal, no geometry edited. Whole five-lobe assembly shares one uniform scale and translation. Three millimetre geometric clearance is a design assumption. Superior inlet plane excluded. This is not a validated anatomical registration.', 'bones_used':[o['id'] for o in bones],'center_world_m':center.tolist(),'proposed_uniform_scale':float(best[0]),'proposed_translation_world_m':best[1:].tolist(),'before':{'samples_outside':int(sum(before>0)),'max_outside_m':float(max(before))},'after':{'samples_outside':int(sum(after>0)),'max_outside_m':float(max(after))},'samples':len(points)}
(ROOT/'qa/reports/lung-fit-proposal.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
