import bpy,json,sys,time,numpy as np,ctypes as C
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'pipeline'))
from native_mesh import lib,FP,IP,buffers
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
objects={o['id']:o for o in bpy.data.collections['THORAX_PRODUCTION'].objects};out=[]
for ident in ['FMA7163','FMA7394','FMA7396','FMA13884','MAL_RUL','FMA7857','FMA31747']:
 o=objects[ident];v,t=buffers(o);p=v[:,:3];minimum=p.min(0);maximum=p.max(0);center=(minimum+maximum)/2;size=maximum-minimum;bvh=BVHTree.FromPolygons([Vector(x) for x in p],t.tolist(),all_triangles=True)
 for style in ['shallow','curved','grazing','miss']:
  corners=[]
  for step in np.linspace(-1,1,5):
   candidate=center+np.array([step*size[0]*.35,np.sin(step*2)*size[1]*.10, size[2]])
   location,normal,_,distance=bvh.find_nearest(Vector(candidate));position=np.array(location);normal=np.array(normal);normal/=max(np.linalg.norm(normal),1e-10)
   axis=np.cross(normal,np.array([0,1,0]));axis/=max(np.linalg.norm(axis),1e-10);other=np.cross(normal,axis)
   depth=.0012 if style=='shallow' else .004 if style=='grazing' else .012
   if style=='miss':position+=normal*.2
   if style=='grazing':position+=normal*.0035
   for i in range(8):corners.append(position+axis*((1 if i&1 else -1)*.0007)+other*((1 if i&2 else -1)*.004)+normal*((1 if i&4 else -1)*depth))
  corners=np.asarray(corners,dtype=np.float32);started=time.perf_counter();h=lib.tl_cut(v.ctypes.data_as(FP),len(v),t.ctypes.data_as(IP),t.size,corners.ctypes.data_as(FP),5,100000,32)
  status=lib.tl_status(h);message=lib.tl_message(h).decode();pieces=lib.tl_piece_count(h);caps=0;positive=True;triangles=0
  for i in range(pieces):
   nv=C.c_int();nt=C.c_int();lib.tl_piece_sizes(h,i,C.byref(nv),C.byref(nt));pv=np.zeros((nv.value,6),np.float32);pt=np.zeros(nt.value,np.int32);lib.tl_piece_copy(h,i,pv.ctypes.data_as(FP),pv.size,pt.ctypes.data_as(IP),pt.size);pt=pt.reshape(-1,3);q=pv[:,:3];volume=np.einsum('ij,ij->i',q[pt[:,0]],np.cross(q[pt[:,1]],q[pt[:,2]])).sum()/6;positive=positive and volume>0;caps+=int(np.sum(np.all(pv[pt,5]>.5,axis=1)));triangles+=len(pt)
  lib.tl_free(h);elapsed=(time.perf_counter()-started)*1000
  passed=(status==2 if style=='miss' else status==0 and positive and caps>0 and triangles<=100000 and pieces<=32)
  row={'id':ident,'stroke':style,'passed':bool(passed),'native_status':status,'message':message,'pieces':pieces,'cut_face_triangles':caps,'positive_tissue_volumes':bool(positive),'output_triangles':triangles,'windows_geometry_ms':round(elapsed,3)};out.append(row);print(row,flush=True)
(ROOT/'qa/reports/actual-anatomy-cuts.json').write_text(json.dumps({'passed':sum(x['passed'] for x in out),'cases':len(out),'checks':out,'headset_measured':False},indent=2))
