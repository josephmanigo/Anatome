"""Build ANATOME's teaching operating room and its material-batched Unity model."""
import bpy, math, json, runpy, shutil
from pathlib import Path
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
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
'Ceiling':material('Ceiling',(.57,.67,.76),0,.63),
'Glass':material('Glass',(.17,.245,.265),.35,.2),'Black':material('Black',(.018,.022,.024),0,.55),
'White':material('White',(.91,.91,.87),0,.35),'Red':material('Red',(.46,.035,.025),0,.45),
'Light':material('Light',(.88,.91,.88),0,.4),'Sign':material('Sign',(.06,.10,.10),0,.6),
'Guard':material('Guard',(.14,.27,.25),0,.60),'Ceramic':material('Ceramic',(.60,.73,.68),0,.34),
'MonitorAtlas':material('MonitorAtlas',(.05,.10,.12),0,.32),
'VentFace':material('VentFace',(.66,.72,.77),.32,.48),
'InstrumentSteel':material('InstrumentSteel',(.65,.72,.77),.96,.18),
'Cushion':material('Cushion',(.025,.030,.036),0,.46),
'EquipmentBlue':material('EquipmentBlue',(.033,.19,.33),.12,.35),
'CylinderGreen':material('CylinderGreen',(.027,.25,.13),.13,.34),
'Display':material('Display',(.015,.041,.049),.12,.27)}
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
 cu=bpy.data.curves.new(name,'FONT');cu.body=content;cu.size=size;cu.align_x='CENTER'
 # Printed lettering is flat. Extruded glyph edges exhaust Unity's lightmap UV chart packer.
 cu.extrude=0;cu.resolution_u=4
 o=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=rot;cu.materials.append(M[mat]);objects.append(o);return o
def curve(name,points,r=.012,mat='Stainless'):
 cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.bevel_depth=r;cu.bevel_resolution=1 if r<.014 else 2
 cu.resolution_u=2 if mat=='InstrumentSteel' else 4 if r<.014 else 8
 sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
 for p,co in zip(sp.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
 o=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(o);cu.materials.append(M[mat]);objects.append(o);return o

def metric_uv(o):
 if o.get('authored_uvs'):return
 mesh=o.data
 layer=mesh.uv_layers.active or mesh.uv_layers.new(name='Surface metres')
 world=o.matrix_world;normals=world.to_3x3().inverted().transposed()
 points=[world@v.co for v in mesh.vertices]
 for face in mesh.polygons:
  normal=normals@face.normal
  axis=max(range(3),key=lambda n:abs(normal[n]))
  a,b=((1,2),(0,2),(0,1))[axis]
  for loop in face.loop_indices:
   co=points[mesh.loops[loop].vertex_index];layer.data[loop].uv=(co[a],co[b])

def scrub_basin(x):
 # The counter stops at the bowl's perimeter; the basin and strainer are visible from above.
 for side in (-1,1):
  box('Sink side deck',(x+side*.465,4.08,.94),(.25,.76,.065),'Stainless',.007)
 box('Sink front deck',(x,3.74,.94),(.68,.08,.065),'Stainless',.004)
 box('Sink tap deck',(x,4.345,.94),(.68,.23,.065),'Stainless',.004)
 rings=[(.345,.231,.973),(.322,.209,.978),(.303,.193,.962),(.265,.155,.822),(.20,.105,.796),(.032,.032,.787)]
 verts=[];faces=[]
 for w,d,h in rings:
  for i in range(40):
   a=i*math.tau/40;c,s=math.cos(a),math.sin(a)
   power=1 if w<.04 else .32
   verts.append((x+w*math.copysign(abs(c)**power,c),4.005+d*math.copysign(abs(s)**power,s),h))
 for k in range(len(rings)-1):
  for i in range(40):
   a=k*40+i;b=k*40+(i+1)%40;faces.append((a,b,b+40,a+40))
 mesh=bpy.data.meshes.new('Pressed sink bowl');mesh.from_pydata(verts,[],faces);mesh.update()
 o=bpy.data.objects.new('Pressed sink bowl',mesh);bpy.context.collection.objects.link(o);finish(o,o.name,'Stainless')
 for face in mesh.polygons:face.use_smooth=True
 cylinder('Basket strainer',(x,4.005,.781),(x,4.005,.787),.034,'DarkSteel',24)
 for i in range(-2,3):
  half=math.sqrt(.028**2-(i*.009)**2)
  box('Strainer slot',(x+i*.009,4.005,.788),(.003,half*2,.002),'Stainless',0)

def turned(name, origin, axis, profile, mat, sides=36):
    """Turn a measured cross-section about the local axis, including tilted lamp faces."""
    axis = Vector(axis).normalized()
    u = axis.orthogonal().normalized()
    v = axis.cross(u)
    origin = Vector(origin)
    vertices = [origin + axis * h + radius * (u * math.cos(i * math.tau / sides) + v * math.sin(i * math.tau / sides))
                for h, radius in profile for i in range(sides)]
    faces = [tuple(reversed(range(sides)))]
    for row in range(len(profile) - 1):
        for i in range(sides):
            a = row * sides + i
            b = row * sides + (i + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple(range(len(vertices) - sides, len(vertices))))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, name, mat)
    for face in list(mesh.polygons)[1:-1]:
        face.use_smooth = True
    return obj


def operating_table(x, y):
    name = 'Main teaching table '
    box('Operating table mobile base', (x,y,.105), (.64,1.31,.17), 'Stainless', .063)
    box('Operating table impact bumper', (x,y,.060), (.65,1.32,.065), 'DarkSteel', .030)
    for dx in (-.24,.24):
        for dy in (-.48,.48):
            cylinder('Operating table recessed caster', (x+dx-.028,y+dy,.051), (x+dx+.028,y+dy,.051), .050, 'Black')
    box('Operating table fixed lift sleeve', (x,y,.305), (.32,.48,.35), 'Stainless', .025)
    box(name+'telescoping ram', (x,y,.4375), (.272,.426,.505), 'Stainless', .020)
    for i in range(5):
        box(name+'telescoping bellows', (x,y,.585+i*.023), (.35,.50,.016), 'DarkSteel', .007)
    box(name+'tilt carriage', (x,y,.718), (.39,.66,.10), 'Stainless', .028)
    box(name+'radiolucent deck', (x,y,.799), (.84,2.15,.077), 'DarkSteel', .025)
    for dy, length in ((-.944,.268),(-.526,.54),(.09,.66),(.764,.65)):
        box(name+'upholstered section', (x,y+dy,.85), (.858,length,.080), 'Cushion', .032)
        box(name+'section seam', (x,y+dy,.824), (.861,length+.002,.010), 'Black', .004)
    for side in (-1,1):
        xx = x + side*.461
        for dy in (-.83,-.34,.32,.84):
            cylinder(name+'rail standoff',(x+side*.403,y+dy,.789),(xx,y+dy,.789),.023)
        box(name+'accessory rail',(xx,y,.791),(.021,1.96,.039),'Stainless',.008)
        for dy in (-.57,.63):
            box(name+'rail clamp',(xx,y+dy,.791),(.048,.069,.059),'Black',.011)
            cylinder(name+'rail release',(xx,y+dy,.77),(xx,y+dy-.051,.732),.013,'Stainless',12)
    curve(name+'handset cable',[(x+.19,y-.23,.60),(x+.29,y-.46,.31),(x+.37,y-.60,.46),(x+.43,y-.56,.72)],.008,'Black')
    box(name+'pendant',(x+.433,y-.57,.705),(.044,.047,.14),'Cushion',.014)


def surgical_lamp(name, mount, elbow, wrist, head, normal, radius):
    centre = Vector(head)
    outward = Vector(normal).normalized()
    across = (Vector((1,0,0)) - outward * outward.x).normalized()
    along = outward.cross(across)
    turned(name+' ceiling canopy', mount, (0,0,1), [(-.17,.080),(-.11,.17),(-.045,.205),(0,.20)], 'White')
    pivot = (mount[0],mount[1],mount[2]-.30)
    cylinder(name+' down tube',mount,pivot,.058,'White',24)
    for a,b,width in ((pivot,elbow,.069),(elbow,wrist,.050)):
        cylinder(name+' counterbalanced arm',a,b,width,'White',24)
        sphere(name+' articulated joint',b,(width*1.52,width*1.52,width*1.30),'White')
        cylinder(name+' pivot cap',(b[0],b[1]-.90*width,b[2]),(b[0],b[1]-1.28*width,b[2]),width*.56,'DarkSteel',18)
    crown = centre - outward*.112
    curve(name+' lamp yoke',[wrist,tuple((Vector(wrist)+crown)/2+across*.07),tuple(crown)],.036,'White')
    turned(name+' reflector housing',centre,outward,[(-.131,.065),(-.107,.16),(-.066,radius*.79),(-.021,radius),(.012,radius),(.025,radius*.94)],'White',64)
    turned(name+' silver bezel',centre,outward,[(.014,radius*1.012),(.024,radius*1.012),(.033,radius*.94)],'Stainless',64)
    turned(name+' optical field',centre,outward,[(.030,radius*.944),(.037,radius*.926)],'Display',56)
    clusters=4 if radius>.4 else 3
    for index in range(clusters):
        angle=(index+.125)*math.tau/clusters
        p=centre+outward*.036+(across*math.cos(angle)+along*math.sin(angle))*radius*.54
        lens=radius*.325
        turned(name+' reflector cup',p,outward,[(0,lens),(.009,lens),(.014,lens*.93)],'Stainless',28)
        for cell in range(9):
            phase=angle+cell*math.tau/8
            offset=Vector((0,0,0)) if cell==8 else (across*math.cos(phase)+along*math.sin(phase))*lens*.65
            spot=p+outward*.014+offset
            turned(name+' collimator',spot,outward,[(0,lens*.265),(.004,lens*.246)],'White',12)
            turned(name+' LED lens',spot,outward,[(.004,lens*.18),(.007,lens*.173),(.010,lens*.12)],'Light',12)
    turned(name+' sterile centre handle',centre,outward,[(.037,.055),(.060,.055),(.067,.023),(.154,.021),(.163,.027)],'Guard',24)
    for side in (-1,1):
        points=[centre + across*(side*radius*dx) + along*(radius*dy) + outward*.008
                for dx,dy in ((.81,-.42),(1.09,-.30),(1.14,0),(1.09,.30),(.81,.42))]
        curve(name+' external grip',points,.013,'White')


def caster(name,x,y):
    box(name+' swivel fork',(x,y,.119),(.072,.065,.093),'Stainless',.015)
    for offset in (-.024,.024):
        cylinder(name+' twin wheel',(x+offset-.011,y,.066),(x+offset+.011,y,.066),.064,'Black',20)


def anaesthesia_workstation(x,y):
    def body(label,dx,dy,z,w,d,h,mat='White',bevel=.018):
        return box('Anesthesia '+label,(x+dx,y+dy,z),(w,d,h),mat,bevel)
    body('wheelbase',0,0,.155,.82,.66,.095,'DarkSteel',.035)
    for dx in (-.32,.32):
        for dy in (-.24,.24):
            caster('Anesthesia',x+dx,y+dy)
    body('lower cabinet',0,0,.53,.72,.56,.69,bevel=.038)
    for i in range(3):
        z=.32+i*.176
        body('drawer shadow',0,-.283,z,.626,.017,.169,'DarkSteel',.010)
        body('drawer fascia',0,-.300,z,.611,.030,.154,bevel=.014)
        curve('Anesthesia drawer pull',[(x-.135,y-.315,z+.042),(x-.104,y-.351,z+.031),
              (x+.104,y-.351,z+.031),(x+.135,y-.315,z+.042)],.012,'Stainless')
    body('worktop',0,-.038,.896,.835,.74,.055,'White',.025)
    body('edge bumper',0,-.032,.87,.842,.738,.020,'Guard',.009)
    body('upper cabinet',.075,.085,1.18,.55,.35,.53,bevel=.025)
    body('control recess',.08,-.10,1.175,.461,.022,.353,'DarkSteel',.012)
    for i in range(3):
        px=x-.095+i*.078
        box('Flow meter housing',(px,y-.118,1.216),(.042,.020,.191),'White',.007)
        box('Flow meter sight',(px,y-.131,1.230),(.012,.006,.142),'EquipmentBlue',.003)
        cylinder('Flow dial',(px,y-.119,1.068),(px,y-.159,1.068),.025,('White','Guard','EquipmentBlue')[i],20)
    for z in (1.13,1.239):
        cylinder('Gas socket',(x+.235,y-.12,z),(x+.235,y-.15,z),.035,'Stainless',24)
        cylinder('Gas socket insert',(x+.235,y-.149,z),(x+.235,y-.157,z),.019,'Black',16)
    body('monitor pedestal',.055,.08,1.485,.15,.16,.12,'Stainless')
    body('display housing',.055,.073,1.731,.65,.112,.437,'White',.027)
    body('display surround',.055,.012,1.735,.608,.020,.391,'Black',.014)
    body('standby display',.055,-.001,1.747,.551,.009,.320,'Display',.005)
    display_face('Workstation ventilation screen',x+.055,y-.009,1.746,.537,.293,2)
    body('side display',.390,.015,1.393,.213,.074,.193,'DarkSteel',.012)
    display_face('Workstation status screen',x+.390,y-.026,1.393,.183,.137,3)
    for i in range(3):
        body('soft key',.183+i*.036,-.005,1.565,.022,.012,.008,'White',.002)
    cylinder('Breathing system post',(x-.345,y+.04,.91),(x-.345,y+.04,1.38),.031,'Stainless',16)
    body('breathing head',-.342,-.043,1.25,.22,.21,.14,'White',.019)
    turned('Absorber canister',(x-.344,y-.045,.96),(0,0,1),[(0,.068),(.018,.089),(.195,.089),(.213,.079)],'Ceramic',32)
    for z in (.978,1.164):
        cylinder('Canister collar',(x-.344,y-.045,z),(x-.344,y-.045,z+.022),.095,'White',28)
    for offset in (0,.061):
        curve('Breathing circuit',[(x-.42+offset,y-.12,1.26),(x-.50+offset,y-.19,1.10),
              (x-.54+offset,y-.24,.68),(x-.45+offset,y-.29,.57),
              (x-.34+offset,y-.32,.81),(x-.33+offset,y-.34,.92)],.018,'Cloth')
    sphere('Reservoir bag',(x-.405,y-.242,.849),(.059,.055,.125),'CylinderGreen')


def gas_cylinder(x,y,mat):
    for dx in (-.125,.125):
        cylinder('Cylinder trolley wheel',(x+dx-.021,y+.06,.088),(x+dx+.021,y+.06,.088),.083,'Black',18)
    box('Cylinder trolley foot',(x,y,.088),(.31,.31,.039),'DarkSteel',.008)
    curve('Cylinder trolley handle',[(x-.12,y+.12,.14),(x-.12,y+.12,1.21),
          (x,y+.13,1.29),(x+.12,y+.12,1.21),(x+.12,y+.12,.14)],.015,mat)
    turned('Cylinder enamel bottle',(x,y,.114),(0,0,1),[(0,.093),(.028,.122),(.11,.128),
           (.87,.128),(.925,.116),(.968,.077),(.989,.038)],mat,32)
    cylinder('Cylinder valve neck',(x,y,1.10),(x,y,1.179),.034,'Stainless',18)
    cylinder('Cylinder handwheel',(x,y,1.175),(x,y,1.201),.051,mat,18)
    cylinder('Pressure gauge case',(x+.057,y,1.15),(x+.057,y-.036,1.15),.043,'DarkSteel',24)
    cylinder('Pressure gauge dial',(x+.057,y-.035,1.15),(x+.057,y-.040,1.15),.036,'White',24)
    cylinder('Pressure gauge needle',(x+.057,y-.041,1.15),(x+.075,y-.041,1.17),.0025,'Sign',8)
    curve('Cylinder restraint',[(x-.12,y+.09,.72),(x-.108,y-.072,.72),
          (x,y-.124,.72),(x+.108,y-.072,.72),(x+.12,y+.09,.72)],.009,'DarkSteel')


def mayo_stand(x,y):
    for dx in (-.23,.23):
        box('Mayo U base',(x+dx,y,.079),(.041,.54,.046),'Stainless',.011)
        for dy in (-.226,.226):
            cylinder('Mayo caster',(x+dx-.022,y+dy,.045),(x+dx+.022,y+dy,.045),.044,'Black',14)
    box('Mayo cross brace',(x,y+.238,.079),(.47,.039,.046),'Stainless',.008)
    for dx in (-.195,.195):
        cylinder('Mayo outer post',(x+dx,y+.205,.095),(x+dx,y+.205,.562),.020)
        cylinder('Mayo adjustable post',(x+dx,y+.205,.55),(x+dx,y+.205,.915),.014)
    box('Mayo tray',(x,y,.927),(.73,.50,.033),'Stainless',.011)
    for dx in (-.356,.356):
        box('Mayo raised lip',(x+dx,y,.952),(.017,.50,.039),'Stainless',.007)
    for dy in (-.241,.241):
        box('Mayo raised lip',(x,y+dy,.952),(.71,.017,.039),'Stainless',.007)
    box('Folded sterile field',(x+.09,y,.954),(.37,.35,.018),'Blue',.008)


def attire_trolley(x,y):
    for z in (.247,.858):
        box('Dressing trolley shelf',(x,y,z),(.63,1.03,.025),'Stainless',.009)
    for dx in (-.29,.29):
        for dy in (-.49,.49):
            cylinder('Dressing trolley leg',(x+dx,y+dy,.10),(x+dx,y+dy,.938),.015)
            cylinder('Dressing trolley caster',(x+dx-.020,y+dy,.062),(x+dx+.020,y+dy,.062),.060,'Black',18)
    for dy in (-.49,.49):
        curve('Dressing trolley guard',[(x-.29,y+dy,.89),(x-.27,y+dy,.953),(x+.27,y+dy,.953),(x+.29,y+dy,.89)],.011)
    for i in range(3):
        box('Sterile linen pack',(x-.06,y+.23,.272+i*.039),(.37,.34,.035),'Cloth',.012)
    for i in range(2):
        box('Folded sterile wrap',(x+.05,y-.04,.882+i*.018),(.45,.52,.016),'Blue',.006)


def theatre_doors(x):
    y=4.395
    for side in (-1,1):
        first=len(objects); dx=x+side*.516; wall=y-.063
        box('Scrub door lower panel',(dx,wall,.664),(1.014,.068,1.32),'White',.009)
        box('Scrub door top panel',(dx,wall,2.067),(1.014,.068,.254),'White',.009)
        for edge in (-1,1):
            box('Scrub door vision stile',(dx+edge*.361,wall,1.63),(.292,.068,.620),'White',.005)
            box('Door vision gasket side',(dx+edge*.205,wall,1.63),(.022,.069,.620),'DarkSteel',.004)
            box('Door vision gasket rail',(dx,wall,1.63+edge*.30),(.427,.069,.025),'DarkSteel',.004)
        box('Scrub door vision glass',(dx,wall,1.63),(.405,.012,.58),'Glass',.003)
        for face in (-1,1):
            box('Door protection plate',(dx,wall+face*.039,.976),(.998,.012,.203),'Stainless',.003)
            box('Door kick plate',(dx,wall+face*.039,.148),(.998,.012,.264),'Stainless',.003)
        for piece in objects[first:]: piece['room_batch']='Lab_ScrubSouth_' if side<0 else 'Lab_ScrubNorth_'
    for side in (-1,1):
        box('Theatre jamb',(x+side*1.081,y-.050,1.12),(.066,.132,2.25),'Stainless',.007)
    box('Theatre header',(x,y-.050,2.245),(2.225,.132,.081),'Stainless',.006)
    box('Scrub entrance signage',(x,y-.070,2.405),(1.71,.025,.174),'Cloth',.008)
    text('Theatre door sign','SCRUB LOBBY',(x,y-.085,2.360),.115,mat='Sign')
    box('Automatic door operator',(x,y-.019,2.251),(3.78,.097,.118),'Stainless',.012)
def faucet(name,x,y):
 curve(name,[(x,y,.95),(x,y,1.22),(x,y-.07,1.36),(x,y-.22,1.34),(x,y-.24,1.21)],.014)
 cylinder(name+'_valve',(x+.10,y,.95),(x+.1,y,1.01),.022)
def display_face(name,x,y,z,width,height,pane):
    """One emissive atlas serves all monitors. The front of a cart faces -Y."""
    verts=[(x-width/2,y,z-height/2),(x+width/2,y,z-height/2),
           (x+width/2,y,z+height/2),(x-width/2,y,z+height/2)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],[(0,1,2,3)]);mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    left=(pane%2)*.5+.003; bottom=(1-pane//2)*.5+.006
    for loop,co in zip(uv.data,((left,bottom),(left+.494,bottom),(left+.494,bottom+.488),(left,bottom+.488))):
        loop.uv=co
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o['authored_uvs']=True
    finish(o,name,'MonitorAtlas')


def sterile_field(name,x,y,hw,hl,height,drop,aperture=None):
    """A cut field follows the patient table. The instrument table has a closed, draped top."""
    outline=[]
    corners=[(-1,-1),(1,-1),(1,1),(-1,1)]
    for side,(u0,v0) in enumerate(corners):
        u1,v1=corners[(side+1)%4]
        count=32 if v0==v1 else 48
        for i in range(count):
            t=i/count
            outline.append((u0+(u1-u0)*t, v0+(v1-v0)*t))
    vertices=[];uvcoords=[];faces=[]
    total=len(outline)
    for band in range(13):
        for i,(u,v) in enumerate(outline):
            angle=math.atan2(v,u)
            normal=Vector((math.copysign(max(0,(abs(u)-.78)/.22)**1.4,u),math.copysign(max(0,(abs(v)-.78)/.22)**1.4,v))).normalized()
            corner=abs(u*v)**3
            perimeter=i/total*math.tau
            if band<=4:
                t=band/4
                ax,ay=aperture if aperture else (.018,.018)
                xx=(1-t)*ax*math.cos(angle)+t*hw*u; yy=(1-t)*ay*math.sin(angle)+t*hl*v
                zz=height+t**2*(.004+.0035*math.sin(8*perimeter+1.1)+.002*math.sin(13*perimeter))
                uu,vv=xx,yy
            else:
                t=.025+(band-5)*.975/7
                pleat=math.sin(9*perimeter+.7*math.sin(3*perimeter)+1.25*t)+.36*math.sin(16*perimeter-1.7*t)
                # Extra corner fabric pulls diagonally from the hem into the table edge.
                distance=(1-abs(v))*hl+(1-abs(u))*hw
                ridge=math.exp(-((distance-.20*t)/(.025+.026*t))**2)
                valley=math.exp(-((distance-.086*t)/(.020+.018*t))**2)
                spread=t*(.018+.023*pleat+.084*ridge-.048*valley)
                xx=hw*u+normal.x*spread; yy=hl*v+normal.y*spread
                length=drop*(.76+.39*corner+.039*math.cos(4*perimeter+1.1))
                zz=height-t*length+.006*math.sin(8*perimeter)*(1-t)
                uu=hw*u+normal.x*t*length; vv=hl*v+normal.y*t*length
            vertices.append((x+xx,y+yy,zz));uvcoords.append((uu,vv))
        if band:
            for i in range(total):
                a=(band-1)*total+i; b=(band-1)*total+(i+1)%total
                faces.append((a,a+total,b+total,b))
    if not aperture: faces.append(tuple(range(total)))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops: uv.data[loop.index].uv=uvcoords[loop.vertex_index]
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o['authored_uvs']=True;finish(o,name,'Blue')
    for face in mesh.polygons: face.use_smooth=True


def instrument_table(x,y):
    hw,hl=.49,.78
    for dx in (-.40,.40):
        for dy in (-.67,.67):
            cylinder('Sterile table tubular leg',(x+dx,y+dy,.12),(x+dx,y+dy,.95),.022)
            caster('Sterile table',x+dx,y+dy)
    box('Sterile table frame',(x,y,.928),(.96,1.54,.055),'Stainless',.015)
    box('Sterile table lower brace',(x,y,.23),(.78,1.32,.020),'Stainless',.007)
    top=.963
    sterile_field('Sterile instrument field',x,y,hw,hl,top,.43)
    def rod(label,a,b,r=.0033):
        return cylinder(label,(x+a[0],y+a[1],top+a[2]),(x+b[0],y+b[1],top+b[2]),r,'InstrumentSteel',8)
    for i in range(8):
        xx=-.371+i*.087; yy=-.37 + (i%3)*.016
        for side in (-1,1):
            loop=[(x+xx+side*.015+.012*math.cos(a*math.tau/12),y+yy+.021*math.sin(a*math.tau/12),top+.013) for a in range(13)]
            curve('Clamp finger ring',loop,.0026,'InstrumentSteel')
            rod('Clamp shank',(xx+side*.012,yy+.013,.013),(xx-side*.002,yy+.106,.011))
            rod('Clamp jaw',(xx-side*.002,yy+.106,.011),(xx+side*.003,yy+.176,.015),.0024)
        turned('Clamp joint',(x+xx,y+yy+.105,top+.012),(0,0,1),[(0,.005),(.004,.005)],'InstrumentSteel',10)
    for i in range(6):
        xx=-.38+i*.083; yy=.02+i%2*.018
        for side in (-1,1):
            curve('Dressing forceps',[(x+xx,y+yy+.177,top+.012),(x+xx+side*.009,y+yy+.133,top+.020),
                  (x+xx+side*.008,y+yy+.042,top+.016),(x+xx+side*.002,y+yy,top+.011)],.0035,'InstrumentSteel')
    for xx,yy,rad in ((.265,.31,.142),(.277,-.01,.092)):
        turned('Spun instrument bowl',(x+xx,y+yy,top+.009),(0,0,1),
               [(0,rad*.49),(.011,rad*.60),(.073,rad*.985),(.080,rad),(.083,rad*.993),
                (.079,rad*.955),(.069,rad*.947),(.013,rad*.53),(.010,rad*.47)],'InstrumentSteel',48)
    for i in range(3):
        box('Folded sterile towel',(x-.19,y+.36,top+.019+i*.019),(.39,.27,.017),'Blue',.006)


def observation_window(x):
    y=3.74
    # Two optically clear fixed panes reveal the observation room across the theatre wall.
    for z in (.89,2.20):
        box('Observation window head or sill',(x,y,z),(2.87,.132,.065),'Stainless',.009)
        box('Glazing channel',(x,y-.045,z),(2.81,.025,.030),'DarkSteel',.003)
    for dx in (-1.39,0,1.39):
        box('Observation mullion',(x+dx,y,1.55),(.043,.098,1.35),'Stainless',.006)
    for dx in (-.698,.698):
        box('Internal observation glazing',(x+dx,y+.016,1.55),(1.343,.013,1.273),'Glass',.002)
    box('Flush observation sill',(x,y-.021,.86),(2.90,.157,.038),'Stainless',.005)
    box('Observation window label',(x,y-.054,2.353),(1.69,.021,.151),'Cloth',.006)
    text('Observation window lettering','OBSERVATION',(x,y-.067,2.309),.109,mat='Sign')


def equipment_tower(x,y):
    for dx in (-.24,.24):
        for dy in (-.22,.22): caster('Equipment tower',x+dx,y+dy)
    box('Endoscopy trolley base',(x,y,.16),(.65,.58,.084),'DarkSteel',.025)
    for dx in (-.281,.281):
        box('Tower extrusion',(x+dx,y+.19,.81),(.058,.077,1.29),'Stainless',.011)
    for i in range(3):
        z=.34+i*.244
        box('Rack shelf',(x,y,z),(.66,.53,.033),'White',.007)
        box('Video processor module',(x,y,z+.11),(.57,.43,.165),'White',.013)
        box('Recessed module fascia',(x,y-.222,z+.115),(.51,.018,.093),'DarkSteel',.006)
        for j in range(5):
            box('Vent slot',(x-.157+j*.053,y-.234,z+.123),(.033,.007,.010),'Black',.002)
        cylinder('Module encoder',(x+.141,y-.229,z+.115),(x+.141,y-.25,z+.115),.022,'Stainless',14)
    cylinder('Tower monitor pole',(x,y+.15,1.0),(x,y+.15,2.23),.030)
    for k,(z,w,h) in enumerate(((1.38,.75,.458),(1.986,.86,.511))):
        box('Clinical display shell',(x,y+.04,z),(w,.094,h),'DarkSteel',.016)
        box('Clinical display bezel',(x,y-.01,z),(w-.025,.021,h-.024),'Black',.010)
        display_face('Teaching atlas screen' if k==0 else 'Vital signs screen',x,y-.023,z,w-.069,h-.072,1 if k==0 else 0)
    for i in range(3):
        points=[(x-.36+.106*math.sin(j*math.tau/24), y-.05-i*.016, .94+.199*math.cos(j*math.tau/24)) for j in range(25)]
        curve('Coiled monitoring cable',points,.006,'Cloth')


def supply_cart(x,y):
    for dx in (-.284,.284):
        for dy in (-.221,.221): caster('Supply cart',x+dx,y+dy)
    box('Supply cart bumper',(x,y,.16),(.73,.59,.085),'EquipmentBlue',.024)
    box('Mobile drawer body',(x,y,.658),(.67,.53,.94),'White',.023)
    for i in range(5):
        z=.32+i*.167
        box('Drawer gasket',(x,y-.274,z),(.605,.018,.153),'DarkSteel',.005)
        box('Supply drawer front',(x,y-.288,z),(.594,.027,.142),'White',.006)
        box('Recessed pull',(x,y-.308,z+.036),(.210,.015,.020),'Stainless',.005)
    box('Cart worktop',(x,y,1.156),(.75,.59,.050),'Stainless',.011)
    for dx in (-.27,.27):
        box('Supply rack upright',(x+dx,y+.196,1.46),(.024,.033,.60),'Stainless',.003)
    for z in (1.40,1.71):
        box('Supply rack shelf',(x,y+.085,z),(.61,.25,.024),'DarkSteel',.004)
        for dx in (-.153,.135):
            box('Sterile carton',(x+dx,y+.06,z+.069),(.225,.152,.113),'Cloth',.006)


def infusion_bag(x,y,z):
    outline=[(-.024,.112),(.024,.112),(.056,.095),(.071,.075),(.067,-.069),
             (.053,-.09),(.022,-.105),(-.033,-.105),(-.058,-.091),(-.069,-.070),(-.071,.074),(-.053,.095)]
    verts=[(x+u*scale,y+depth,z+v*scale) for depth,scale in ((-.030,.77),(-.003,1),(.003,1),(.030,.77)) for u,v in outline]
    n=len(outline); faces=[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
    for ring in range(3):
        for j in range(n):
            a=ring*n+j; b=ring*n+(j+1)%n
            faces.append((a,b,b+n,a+n))
    mesh=bpy.data.meshes.new('Heat-sealed infusion bag');mesh.from_pydata(verts,[],faces);mesh.update()
    bag=bpy.data.objects.new(mesh.name,mesh);bpy.context.collection.objects.link(bag);finish(bag,mesh.name,'Glass')
    for face in list(mesh.polygons)[2:]: face.use_smooth=True
    box('Fluid meniscus',(x,y,z+.043),(.119,.045,.004),'Glass',.001)
    box('Bag label',(x-.012,y-.031,z-.016),(.070,.002,.056),'Cloth',.001)
    for row in range(4):
        box('Fluid graduations',(x+.039,y-.031,z-.041+row*.023),(.014 if row%2 else .022,.002,.0025),'EquipmentBlue',0)
    curve('Bag loop',[(x-.023,y,z+.104),(x,y,z+.147),(x+.023,y,z+.104)],.004,'Cloth')
    cylinder('Infusion port',(x-.026,y,z-.143),(x-.026,y,z-.096),.008,'White',12)
    curve('Infusion tubing',[(x-.026,y,z-.140),(x-.044,y-.035,z-.39),
          (x+.049,y-.036,z-.73),(x+.124,y-.076,z-.94)],.003,'Cloth')

# The training rig and specimen use this room footprint and table datum.
box('Floor',(0,0,-.1),(12,9,.2),'Floor',0)
for y in (-4.55,4.55):
    box('Glazed wall panels',(0,y,1.50),(12,.16,3),'Wall',0)
    box('Resin cove',(0,y-math.copysign(.091,y),.055),(12,.024,.11),'Floor',.006)
for x in (-6.05,6.05):
    if x>0:
        box('Glazed wall panels',(x,0,1.50),(.16,9,3),'Wall',0)
    else:
        # Clear wall openings for the automatic scrub-lobby doors and fixed observation glazing.
        spans=((-4.5,-3.58),(-1.46,-.85),(1.97,4.50))
        for lo,hi in spans:
            box('Glazed theatre wall',(x,(lo+hi)/2,1.50),(.16,hi-lo,3),'Wall',0)
        box('Scrub entrance lintel',(x,-2.52,2.63),(.16,2.12,.74),'Wall',0)
        for bottom,top in ((0,.875),(2.218,3)):
            box('Tiled observation head or sill',(x,.56,(bottom+top)/2),(.16,2.82,top-bottom),'Wall',0)
    for lo,hi in (((-4.5,4.5),) if x>0 else ((-4.5,-3.62),(-1.42,4.5))):
        box('Resin cove',(x-math.copysign(.091,x),(lo+hi)/2,.055),(.024,hi-lo,.11),'Floor',.006)
        # The glazing sill and open door both interrupt the bumper.
        rails=((lo,hi),) if x>0 or hi<0 else ((lo,-.90),(2.03,hi))
        for rlo,rhi in rails:
            box('Equipment protection rail',(x-math.copysign(.13,x),(rlo+rhi)/2,.91),(.05,rhi-rlo,.080),'Guard',.010)
box('Sealed modular ceiling',(0,0,3.03),(12,9,.035),'Ceiling',0)
for x in (-4.5,-1.65,1.65,4.5):
    for y in (-3,0,3):
        box('Recessed luminaire bezel',(x,y,3.002),(.38,1.63,.024),'DarkSteel',.006)
        box('Light panel',(x,y,2.985),(.31,1.55,.018),'Light',.004)
runpy.run_path(str(ROOT/'pipeline/operating_room_suite.py'),init_globals=globals(),run_name='__main__')

operating_table(0,-1.35)
sterile_field('Main teaching table fenestrated surgical field',0,-1.35,.487,1.115,.897,.49,(.31,.435))
surgical_lamp('Main surgical light',(-1.79,-.14,3.012),(-1.15,-.56,2.74),(-.88,-.78,2.57),
              (-.52,-.95,2.36),(.10,-.57,-.82),.427)
surgical_lamp('Satellite surgical light',(.93,.38,3.012),(1.06,-.30,2.75),(.89,-.47,2.55),
              (.52,-.57,2.32),(-.22,-.60,-.77),.362)
anaesthesia_workstation(.69,.78)
gas_cylinder(1.76,2.13,'EquipmentBlue')
gas_cylinder(2.06,2.23,'CylinderGreen')
mayo_stand(-2.61,-.06)
instrument_table(2.34,-1.96)
equipment_tower(-2.01,1.69)
supply_cart(2.52,1.12)
attire_trolley(3.48,1.60)
window_start=len(objects)
observation_window(0)
bpy.context.view_layer.update()
window_placement=Matrix.Translation((-5.95,.56,0)) @ Matrix.Rotation(math.pi/2,4,'Z') @ Matrix.Translation((0,-3.74,0))
for part in objects[window_start:]:
    part.matrix_world=window_placement @ part.matrix_world
# Stainless staging shelves behind the monitor column carry wrapped sets and cassettes.
for sx in (-3.99,-2.26):
    for sy in (3.71,4.16):
        box('Prep rack leg',(sx,sy,.48),(.036,.036,.94),'DarkSteel',.005)
for shelf in (.24,.92,1.49):
    box('Prep rack shelf',(-3.125,3.94,shelf),(1.84,.53,.038),'Stainless',.008)
for sx in (-3.99,-2.26):
    box('Riser for sterile shelf',(sx,4.16,1.23),(.033,.033,.64),'Stainless',.003)
for row,z in enumerate((.27,.95,1.52)):
    for j in range(3):
        xx=-3.7+j*.56
        box('Wrapped sterile pack',(xx,3.94,z+.052),(.43,.32,.103),'Blue' if row==1 else 'Cloth',.020)
        box('Pack seal label',(xx,3.77,z+.05),(.085,.006,.028),'Cloth' if row==1 else 'EquipmentBlue',.003)
door_start=len(objects)
theatre_doors(0)
bpy.context.view_layer.update()
# Keep the entrance from the walkthrough, with the pass-through in the reference photo's sight line.
door_placement=Matrix.Translation((-5.93,-2.52,0)) @ Matrix.Rotation(math.pi/2,4,'Z') @ Matrix.Translation((0,-4.46,0))
for part in objects[door_start:]:
    part.matrix_world=door_placement @ part.matrix_world

# Compact mobile suction unit against the head wall.
sx,sy=-1.53,1.05
box('Suction cart base',(sx,sy,.141),(.51,.47,.080),'White',.025)
for dx in (-.193,.193):
    for dy in (-.156,.156):
        caster('Suction',sx+dx,sy+dy)
box('Suction equipment column',(sx,sy+.14,.58),(.38,.11,.83),'White',.016)
box('Suction shelf',(sx,sy,.274),(.46,.39,.035),'White',.011)
for dx in (-.103,.103):
    turned('Collection jar',(sx+dx,sy-.04,.30),(0,0,1),[(0,.061),(.016,.080),(.185,.085),(.195,.074)],'Glass',24)
    cylinder('Collection lid',(sx+dx,sy-.04,.491),(sx+dx,sy-.04,.519),.086,'White',24)
    curve('Suction hose',[(sx+dx,sy-.04,.52),(sx+dx,sy-.10,.64),(sx+dx*.67,sy-.04,.90)],.007,'Ceramic')
box('Suction control cabinet',(sx,sy,1.03),(.49,.37,.22),'White',.027)
box('Suction control fascia',(sx,sy-.19,1.038),(.411,.015,.132),'EquipmentBlue',.008)
for dx in (-.107,.045):
    cylinder('Vacuum gauge bezel',(sx+dx,sy-.20,1.044),(sx+dx,sy-.216,1.044),.047,'Stainless',24)
    cylinder('Vacuum gauge face',(sx+dx,sy-.215,1.044),(sx+dx,sy-.221,1.044),.040,'White',24)
    cylinder('Vacuum gauge pointer',(sx+dx,sy-.223,1.044),(sx+dx+.018,sy-.223,1.070),.0026,'Sign',8)

# A narrow IV stand and a round procedure stool leave the approaches to the patient clear.
ix,iy=-.46,.72
for i in range(5):
    a=i*math.tau/5
    tip=(ix+.235*math.cos(a),iy+.235*math.sin(a),.062)
    cylinder('Infusion stand foot',(ix,iy,.13),tip,.016,'Stainless',12)
    sphere('Infusion stand wheel',tip,(.034,.028,.034),'Black')
cylinder('Infusion stand lower pole',(ix,iy,.105),(ix,iy,1.05),.018)
cylinder('Infusion stand upper pole',(ix,iy,1.02),(ix,iy,1.89),.011)
for side in (-1,1):
    curve('Infusion hook',[(ix,iy,1.85),(ix+side*.12,iy,1.91),(ix+side*.16,iy,1.89),(ix+side*.15,iy,1.85)],.006)
for side in (-1,1):
    infusion_bag(ix+side*.147,iy,1.733)
ix2,iy2=2.49,-.37
cylinder('IV cart upright',(ix2,iy2,.13),(ix2,iy2,2.015),.017)
for i in range(4):
    a=i*math.pi/2
    cylinder('IV cart foot',(ix2,iy2,.13),(ix2+.22*math.cos(a),iy2+.22*math.sin(a),.045),.017)
for side in (-1,1):
    curve('IV crossbar',[(ix2,iy2,1.98),(ix2+side*.16,iy2,2.02),(ix2+side*.17,iy2,1.974)],.007)
    infusion_bag(ix2+side*.16,iy2,1.84)
sx,sy=-3.08,.62
turned('Procedure stool upholstered seat',(sx,sy,.550),(0,0,1),[(-.059,.149),(-.047,.192),(-.016,.20),(.001,.18),(.007,.12)],'Cushion',40)
cylinder('Procedure stool post',(sx,sy,.12),(sx,sy,.51),.030)
for i in range(5):
    a=i*math.tau/5
    tip=(sx+.256*math.cos(a),sy+.256*math.sin(a),.070)
    cylinder('Procedure stool foot',(sx,sy,.132),tip,.020,'Stainless',12)
    sphere('Procedure stool caster',tip,(.044,.033,.044),'Black')

# The scrub lobby is outside the theatre. This wall remains available for diagnostic equipment.
runpy.run_path(str(ROOT/'pipeline/operating_room_devices.py'),init_globals=globals(),run_name='__main__')
# Glazed supply cabinet has a real interior behind the glass.
gx=1.14
box('Supply cabinet backing',(gx,4.391,1.63),(1.34,.054,1.12),'White',.009)
for dx in (-.66,.66):
    box('Supply cabinet side',(gx+dx,4.26,1.63),(.033,.31,1.12),'White',.008)
for z in (1.08,1.42,1.78,2.18):
    box('Supply cabinet shelf',(gx,4.26,z),(1.32,.31,.027),'White',.005)
for dx in (-.326,.326):
    box('Glazed supply door',(gx+dx,4.087,1.63),(.626,.016,1.04),'Glass',.005)
    for edge in (-.303,.303):
        box('Supply cabinet frame',(gx+dx+edge,4.077,1.63),(.024,.024,1.06),'Stainless',.003)
for z in (1.12,1.46,1.82):
    for dx in (-.42,0,.42):
        for i in range(2):
            box('Folded theatre linen',(gx+dx,4.23,z+i*.043),(.31,.20,.037),'Cloth' if i else 'Blue',.009)
box('Medical service rail',(-1.06,4.411,1.31),(1.37,.083,.195),'White',.014)
for i,mat in enumerate(('EquipmentBlue','CylinderGreen','White','DarkSteel')):
    px=-1.49+i*.232
    box('Service outlet face',(px,4.361,1.31),(.158,.022,.133),mat,.009)
    cylinder('Outlet collar',(px,4.352,1.31),(px,4.334,1.31),.044,'Stainless',20)
    cylinder('Outlet recess',(px,4.335,1.31),(px,4.329,1.31),.027,'Display',16)
# A live Unity display is set just in front of this sealed dual-clock face.
box('Digital theatre timer case',(-.82,4.402,2.34),(1.31,.085,.448),'Stainless',.018)
box('Digital theatre timer bezel',(-.82,4.357,2.34),(1.265,.031,.417),'Black',.010)
box('Digital display smoked face',(-.82,4.346,2.34),(1.22,.012,.387),'Display',.006)
# The editable master has a representative display. Unity supplies live segments on the same face.
for line,z in (('TIME       14:28:36',2.367),('ELAPSED    00:00:00',2.210)):
    font=bpy.data.curves.new('Clock authoring preview','FONT');font.body=line;font.size=.111;font.align_x='CENTER'
    preview=bpy.data.objects.new('Clock authoring preview',font);bpy.context.collection.objects.link(preview)
    preview.location=(-.82,4.335,z);preview.rotation_euler=(math.pi/2,0,0);font.materials.append(M['Light'])
box('Waste pedal bin',(-2.03,3.94,.252),(.30,.32,.46),'White',.027)
box('Waste bin lid',(-2.03,3.94,.492),(.324,.345,.049),'Red',.018)
# Side-wall fittings seen during the theatre walkthrough.
board_x, board_y = 5.91, -2.02
box('Case board frame',(board_x,board_y,1.69),(.051,1.53,.84),'EquipmentBlue',.017)
box('Dry erase board',(board_x-.029,board_y,1.69),(.014,1.49,.80),'White',.008)
letter_rotation = (math.pi/2,0,-math.pi/2)
text('Case board title','OPERATING LIST',(board_x-.040,board_y,1.92),.079,letter_rotation,'Sign')
for dy, title in ((.47,'CASE'),(0,'TEAM'),(-.47,'TIME')):
    text('Case board column',title,(board_x-.040,board_y+dy,1.79),.048,letter_rotation,'Sign')
for z in (1.747,1.625,1.501):
    box('Board ruling',(board_x-.040,board_y,z),(.003,1.39,.006),'EquipmentBlue',0)
for dy in (-.244,.244):
    box('Board column rule',(board_x-.040,board_y+dy,1.61),(.003,.005,.267),'EquipmentBlue',0)
box('Marker tray',(board_x-.050,board_y,1.272),(.096,.93,.018),'Stainless',.004)
for dy,mat in ((-.14,'EquipmentBlue'),(.06,'Black')):
    cylinder('Dry erase marker',(board_x-.060,board_y+dy-.060,1.291),(board_x-.060,board_y+dy+.060,1.291),.009,'White',12)
    cylinder('Marker cap',(board_x-.060,board_y+dy+.046,1.291),(board_x-.060,board_y+dy+.073,1.291),.010,mat,12)
# The surgical downflow plenum and perimeter return grilles provide theatre air conditioning.
# Area lights for the Blender authoring preview. Unity uses its own baked lighting.
for x in (-4.5,-1.65,1.65,4.5):
 for y in (-3,0,3):
  data=bpy.data.lights.new('Ceiling soft illumination','AREA');data.energy=103;data.color=(.84,.93,1);data.shape='RECTANGLE';data.size=.31;data.size_y=1.55
  o=bpy.data.objects.new(data.name,data);bpy.context.collection.objects.link(o);o.location=(x,y,2.94)
data=bpy.data.cameras.new('Lab overview');cam=bpy.data.objects.new('Lab overview',data);bpy.context.collection.objects.link(cam)
cam.location=(2.42,-4.13,1.65);cam.rotation_euler=(Vector((-.36,.58,1.18))-cam.location).to_track_quat('-Z','Y').to_euler();data.lens=17;scene.camera=cam
glass=M['Glass'].node_tree.nodes.get('Principled BSDF');glass.inputs['Alpha'].default_value=.16;glass.inputs['Transmission Weight'].default_value=.45;glass.inputs['Roughness'].default_value=.12
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=-.55
scene.render.resolution_x=1500;scene.render.resolution_y=950;scene.render.resolution_percentage=100
asset_dir=ROOT/'unity/ThoraxLab/Assets/ThoraxLab'
surface_maps={'Floor':('Laboratory/Theatre resin',.65),'Stainless':('Laboratory/Brushed stainless',2),
 'InstrumentSteel':('Laboratory/Brushed stainless',3),'DarkSteel':('Laboratory/Brushed stainless',2),'Blue':('Laboratory/Surgical nonwoven',1.45),
 'Cloth':('Laboratory/Woven linen',4),'Wall':('Laboratory/Theatre ceramic panels',1/1.2),'Ceiling':('Laboratory/Sealed ceiling tile',1/.6),
 'Guard':('Laboratory/Washable coating',1.4),'Ceramic':('Laboratory/Glazed scrub tile',1/.6),
 'VentFace':('Laboratory/Perforated diffuser',2)}
for finish in ('White','Black','Guard','EquipmentBlue','CylinderGreen','Red'):
 surface_maps[finish]=('Laboratory/Medical equipment coating',2.4)
surface_normals={'Floor':('Theatre resin normal',.25),'Wall':('Theatre ceramic normal',.68),'Stainless':('Brushed alloy normal',.29),
 'InstrumentSteel':('Brushed alloy normal',.18),'DarkSteel':('Brushed alloy normal',.27),'Blue':('Surgical textile normal',.51),
 'Ceramic':('Glazed scrub tile normal',.50),'Cloth':('Woven linen normal',.40),'Ceiling':('Sealed ceiling tile normal',.22)}
resin=M['Floor'].node_tree.nodes.get('Principled BSDF')
resin.inputs['Coat Weight'].default_value=.85;resin.inputs['Coat Roughness'].default_value=.065
for finish in ('White','Black','Guard','EquipmentBlue','CylinderGreen','Red'):
 surface_normals[finish]=('Medical equipment coating normal',.23)
screen_tree=M['MonitorAtlas'].node_tree
screen_map=screen_tree.nodes.new('ShaderNodeTexImage');screen_map.image=bpy.data.images.load(str(asset_dir/'Laboratory/Theatre monitors.png'))
screen_emit=screen_tree.nodes.new('ShaderNodeEmission');screen_emit.inputs['Strength'].default_value=.92
screen_tree.links.new(screen_map.outputs['Color'],screen_emit.inputs['Color'])
screen_tree.links.new(screen_emit.outputs['Emission'],screen_tree.nodes.get('Material Output').inputs['Surface'])
for key,(filename,repeat) in surface_maps.items():
 mat=M[key];tree=mat.node_tree;bs=tree.nodes.get('Principled BSDF')
 tex=tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(asset_dir/(filename+'.png')),check_existing=True)
 tex.image.alpha_mode='CHANNEL_PACKED'
 scale=tree.nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=repeat
 tree.links.new(tree.nodes.new('ShaderNodeTexCoord').outputs['UV'],scale.inputs[0]);tree.links.new(scale.outputs['Vector'],tex.inputs['Vector'])
 tint={'DarkSteel':(.13,.17,.20),'Guard':(.28,.37,.43),'Ceiling':(.72,.81,.88)}.get(key,tuple(mat.diffuse_color[:3]) if key in ('White','Black','EquipmentBlue','CylinderGreen','Red') else (1,1,1))
 mix=tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*tint,1)
 tree.links.new(tex.outputs['Color'],mix.inputs[1]);tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
 rough=tree.nodes.new('ShaderNodeMath');rough.operation='SUBTRACT';rough.inputs[0].default_value=1
 tree.links.new(tex.outputs['Alpha'],rough.inputs[1]);tree.links.new(rough.outputs[0],bs.inputs['Roughness'])
 if key in surface_normals:
  filename,strength=surface_normals[key];normal_tex=tree.nodes.new('ShaderNodeTexImage')
  normal_tex.image=bpy.data.images.load(str(asset_dir/'Laboratory'/(filename+'.png')),check_existing=True)
  normal_tex.image.colorspace_settings.name='Non-Color'
  normal=tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=strength
  tree.links.new(scale.outputs['Vector'],normal_tex.inputs['Vector']);tree.links.new(normal_tex.outputs['Color'],normal.inputs['Color']);tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])
bpy.context.view_layer.update()
for o in objects:
 if o.type=='MESH':metric_uv(o)
bpy.ops.file.pack_all()
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Laboratory_MASTER.blend'))
# Export material batches while retaining editable objects in the saved master.
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.convert(target='MESH')
export_objects=list(bpy.context.selected_objects)
for o in export_objects:metric_uv(o)
def room_batch(obj):
 return obj.get('room_batch') or ('Lab_MainTable_' if obj.name.startswith('Main teaching table') else 'Lab_')
for prefix in ('Lab_MainTable_','Lab_ScrubSouth_','Lab_ScrubNorth_','Lab_'):
 for mat in M.values():
  group=[o for o in bpy.data.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat and room_batch(o)==prefix and not o.name.startswith('Lab_')]
  if not group:continue
  bpy.ops.object.select_all(action='DESELECT')
  for o in group:o.select_set(True)
  bpy.context.view_layer.objects.active=group[0]
  if len(group)>1:bpy.ops.object.join()
  group[0].name=prefix+mat.name
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
 if o.type=='MESH' and o.name.startswith('Lab_'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports/Laboratory.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
removed_room_triangles=runpy.run_path(str(ROOT/'pipeline/repair_environment_export.py'))['clean_generated'](ROOT/'exports/Laboratory.fbx')
shutil.copyfile(ROOT/'exports/Laboratory.fbx',asset_dir/'Models/Laboratory.fbx')
meshes=[o for o in bpy.context.selected_objects if o.type=='MESH']
contact_baker=runpy.run_path(str(ROOT/'pipeline/bake_operating_room_contact.py'))['bake']
contact_baker(meshes,asset_dir/'Laboratory/Theatre floor contact.png')
report={'master':'blender/Laboratory_MASTER.blend','export':'exports/Laboratory.fbx','original_authoring':True,'reference':'operating1.webp (appearance); operating room.mp4 and London Health Sciences Centre (equipment layout)','room_metres':[12,9,3],'renderers':len(meshes),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),'materials':sorted({o.data.materials[0].name for o in meshes})}
report['triangles']-=removed_room_triangles
(ROOT/'qa/reports/environment-build.json').write_text(json.dumps(report,indent=2))
print('LABORATORY_BUILD_COMPLETE',json.dumps(report))
# Keep the chair in the editable room but outside the static room's baked UV atlases.
runpy.run_path(str(ROOT/'pipeline/build_lab_chair.py'),run_name='__main__')
# Render the editable, unpacked room after exporting the batched runtime meshes.
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Laboratory_MASTER.blend'))
bpy.context.scene.render.filepath=str(ROOT/'qa/renders/laboratory-overview.png');bpy.ops.render.render(write_still=True)
