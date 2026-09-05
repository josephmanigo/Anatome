"""Original, editable photo-inspired lab. Blender 5.2 background entry point."""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
random.seed(24)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.world=bpy.data.worlds.new('Laboratory ambient')
scene.world.color=(.2,.2,.2)
objects=[]
def material(name,color,metal=0,rough=.5):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
 if name in ('Stainless','Floor','Cloth'):
  tex=m.node_tree.nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=170 if name=='Stainless' else 85
  bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.001
  m.node_tree.links.new(tex.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
 return m
M={
'Wall':material('Wall',(.74,.735,.70)), 'Floor':material('Floor',(.31,.335,.34),0,.62),
'Stainless':material('Stainless',(.30,.34,.37),.94,.24), 'DarkSteel':material('DarkSteel',(.07,.085,.09),.8,.4),
'Cloth':material('Cloth',(.89,.90,.90),0,.8),'Blue':material('Blue',(.13,.34,.43),0,.8),
'Ceiling':material('Ceiling',(.77,.77,.74),0,.85),'Wood':material('Wood',(.35,.21,.095),0,.52),
'Glass':material('Glass',(.17,.245,.265),.35,.2),'Black':material('Black',(.018,.022,.024),0,.55),
'White':material('White',(.91,.91,.87),0,.35),'Red':material('Red',(.46,.035,.025),0,.45),
'Light':material('Light',(.96,.96,.91),0,.2),'Sign':material('Sign',(.11,.15,.17),0,.6)}
bs=M['Light'].node_tree.nodes.get('Principled BSDF');bs.inputs['Emission Color'].default_value=(1,.96,.88,1);bs.inputs['Emission Strength'].default_value=2
def finish(o,name,mat,bevel=0):
 o.name=name;o.data.materials.append(M[mat]);objects.append(o)
 if bevel:
  mod=o.modifiers.new('Manufactured edge bevel','BEVEL');mod.width=bevel;mod.segments=2
  mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
 return o
def box(name,loc,size,mat='Stainless',bevel=.006):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,name,mat,bevel)
def cylinder(name,a,b,r,mat='Stainless',verts=16):
 a,b=Vector(a),Vector(b);delta=b-a
 bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=delta.length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_quaternion=delta.to_track_quat('Z','Y');o.rotation_mode='QUATERNION';o.rotation_quaternion=delta.to_track_quat('Z','Y')
 for p in o.data.polygons:p.use_smooth=True
 return finish(o,name,mat,.002)
def sphere(name,loc,scale,mat='White'):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,radius=1,location=loc);o=bpy.context.object;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for p in o.data.polygons:p.use_smooth=True
 return finish(o,name,mat)
def text(name,content,loc,size=.1,rot=(math.pi/2,0,0),mat='Sign'):
 cu=bpy.data.curves.new(name,'FONT');cu.body=content;cu.size=size;cu.align_x='CENTER';cu.extrude=.0004
 o=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=rot;cu.materials.append(M[mat]);objects.append(o);return o
def curve(name,points,r=.012,mat='Stainless'):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=2;cu.resolution_u=8
 sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
 for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(o);cu.materials.append(M[mat]);objects.append(o);return o
def table(name,x,y,covered=False):
 box(name+'_pedestal',(x,y,.40),(.45,1.35,.72),'Stainless',.018)
 box(name+'_foot',(x,y,.055),(.73,1.8,.11),'DarkSteel',.035)
 box(name+'_top',(x,y,.855),(.86,2.15,.07),'Stainless',.012)
 for sx in (-.43,.43):box(name+'_raised_rim',(x+sx,y,.901),(.018,2.15,.025),'Stainless',.004)
 for sy in (-1.07,1.07):box(name+'_end_rim',(x,y+sy,.901),(.86,.016,.025),'Stainless',.004)
 for sy in (-.85,.85):cylinder(name+'_drain',(x-.34,y+sy,.85),(x-.34,y+sy,.61),.012)
 if covered:
  # Original cloth over a teaching-table form, not an anatomy model.
  nx,ny=28,55;vs=[];fs=[]
  for j in range(ny):
   v=j/(ny-1);yy=(v-.5)*2.25
   for i in range(nx):
    u=i/(nx-1);xx=(u-.5)*1.13
    hump=.19*math.exp(-((xx/.25)**4+(yy/.74)**4))+.08*math.exp(-((xx/.22)**2+((yy-.72)/.23)**2))
    drop=max(0,abs(xx)-.4)*1.5+max(0,abs(yy)-1.03)*.9
    folds=.012*math.sin(xx*43+yy*8)+.009*math.sin(yy*39-xx*11)
    vs.append((x+xx,y+yy,.91+hump-drop+folds))
  for j in range(ny-1):
   for i in range(nx-1):a=j*nx+i;fs.append((a,a+1,a+1+nx,a+nx))
  mesh=bpy.data.meshes.new(name+'_draped_linen');mesh.from_pydata(vs,[],fs);mesh.update();o=bpy.data.objects.new(name+'_draped_linen',mesh);bpy.context.collection.objects.link(o);finish(o,o.name,'Cloth')
  for p in mesh.polygons:p.use_smooth=True
def cart(name,x,y):
 for z in (.23,.87):
  box(name+'_shelf',(x,y,z),(.62,.87,.028),'Stainless',.012)
  for sx in (-.31,.31):box(name+'_shelf_rim',(x+sx,y,z+.023),(.016,.87,.05),'Stainless')
 for sx in (-.27,.27):
  for sy in (-.39,.39):
   cylinder(name+'_leg',(x+sx,y+sy,.12),(x+sx,y+sy,.95),.016)
   cylinder(name+'_caster',(x+sx-.028,y+sy,.07),(x+sx+.028,y+sy,.07),.064,'Black',20)
   box(name+'_caster_fork',(x+sx,y+sy,.14),(.05,.06,.1),'Stainless')
 curve(name+'_handle',[(x-.31,y+.46,.9),(x-.31,y+.52,1.02),(x+.31,y+.52,1.02),(x+.31,y+.46,.9)])
 box(name+'_blue_drape',(x,y,.894),(.51,.7,.008),'Blue',.002)
 for i in range(7):
  px=x-.22+i*.067;box(name+'_instrument',(px,y,.905),(.012,.3,.006),'Stainless',.002)
  cylinder(name+'_instrument_tip',(px,y+.1,.913),(px+.013,y+.2,.913),.003,'Stainless',8)
def exam_light(name,x,y):
 cylinder(name+'_mount',(x,y,2.98),(x,y,2.7),.065,'White',24)
 a=(x,y,2.7);b=(x+.55,y+.35,2.7);c=(x+.9,y+.35,2.34)
 cylinder(name+'_arm_a',a,b,.037,'White');cylinder(name+'_arm_b',b,c,.03,'White')
 sphere(name+'_joint',b,(.08,.08,.08),'White')
 cylinder(name+'_rim',(c[0],c[1],2.21),(c[0],c[1],2.3),.27,'Stainless',40)
 cylinder(name+'_face',(c[0],c[1],2.20),(c[0],c[1],2.213),.245,'White',40)
 for j in range(8):
  a=j*math.tau/8;xx=c[0]+.16*math.cos(a);yy=c[1]+.16*math.sin(a)
  cylinder(name+'_led',(xx,yy,2.189),(xx,yy,2.198),.048,'Light',12)
 cylinder(name+'_handle',(c[0],c[1],2.10),(c[0],c[1],2.23),.022,'DarkSteel')
def faucet(name,x,y):
 curve(name,[(x,y,.95),(x,y,1.22),(x,y-.07,1.36),(x,y-.22,1.34),(x,y-.24,1.21)],.014)
 cylinder(name+'_valve',(x+.10,y,.95),(x+.1,y,1.01),.022)
box('Floor',(0,0,-.1),(12,9,.2),'Floor',0)
box('Rear wall',(0,4.55,1.5),(12,.16,3),'Wall',0)
box('Left wall',(-6.05,0,1.5),(.16,9,3),'Wall',0)
box('Right wall',(6.05,0,1.5),(.16,9,3),'Wall',0)
box('Entry wall',(0,-4.55,1.5),(12,.16,3),'Wall',0)
for x in range(-6,6):
 for y in range(-4,5):
  box('Ceiling tile',(x+.5,y-.0,3.03),(.984,.984,.035),'Ceiling',.001)
for x in (-4,-1,2,5):
 for y in (-3,0,3):box('Light panel',(x,y,2.996),(.65,1.25,.018),'Light',.001)
for x in (-5.97,5.97):box('Coved skirting',(x,0,.08),(.035,9,.16),'Stainless',.007)
box('Rear skirting',(0,4.43,.08),(12,.035,.16),'Stainless')
table('Main teaching table',0,-1.35)
table('Covered station A',-3,1.5,True);table('Covered station B',0,2.0,True);table('Covered station C',3.5,1.2,True)
cart('Primary instrument cart',1.2,-1.6);cart('Rear instrument cart',2.0,2.2);cart('Side instrument cart',-4.3,.6)
for x,y in ((-.6,-1.3),(-3.7,1.5),(2.7,1.4)):
 cylinder('Bucket stand',(x,y,.07),(x,y,.63),.021)
 bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=.12,radius2=.18,depth=.22,location=(x,y,.66));finish(bpy.context.object,'Stainless bucket','Stainless')
 for a in (0,2.1,4.2):cylinder('Bucket stand foot',(x,y,.05),(x+.25*math.cos(a),y+.25*math.sin(a),.04),.014)
for x in (-2.8,-1.6,-.4):
 box('Cabinet back',(x,4.435,1.66),(1.16,.025,1.8),'Stainless',.006)
 for sx in (-.568,.568):box('Cabinet side',(x+sx,4.17,1.66),(.025,.55,1.8),'Stainless',.006)
 for z in (.772,2.548):box('Cabinet top bottom',(x,4.17,z),(1.16,.55,.025),'Stainless',.006)
 for z in (1.0,1.36,1.72,2.08,2.44):box('Cabinet shelf',(x,4.17,z),(1.10,.49,.025),'Stainless')
 for sx in (-.285,.285):
  box('Glazed cabinet door',(x+sx,3.866,1.75),(.51,.018,1.36),'Glass',.004)
  for sz in (-.69,.69):box('Cabinet frame',(x+sx,3.843,1.75+sz),(.55,.03,.027),'Stainless')
  cylinder('Cabinet pull',(x+sx+.18,3.81,1.68),(x+sx+.18,3.81,1.86),.009)
 for z in (1.13,1.48,1.84,2.18):
  for sx in (-.32,.1):
   for k in range(3):box('Folded linens',(x+sx,4.05,z+k*.035),(.31,.16,.027),'Cloth' if k%2 else 'Blue',.01)
for x in (1.15,2.35,3.55,4.75):
 box('Rear counter',(x,4.08,.94),(1.18,.76,.065),'Stainless',.012)
 box('Counter base',(x,4.17,.48),(1.15,.52,.84),'Stainless',.008)
 for sx in (-.29,.29):
  box('Under counter door',(x+sx,3.89,.48),(.55,.02,.75),'Stainless',.008)
  cylinder('Door pull',(x+sx,3.86,.59),(x+sx,3.86,.76),.009)
for x in (1.15,3.55):
 box('Sink dark interior',(x,4,.974),(.65,.43,.008),'DarkSteel',.04)
 for sx in (-.34,.34):box('Sink lip',(x+sx,4,.989),(.028,.47,.028),'Stainless')
 faucet('Gooseneck faucet',x,4.27)
for x in (2.1,3.4):
 box('Wall dispenser',(x,4.39,1.55),(.22,.15,.28),'White',.02)
 box('Dispenser window',(x,4.306,1.55),(.1,.005,.15),'Black',.005)
box('Paper dispenser',(4.55,4.33,1.57),(.31,.20,.31),'Black',.025)
box('Whiteboard',(3.30,4.41,2.19),(2.7,.035,.79),'Stainless',.012)
box('Whiteboard surface',(3.30,4.385,2.19),(2.64,.02,.73),'White',.005)
text('Whiteboard text','RESPECT  /  EDUCATION  /  DISCOVERY',(3.3,4.366,2.27),.083)
text('Whiteboard subtitle','THORACIC ANATOMY LABORATORY',(3.3,4.364,2.06),.065)
# Rear mortuary-style teaching storage bank.
box('Storage bank',(-4.67,4.02,1.43),(2,.9,2.54),'Stainless',.025)
for col in range(2):
 for row in range(6):
  x=-5.18+col*1.02;z=.41+row*.38
  box('Storage drawer',(x,3.55,z),(.95,.035,.34),'Stainless',.008)
  cylinder('Storage latch',(x+.30,3.50,z-.055),(x+.30,3.50,z+.055),.012)
  box('Storage label',(x-.24,3.525,z),(.15,.008,.045),'White',.001)
for y in (-2.9,2.5):
 box('Door frame',(-5.95,y,1.16),(.1,1.05,2.32),'DarkSteel',.008)
 box('Oak door',(-5.88,y,1.14),(.06,.94,2.22),'Wood',.008)
 box('Door vision panel',(-5.835,y,1.63),(.016,.20,.53),'Black',.006)
 cylinder('Door lever',(-5.80,y-.29,.99),(-5.80,y-.13,.99),.012)
text('Lab wall title','SCIENCE\nMEDICINE\nHUMANITY',(-5.8,.2,2.2),.15,(math.pi/2,0,math.pi/2))
for x,y in ((-3,.9),(-.5,-1.6),(3,.8)):exam_light('Examination lamp',x,y)
# Wall clock, rendered face is original and editable.
cylinder('Wall clock',(0.55,4.38,2.57),(0.55,4.32,2.57),.17,'Black',40)
cylinder('Clock face',(0.55,4.315,2.57),(0.55,4.308,2.57),.15,'White',40)
for i in range(12):
 a=i*math.tau/12;cylinder('Clock marker',(.55+.126*math.sin(a),4.297,2.57+.126*math.cos(a)),(.55+.141*math.sin(a),4.297,2.57+.141*math.cos(a)),.002,'Black',6)
cylinder('Clock minute hand',(.55,4.29,2.57),(.55+.11,4.29,2.57+.04),.003,'Black',8)
cylinder('Clock hour hand',(.55,4.287,2.57),(.55-.04,4.287,2.57+.072),.004,'Black',8)
box('Floor trench',(1.98,-.3,.003),(.13,6,.006),'DarkSteel',.001)
for i in range(120):box('Drain grille',(1.98,-3.25+i*.05,.009),(.125,.018,.009),'Stainless',.001)
box('Waste bin',(4.5,3.6,.26),(.34,.35,.5),'Red',.03)
box('Waste bin lid',(4.5,3.6,.53),(.38,.38,.035),'White',.016)
# Area lights for the Blender authoring preview. Unity uses its own baked lighting.
for x in (-3,0,3):
 for y in (-2,1,3):
  data=bpy.data.lights.new('Ceiling soft illumination','AREA');data.energy=160;data.shape='RECTANGLE';data.size=1.3;data.size_y=.7
  o=bpy.data.objects.new(data.name,data);bpy.context.collection.objects.link(o);o.location=(x,y,2.94)
data=bpy.data.cameras.new('Lab overview');cam=bpy.data.objects.new('Lab overview',data);bpy.context.collection.objects.link(cam)
cam.location=(5.45,-4.1,2.15);cam.rotation_euler=(Vector((-.5,1.4,1.20))-cam.location).to_track_quat('-Z','Y').to_euler();data.lens=23;scene.camera=cam
glass=M['Glass'].node_tree.nodes.get('Principled BSDF');glass.inputs['Alpha'].default_value=.16;glass.inputs['Transmission Weight'].default_value=.45;glass.inputs['Roughness'].default_value=.12
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=-.55
scene.render.resolution_x=1500;scene.render.resolution_y=950;scene.render.resolution_percentage=100
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Laboratory_MASTER.blend'))
scene.render.filepath=str(ROOT/'qa/renders/laboratory-overview.png');bpy.ops.render.render(write_still=True)
# Bake procedural detail into a portable, modest shared texture for each material.
texture_dir=ROOT/'exports/textures';texture_dir.mkdir(parents=True,exist_ok=True)
for key,mat in M.items():
 img=bpy.data.images.new(key+'_BaseColor',width=256,height=256)
 color=mat.diffuse_color[:3];pixels=[]
 for yy in range(256):
  for xx in range(256):
   variation=(random.random()-.5)*(.035 if key in ('Floor','Cloth','Stainless','Wood') else .008)
   if key=='Stainless':variation+=.009*math.sin(yy*8)
   pixels.extend([max(0,min(1,c+variation)) for c in color]+[1])
 img.pixels.foreach_set(pixels);img.filepath_raw=str(texture_dir/(key+'_BaseColor.png'));img.file_format='PNG';img.save()
# Export material batches while retaining editable objects in the saved master.
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.convert(target='MESH')
export_objects=list(bpy.context.selected_objects)
for main in (True,False):
 for mat in M.values():
  group=[o for o in bpy.data.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat and o.name.startswith('Main teaching table')==main and not o.name.startswith('Lab_')]
  if not group:continue
  bpy.ops.object.select_all(action='DESELECT')
  for o in group:o.select_set(True)
  bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();group[0].name=('Lab_MainTable_' if main else 'Lab_')+mat.name
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
 if o.type=='MESH' and o.name.startswith('Lab_'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports/Laboratory.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='COPY',use_mesh_modifiers=True)
meshes=[o for o in bpy.context.selected_objects if o.type=='MESH']
report={'master':'blender/Laboratory_MASTER.blend','export':'exports/Laboratory.fbx','original_authoring':True,'reference':'user-supplied laboratory photograph','room_metres':[12,9,3],'renderers':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'materials':[m.name for m in M.values()]}
(ROOT/'qa/reports/environment-build.json').write_text(json.dumps(report,indent=2))
print('LABORATORY_BUILD_COMPLETE',json.dumps(report))
