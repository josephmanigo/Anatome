"""Record corresponding named surface-centroid estimates, not picked fiducials."""
import json
from pathlib import Path
import numpy as np
from vtk_reader import read_vtk_polydata

ROOT=Path(__file__).resolve().parent
BP3D=ROOT.parents[1]/'bodyparts3d/obj'
def obj_mesh(path):
    v=[];f=[]
    for line in path.read_text().splitlines():
        if line.startswith('v '):v.append([float(x) for x in line.split()[1:4]])
        if line.startswith('f '):
            p=[int(x.split('/')[0])-1 for x in line.split()[1:]]
            f.extend([[p[0],p[i],p[i+1]] for i in range(1,len(p)-1)])
    return np.asarray(v),np.asarray(f)
def centroid(v,f):
    t=v[f];weights=np.linalg.norm(np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]),axis=1)
    return np.average(t.mean(axis=1),axis=0,weights=weights)

entries=[]
for name,id in [('manubrium of sternum','FJ3290'),('body of sternum','FJ3178'),('xiphoid process','FJ3153')]:
    mv,mf=read_vtk_polydata(ROOT/'models'/(name+'.vtk'));bv,bf=obj_mesh(BP3D/(id+'.obj'))
    m=centroid(mv,mf);b=centroid(bv,bf)
    entries.append({'name':name,'mal_path':'models/'+name+'.vtk','bp3d_element':id,
      'kind':'area_weighted_centroid_of_corresponding_named_surface',
      'mal_ras_mm':m.tolist(),'mal_lps_mm':(m*np.asarray([-1,-1,1])).tolist(),
      'bp3d_mm':b.tolist(),
      'limitation':'Geometric centroid estimates from two different specimens, not manually identified anatomical point fiducials.'})
report={'schema_version':1,'coordinate_basis_change_vtk_to_bp3d':{'diagonal':[-1,-1,1],'unit':'millimetres'},
 'registration_performed':False,'proposed_correspondences':entries,
 'limitations':['These three sternum-region centers are nearly collinear and do not independently constrain all rotational degrees of freedom.',
 'Use anatomy axis basis as prior or acquire additional corresponding landmarks; do not silently call these verified fiducials.',
 'MAL trachea has separate carina model; BP3D trachea has no named carina model. A bounding-box end is not automatically a homologous carina landmark.',
 'No MAL diaphragm mesh was present in the live atlas structure.'],
 'recommended_record_for_registration':'Record one common transform, input landmarks and their estimation method, residuals in millimetres, and a source-specific approximation note for all MAL-derived objects.'}
(ROOT/'registration-landmarks.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
