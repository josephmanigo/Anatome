"""Bounded production-only topology repair, LOD export, texture sets and QA views."""
import bpy,bmesh,json,math,numpy as np,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
scene=bpy.context.scene;production=bpy.data.collections['THORAX_PRODUCTION'];catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];byid={r['id']:r for r in catalog}
failures={r['id'] for r in json.loads((ROOT/'qa/reports/anatomy-native-validation.json').read_text())['checks'] if r['native_status']!=2}
reports=[]
def active(o):
 bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);bpy.context.view_layer.objects.active=o
def apply(o,m):active(o);bpy.ops.object.modifier_apply(modifier=m.name)
def info(o):
 bm=bmesh.new();bm.from_mesh(o.data);result={'triangles':sum(len(f.verts)-2 for f in bm.faces),'volume_m3':abs(bm.calc_volume(signed=True)),'boundary':sum(e.is_boundary for e in bm.edges),'nonmanifold':sum(not e.is_manifold and not e.is_boundary for e in bm.edges)};bm.free();return result
for o in production.objects:
 r=byid[o['id']];before=info(o);repairs=[]
 if r['id'] in failures:
  # Close only tiny connected degeneracy holes; never a large lumen or chamber.
  bm=bmesh.new();bm.from_mesh(o.data);remaining={e for e in bm.edges if e.is_boundary};diameters=[]
  while remaining:
   edge=remaining.pop();group={edge};verts=set(edge.verts);todo=list(edge.verts)
   while todo:
    v=todo.pop()
    for e in v.link_edges:
     if e in remaining:
      remaining.remove(e);group.add(e)
      for w in e.verts:
       if w not in verts:verts.add(w);todo.append(w)
   points=np.array([v.co[:] for v in verts]);diam=float(np.linalg.norm(points.max(0)-points.min(0)));diameters.append(diam)
   if diam<=.00025:
    bmesh.ops.holes_fill(bm,edges=list(group),sides=0);repairs.append({'operation':'close degeneracy loop','bounding_diagonal_m':diam})
  bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
  current=info(o)
  if current['boundary'] or current['nonmanifold']:
   # Explicit volumetric resampling of defective source surfaces. Heart cavities
   # receive a separate source-derived winding-point check before publication.
   original=o.data.copy();voxel=.00045 if r['tissue'] in ('cardiac','bone') else .0006
   o.data.remesh_voxel_size=voxel;o.data.remesh_voxel_adaptivity=0;active(o)
   try:
    bpy.ops.object.voxel_remesh();after=info(o)
    if after['volume_m3']<=0 or abs(after['volume_m3']/before['volume_m3']-1)>.08:
     o.data=original;repairs.append({'operation':'voxel repair rejected','voxel_m':voxel,'reason':'volume change exceeds eight percent'})
    else:repairs.append({'operation':'production surface voxel resampling','voxel_m':voxel,'volume_change_fraction':after['volume_m3']/before['volume_m3']-1})
   except Exception as ex:o.data=original;repairs.append({'operation':'voxel repair rejected','reason':str(ex)})
 # Runtime display budget, with the source unchanged in its hidden collection.
 tissue=r['tissue'];cap=6500 if tissue in ('lung','cardiac') else 4000 if tissue in ('muscle','skin','fat','fascia') else 3000 if tissue=='bone' else 5500
 count=info(o)['triangles']
 if count>cap:
  d=o.modifiers.new('Display budget','DECIMATE');d.ratio=cap/count;apply(o,d)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 active(o);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
 for p in o.data.polygons:p.use_smooth=True
 after=info(o);r['cutting_ready']=after['boundary']==0 and after['nonmanifold']==0;o['cutting_ready']=r['cutting_ready']
 if repairs:r['notes']+=' Production repair details in qa/reports/anatomy-refinement.json.'
 reports.append({'id':r['id'],'before':before,'after':after,'repairs':repairs})
 print('REFINED',r['id'],after,flush=True)
# Correct formal identifiers from the downloaded BP3D concept list.
for code,fma in [('RUL','FMA7333'),('RML','FMA7383'),('RLL','FMA7337'),('LUL','FMA7370'),('LLL','FMA7371')]:byid['MAL_'+code]['anatomical_identifier']=fma
# Shared portable textures use deterministic restrained color modulation.
rng=np.random.default_rng(82);texture_dir=ROOT/'exports/textures';texture_dir.mkdir(exist_ok=True)
for m in bpy.data.materials:
 if not m.name.startswith('Tissue_'):continue
 color=np.array(m.diffuse_color[:3]);pixels=np.ones((256,256,4),dtype=np.float32)
 pattern=rng.normal(0,.008,(256,256,1));pixels[:,:,:3]=np.clip(color+pattern,0,1)
 img=bpy.data.images.new(m.name+'_BaseColor',width=256,height=256);img.pixels.foreach_set(pixels.ravel());img.filepath_raw=str(texture_dir/(m.name+'_BaseColor.png'));img.file_format='PNG';img.save()
 # Distinct exposed tissue appearance; bone gets a separate cancellous pattern.
 base=color*.72+np.array([.60,.27,.20])*.28
 if m.name=='Tissue_bone':base=np.array([.55,.36,.20]);pattern=(rng.random((256,256,1))>.75)*-.17
 pixels[:,:,:3]=np.clip(base+pattern*2,0,1);img=bpy.data.images.new(m.name.replace('Tissue_','Interior_')+'_BaseColor',width=256,height=256);img.pixels.foreach_set(pixels.ravel());img.filepath_raw=str(texture_dir/(img.name+'.png'));img.file_format='PNG';img.save()
(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2))
(ROOT/'qa/reports/anatomy-refinement.json').write_text(json.dumps({'structures':len(catalog),'triangles':sum(x['after']['triangles'] for x in reports),'checks':reports},indent=2))
for o in production.objects:o.hide_render=False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
def export(path):
 bpy.ops.object.select_all(action='DESELECT')
 for o in production.objects:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_custom_props=True)
export(ROOT/'exports/Thorax.fbx')
# Review anatomy from explicitly named directions. Front = anterior (supine up).
target=Vector((0,-1.35,1.035));scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.cycles.samples=24;scene.view_settings.exposure=-2.0
for name,loc,hide_bones in [('anterior',(0,-1.35,2.0),False),('organs',(0,-1.35,2.0),True),('posterior',(0,-1.35,.10),False),('left_lateral',(.85,-1.35,1.05),False),('superior',(0,-.5,1.07),False)]:
 for o in production.objects:
  r=byid[o['id']];o.hide_render=r['layer']<3 or r['tissue']=='membrane' or (hide_bones and r['system'] in ('Skeletal','Muscular'))
 scene.camera.location=loc;scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=.54
 scene.render.filepath=str(ROOT/'qa/renders'/('thorax-'+name+'.png'));bpy.ops.render.render(write_still=True)
# LOD meshes are separate exports. Interactive components always own LOD0 collider.
for level,ratio in [(1,.48),(2,.45)]:
 for o in production.objects:
  if len(o.data.polygons)>200:
   d=o.modifiers.new('LOD'+str(level),'DECIMATE');d.ratio=ratio;apply(o,d)
 export(ROOT/'exports'/('Thorax_LOD'+str(level)+'.fbx'))
print('REFINEMENT_COMPLETE',flush=True)
