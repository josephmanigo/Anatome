"""Read-only topology and signed-winding checks of source ventricular tissue."""
import json
from collections import Counter, defaultdict
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parent
def read_obj(id):
    vertices,faces=[],[]
    for line in (ROOT/'bodyparts3d/obj'/(id+'.obj')).read_text().splitlines():
        if line.startswith('v '):vertices.append([float(x) for x in line.split()[1:4]])
        if line.startswith('f '):
            poly=[int(x.split('/')[0])-1 for x in line.split()[1:]]
            faces.extend([[poly[0],poly[i],poly[i+1]] for i in range(1,len(poly)-1)])
    return np.asarray(vertices),np.asarray(faces,dtype=np.int64)

def winding(vertices,faces,point):
    tri=vertices[faces]-point
    a,b,c=tri[:,0],tri[:,1],tri[:,2]
    la,lb,lc=np.linalg.norm(a,axis=1),np.linalg.norm(b,axis=1),np.linalg.norm(c,axis=1)
    num=np.einsum('ij,ij->i',a,np.cross(b,c))
    den=la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb
    return float(np.arctan2(num,den).sum()/(2*np.pi))

v,f=read_obj('FJ2428')
edgecounts=Counter(tuple(sorted((int(a),int(b)))) for tri in f for a,b in zip(tri,np.roll(tri,-1)))
unique, inverse = np.unique(v,axis=0,return_inverse=True)
welded_faces=inverse[f]
welded_edges=Counter(tuple(sorted((int(a),int(b)))) for tri in welded_faces for a,b in zip(tri,np.roll(tri,-1)))
tri=v[f]
volume=float(np.einsum('ij,ij->i',tri[:,0],np.cross(tri[:,1],tri[:,2])).sum()/6/1000)
report={'mesh_id':'FJ2428','source_identity':'FMA13884 wall of ventricle',
 'vertices':len(v),'triangles':len(f),'boundary_edges':sum(c==1 for c in edgecounts.values()),
 'nonmanifold_edges':sum(c>2 for c in edgecounts.values()),'signed_volume_cm3':volume,
 'exact_position_weld_diagnostic':{'unique_vertices':len(unique),
   'boundary_edges':sum(c==1 for c in welded_edges.values()),
   'nonmanifold_edges':sum(c>2 for c in welded_edges.values())},
 'bounds_mm':{'min':v.min(axis=0).tolist(),'max':v.max(axis=0).tolist()},'cavity_tests':[]}
for id in ('FJ2422','FJ2423'):
    cv,cf=read_obj(id);center=cv.mean(axis=0)
    sample_indices=np.linspace(0,len(cv)-1,24,dtype=int)
    points=np.vstack([center,center+(cv[sample_indices]-center)*.35])
    samples=[]
    for point in points:
        own=winding(cv,cf,point);wall=winding(v,f,point)
        samples.append({'point_mm':point.tolist(),'cavity_winding':own,'wall_winding':wall})
    valid=[s for s in samples if abs(s['cavity_winding'])>.9]
    report['cavity_tests'].append({'cavity_mesh_id':id,'samples':samples,
      'samples_confirmed_inside_cavity':len(valid),
      'confirmed_cavity_samples_outside_wall':sum(abs(s['wall_winding'])<.1 for s in valid)})
report['interpretation']='Sample winding checks are geometric evidence only; they do not certify all chamber boundaries or valve continuity.'
(ROOT/'heart-source-geometry-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({**report,'cavity_tests':[{k:v for k,v in t.items() if k!='samples'} for t in report['cavity_tests']]},indent=2))
