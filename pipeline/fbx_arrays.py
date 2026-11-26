"""Edit selected native FBX arrays, retaining every untouched raw property payload."""
from pathlib import Path
import struct
import sys
import types
import zlib
import numpy as np


def read_tree(path, io_scene_fbx_dir):
    name="_anatome_fbx_reader"
    if name not in sys.modules:
        p=types.ModuleType(name);p.__path__=[str(io_scene_fbx_dir)];sys.modules[name]=p
    from _anatome_fbx_reader import parse_fbx
    return parse_fbx.parse(str(path))[0]


def child(parent, name):
    return next(x for x in parent.elems if x.id==name)


def geometry_by_model(tree):
    items=child(tree,b'Objects').elems
    models={o.props[0]:o.props[1].split(b'\0\1')[0].decode() for o in items if o.id==b'Model'}
    geos={o.props[0]:o for o in items if o.id==b'Geometry' and o.props[2]==b'Mesh'}
    return {models[c.props[2]]:geos[c.props[1]] for c in child(tree,b'Connections').elems
            if c.props[0]==b'OO' and c.props[1] in geos and c.props[2] in models}


ARRAY_TYPES={b'f':'<f4',b'd':'<f8',b'i':'<i4',b'l':'<i8',b'b':'u1',b'c':'u1'}
SCALAR_BYTES={b'Y':2,b'C':1,b'I':4,b'F':4,b'D':8,b'L':8}


def property_payload(raw, start, end, count, edits):
    offset=start;out=bytearray()
    for number in range(count):
        first=offset;tag=raw[first:first+1];offset+=1
        if tag in ARRAY_TYPES:
            length,encoding,size=struct.unpack_from('<III',raw,offset);offset+=12+size
        elif tag in (b'S',b'R'):
            size=struct.unpack_from('<I',raw,offset)[0];offset+=4+size
        else:
            offset+=SCALAR_BYTES[tag]
        if number not in edits:
            out.extend(raw[first:offset]);continue
        if tag not in ARRAY_TYPES or encoding not in (0,1):
            raise ValueError(f'Unsupported replacement type or encoding: {tag!r}')
        arr=np.asarray(edits[number],dtype=ARRAY_TYPES[tag]).ravel()
        payload=arr.tobytes()
        if encoding:payload=zlib.compress(payload,1)
        out.extend(tag+struct.pack('<III',arr.size,encoding,len(payload))+payload)
    if offset!=end or any(i<0 or i>=count for i in edits):raise ValueError('Inconsistent FBX property range')
    return out


def write_arrays(original, output, tree, changes):
    original=Path(original);output=Path(output)
    if original.resolve()==output.resolve():raise ValueError('Use a distinct output path')
    raw=original.read_bytes()
    if not raw.startswith(b'Kaydara FBX Binary  \x00\x1a\x00'):raise ValueError('Binary FBX required')
    version=struct.unpack_from('<I',raw,23)[0]
    fmt=struct.Struct('<IIIB' if version<7500 else '<QQQB')
    buf=bytearray(raw[:27]); touched=set()
    def emit(begin, elem):
        end,count,nprops,namelen=fmt.unpack_from(raw,begin)
        outstart=len(buf);namestart=begin+fmt.size;ps=namestart+namelen;pe=ps+nprops
        if raw[namestart:ps]!=elem.id or count!=len(elem.props) or not end:raise ValueError('FBX tree does not match source')
        buf.extend(b'\0'*fmt.size);buf.extend(raw[namestart:ps])
        edits=changes.get(id(elem))
        if edits:
            payload=property_payload(raw,ps,pe,count,edits);buf.extend(payload); plen=len(payload);touched.add(id(elem))
        else:
            buf.extend(raw[ps:pe]);plen=nprops
        pos=pe
        for sub in elem.elems:pos=emit(pos,sub)
        if pos>end or any(raw[pos:end]):raise ValueError('Unexpected FBX node terminator')
        buf.extend(raw[pos:end]);fmt.pack_into(buf,outstart,len(buf),count,plen,namelen)
        return end
    pos=27
    for el in tree.elems:pos=emit(pos,el)
    if touched!=set(changes):raise ValueError('At least one replacement did not address this FBX tree')
    if raw[pos:pos+fmt.size]!=bytes(fmt.size):raise ValueError('Missing FBX root terminator')
    buf.extend(raw[pos:pos+fmt.size]);footer=raw[pos+fmt.size:]
    # Standard FBX footer aligns the version field to 16 bytes.
    if len(footer)>=160 and footer[-140:-136]==struct.pack('<I',version) and not any(footer[20:-140]):
        buf.extend(footer[:20]);buf.extend(bytes((-len(buf))%16 or 16));buf.extend(footer[-140:])
    else:buf.extend(footer)
    output.parent.mkdir(parents=True,exist_ok=True)
    temp=output.with_suffix(output.suffix+'.partial');temp.write_bytes(buf);temp.replace(output)
