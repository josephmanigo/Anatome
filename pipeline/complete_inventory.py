"""Missing lower costal cartilage reconstructions and non-tissue cardiac regions."""
import bpy,bmesh,json,math,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
production=bpy.data.collections['THORAX_PRODUCTION'];catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];objects={o['id']:o for o in production.objects};records={r['id']:r for r in catalog};notes=[]
def curve_mesh(ident,paths,radius,material):
 cu=bpy.data.curves.new(ident,'CURVE');cu.dimensions='3D';cu.bevel_depth=radius;cu.bevel_resolution=1;cu.use_fill_caps=True;cu.resolution_u=10
 for path in paths:
  sp=cu.splines.new('BEZIER' if len(path)>2 else 'POLY')
  if len(path)>2:
   sp.bezier_points.add(len(path)-1)
   for p,v in zip(sp.bezier_points,path):p.co=v;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
  else:
   sp.points.add(len(path)-1)
   for p,v in zip(sp.points,path):p.co=(*v,1)
 o=bpy.data.objects.new('A_'+ident,cu);production.objects.link(o);cu.materials.append(material);bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 center=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
 for v in o.data.vertices:v.co-=center
 o.location=center
 return o
def register(o,ident,name,tissue,group,method,source_ids=(),reference=False):
 r={'id':ident,'name':name,'object_name':o.name,'system':'Cardiovascular' if reference else 'Skeletal','tissue':tissue,'layer':6 if reference else 3,'source_kind':'source_region_annotation' if reference else 'authored_reconstruction','mesh_ids':list(source_ids),'notes':method,'cutting_ready':not reference,'reference_region':reference,'required_group':group,'source_record':'docs/inventory-completion.json'}
 for key in ['id','name','system','tissue','layer','source_kind','cutting_ready']:o[key]=r[key]
 o['reference_region']=reference;o['provenance_notes']=method;catalog.append(r);records[ident]=r;objects[ident]=o;notes.append(r)
 for p in o.data.polygons:p.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
def world_vertices(o):return [o.matrix_world@v.co for v in o.data.vertices]
ordinal={8:'eighth',9:'ninth',10:'tenth',11:'eleventh',12:'twelfth'}
reference='https://openstax.org/books/anatomy-and-physiology/pages/7-4-the-thoracic-cage'
for side,sign,cart7 in [('right',-1,'FMA8248'),('left',1,'FMA8275')]:
 previous=objects[cart7]
 for n in range(8,13):
  ident=f'REC_{side}_costal_cartilage_{n}'
  if ident in objects:previous=objects[ident];continue
  r=next(r for r in catalog if r['name']==side+' '+ordinal[n]+' rib');rib=objects[r['id']];points=world_vertices(rib);front=max(p.z for p in points);tip=sum((p for p in points if p.z>front-.002),Vector())/sum(p.z>front-.002 for p in points)
  if n<=10:
   candidates=world_vertices(previous);end=min(candidates,key=lambda p:(p-tip).length);middle=(tip+end)*.5+Vector((-sign*.005,0,.005));path=[tip,middle,end];method='Authored costal margin segment from source rib tip to the preceding costal cartilage. Ribs 8-10 join the cartilage above; dimensions and bone/cartilage boundary are author estimates. Reference: '+reference
  else:path=[tip,tip+Vector((-sign*.008,-.002,.002))];method='Short authored terminal costal-cartilage cap on the floating rib, without connection to the sternum or other cartilage. Length and boundary are author estimates. Reference: '+reference
  o=curve_mesh(ident,[path],.0038 if n<=10 else .0022,bpy.data.materials['Tissue_cartilage']);register(o,ident,side+' costal cartilage '+str(n),'cartilage','costal_cartilages',method,r['mesh_ids']);previous=o
# Three orthogonal contour traces indicate each actual source cavity. They are
# annotations of empty space, excluded from tissue cutting and default display.
selection=json.loads((ROOT/'source/thorax-selection.json').read_text())['records']
for r in [r for r in selection if r.get('reference_only')]:
 if r['id'] in objects:continue
 ref=bpy.data.objects['CAVITY_'+r['id']];ref.data.calc_loop_triangles();v=np.array([(ref.matrix_world@p.co)[:] for p in ref.data.vertices]);faces=np.array([t.vertices[:] for t in ref.data.loop_triangles]);center=v.mean(0);paths=[]
 for axis in range(3):
  for face in faces:
   tri=v[face];dist=tri[:,axis]-center[axis];cross=[]
   for i,j in [(0,1),(1,2),(2,0)]:
    if dist[i]*dist[j]<0:cross.append(tri[i]+(tri[j]-tri[i])*dist[i]/(dist[i]-dist[j]))
   if len(cross)==2:paths.append([Vector(p) for p in cross])
 o=curve_mesh(r['id'],paths,.00025,bpy.data.materials['Tissue_vein']);register(o,r['id'],r['name'],'cavity','cardiac_chambers','Contour annotation of an actual source cavity, representing empty space. Select the surrounding atrial or ventricular wall for cutting.',r['mesh_ids'],True);o.hide_render=True
# Septa are regions within the existing source walls, not extra solid overlays.
audit=json.loads((ROOT/'source/chamber-wall-followup-audit.json').read_text());vent=next(v for k,v in audit.items() if isinstance(v,dict) and 'wall_samples_between_cavities' in v)
points=[s['point_mm'] for s in vent['samples'] if abs(s['ventricular_wall_winding'])>.9 and abs(s['left_cavity_winding'])<.1 and abs(s['right_cavity_winding'])<.1]
if points:
 p=np.mean(points,axis=0);world=Vector((p[0]*.001,(p[2]-1212.5)*.001-1.35,-p[1]*.001+.935));ident='REGION_InterventricularSeptum';o=curve_mesh(ident,[[world+Vector((-.004,0,0)),world+Vector((.004,0,0))],[world+Vector((0,-.004,0)),world+Vector((0,.004,0))]],.0004,bpy.data.materials['Tissue_cardiac']);register(o,ident,'Interventricular septal region','cardiac','cardiac_septa','Region anchor within sampled separating ventricular wall tissue (FMA13884). Full septal extent is not separately segmented; use its parent wall for cutting.',('FJ2428',),True);o.hide_render=True
(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));(ROOT/'docs/inventory-completion.json').write_text(json.dumps(notes,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'));print('INVENTORY_STRUCTURES',len(catalog),flush=True)
