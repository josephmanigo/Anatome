"""Read-only real-asset manifold audit in Blender; a miss stroke validates topology."""
import bpy,ctypes as C,json,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
lib=C.CDLL(str(ROOT/'unity/ThoraxAssetDemo/Assets/Plugins/x86_64/thoraxcut.dll'))
lib.tl_cut.argtypes=[C.POINTER(C.c_float),C.c_int,C.POINTER(C.c_int),C.c_int,C.POINTER(C.c_float),C.c_int,C.c_int,C.c_int];lib.tl_cut.restype=C.c_void_p
lib.tl_status.argtypes=[C.c_void_p];lib.tl_message.argtypes=[C.c_void_p];lib.tl_message.restype=C.c_char_p;lib.tl_free.argtypes=[C.c_void_p]
blade=np.array([[10+((i&1)>0)*.01+j*.01,10+((i&2)>0)*.01,10+((i&4)>0)*.01] for j in range(2) for i in range(8)],dtype=np.float32).ravel()
out=[]
catalog={r['id']:r for r in json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records']}
for o in bpy.data.collections['THORAX_PRODUCTION'].objects:
 if o.type!='MESH':continue
 o.data.calc_loop_triangles();p=np.array([v.co[:] for v in o.data.vertices],dtype=np.float32);t=np.array([f.vertices[:] for f in o.data.loop_triangles],dtype=np.int32)
 volume=np.sum(np.einsum('ij,ij->i',p[t[:,0]],np.cross(p[t[:,1]],p[t[:,2]])))/6
 if volume<0:t=t[:,[0,2,1]].copy()
 v=np.zeros((len(p),6),dtype=np.float32);v[:,:3]=p
 h=lib.tl_cut(v.ctypes.data_as(C.POINTER(C.c_float)),len(v),t.ctypes.data_as(C.POINTER(C.c_int)),t.size,blade.ctypes.data_as(C.POINTER(C.c_float)),2,100000,32)
 status=lib.tl_status(h);message=lib.tl_message(h).decode();lib.tl_free(h)
 out.append({'id':o['id'],'reference_region':catalog[o['id']].get('reference_region',False),'native_status':status,'message':message,'triangles':len(t),'signed_volume_m3':float(volume)})
 print(o['id'],status,message,flush=True)
tissue=[x for x in out if not x['reference_region']];accepted=sum(x['native_status']==2 and x['message']=='No material intersected.' for x in tissue)
(ROOT/'qa/reports/anatomy-native-validation.json').write_text(json.dumps({'checks':out,'closed_accepted_tissue':accepted,'total_tissue':len(tissue),'annotation_count':len(out)-len(tissue),'total':len(out)},indent=2))
if accepted!=len(tissue):raise RuntimeError('Production tissue topology validation failed')
