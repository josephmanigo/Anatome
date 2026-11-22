"""Supporting spaces and ventilation for the general operating room. Coordinates are metres."""
# The room builder supplies geometry, materials and the shared object list through run_path.


def sign(label, location, width, text_size, rotation=(math.pi / 2, 0, 0)):
    x,y,z=location
    panel=box(label+' plaque',(x,y,z),(width,.023,.168),'Cloth',.008)
    panel.rotation_euler.z=rotation[2]
    forward=Vector((math.sin(rotation[2]),-math.cos(rotation[2]),0))
    text(label+' lettering',label,Vector(location)+forward*.014-Vector((0,0,text_size*.34)),text_size,rotation,'Sign')


def scrub_and_observation():
    # The scrub lobby adjoins theatre 1 across its left wall. A sealed partition separates the observation space.
    box('Scrub lobby and observation floor',(-7.60,-.93,-.09),(3.24,7.31,.18),'Floor',0)
    box('Annex ceiling',(-7.60,-.93,3.03),(3.24,7.31,.036),'Ceiling',0)
    # Staff enter the scrub lobby from circulation, then pass through the automatic door into theatre.
    box('Restricted staff corridor floor',(-10.095,-.93,-.09),(1.83,7.31,.18),'Floor',0)
    box('Staff corridor ceiling',(-10.095,-.93,3.03),(1.83,7.31,.036),'Ceiling',0)
    box('Staff corridor outer wall',(-11.03,-.93,1.50),(.12,7.38,3),'Wall',0)
    for wy in (-4.56,2.72):
        box('Staff corridor end',(-10.11,wy,1.5),(1.85,.13,3),'Wall',0)
    for lo,hi in ((-4.59,-3.34),(-1.84,-.80),(.24,2.76)):
        box('Annex circulation divider',(-9.26,(lo+hi)/2,1.50),(.12,hi-lo,3),'Wall',0)
    for py,width in ((-2.59,1.50),(-.28,1.04)):
        box('Circulation entrance lintel',(-9.26,py,2.64),(.12,width,.72),'Wall',0)
        for edge in (-1,1):
            box('Lobby opening stainless jamb',(-9.26,py+edge*(width/2-.015),1.13),(.139,.034,2.26),'Stainless',.004)
        box('Lobby door head',(-9.26,py,2.265),(.139,width,.038),'Stainless',.004)
    sign('SCRUB IN',(-9.336,-2.59,2.44),.94,.102,(math.pi/2,0,-math.pi/2))
    sign('STAFF CIRCULATION',(-10.96,-2.59,1.70),1.60,.088,(math.pi/2,0,math.pi/2))
    for cy in (-2.64,1.1):
        box('Staff corridor ceiling lamp',(-10.08,cy,2.987),(.35,.96,.022),'Light',.007)
        d=bpy.data.lights.new('Staff circulation indirect light','AREA');d.energy=72;d.size=.42
        obj=bpy.data.objects.new(d.name,d);bpy.context.collection.objects.link(obj);obj.location=(-10.08,cy,2.95)
    for wy in (-4.56,-.88,2.72):
        box('Annex end wall',(-7.65,wy,1.50),(3.26,.13,3),'Wall',0)
        box('Annex sanitary cove',(-7.65,wy+(.08 if wy<-.9 else -.08),.055),(3.19,.027,.11),'Floor',.004)
    # Reuse the pressed basins with their genuinely recessed bowls, now exclusively outside the theatre.
    start=len(objects)
    for cx in (-.59,.59):
        scrub_basin(cx)
        faucet('Hands-free surgical scrub tap',cx,4.28)
        box('Scrub tap infrared sensor',(cx+.089,4.273,.99),(.031,.028,.026),'Display',.002)
        box('Scrub sink front apron',(cx,3.69,.812),(1.16,.047,.256),'Stainless',.018)
        box('Scrub sink wall bracket',(cx,4.16,.66),(.40,.22,.42),'DarkSteel',.009)
        for dx in (-.12,.12):
            box('Antiseptic or soap dispenser',(cx+dx,4.33,1.52),(.155,.13,.236),'White',.016)
            box('Dispenser view strip',(cx+dx,4.257,1.53),(.041,.012,.110),'EquipmentBlue',.004)
            box('Hands free dispenser outlet',(cx+dx,4.27,1.377),(.077,.047,.042),'DarkSteel',.005)
    box('External scrub backsplash',(0,4.405,1.29),(2.49,.048,.90),'Ceramic',.008)
    for side in (-1,1):
        box('Scrub splash end',(side*1.21,4.04,1.046),(.052,.74,.28),'Stainless',.019)
    bpy.context.view_layer.update()
    # Working faces face north from the south wall of the lobby.
    placement=Matrix.Translation((-7.66,-4.47,0)) @ Matrix.Rotation(math.pi,4,'Z') @ Matrix.Translation((0,-4.43,0))
    for part in objects[start:]:
        part.matrix_world=placement @ part.matrix_world
    sign('SURGICAL SCRUB',(-7.65,-4.453,2.02),1.57,.137,(math.pi/2,0,math.pi))
    window_y=.56
    station_rows=(window_y-.698, window_y+.698)
    # Keep the specimen clear of the middle window frame when a seated observer leans.
    desk_x=-6.57
    box('Observation worktop',(desk_x,window_y,.735),(.74,2.48,.050),'Stainless',.010)
    for side in (-1,1):
        py=window_y+side*1.16
        box('Console support plinth',(desk_x,py,.036),(.56,.096,.072),'DarkSteel',.008)
        box('Console end panel',(desk_x,py,.386),(.59,.075,.654),'Guard',.012)
    box('Console rear cable tray',(desk_x+.31,window_y,.674),(.090,2.28,.074),'DarkSteel',.006)
    add_chair=runpy.run_path(str(ROOT/'pipeline/build_lab_chair.py'))['add_chair']
    for pane,py in enumerate(station_rows):
        box('Observation monitor foot',(desk_x+.13,py,.771),(.23,.29,.022),'DarkSteel',.009)
        box('Observation monitor post',(desk_x+.18,py,.859),(.039,.054,.169),'DarkSteel',.005)
        box('Observation display bezel',(desk_x+.146,py,.965),(.055,.500,.270),'Black',.009)
        box('Observation desk display',(desk_x+.116,py,.965),(.006,.474,.245),'Display',.003)
        display_face('Observer telemetry repeater',0,0,.965,.464,.235,pane)
        objects[-1].matrix_world=Matrix.Translation((desk_x+.111,py,0)) @ Matrix.Rotation(-math.pi/2,4,'Z')
        box('Washable observation keyboard',(desk_x-.191,py,.770),(.159,.366,.020),'DarkSteel',.006)
        for row in range(4):
            for col in range(11):
                box('Sealed keyboard key',(desk_x-.223+row*.024,py+(col-5)*.028,.7815),(.017,.023,.004),'Guard',0)
        box('Sealed keyboard space bar',(desk_x-.247,py,.7815),(.017,.150,.004),'Guard',0)
        sphere('Washable mouse',(desk_x-.19,py-.274,.769),(.049,.030,.018),'DarkSteel')
        chair=bpy.data.objects.new('Observation chair '+str(pane+1),None)
        bpy.context.collection.objects.link(chair)
        chair.empty_display_size=.06;chair.location=(desk_x-.78,py,0);chair.rotation_euler.z=math.pi/2
        for part in add_chair(bpy.context.collection,chair):
            part.name='Observer '+part.name.removeprefix('Lab chair - ')
            if part.data.materials[0]==M['Guard']:
                part.data.materials[0]=M['EquipmentBlue']
            objects.append(part)
    sign('OBSERVATION',(-9.184,-.28,2.44),1.05,.112,(math.pi/2,0,math.pi/2))
    for cy in (-3.55,-1.72,*station_rows):
        box('Annex light surround',(-7.65,cy,3.002),(.48,1.03,.022),'DarkSteel',.006)
        box('Annex luminous diffuser',(-7.65,cy,2.986),(.44,.99,.013),'Light',.006)
        light_data=bpy.data.lights.new('Scrub lobby and observation light','AREA')
        light_data.energy=80;light_data.color=(.96,.98,1.0);light_data.shape='RECTANGLE';light_data.size=.44;light_data.size_y=.99
        lamp=bpy.data.objects.new(light_data.name,light_data);bpy.context.collection.objects.link(lamp);lamp.location=(-7.65,cy,2.96)


def theatre_ventilation():
    # Downflow array centred over the table. Trim, fine perforation and seams are visible from below.
    cy=-1.35
    box('Surgical downflow plenum',(0,cy,3.002),(2.20,3.18,.036),'DarkSteel',.012)
    for col in (-1,1):
        for row in (-1,0,1):
            px,py=col*.536,cy+row*1.04
            box('Flush air diffuser cassette',(px,py,2.971),(1.046,1.009,.019),'VentFace',.006)
            for dx in (-.494,.494):
                for dy in (-.472,.472):
                    cylinder('Diffuser captive fastener',(px+dx,py+dy,2.958),(px+dx,py+dy,2.962),.007,'DarkSteel',8)
    # Pair of low-level return grilles at diagonally opposed perimeter locations.
    for side,py in ((-1,3.45),(1,-3.24)):
        px=side*5.951
        box('HVAC low return frame',(px,py,.411),(.050,1.024,.465),'Stainless',.011)
        box('Return duct recess',(px-side*.029,py,.411),(.015,.965,.406),'Display',.004)
        for row in range(12):
            box('Return grille angled blade',(px-side*.041,py,.230+row*.032),(.027,.943,.014),'Guard',.003)
        for dy in (-.31,0,.31):
            box('Return grille mullion',(px-side*.051,py+dy,.411),(.012,.013,.408),'Stainless',.002)


scrub_and_observation()
theatre_ventilation()
