"""Read-only section-contour and winding audit of original airway/esophagus OBJs.

Meshes are never altered. Principal-axis planes sample source geometry; results
describe representation at those planes, not histological tissue thickness.
"""
import json
from pathlib import Path
from collections import defaultdict,Counter
import numpy as np

ROOT=Path(__file__).resolve().parent
def read_obj(id):
    v=[];f=[]
    for line in (ROOT/'bodyparts3d/obj'/(id+'.obj')).read_text().splitlines():
        if line.startswith('v '):v.append([float(x) for x in line.split()[1:4]])
        if line.startswith('f '):
            p=[int(x.split('/')[0])-1 for x in line.split()[1:]]
            f.extend([[p[0],p[i],p[i+1]] for i in range(1,len(p)-1)])
    return np.asarray(v),np.asarray(f)
def winding(tri,point):
    a,b,c=(tri-point).transpose(1,0,2)
    la,lb,lc=np.linalg.norm(a,axis=1),np.linalg.norm(b,axis=1),np.linalg.norm(c,axis=1)
    num=np.einsum('ij,ij->i',a,np.cross(b,c))
    den=la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb
    return float(np.arctan2(num,den).sum()/(2*np.pi))
def point_inside(pt,poly):
    inside=False;x,y=pt
    for a,b in zip(poly,np.roll(poly,-1,axis=0)):
        if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside
def section(v,f,axis,basis,distance):
    tri=v[f];s=tri@axis-distance;crossing=(s.min(axis=1)<0)&(s.max(axis=1)>0)
    graph=defaultdict(set);coordinates={}
    for t,sign in zip(tri[crossing],s[crossing]):
        points=[]
        for i,j in ((0,1),(1,2),(2,0)):
            if sign[i]*sign[j]<0:
                p=t[i]+(t[j]-t[i])*sign[i]/(sign[i]-sign[j]);points.append(p)
        if len(points)!=2:continue
        keys=[tuple(np.round(p,4)) for p in points]
        if keys[0]==keys[1]:continue
        a,b=keys;coordinates[a]=points[0];coordinates[b]=points[1];graph[a].add(b);graph[b].add(a)
    visited=set();loops=[];opencomponents=0
    for start in graph:
        if start in visited:continue
        component=set();stack=[start]
        while stack:
            n=stack.pop()
            if n in component:continue
            component.add(n);stack.extend(graph[n]-component)
        visited.update(component)
        if any(len(graph[n])!=2 for n in component):opencomponents+=1;continue
        order=[start];previous=None;current=start
        for _ in range(len(component)+1):
            choices=graph[current]-({previous} if previous is not None else set());nxt=next(iter(choices))
            if nxt==start:break
            order.append(nxt);previous,current=current,nxt
        xyz=np.asarray([coordinates[k] for k in order]);xy=xyz@basis
        cross=xy[:,0]*np.roll(xy[:,1],-1)-np.roll(xy[:,0],-1)*xy[:,1]
        signed_area=cross.sum()/2
        if abs(signed_area)<1e-7:continue
        center=((xy+np.roll(xy,-1,axis=0))*cross[:,None]).sum(axis=0)/(6*signed_area)
        point=axis*distance+basis@center
        loops.append({'poly':xy,'center':center,'sample_mm':point.tolist(),
                      'area_mm2':abs(float(signed_area)),'centroid_inside_own_contour':point_inside(center,xy),
                      'winding_at_centroid':winding(tri,point)})
    for loop in loops:
        loop['larger_contours_containing_centroid']=sum(other['area_mm2']>loop['area_mm2']*1.001 and point_inside(loop['center'],other['poly']) for other in loops)
    return {'plane_offset_mm':float(distance),'closed_contours':len(loops),'open_or_ambiguous_components':opencomponents,
       'loops':[{k:x for k,x in l.items() if k not in ('poly','center')} for l in loops]}

selection=json.loads((ROOT/'thorax-selection.json').read_text())
targets=[r for r in selection['records'] if r['required_group'] in ('trachea','main_bronchi','lobar_bronchial_trees','remaining_airway_branches','esophagus')]
reports=[]
for record in targets:
 for id in record['mesh_ids']:
    v,f=read_obj(id);_,_,axes=np.linalg.svd(v-v.mean(axis=0),full_matrices=False);axis=axes[0]
    if axis[np.argmax(np.abs(axis))]<0:axis=-axis
    basis=axes[1:].T;projection=v@axis;lo,hi=projection.min(),projection.max()
    sections=[section(v,f,axis,basis,lo+(hi-lo)*t) for t in (.23,.41,.59,.77)]
    loops=[l for s in sections for l in s['loops'] if l['centroid_inside_own_contour']]
    filled=sum(abs(l['winding_at_centroid'])>.9 for l in loops)
    empty=sum(abs(l['winding_at_centroid'])<.1 for l in loops)
    nestedempty=sum(l['larger_contours_containing_centroid']>0 and abs(l['winding_at_centroid'])<.1 for l in loops)
    classification='hollow_wall_evidence_at_sampled_sections' if nestedempty else 'solid_cast_at_sampled_sections' if filled and not empty else 'mixed_or_inconclusive'
    reports.append({'mesh_id':id,'selection_id':record['id'],'selection_name':record['name'],
      'triangles':len(f),'principal_axis':axis.tolist(),'sections':sections,
      'sampled_contour_centroids':len(loops),'filled_centroids':filled,'empty_centroids':empty,
      'nested_empty_centroids':nestedempty,'classification':classification})
    print(id,classification,'filled',filled,'empty',empty,'nested empty',nestedempty,flush=True)
output={'schema_version':1,'method':'Four principal-axis sections per original mesh. Closed intersection loops and signed winding at contour centroids. Rounding only matches diagnostic section endpoints at 0.0001 mm; originals unchanged.',
 'mesh_count':len(reports),'classification_counts':dict(Counter(r['classification'] for r in reports)),
 'meshes':reports,'limitation':'Finite sectional samples characterize representation; they cannot certify every lumen or exclude defects away from sampled planes.'}
(ROOT/'airway-lumen-audit.json').write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps(output['classification_counts']))
