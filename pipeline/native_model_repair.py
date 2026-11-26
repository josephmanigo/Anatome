"""Repair verified derived game meshes through guarded edits to original native FBX arrays."""
import argparse
import array
import hashlib
import json
import os
import shutil
import struct
import sys
import tempfile
from pathlib import Path

import numpy as np

from fbx_arrays import child, geometry_by_model, read_tree, write_arrays


STEMS = ('Thorax', 'Thorax_LOD1', 'Thorax_LOD2')
EXPECTED = (
    'ceff740bd9109f52f622968ab000a12aa60c876fce7eadf31a732de45a9ee74a',
    '73268eb703ed66f810818918be8df6646584493705dfed68c82876720a72bbf1',
    '14cb3c5126a5b78264c88acf3bdac0b66ab160af889bdc3130cb0108b27da257',
)
PULMONARY = 'A_FMA50872'
BRONCHUS = 'A_FMA7396'
INTERNAL = 'A_FMA9757'
COLLAPSED = (
    {},
    {'A_FMA7240':[(1786,1787)],'A_FMA9756':[(769,770)],INTERNAL:[(1038,1039)]},
    {PULMONARY:[(108,109),(113,136),(244,245),(994,996)],'A_FMA7240':[(816,817)],
     'A_FMA7265':[(96,120)],'A_FMA9756':[(383,384)],INTERNAL:[(488,489)]},
)
NAMES = sorted({BRONCHUS,PULMONARY,*set().union(*(set(x) for x in COLLAPSED))})


def find_addon(override=None):
    if override:
        candidates=[Path(override)]
    else:
        candidates=[]
        if os.environ.get('BLENDER_FBX_ADDON'):candidates.append(Path(os.environ['BLENDER_FBX_ADDON']))
        try:
            import bpy
            candidates.extend(Path(p)/'io_scene_fbx' for d in ('addons_core','addons') for p in bpy.utils.script_paths(subdir=d))
        except ImportError:pass
        ex=shutil.which('blender')
        roots=[Path(ex).resolve().parent] if ex else []
        if os.environ.get('ProgramFiles'):
            install=Path(os.environ['ProgramFiles'])/'Blender Foundation'
            roots.extend(sorted(install.glob('Blender *'),reverse=True))
        roots+=list(map(Path,('/usr/share/blender','/usr/local/share/blender','/Applications/Blender.app/Contents/Resources')))
        for root in roots:
            for kind in ('addons_core','addons'):
                candidates.extend(root.glob('*/scripts/'+kind+'/io_scene_fbx'))
    for path in candidates:
        if (path/'parse_fbx.py').is_file():return path
    raise ValueError('Locate Blender scripts/addons_core/io_scene_fbx with --fbx-addon or BLENDER_FBX_ADDON.')


def payload(data):
    if isinstance(data,array.array):return data.typecode.encode()+data.tobytes()
    if isinstance(data,(bytes,bytearray)):return b'B'+data
    if isinstance(data,(bool,int)):return b'I'+str(data).encode()
    if isinstance(data,float):return b'F'+struct.pack('<d',data)
    raise TypeError(type(data))


def node_hash(elements):
    result=hashlib.sha256()
    def add(piece):
        result.update(struct.pack('<Q',len(piece)));result.update(piece)
    def visit(el):
        add(el.id);add(bytes(el.props_type));add(struct.pack('<I',len(el.elems)))
        for prop in el.props:add(payload(prop))
        for sub in el.elems:visit(sub)
    for el in elements:visit(el)
    return result.hexdigest()


def geometry_fingerprints(tree):
    geometries=geometry_by_model(tree)
    assert len(geometries)==249
    models={x.props[1].split(b'\0\1')[0].decode():x for x in child(tree,b'Objects').elems if x.id==b'Model'}
    mesh={};frames={}
    global_frame=child(child(tree,b'GlobalSettings'),b'Properties70')
    frames['axes_and_units']=node_hash([p for p in global_frame.elems if b'Axis' in p.props[0] or b'UnitScale' in p.props[0]])
    links=[c.props[1:3] for c in child(tree,b'Connections').elems if c.props[0]==b'OO']
    for name in NAMES:
        # Omit the dynamic geometry ID and its metadata. Hash all data consumed or modified.
        g=geometries[name]
        mesh[name]=node_hash([child(g,z) for z in (b'Vertices',b'PolygonVertexIndex',b'Edges',b'LayerElementNormal',
                                                   b'LayerElementUV',b'LayerElementMaterial')])
        transform=[p for p in child(models[name],b'Properties70').elems if p.props[0].startswith((b'Lcl ',b'Geometric',b'PreRotation',b'PostRotation',b'RotationOffset',b'RotationPivot',b'ScalingOffset',b'ScalingPivot'))]
        frames[name]=node_hash(sorted(transform,key=lambda p:p.props[0]))
        if [target for source,target in links if source==models[name].props[0]]!=[0]:
            raise ValueError(f'{name} must be an unparented model in the verified coordinate frame.')
    return mesh,frames


def group_vertices(v_count,faces):
    adjacent=[set() for _ in range(v_count)]
    for a,b,c in faces.tolist():
        adjacent[a].update((b,c));adjacent[b].update((a,c));adjacent[c].update((a,b))
    labels=np.full(v_count,-1,np.int32);count=0
    for index in range(v_count):
        if labels[index]>=0:continue
        labels[index]=count;pending=[index]
        while pending:
            at=pending.pop()
            for neighbor in adjacent[at]:
                if labels[neighbor]<0:labels[neighbor]=count;pending.append(neighbor)
        count+=1
    return count,labels


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def get(node, name):
    return np.asarray(child(node, name).props[0])


class Mesh:
    def __init__(self, geometry):
        self.geometry=geometry
        self.v=get(geometry,b'Vertices').reshape(-1,3).copy()
        self.f=get(geometry,b'PolygonVertexIndex').reshape(-1,3).copy()
        assert (self.f[:,:2]>=0).all() and (self.f[:,2]<0).all()
        self.f[:,2]=-self.f[:,2]-1
        self.normals=child(geometry,b'LayerElementNormal')
        self.uvs=child(geometry,b'LayerElementUV')
        assert child(self.normals,b'MappingInformationType').props[0] in (b'ByVertice',b'ByVertex')
        assert child(self.uvs,b'MappingInformationType').props[0]==b'ByPolygonVertex'
        assert child(self.normals,b'ReferenceInformationType').props[0]==child(self.uvs,b'ReferenceInformationType').props[0]==b'IndexToDirect'
        assert child(child(geometry,b'LayerElementMaterial'),b'MappingInformationType').props[0]==b'AllSame'
        self.n=get(self.normals,b'Normals').reshape(-1,3).copy()
        self.ni=get(self.normals,b'NormalsIndex').copy()
        self.uv=get(self.uvs,b'UV').reshape(-1,2).copy()
        self.ui=get(self.uvs,b'UVIndex').reshape(-1,3).copy()
        self.edges=get(geometry,b'Edges').copy()
        nc,vc=group_vertices(len(self.v),self.f)
        self.parts=[np.flatnonzero(vc==i) for i in range(nc)]
        self.face_part=vc[self.f[:,0]]
        self.part_faces=[np.flatnonzero(self.face_part==i) for i in range(nc)]
        self.bounds=np.array([[self.v[v].min(0),self.v[v].max(0)] for v in self.parts])
        assert all(len(a)>0 for a in self.part_faces)

    def volume(self, component):
        triangles=self.v[self.f[self.part_faces[component]]]-self.v[self.parts[component][0]]
        return np.einsum('ij,ij->i',triangles[:,0],np.cross(triangles[:,1],triangles[:,2])).sum()/6

    def encoded_indices(self):
        q=self.f.copy()
        q[:,2]=-q[:,2]-1
        return q

    def new_edges(self):
        # Preserve surviving native edges in their original order where possible.
        # The FBX edge table addresses polygon corners: each is the edge to the next corner.
        first={}
        for i,fa in enumerate(self.f):
            for j in range(3):
                e=tuple(sorted((int(fa[j]),int(fa[(j+1)%3]))))
                first.setdefault(e,3*i+j)
        current={}
        for corner in self.edges:
            face,side=divmod(int(corner),3)
            original=self.original_faces[face]
            a,b=self.old_to_new[original[[side,(side+1)%3]]]
            if a<0 or b<0:continue
            edge=tuple(sorted((int(a),int(b))))
            if edge in first:current[edge]=first[edge]
        current.update((key,corner) for key,corner in first.items() if key not in current)
        return np.fromiter(current.values(),dtype=np.int32)

    def edits(self, topology):
        possible={id(child(self.geometry,b'Vertices')):(get(self.geometry,b'Vertices'),self.v),
                  id(child(self.normals,b'Normals')):(get(self.normals,b'Normals'),self.n),
                  id(child(self.normals,b'NormalsIndex')):(get(self.normals,b'NormalsIndex'),self.ni)}
        if topology:
            possible.update({id(child(self.geometry,b'PolygonVertexIndex')):(get(self.geometry,b'PolygonVertexIndex'),self.encoded_indices()),
                             id(child(self.geometry,b'Edges')):(get(self.geometry,b'Edges'),self.new_edges()),
                             id(child(self.uvs,b'UV')):(get(self.uvs,b'UV'),self.uv),
                             id(child(self.uvs,b'UVIndex')):(get(self.uvs,b'UVIndex'),self.ui)})
        return {key:{0:new} for key,(old,new) in possible.items() if not np.array_equal(old.ravel(),new.ravel())}


def match_part(current, other, component):
    differences=current.bounds[:,None]-other.bounds[None]
    costs=np.linalg.norm(differences.reshape(len(current.parts),len(other.parts),6),axis=2)
    nearest=int(np.argmin(costs[component]))
    assert int(np.argmin(costs[:,nearest]))==component and costs[component,nearest]<.005
    return nearest


def source_branches(mesh, records, matrix):
    v=mesh.v@matrix[:3,:3].T+matrix[:3,3]
    cc=np.array([(v[ix].min(0)+v[ix].max(0))*.5 for ix in mesh.parts])
    ref=np.array([a['center'] for a in records])
    assert len(cc)==len(ref)==55
    i=np.arange(len(cc)); j=np.argmin(np.linalg.norm(cc[:,None]-ref[None],axis=2),axis=1)
    assert len(set(j.tolist()))==55
    assert max(np.linalg.norm(cc[i]-ref[j],axis=1))<.007
    return {records[b]['source']:int(a) for a,b in zip(i,j)}


def move_spike(mesh, level, matrix):
    tip=(5357,2589,1172)[level]
    incident=mesh.f[(mesh.f==tip).any(axis=1)]
    ring=np.setdiff1d(np.unique(incident),[tip])
    mean=mesh.v[ring].mean(0)
    world=lambda x:x@matrix[:3,:3].T+matrix[:3,3]
    before=world(mesh.v[tip]);after=world(mean)
    delta=np.linalg.norm(after-before)
    ring_diag=np.linalg.norm(np.ptp(world(mesh.v[ring]),axis=0))
    assert np.linalg.norm(before-[.0276948176,-1.2709505558,1.0528233051])<.001
    assert .05<delta<.055 and delta>2*ring_diag
    mesh.v[tip]=mean
    for vertex in np.r_[tip,ring]:
        fi,ci=np.where(mesh.f==vertex)
        a=mesh.v[mesh.f[fi,(ci+1)%3]]-mesh.v[vertex]
        b=mesh.v[mesh.f[fi,(ci+2)%3]]-mesh.v[vertex]
        cross=np.cross(a,b);length=np.linalg.norm(cross,axis=1)
        assert (length>0).all()
        weight=np.arctan2(length,np.einsum('ij,ij->i',a,b))
        n=np.sum(cross/length[:,None]*weight[:,None],axis=0)
        n=n/np.linalg.norm(n)
        mesh.ni[vertex]=len(mesh.n);mesh.n=np.vstack([mesh.n,n])
    return {'kind':'repair_local_fan','vertex':tip,'neighbors':ring.tolist(),'from_world':before.tolist(),
            'to_world':after.tolist(),'displacement_mm':delta*1000,'ring_diagonal_mm':ring_diag*1000,
            'normal_vertices':int(len(ring)+1)}


def graft(mesh, old_part, donor, donor_part):
    oldv=mesh.parts[old_part]; oldf=mesh.part_faces[old_part]
    dv=donor.parts[donor_part]; df=donor.part_faces[donor_part]
    assert len(oldv)==3 and len(oldf)==2 and len(dv)>=4 and len(df)>=4
    newv=np.r_[oldv, np.arange(len(mesh.v),len(mesh.v)+len(dv)-3)]
    mesh.v[oldv]=donor.v[dv[:3]]
    mesh.v=np.vstack([mesh.v,donor.v[dv[3:]]])
    normal_slots=np.arange(len(mesh.n),len(mesh.n)+len(dv))
    mesh.n=np.vstack([mesh.n,donor.n[donor.ni[dv]]])
    mesh.ni[oldv]=normal_slots[:3];mesh.ni=np.r_[mesh.ni,normal_slots[3:]]
    mapping={int(o):int(n) for o,n in zip(dv,newv)}
    new_f=np.vectorize(mapping.__getitem__,otypes=[np.int32])(donor.f[df])
    add_ui=donor.ui[df];uv_ids,uv_map=np.unique(add_ui,return_inverse=True)
    new_ui=(uv_map.reshape(add_ui.shape)+len(mesh.uv)).astype(np.int32)
    mesh.uv=np.vstack([mesh.uv,donor.uv[uv_ids]])
    mesh.f[oldf]=new_f[:2];mesh.ui[oldf]=new_ui[:2]
    mesh.f=np.vstack([mesh.f,new_f[2:]]);mesh.ui=np.vstack([mesh.ui,new_ui[2:]])
    return {'vertices':len(dv),'triangles':len(df),'donor_signed_local_volume':donor.volume(donor_part)}


def generate(cli,stage):
    logs=[];original=[]
    guards=json.loads(cli.guards.read_text())
    source=json.loads(cli.source_match.read_text()) if cli.source_match else guards['pulmonary_sources']
    lookup=lambda rows:{r['source']:r['center'] for r in rows}
    if lookup(source)!=lookup(guards['pulmonary_sources']):raise ValueError('Pulmonary source centres do not match the verified release registration.')
    for level,stem in enumerate(STEMS):
        path=cli.models/(stem+'.fbx');result=stage/path.name
        tree=read_tree(path,cli.fbx_addon);objects=geometry_by_model(tree);changes={}
        meshes,transforms=geometry_fingerprints(tree);spec=guards['levels'][level];source_sha=digest(path)
        original.append({name:Mesh(objects[name]) for name in NAMES})
        if transforms!=spec['frames']:raise ValueError(f'{stem}: a guarded model or world transform changed.')
        if meshes==spec['derived_meshes']:
            write_arrays(path,result,tree,{})
            assert digest(result)==source_sha
            logs.append({'level':level,'file':path.name,'release_sha256':EXPECTED[level],'source_sha256':source_sha,
                         'derived_sha256':source_sha,'edits':{},'status':'already_corrected'})
            continue
        if meshes!=spec['release_meshes']:
            mismatch=[n for n in NAMES if spec['release_meshes'][n]!=meshes[n]]
            raise ValueError(f'{stem}: unknown mesh fingerprint for {mismatch}.')
        per_object={}
        for name in sorted(set(COLLAPSED[level])|{PULMONARY,BRONCHUS}):
            m=Mesh(objects[name]);matrix=np.array(spec['world'][name])
            m.original_faces=m.f.copy();m.old_to_new=np.arange(len(m.v));events=[];excluded=set()
            if name==BRONCHUS:
                events.append(move_spike(m,level,matrix))
            if name==PULMONARY:
                sources=source_branches(m,source,matrix)
                for src in ('FJ2041','FJ2044'):
                    c=sources[src];excluded.add(c)
                    events.append({'kind':'omit_misregistered_derived_element','source':src,'component':c,
                                   'vertices':len(m.parts[c]),'triangles':len(m.part_faces[c])})
            for a,b in COLLAPSED[level].get(name,[]):
                part=int(m.face_part[a]);assert m.face_part[b]==part
                assert sorted(m.part_faces[part].tolist())==[a,b] and sorted(m.f[a])==sorted(m.f[b])
                if part in excluded:continue
                for higher in ([0] if name==INTERNAL else reversed(range(level))):
                    donor=original[higher][name];d=match_part(original[level][name],donor,part)
                    if len(donor.part_faces[d])>2:break
                volume=donor.volume(d)
                if name==INTERNAL:
                    diag=np.linalg.norm(np.ptp(donor.v[donor.parts[d]],axis=0))
                    assert higher==0 and len(donor.part_faces[d])==6 and abs(volume+2.8113272322661928e-15)<1e-18 and abs(diag-.001229223795235157)<1e-7
                    excluded.add(part)
                    events.append({'kind':'omit_documented_negative_occupancy_component','donor_level':higher,
                                   'donor_faces':donor.part_faces[d].tolist(),'collapsed_faces':[a,b]})
                else:
                    if name=='A_FMA7265':
                        # All 14 original inner-shell vertices lie inside the main solid.
                        assert level==2 and higher==1 and len(donor.part_faces[d])==8 and -5e-10<volume<-4e-10
                    else:assert volume>0,(name,level,part,d,volume)
                    event={'kind':'restore_closed_source_component','collapsed_faces':[a,b],'component':part,
                           'donor_level':int(higher),'donor_component':int(d),'donor_faces':donor.part_faces[d].tolist()}
                    event.update(graft(m,part,donor,d));events.append(event)
            if excluded:
                remove=np.concatenate([m.part_faces[c] for c in excluded])
                keep=np.ones(len(m.f),dtype=bool);keep[remove]=False
                m.f=m.f[keep];m.ui=m.ui[keep]
                used=np.unique(m.f)
                remap=np.full(len(m.v),-1,dtype=np.int32);remap[used]=np.arange(len(used))
                m.old_to_new=remap[:len(m.old_to_new)]
                m.v=m.v[used];m.ni=m.ni[used];m.f=remap[m.f]
            changes.update(m.edits(topology=name!=BRONCHUS))
            per_object[name]=events
        write_arrays(path,result,tree,changes)
        result_meshes,result_frames=geometry_fingerprints(read_tree(result,cli.fbx_addon))
        if result_meshes!=spec['derived_meshes'] or transforms!=result_frames:raise ValueError(f'{stem}: generated mesh failed output fingerprint.')
        logs.append({'level':level,'file':path.name,'release_sha256':EXPECTED[level],'source_sha256':source_sha,'derived_sha256':digest(result),
                     'edits':per_object})
        print(stem,logs[-1]['derived_sha256'],[(n,len(e)) for n,e in per_object.items()],flush=True)
    (stage/'repair-manifest.json').write_text(json.dumps(logs,indent=2))


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('models',type=Path);ap.add_argument('out',type=Path)
    ap.add_argument('--guards',type=Path,default=Path(__file__).with_name('repair-guards.json'))
    ap.add_argument('--source-match',type=Path,help='Override the 55 source-centre registrations from source_artery.py.')
    ap.add_argument('--fbx-addon',type=Path)
    cli=ap.parse_args();cli.out.mkdir(parents=True,exist_ok=True)
    if not __debug__:raise RuntimeError('Run without -O so every numerical and topology guard executes.')
    cli.fbx_addon=find_addon(cli.fbx_addon)
    if cli.models.resolve()==cli.out.resolve():raise ValueError('Use a distinct output directory.')
    # Publish only after all three files have passed their checks.
    with tempfile.TemporaryDirectory(prefix='anatome-native-',dir=cli.out.parent) as tmp:
        stage=Path(tmp);generate(cli,stage)
        names=[s+'.fbx' for s in STEMS]+['repair-manifest.json']
        originals={};published=[]
        for name in names:
            target=cli.out/name
            if target.exists():
                originals[name]=stage/(name+'.previous')
                shutil.copyfile(target,originals[name])
        try:
            for name in names:
                (stage/name).replace(cli.out/name);published.append(name)
        except OSError:
            for name in reversed(published):
                if name in originals:originals[name].replace(cli.out/name)
                else:(cli.out/name).unlink()
            raise


if __name__=='__main__':
    main()
