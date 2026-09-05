"""Verify MAL mesh coordinates against the authoritative labeled NRRD."""
import gzip
import json
import re
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parent
atlas=json.loads((ROOT/'atlasStructure.json').read_text())
labels={int(x['sourceSelector'][0]['dataKey']):x['annotation']['name'] for x in atlas
        if x.get('@type')=='Structure' and 'lung' in x.get('annotation',{}).get('name','')}
manifest=json.loads((ROOT/'mal-mesh-inventory.json').read_text())
mesh_by_name={m['name']:m for m in manifest['meshes']}
with (ROOT/'Data/Segmentation-flattened.seg.nrrd').open('rb') as raw:
    header=[]
    while (line:=raw.readline()).strip():header.append(line.decode())
    text=''.join(header)
    fields=dict(line.strip().split(': ',1) for line in header if ': ' in line and ':=' not in line)
    nx,ny,nz=map(int,fields['sizes'].split());assert fields['type']=='double' and fields['encoding']=='gzip'
    origin=np.asarray([float(v) for v in re.findall(r'-?\d+(?:\.\d+)?',fields['space origin'])])
    directions=np.asarray([[float(v) for v in group.split(',')] for group in re.findall(r'\(([^)]+)\)',fields['space directions'])])
    assert fields['space']=='left-posterior-superior'
    bounds={key:{'min':np.asarray([nx,ny,nz]),'max':np.asarray([-1,-1,-1]),'voxels':0} for key in labels}
    stream=gzip.GzipFile(fileobj=raw)
    for z in range(nz):
        chunk=stream.read(nx*ny*8);assert len(chunk)==nx*ny*8
        arr=np.frombuffer(chunk,dtype='<f8').reshape(ny,nx)
        for key in labels:
            yy,xx=np.nonzero(arr==key)
            if len(xx):
                b=bounds[key];b['min']=np.minimum(b['min'],[xx.min(),yy.min(),z]);b['max']=np.maximum(b['max'],[xx.max(),yy.max(),z]);b['voxels']+=len(xx)
    assert stream.read(1)==b''
records=[]
for key,name in labels.items():
    b=bounds[key];lps_min=origin+b['min']@directions;lps_max=origin+b['max']@directions
    ras_min=lps_min.copy();ras_max=lps_max.copy();ras_min[:2]=-lps_max[:2];ras_max[:2]=-lps_min[:2]
    mesh=mesh_by_name[name]['bounds_source_units'];error=np.max(np.abs(np.asarray(mesh['min']+mesh['max'])-np.concatenate([ras_min,ras_max])))
    records.append({'name':name,'label':key,'voxel_count':b['voxels'],
      'voxel_center_bounds_lps_mm':{'min':lps_min.tolist(),'max':lps_max.tolist()},
      'same_bounds_converted_to_ras_mm':{'min':ras_min.tolist(),'max':ras_max.tolist()},
      'vtk_bounds_ras_mm':mesh,'largest_bbox_discrepancy_mm':float(error)})
report={'schema_version':1,'volume_declared_space':'left-posterior-superior',
 'voxel_spacing_mm':np.diag(directions).tolist(),'origin_lps_mm':origin.tolist(),
 'mesh_space_conclusion':'RAS inferred and numerically checked against the corresponding authoritative LPS-labeled voxels; apply x/y sign inversion to compare in LPS.',
 'meshes':records,'note':'This checks global coordinate basis and gross label correspondence, not anatomical accuracy or BP3D registration.'}
(ROOT/'coordinate-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
