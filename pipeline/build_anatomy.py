"""Build source-preserving thorax and separately documented teaching reconstructions."""
import bpy,bmesh,json,math,random,sys,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
DATA=json.loads((ROOT/'source/thorax-selection.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
source=bpy.data.collections.new('IMMUTABLE_SOURCE_REFERENCE');scene.collection.children.link(source)
production=bpy.data.collections.new('THORAX_PRODUCTION');scene.collection.children.link(production)
references=bpy.data.collections.new('CAVITY_REFERENCES');scene.collection.children.link(references)
offset=Vector((0,-1.35,.935));zcenter=1212.5
palette={'bone':(.73,.67,.52),'cartilage':(.68,.75,.71),'muscle':(.37,.115,.10),'skin':(.52,.34,.24),'cardiac':(.40,.16,.14),'artery':(.47,.13,.115),'vein':(.18,.24,.34),'airway':(.64,.51,.39),'lung':(.55,.31,.30),'membrane':(.73,.63,.46),'nerve':(.73,.58,.19),'fat':(.70,.53,.23),'fascia':(.70,.67,.58),'duct':(.51,.65,.43)}
materials={};random.seed(93)
for tissue,color in palette.items():
 m=bpy.data.materials.new('Tissue_'+tissue);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.53 if tissue not in ('bone','nerve') else .61
 bs.inputs['Subsurface Weight'].default_value=.065 if tissue in ('lung','cardiac','skin','muscle') else .02
 noise=m.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=180 if tissue=='lung' else 280
 bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.13;bump.inputs['Distance'].default_value=.00035
 m.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal']);materials[tissue]=m
def read_obj(mid):
 verts=[];faces=[]
 for line in (ROOT/'source/bodyparts3d/obj'/f'{mid}.obj').read_text(errors='replace').splitlines():
  if line.startswith('v '):
   p=list(map(float,line.split()[1:4]));verts.append((p[0]*.001,(p[2]-zcenter)*.001,-p[1]*.001))
  elif line.startswith('f '):faces.append(tuple(int(x.split('/')[0])-1 for x in line.split()[1:]))
 return verts,faces
def object_mesh(name,verts,faces,collection=production):
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);collection.objects.link(o);o.location=offset;return o
def clean(o):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
 bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-8)
 seen=set();dupes=[]
 bm.verts.index_update()
 for f in bm.faces:
  k=tuple(sorted(v.index for v in f.verts))
  if k in seen:dupes.append(f)
  else:seen.add(k)
 if dupes:bmesh.ops.delete(bm,geom=dupes,context='FACES_ONLY')
 bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
def crop(o,zlow=1.030,zhi=1.395):
 # Source superior axis maps to local Y. Regional boundaries are explicit teaching cuts.
 bm=bmesh.new();bm.from_mesh(o.data)
 for pos,normal in ((zlow-zcenter*.001,(0,1,0)),(zhi-zcenter*.001,(0,-1,0))):
  geom=list(bm.verts)+list(bm.edges)+list(bm.faces)
  ret=bmesh.ops.bisect_plane(bm,geom=geom,plane_co=(0,pos,0),plane_no=normal,clear_inner=True,dist=1e-7)
  edges=[e for e in ret['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_boundary]
  # Only a regional cut edge is filled here; pre-existing anatomical defects are not.
  if edges:
   try:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
   except Exception:pass
 bm.to_mesh(o.data);bm.free();clean(o)
def apply_mod(o,mod):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.object.modifier_apply(modifier=mod.name)
def stats(o):
 bm=bmesh.new();bm.from_mesh(o.data);boundary=sum(e.is_boundary for e in bm.edges);non=sum(not e.is_manifold and not e.is_boundary for e in bm.edges);volume=bm.calc_volume(signed=True);bm.free()
 return {'triangles':len(o.data.polygons),'boundary_edges':boundary,'nonmanifold_edges':non,'signed_volume_m3':volume}
catalog=[];lookup={};audit=[];recon=[]
def register(o,r,notes='',kind=None):
 r=dict(r);r['object_name']=o.name;r['notes']=notes or r.get('notes','');r['source_kind']=kind or r.get('source_kind','source_mesh')
 tissue=r['tissue'];o.data.materials.clear();o.data.materials.append(materials.get(tissue,materials['fascia']))
 for f in o.data.polygons:f.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 if o.data.polygons:
  bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15192,island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
 s=stats(o);r['cutting_ready']=s['boundary_edges']==0 and s['nonmanifold_edges']==0 and abs(s['signed_volume_m3'])>1e-12
 for key in ('id','name','system','tissue','source_kind','layer','cutting_ready'):o[key]=r[key]
 o['provenance_notes']=r['notes'];o['source_mesh_ids']=','.join(r.get('mesh_ids',[]))
 catalog.append(r);lookup[r['id']]=o;audit.append({'id':r['id'],**s,'cutting_ready':r['cutting_ready']})
 return o
for r in DATA['records']:
 vs=[];fs=[]
 for mid in r['mesh_ids']:
  v,f=read_obj(mid);n=len(vs);vs.extend(v);fs.extend(tuple(i+n for i in x) for x in f)
 if not vs:continue
 if r.get('reference_only') or r['tissue']=='cavity':
  o=object_mesh('CAVITY_'+r['id'],vs,fs,references);o.hide_render=True;continue
 raw=object_mesh('SOURCE_'+r['id'],vs,fs,source);raw.hide_render=True
 o=object_mesh('A_'+r['id'],vs,fs);clean(o)
 notes='BodyParts3D 4.0 original spatial assembly; production copy welded, degenerate/duplicate faces removed, UV mapped and optimized.'
 if r['tissue']=='skin' or r['id'] in ('FMA7131','FMA10951','FMA87217','FMA3953','FMA4694'):
  crop(o);notes+=' Explicit thorax-region trim and exposed regional cut surface.'
 # Keep delicate branching and cardiac walls detailed; large surfaces get bounded simplification.
 n=len(o.data.polygons);limit=9000 if r['tissue'] in ('skin','muscle') else 14000 if r['tissue']=='cardiac' else 4500 if r['tissue']=='bone' else 12000
 if n>limit:
  d=o.modifiers.new('Runtime topology reduction','DECIMATE');d.ratio=limit/n;apply_mod(o,d);clean(o)
 register(o,r,notes)
 print('SOURCE_STRUCTURE',r['id'],len(o.data.polygons),flush=True)

GRAY806='https://commons.wikimedia.org/wiki/File:Gray806.png';GRAY968='https://commons.wikimedia.org/wiki/File:Gray968.png'
def reconstruction(o,ident,name,system,tissue,layer,method,refs=(GRAY968,),mesh_ids=()):
 clean(o);r={'id':ident,'name':name,'system':system,'tissue':tissue,'layer':layer,'mesh_ids':list(mesh_ids),'source_kind':'authored_reconstruction'}
 register(o,r,method+' Educational reconstruction; dimensions are author-selected, not measured tissue parameters.')
 recon.append({'id':ident,'name':name,'method':method,'references':list(refs),'mesh_ids':list(mesh_ids),'independently_anatomically_validated':False})
 return o
def hull(name,points):
 bm=bmesh.new()
 for p in points:bm.verts.new(p)
 bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
 loose=[v for v in bm.verts if not v.link_faces]
 if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
 me=bpy.data.meshes.new(name);bm.to_mesh(me);bm.free();o=bpy.data.objects.new(name,me);production.objects.link(o);o.location=offset
 if len(o.data.polygons)>550:
  d=o.modifiers.new('Membrane budget','DECIMATE');d.ratio=550/len(o.data.polygons);apply_mod(o,d)
 sub=o.modifiers.new('Envelope fairing','SUBSURF');sub.levels=1;apply_mod(o,sub);return o
def tube(name,points,radius):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=8;cu.bevel_depth=radius;cu.bevel_resolution=2;cu.use_fill_caps=True
 sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
 for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,cu);production.objects.link(o);o.location=offset;bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');return o
def sourcepos(x,y,z):return(x*.001,(z-zcenter)*.001,-y*.001)
# A single similarity transform for all five CT-derived MAL lobes. Axis prior fixes
# transverse rotation because the three geometric sternum centers are near-collinear.
mal=ROOT/'source/references/open-anatomy';sys.path.insert(0,str(mal))
from vtk_reader import read_vtk_polydata
landmarks=json.loads((mal/'registration-landmarks.json').read_text())['proposed_correspondences']
A=np.array([p['mal_lps_mm'] for p in landmarks]);B=np.array([p['bp3d_mm'] for p in landmarks]);ac=A.mean(0);bc=B.mean(0)
u,sv,vt=np.linalg.svd((A-ac)[:,1:].T@(B-bc)[:,1:]);rot2=u@vt
if np.linalg.det(rot2)<0: u[:,-1]*=-1;rot2=u@vt
rotation=np.eye(3);rotation[1:,1:]=rot2
scale=float(np.sum(((A-ac)@rotation)*(B-bc))/np.sum((A-ac)**2));translation=bc-scale*ac@rotation
aligned=scale*A@rotation+translation;residuals=np.linalg.norm(aligned-B,axis=1)
registration={'method':'One common axis-constrained similarity based on sternum surface centroids; transverse axes retained as prior. Cross-specimen teaching adaptation, not a validated patient registration.','scale':scale,'row_vector_rotation':rotation.tolist(),'translation_mm':translation.tolist(),'residuals_mm':residuals.tolist(),'landmarks':landmarks,'ras_to_bp_basis':[-1,-1,1]}
(ROOT/'docs/lung-registration.json').write_text(json.dumps(registration,indent=2))
mal_reference=bpy.data.collections.new('MAL_IMMUTABLE_SEPARATE_SPECIMEN');scene.collection.children.link(mal_reference);mal_reference.hide_render=True;mal_reference.hide_viewport=True
lobe_groups=[('FMA7333','RUL','superior lobe of right lung'),('FMA7383','RML','middle lobe of right lung'),('FMA7337','RLL','inferior lobe of right lung'),('FMA7370','LUL','superior lobe of left lung'),('FMA7371','LLL','inferior lobe of left lung')]
lobes=[]
for ident,code,name in lobe_groups:
 v,f=read_vtk_polydata(mal/'models'/(name+'.vtk'))
 object_mesh('MAL_SOURCE_'+code,(v*.001).tolist(),f.tolist(),mal_reference)
 bp=scale*(v*np.array([-1,-1,1]))@rotation+translation
 local=np.column_stack((bp[:,0]*.001,(bp[:,2]-zcenter)*.001,-bp[:,1]*.001))
 lung=object_mesh('A_MAL_'+code,local.tolist(),f.tolist());clean(lung)
 d=lung.modifiers.new('CT surface optimization','DECIMATE');d.ratio=min(1,8000/len(lung.data.polygons));apply_mod(lung,d);clean(lung)
 register(lung,{'id':'MAL_'+code,'name':name,'system':'Respiratory','tissue':'lung','layer':5,'mesh_ids':[name+'.vtk'],'anatomical_identifier':ident},'Actual MAL CT lobe surface; single shared cross-specimen similarity to BodyParts3D, recorded in docs/lung-registration.json. Surface optimized; original remains separate.','registered_reference_mesh');lobes.append(lung)
# Membranes are actual thin volumes with an interior, not solid organ duplicates.
for side in ('Right','Left'):
 source_lobes=[o for o in lobes if side.lower() in next(r['name'] for r in catalog if r['object_name']==o.name).lower()]
 if not source_lobes:continue
 points=[v.co.copy() for o in source_lobes for v in o.data.vertices]
 for typ,scale,thickness in [('Visceral',1.013,.00045),('Parietal',1.045,.00065)]:
  o=hull('A_REC_'+side+'_'+typ+'_Pleura',points);center=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
  for v in o.data.vertices:v.co=center+(v.co-center)*scale
  so=o.modifiers.new('Teaching membrane wall','SOLIDIFY');so.thickness=thickness;so.offset=1;apply_mod(o,so)
  reconstruction(o,'REC_'+side+'_'+typ+'_Pleura',side+' '+typ.lower()+' pleura','Respiratory','membrane',4,'Thin membrane envelope around registered MAL lung extent; simplified hilum and fissural reflections. Layer gap and thickness selected for visibility, with spatial relationships referenced to Gray 968.')
heart=[lookup[x] for x in ('FMA13884','FMA9457','FMA9531') if x in lookup]
if heart:
 points=[v.co.copy() for o in heart for v in o.data.vertices]
 o=hull('A_REC_Pericardium',points);center=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
 for v in o.data.vertices:v.co=center+(v.co-center)*1.09
 so=o.modifiers.new('Pericardial teaching membrane','SOLIDIFY');so.thickness=.0008;so.offset=1;apply_mod(o,so)
 reconstruction(o,'REC_Pericardium','Pericardial sac','Cardiovascular','membrane',4,'Pericardial envelope reconstructed around source heart walls, with a closed tissue shell; 0.8 mm teaching thickness and spacing are author-selected.',(GRAY806,GRAY968))
# Fat and fascia derive from a thoracic skin surface, not borrowed limb anatomy.
skin=lookup.get('FMA7163')
if skin:
 for name,tissue,shrink,thickness in [('Subcutaneous tissue','fat',.97,.003),('Thoracic fascia','fascia',.945,.0007)]:
  o=bpy.data.objects.new('A_REC_'+tissue,skin.data.copy());production.objects.link(o);o.location=offset
  for v in o.data.vertices:v.co.x*=shrink;v.co.z*=shrink
  # Source skin is a closed whole-body envelope. Create an inner wall via solidify.
  so=o.modifiers.new('Regional tissue wall','SOLIDIFY');so.thickness=-thickness;so.offset=-1;apply_mod(o,so)
  reconstruction(o,'REC_'+tissue,name,'Integumentary',tissue,1,'Region-cropped BodyParts3D skin envelope adapted into an inner teaching layer with explicit authored thickness.',mesh_ids=('FJ2810',))
for sign,side in ((-1,'Right'),(1,'Left')):
 # Coordinates explicitly trace relationships, with phrenic anterior and vagus posterior to root.
 paths={
 'Phrenic nerve':[(sign*40,-105,1385),(sign*47,-124,1330),(sign*58,-153,1260),(sign*61,-152,1190),(sign*64,-121,1115)],
 'Vagus nerve':[(sign*27,-76,1385),(sign*25,-78,1320),(sign*28,-87,1280),(sign*19,-76,1220),(sign*12,-70,1100)],
 'Sympathetic trunk':[(sign*36,5,1365),(sign*34,10,1285),(sign*31,16,1200),(sign*30,19,1100),(sign*31,16,1040)]}
 for name,points in paths.items():
  ident='REC_'+side+'_'+name.replace(' ','_');o=tube('A_'+ident,[sourcepos(*p) for p in points],.0011)
  reconstruction(o,ident,side+' '+name.lower(),'Nervous','nerve',6,'Author-traced gross nerve path positioned relative to source heart, root and spine; not a segmented nerve. Diameter enlarged for teaching visibility.',(GRAY806,GRAY968))
 for n in range(1,12):
  rib=next((lookup[r['id']] for r in catalog if r['system']=='Skeletal' and f'{side.lower()} ' in r['name'] and 'rib' in r['name'] and r.get('mesh_ids') and int(''.join(filter(str.isdigit,r.get('source_order','0'))) or 0)==n),None)
  # Source rib records in catalogue order are used below, preserving their paths.
  ribs=[lookup[r['id']] for r in DATA['records'] if r['required_group']=='ribs' and r['name'].startswith(side.lower())]
  if n>len(ribs):continue
  rib=ribs[n-1];verts=[v.co.copy() for v in rib.data.vertices]
  # Average points in bins around the actual source rib, slightly inferior.
  bins={}
  for p in verts:
   a=math.atan2(sign*p.x,p.z+.015);k=int(a*18);bins.setdefault(k,[]).append(p)
  points=[]
  for k in sorted(bins):
   v=sum(bins[k],Vector())/len(bins[k]);v.y-=.004;points.append(v)
  if len(points)<3:continue
  for suffix,tissue,radius,zdelta in [('Nerve','nerve',.0008,0),('Artery','artery',.00065,.002),('Vein','vein',.0008,.004)]:
   ident=f'REC_{side}_IC{n}_{suffix}';o=tube('A_'+ident,[p+Vector((0,zdelta,0)) for p in points],radius)
   reconstruction(o,ident,f'{side} intercostal {suffix.lower()} {n}','Nervous' if tissue=='nerve' else 'Cardiovascular',tissue,6,'Inferior costal neurovascular course reconstructed from the corresponding source rib; order vein-artery-nerve and visible diameters are teaching conventions.',(GRAY968,))
o=tube('A_REC_ThoracicDuct',[sourcepos(8,0,1040),sourcepos(8,-12,1150),sourcepos(6,-27,1230),sourcepos(-4,-33,1290),sourcepos(15,-50,1375)],.001)
reconstruction(o,'REC_ThoracicDuct','Thoracic duct','Lymphatic','duct',6,'Gross duct path reconstructed posterior to esophagus, ascending from right toward left superiorly; not a segmented lymphatic vessel.',(GRAY968,))
# Save source collection hidden, production master in situ on the table.
source.hide_render=True;source.hide_viewport=True;references.hide_render=True;references.hide_viewport=True
for o in production.objects:
 # Local pivots at geometry center without moving the canonical assembly.
 if o.type=='MESH' and o.data.vertices:
  center=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
  for v in o.data.vertices:v.co-=center
  o.location+=center
scene.world=bpy.data.worlds.new('Anatomy studio');scene.world.color=(.12,.12,.12)
scene.view_settings.exposure=-1.25
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
for loc,power in (((.6,-1,2),140),((-.6,-1.5,1.7),100),((0,-.8,1.8),80)):
 d=bpy.data.lights.new('Softbox','AREA');d.energy=power;d.size=.8;o=bpy.data.objects.new('Softbox',d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(offset-o.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('Thorax view');cam=bpy.data.objects.new('Thorax view',camdata);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(.6,-2.05,1.80);cam.rotation_euler=(Vector((0,-1.35,1.06))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.lens=62
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
# Intact shells are initially visible; layer inspection renders hide selected overlying systems.
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
for r in catalog:
 o=lookup[r['id']];o.hide_render=r['layer']<3 or r['tissue']=='membrane'
scene.render.filepath=str(ROOT/'qa/renders/thorax-layered.png');bpy.ops.render.render(write_still=True)
for r in catalog:lookup[r['id']].hide_render=False
bpy.ops.object.select_all(action='DESELECT')
for o in production.objects:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports/Thorax.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,use_custom_props=True,use_mesh_modifiers=True)
(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2))
(ROOT/'qa/reports/anatomy-build.json').write_text(json.dumps({'structures':len(catalog),'triangles':sum(a['triangles'] for a in audit),'cutting_ready':sum(a['cutting_ready'] for a in audit),'structure_checks':audit,'headset_tested':False,'anatomical_professional_review_required':False},indent=2))
(ROOT/'docs/reconstructions.json').write_text(json.dumps(recon,indent=2))
print('ANATOMY_BUILD_COMPLETE',len(catalog),sum(a['triangles'] for a in audit),flush=True)
