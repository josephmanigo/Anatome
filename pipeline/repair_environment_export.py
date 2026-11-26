"""Remove only zero-area draw triangles, preserving the room's visible surfaces and UVs.

Run in Blender with --python repair_environment_export.py -- INPUT.fbx OUTPUT.fbx.
"""
from pathlib import Path
import sys
import subprocess
import tempfile

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
from fbx_arrays import child,geometry_by_model,read_tree,write_arrays


def mesh_edits(geo,mesh,changes):
    polygons=child(geo,b"PolygonVertexIndex")
    raw=np.asarray(polygons.props[0])
    ends=np.flatnonzero(raw<0)+1
    starts=np.r_[0,ends[:-1]]
    source_indices=np.where(raw<0,~raw,raw)
    corners=np.empty(len(mesh.loops),"i4")
    mesh.loops.foreach_get("vertex_index",corners)
    if not np.array_equal(source_indices,corners):
        raise ValueError(f"Polygon order mismatch in {mesh.name}")
    mesh.calc_loop_triangles()
    tri_count=len(mesh.loop_triangles)
    loops=np.empty(tri_count*3,"i4")
    face_ids=np.empty(tri_count,"i4")
    mesh.loop_triangles.foreach_get("loops",loops)
    mesh.loop_triangles.foreach_get("polygon_index",face_ids)
    loops=loops.reshape(-1,3)
    xyz=np.asarray(child(geo,b"Vertices").props[0]).reshape(-1,3)
    a,b,c=xyz[source_indices[loops]].transpose(1,0,2)
    bad=np.all(np.cross(b-a,c-a)==0,axis=1)
    if not np.any(bad):
        return 0
    affected=np.unique(face_ids[bad])
    replacement={}
    for face_id in affected:
        replacement[face_id]=list(loops[(face_ids==face_id)&(~bad)])
    surviving=[]
    for i,(first,last) in enumerate(zip(starts,ends)):
        if i in replacement:
            surviving.extend(replacement[i])
        else:
            surviving.append(np.arange(first,last,dtype='i4'))
    if not surviving:
        raise ValueError(f"Refusing to erase whole model {mesh.name}")
    keep=np.concatenate(surviving)
    new_end=np.cumsum([len(face) for face in surviving])-1
    indices=source_indices[keep].copy()
    raw_new=indices.copy()
    raw_new[new_end]=~raw_new[new_end]
    changes[id(polygons)]={0:raw_new}
    # FBX edge references are corner offsets. Preserve existing edge order when possible.
    next_corner=np.roll(indices,-1)
    next_corner[new_end]=indices[np.r_[0,new_end[:-1]+1]]
    represented={}
    for j,(a,b) in enumerate(zip(indices,next_corner)):
        key=(min(a,b),max(a,b))
        represented.setdefault(key,j)
    original_next=np.roll(source_indices,-1)
    original_next[ends-1]=source_indices[starts]
    edge=child(geo,b"Edges")
    selected=[]
    for j in edge.props[0]:
        a,b=source_indices[j],original_next[j]
        new_j=represented.pop((min(a,b),max(a,b)),None)
        if new_j is not None:
            selected.append(new_j)
    selected.extend(represented.values())
    changes[id(edge)]={0:np.asarray(selected,dtype='i4')}
    for layer in geo.elems:
        if not layer.id.startswith(b"LayerElement"):
            continue
        mapping=child(layer,b"MappingInformationType").props[0]
        if mapping in {b"AllSame",b"ByVertice",b"ByVertex"}:
            continue
        if mapping!=b"ByPolygonVertex" or child(layer,b"ReferenceInformationType").props[0]!=b"IndexToDirect":
            raise ValueError(f"Unexpected layer mapping in {mesh.name}: {layer.id} / {mapping}")
        arrays=[e for e in layer.elems if e.id.endswith(b"Index")]
        if len(arrays)!=1 or len(arrays[0].props[0])!=len(raw):
            raise ValueError(f"Unexpected corner mapping in {mesh.name}: {layer.id}")
        arr=arrays[0]
        changes[id(arr)]={0:np.asarray(arr.props[0])[keep]}
    return int(bad.sum())


def clean(source,target):
    from io_scene_fbx import parse_fbx
    tree=read_tree(source,Path(parse_fbx.__file__).parent)
    geos=geometry_by_model(tree)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source),use_image_search=False)
    changes={}
    removed={}
    for name,geo in geos.items():
        mesh=bpy.data.objects[name].data
        count=mesh_edits(geo,mesh,changes)
        if count:
            removed[name]=count
    write_arrays(source,target,tree,changes)
    return removed


def clean_generated(source):
    """Run on the exported FBX without disturbing the author's open Blender scene."""
    with tempfile.TemporaryDirectory(dir=source.parent) as staging:
        fixed=Path(staging)/source.name
        count=Path(staging)/"removed.txt"
        subprocess.run([bpy.app.binary_path,"--background","--factory-startup","--threads","2",
                        "--python-exit-code","2","--python",str(Path(__file__).resolve()),
                        "--",str(source),str(fixed),str(count)],check=True,
                       creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
        removed=int(count.read_text(encoding="utf-8"))
        fixed.replace(source)
        return removed


if __name__=="__main__":
    source,target,*counter=map(Path,sys.argv[sys.argv.index("--")+1:])
    removed=clean(source,target)
    if counter:
        counter[0].write_text(str(sum(removed.values())),encoding="utf-8")
    print("Removed zero-area triangles",sum(removed.values()),removed,flush=True)
