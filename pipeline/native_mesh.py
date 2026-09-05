import ctypes as C,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
lib=C.CDLL(str(ROOT/'unity/ThoraxAssetDemo/Assets/Plugins/x86_64/thoraxcut.dll'))
FP=C.POINTER(C.c_float);IP=C.POINTER(C.c_int)
lib.tl_normalize.argtypes=[FP,C.c_int,IP,C.c_int];lib.tl_normalize.restype=C.c_void_p
lib.tl_resolve_compound.argtypes=[FP,C.c_int,IP,C.c_int];lib.tl_resolve_compound.restype=C.c_void_p
lib.tl_cut.argtypes=[FP,C.c_int,IP,C.c_int,FP,C.c_int,C.c_int,C.c_int];lib.tl_cut.restype=C.c_void_p
lib.tl_status.argtypes=[C.c_void_p];lib.tl_message.argtypes=[C.c_void_p];lib.tl_message.restype=C.c_char_p
lib.tl_piece_count.argtypes=[C.c_void_p];lib.tl_piece_sizes.argtypes=[C.c_void_p,C.c_int,IP,IP];lib.tl_piece_copy.argtypes=[C.c_void_p,C.c_int,FP,C.c_int,IP,C.c_int];lib.tl_free.argtypes=[C.c_void_p]
def buffers(o):
 o.data.calc_loop_triangles();p=np.array([v.co[:] for v in o.data.vertices],dtype=np.float32);t=np.array([f.vertices[:] for f in o.data.loop_triangles],dtype=np.int32)
 volume=np.einsum('ij,ij->i',p[t[:,0]],np.cross(p[t[:,1]],p[t[:,2]])).sum()
 if volume<0:t=t[:,[0,2,1]].copy()
 v=np.zeros((len(p),6),dtype=np.float32);v[:,:3]=p
 return v,t
def result(h):
 try:
  status=lib.tl_status(h);message=lib.tl_message(h).decode()
  if status!=0:raise RuntimeError(str(status)+' '+message)
  vertices=[];faces=[];count=0
  for i in range(lib.tl_piece_count(h)):
   nv=C.c_int();nt=C.c_int();lib.tl_piece_sizes(h,i,C.byref(nv),C.byref(nt));v=np.zeros((nv.value,6),np.float32);t=np.zeros(nt.value,np.int32);lib.tl_piece_copy(h,i,v.ctypes.data_as(FP),v.size,t.ctypes.data_as(IP),t.size);vertices.extend(v[:,:3].tolist());faces.extend((t.reshape(-1,3)+count).tolist());count+=nv.value
  return vertices,faces
 finally:lib.tl_free(h)
def normalize(o):
 v,t=buffers(o);return result(lib.tl_normalize(v.ctypes.data_as(FP),len(v),t.ctypes.data_as(IP),t.size))
def resolve(o):
 v,t=buffers(o);return result(lib.tl_resolve_compound(v.ctypes.data_as(FP),len(v),t.ctypes.data_as(IP),t.size))
def clear_point(o,point,half=.00035):
 v,t=buffers(o);p=np.array(point);corners=np.array([p+np.array([(-1 if i&1 else 1)*half,(-1 if i&2 else 1)*half,(-1 if i&4 else 1)*half])+np.array([0,0,j*.0001]) for j in range(2) for i in range(8)],np.float32)
 return result(lib.tl_cut(v.ctypes.data_as(FP),len(v),t.ctypes.data_as(IP),t.size,corners.ctypes.data_as(FP),2,100000,32))
