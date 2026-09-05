"""Read the acquired legacy ASCII POLYDATA without changing coordinates."""
from pathlib import Path
import numpy as np

def read_vtk_polydata(path):
    text=Path(path).read_text(encoding='ascii')
    if text.splitlines()[2].strip()!='ASCII':
        raise ValueError('Only the observed legacy ASCII format is supported; binary files must not be guessed.')
    words=text.split();i=words.index('POINTS');count=int(words[i+1]);start=i+3
    vertices=np.asarray(words[start:start+3*count],dtype=np.float64).reshape(count,3)
    p=words.index('POLYGONS');polygons=int(words[p+1]);offset=p+3;faces=[]
    for _ in range(polygons):
        n=int(words[offset]);poly=[int(x) for x in words[offset+1:offset+1+n]];offset+=n+1
        faces.extend([[poly[0],poly[i],poly[i+1]] for i in range(1,n-1)])
    faces=np.asarray(faces,dtype=np.int64)
    if faces.min()<0 or faces.max()>=len(vertices):raise ValueError('Invalid mesh index')
    if not np.isfinite(vertices).all():raise ValueError('Non-finite coordinates')
    return vertices,faces
