"""General operating-room equipment, built with the room's shared geometry and finishes."""
import math
import random


def device_mesh(name, vertices, faces, finish_name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, finish_name)


def detail_blocks(name, blocks, finish_name):
    """Batch small square-section keys, slots and marks into one editable part."""
    vertices, faces = [], []
    corners = ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
               (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))
    sides = ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7))
    for x,y,z,w,d,h in blocks:
        start = len(vertices)
        vertices.extend((x+i*w/2, y+j*d/2, z+k*h/2) for i,j,k in corners)
        faces.extend(tuple(start+i for i in face) for face in sides)
    return device_mesh(name, vertices, faces, finish_name)


def rod(name, start, end, radius, finish_name='Stainless', sides=12):
    axis = Vector(end) - Vector(start)
    return turned(name, start, axis, [(0, radius),(axis.length, radius)], finish_name, sides)


def atlas_swatch(obj, pixel):
    # Constant UV selects a colour from the existing 1024 x 512 clinical display atlas.
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
    xy = ((pixel[0]+.5)/1024, 1-(pixel[1]+.5)/512)
    for loop in uv.data:
        loop.uv = xy
    obj['authored_uvs'] = True
    return obj


def cart_base(name, width, depth, accent):
    box(name+' chassis', (0,0,.150), (width,depth,.077), accent, .030)
    for side in (-1,1):
        for end in (-1,1):
            x, y = side*(width/2-.064), end*(depth/2-.062)
            box(name+' swivel fork', (x,y,.104), (.067,.047,.062), 'Stainless', .006)
            turned(name+' caster tyre', (x,y,.057), (1,0,0),
                   [(-.027,.046),(-.021,.056),(.021,.056),(.027,.046)], 'Black', 16)
            rod(name+' caster hub', (x-.028,y,.057),(x+.028,y,.057),.024,'Guard',12)


def ribbed_hose(name, cubics, radius, finish_name, ribs_per_span):
    """Sweep the corrugation in one mesh along cubic hose centre lines."""
    vertices, faces = [], []
    sides = 10
    steps = ribs_per_span * 3
    for index, coordinates in enumerate(cubics):
        a,b,c,d = map(Vector, coordinates)
        for i in range(1 if index else 0, steps+1):
            t = i / steps
            s = 1-t
            centre = a*s**3 + b*(3*s*s*t) + c*(3*s*t*t) + d*t**3
            tangent = ((b-a)*s*s + (c-b)*(2*s*t) + (d-c)*t*t).normalized()
            across = tangent.cross(Vector((0,1,0))).normalized()
            along = tangent.cross(across)
            r = radius * (.86 if i % 3 == 0 else 1)
            start = len(vertices)
            for j in range(sides):
                angle = j * math.tau / sides
                vertices.append(centre + r*(across*math.cos(angle) + along*math.sin(angle)))
                if start:
                    k = (j+1) % sides
                    faces.append((start-sides+j,start-sides+k,start+k,start+j))
    faces.extend((tuple(reversed(range(sides))), tuple(range(len(vertices)-sides,len(vertices)))))
    obj = device_mesh(name, vertices, faces, finish_name)
    for face in obj.data.polygons:
        face.use_smooth = True


def electrosurgery_station():
    cart_base('Diathermy trolley', .600,.502,'DarkSteel')
    for x in (-.241,.241):
        box('Diathermy rack upright', (x,.136,.538),(.042,.061,.741),'Stainless',.007)
    for z in (.326,.809):
        box('Diathermy rack shelf',(0,0,z),(.562,.451,.032),'White',.010)

    box('Smoke evacuator replaceable filter unit',(0,0,.466),(.525,.372,.239),'White',.025)
    box('Smoke evacuator filter door',(-.108,-.191,.460),(.245,.017,.176),'EquipmentBlue',.009)
    rod('Smoke evacuator inlet collar',(-.130,-.198,.465),(-.130,-.229,.465),.052,'Black',20)
    turned('Smoke evacuator hose adaptor',(-.130,-.226,.465),(0,-1,0),
           [(0,.041),(.014,.040),(.027,.031),(.041,.029)],'Stainless',18)
    curve('Smoke evacuation hose in storage', [(-.130,-.265,.465),(-.24,-.28,.37),
          (-.317,-.20,.435),(-.312,-.054,.697),(-.292,.085,.791),(-.255,.111,.764)],.020,'Cushion')
    box('Smoke evacuator filter latch',(-.036,-.207,.461),(.023,.021,.096),'White',.004)
    detail_blocks('Smoke evacuator cooling grille',[(.114,-.190,.409+i*.014,.162,.009,.006) for i in range(5)],'Sign')
    text('Smoke evacuator printed label','SMOKE EVAC',(.108,-.191,.514),.024)
    rod('Smoke evacuator fan selector',(.197,-.189,.522),(.197,-.213,.522),.017,'DarkSteel')

    box('Diathermy generator enclosure',(0,.010,.963),(.549,.395,.270),'White',.030)
    box('Diathermy generator face',(0,-.191,.953),(.508,.016,.206),'Guard',.011)
    box('Diathermy generator LCD',(-.106,-.203,.996),(.227,.010,.081),'Display',.006)
    text('Diathermy display standby','STBY',(-.106,-.210,.985),.035,mat='White')
    text('Diathermy generator name','DIATHERMY',(.126,-.200,1.008),.027,mat='Sign')
    for x,colour,holes in ((-.150,'MonitorAtlas',3),(0,'EquipmentBlue',2),(.15,'White',2)):
        socket = box('Diathermy coded connector',(x,-.205,.900),(.118,.014,.057),colour,.004)
        if colour == 'MonitorAtlas':
            atlas_swatch(socket,(430,185))
        for i in range(holes):
            xx = x+(i-(holes-1)/2)*.023
            rod('Diathermy connector contact',(xx,-.211,.900),(xx,-.217,.900),.008,'Black',10)
    for x in (.085,.164):
        rod('Diathermy front encoder',(x,-.201,.965),(x,-.225,.965),.024,'Black',16)
    detail_blocks('Diathermy case vents',[(.276,.008+i*.027,.991,.004,.011,.071) for i in range(5)],'Sign')

    box('Diathermy footswitch guard',(0,-.40,.030),(.282,.163,.045),'Black',.012)
    for side,colour in ((-1,'MonitorAtlas'),(1,'EquipmentBlue')):
        x = side*.064
        pedal = box('Diathermy foot pedal',(x,-.410,.059),(.114,.124,.025),colour,.008)
        if side < 0:
            atlas_swatch(pedal,(430,185))
        detail_blocks('Diathermy pedal tread',[(x,-.448+i*.024,.073,.084,.004,.004) for i in range(4)],'Black')
    curve('Diathermy footswitch lead',[(.22,.11,.82),(.26,.17,.66),(.26,.16,.26),(.25,.12,.085),
          (.225,-.009,.021),(.191,-.219,.021),(.11,-.32,.033)],.007,'Black')


def b_mode_screen(y, z):
    """A simulated acoustic section with layered echoes, speckle and a dark fluid lumen."""
    rng = random.Random(811)
    rings, spokes = 30, 46
    vertices, faces, uvcoords = [], [], []
    for row in range(rings+1):
        depth = row/rings
        radius = .015 + depth*.224
        for col in range(spokes+1):
            theta = -.96 + 1.92*col/spokes
            vertices.append((radius*math.sin(theta),y,z+.116-radius*math.cos(theta)))
            if row < rings and col < spokes:
                n = row*(spokes+1)+col
                faces.append((n,n+spokes+1,n+spokes+2,n+1))
            u, v = depth*math.sin(theta), depth*math.cos(theta)
            grain = rng.random()
            tissue = .95+1.55*grain+.37*math.sin(u*19+v*14)
            for layer, strength in ((.20,1.9),(.31,1.4),(.65,1.45),(.82,1.5)):
                centre = layer+.05*math.sin(u*5+layer*11)
                tissue += strength*math.exp(-((v-centre)/.024)**2)
            lumen = ((u-.075)/.19)**2 + ((v-.505+.11*u)/.108)**2
            if lumen < 1:
                tissue = .17+.65*grain
            else:
                tissue += 2.4*math.exp(-((lumen-1)/.27)**2)
            strength = min(1,max(0,tissue/5))
            # Bilinear sampling across this dark/light atlas edge shades the single continuous UV chart.
            uvcoords.append(((639.5+strength)/1024,1-(185.5+.02*depth)/512))
    obj = device_mesh('Ultrasound B-mode acoustic sector',vertices,faces,'MonitorAtlas')
    uv = obj.data.uv_layers.new(name='UVMap')
    for loop in obj.data.loops:
        uv.data[loop.index].uv = uvcoords[loop.vertex_index]
    obj['authored_uvs'] = True


def ultrasound_cart():
    cart_base('Ultrasound cart',.520,.478,'EquipmentBlue')
    box('Ultrasound computing enclosure',(0,.057,.325),(.369,.298,.284),'White',.055)
    detail_blocks('Ultrasound base vents',[(0,-.094,.268+i*.019,.216,.005,.008) for i in range(5)],'Sign')
    turned('Ultrasound telescoping lift',(0,.056,.441),(0,0,1),
           [(0,.092),(.051,.093),(.066,.078),(.314,.072),(.336,.065),(.512,.059)],'White',20)
    box('Ultrasound floating keyboard console',(0,-.039,.974),(.604,.402,.081),'White',.032)
    box('Ultrasound console input panel',(0,-.027,1.017),(.547,.315,.012),'Guard',.006)
    detail_blocks('Ultrasound alphanumeric keypad',
        [(-.142+col*.032,.086-row*.029,1.026,.025,.018,.009) for row in range(3) for col in range(9)],'White')
    for x in (-.211,-.153,.148,.208):
        rod('Ultrasound rotary control',(x,-.085,1.021),(x,-.085,1.049),.022,'White',14)
    turned('Ultrasound trackball well',(0,-.119,1.021),(0,0,1),[(0,.042),(.007,.041)],'Black',18)
    sphere('Ultrasound trackball',(0,-.119,1.029),(.029,.029,.018),'White')
    curve('Ultrasound steering handle',[(-.244,-.190,.96),(-.25,-.260,.960),(-.195,-.282,.958),
          (.195,-.282,.958),(.25,-.260,.96),(.244,-.190,.96)],.013,'Stainless')
    rod('Ultrasound display arm',(0,.100,1.00),(0,.105,1.225),.038,'DarkSteel',16)
    box('Ultrasound articulated display',(0,.083,1.414),(.540,.069,.374),'White',.022)
    box('Ultrasound display bezel',(0,.043,1.414),(.516,.017,.351),'Black',.007)
    box('Ultrasound LCD',(0,.031,1.422),(.490,.008,.292),'Display',.002)
    b_mode_screen(.025,1.421)
    text('Ultrasound scan mode','B MODE',(-.182,.023,1.535),.018,mat='White')
    detail_blocks('Ultrasound depth scale',[(.205,.023,1.325+i*.027,.012 if i%2 else .021,.002,.0025) for i in range(8)],'White')
    text('Ultrasound display name','ULTRASOUND',(0,.032,1.249),.023,mat='White')
    box('Ultrasound probe cradle bracket',(.292,.016,.988),(.081,.085,.023),'White',.006)
    turned('Ultrasound probe cradle',(.314,.016,1.00),(0,0,1),
           [(0,.029),(.018,.044),(.043,.047),(.047,.043),(.044,.027),(.016,.023)],'EquipmentBlue',20)
    turned('Ultrasound convex probe',(.314,.022,.969),(0,-.12,1),
           [(0,.012),(.034,.014),(.085,.024),(.143,.025),(.169,.033)],'White',18)
    box('Ultrasound curved-array probe head',(.314,.002,1.149),(.083,.038,.039),'White',.014)
    box('Ultrasound acoustic window',(.314,.002,1.169),(.076,.035,.013),'Guard',.006)
    curve('Ultrasound probe cable',[(.314,.021,.975),(.343,.013,.79),(.348,-.089,.48),
          (.286,-.136,.38),(.210,-.144,.44),(.191,-.109,.62),(.184,-.05,.74)],.006,'Cloth')
    box('Ultrasound probe connector',(.165,-.034,.755),(.112,.074,.065),'White',.007)
    rod('Ultrasound connector support',(.02,.050,.744),(.137,-.015,.746),.018,'Stainless')


def crash_trolley():
    cart_base('Resuscitation trolley',.717,.544,'DarkSteel')
    box('Crash trolley cabinet',(0,0,.576),(.653,.469,.778),'Red',.031)
    for z,h in ((.302,.179),(.500,.182),(.672,.135),(.814,.122)):
        box('Crash drawer seal',(0,-.237,z),(.596,.017,h+.011),'Black',.006)
        box('Crash drawer', (0,-.251,z),(.581,.023,h),'Red',.010)
        box('Crash drawer pull',(0,-.273,z+h*.29),(.270,.038,.022),'Stainless',.006)
    box('Crash trolley worktop',(0,0,.979),(.734,.545,.055),'White',.017)
    box('Crash trolley identification plate',(0,-.249,.918),(.327,.010,.061),'White',.004)
    text('Crash trolley identification','CRASH CART',(0,-.256,.900),.043)
    curve('Crash trolley steering handle',[(.315,-.165,.925),(.387,-.152,.939),(.397,.146,.939),(.315,.159,.925)],.016,'Stainless')

    detail_blocks('Defibrillator rubber feet',[(x,y,1.015,.057,.049,.023) for x in (-.169,.169) for y in (-.079,.079)],'Black')
    box('Defibrillator case',(0,0,1.168),(.482,.279,.291),'White',.038)
    box('Defibrillator front panel',(0,-.143,1.170),(.441,.016,.239),'Guard',.009)
    box('Defibrillator ECG bezel',(-.065,-.154,1.187),(.260,.013,.166),'Black',.006)
    display_face('Defibrillator ECG screen',-.065,-.163,1.187,.244,.150,0)
    text('Defibrillator name','DEFIB',(.141,-.153,1.249),.027)
    rod('Defibrillator rotary selector',(.135,-.151,1.197),(.135,-.186,1.197),.035,'Black',18)
    box('Defibrillator selector index',(.135,-.188,1.214),(.005,.004,.018),'White',0)
    for x,colour in ((.102,'White'),(.171,'Red')):
        box('Defibrillator membrane key',(x,-.156,1.114),(.049,.015,.033),colour,.006)
    detail_blocks('Defibrillator soft keys',[(-.148+i*.056,-.156,1.076,.037,.012,.012) for i in range(4)],'White')
    curve('Defibrillator carrying handle',[(-.088,.073,1.30),(-.086,.073,1.397),(-.062,.073,1.420),
          (.062,.073,1.420),(.086,.073,1.397),(.088,.073,1.30)],.013,'DarkSteel')
    for side in (-1,1):
        x=side*.168
        box('Defibrillator paddle dock',(x,.017,1.318),(.104,.180,.021),'Black',.009)
        box('Defibrillator paddle electrode',(x,.013,1.331),(.088,.149,.012),'Stainless',.006)
        box('Defibrillator paddle insulation',(x,.013,1.350),(.096,.159,.033),'White',.010)
        curve('Defibrillator paddle grip',[(x,-.046,1.361),(x,-.032,1.390),(x,.038,1.391),(x,.074,1.361)],.014,'Black')
        box('Defibrillator paddle switch',(x,.053,1.386),(.029,.029,.010),'Red',.003)
        curve('Defibrillator paddle cord',[(x,.086,1.353),(side*.236,.115,1.330),(side*.265,.094,1.201),
              (side*.273,-.024,1.093),(side*.250,-.091,1.104),(side*.233,-.081,1.141)],.006,'Black')
    box('Crash cart electrode pouch holder',(-.333,.012,.700),(.033,.253,.230),'DarkSteel',.006)
    box('Sealed spare electrode pads',(-.354,.010,.745),(.022,.214,.230),'White',.007)


def infusion_pumps():
    for z in (1.08,1.37):
        rod('IV pump pole clamp',(0,0,z-.026),(0,0,z+.026),.036,'DarkSteel',14)
        box('IV pump mounting slide',(0,-.057,z),(.082,.117,.047),'Stainless',.005)
        rod('IV pole clamp knob',(.014,.009,z),(.066,.009,z),.017,'Black')
    box('Volumetric infusion pump',(0,-.127,1.373),(.252,.144,.223),'White',.017)
    box('IV pump cassette door',(-.080,-.205,1.370),(.064,.017,.169),'EquipmentBlue',.006)
    box('IV pump door release',(-.095,-.220,1.362),(.018,.019,.064),'White',.004)
    box('IV pump LCD',(.033,-.205,1.406),(.118,.009,.061),'Display',.004)
    text('IV pump hold indication','HOLD',(.033,-.211,1.398),.023,mat='White')
    detail_blocks('IV pump numeric keys',[(.004+c*.033,-.207,1.318+r*.022,.023,.008,.014) for c in range(3) for r in range(2)],'Guard')
    text('IV pump identification','INFUSION',(0,-.202,1.455),.018)
    curve('IV pump input line',[(-.187,-.015,1.612),(-.163,-.060,1.568),(-.135,-.117,1.516),(-.084,-.134,1.472)],.003,'Cloth')
    curve('IV pump output line',[(-.085,-.146,1.264),(-.117,-.177,1.176),(-.157,-.157,1.034),(-.160,-.091,.930)],.003,'Cloth')
    box('Syringe driver carriage',(0,-.113,1.076),(.342,.128,.141),'White',.013)
    box('Syringe driver display',(-.098,-.181,1.088),(.093,.009,.060),'Display',.005)
    box('Syringe driver bed',(.065,-.184,1.051),(.179,.054,.034),'EquipmentBlue',.005)
    rod('Syringe fluid barrel',(-.020,-.206,1.095),(.108,-.206,1.095),.020,'Glass',16)
    rod('Syringe driver plunger',(.042,-.206,1.095),(.144,-.206,1.095),.007,'White')
    for x in (-.018,.104):
        rod('Syringe retaining clip',(x-.006,-.206,1.095),(x+.006,-.206,1.095),.024,'White',14)
    box('Syringe driver pusher',(.151,-.196,1.091),(.030,.060,.085),'Guard',.005)
    rod('Syringe line adaptor',(-.042,-.206,1.095),(-.019,-.206,1.095),.008,'White')
    curve('Syringe pump extension line',[(-.039,-.206,1.095),(-.147,-.221,1.061),(-.176,-.186,.910),(-.144,-.111,.845)],.003,'Cloth')


def air_warmer():
    cart_base('Air warmer',.428,.386,'DarkSteel')
    rod('Air warmer trolley post',(0,.034,.183),(0,.034,.543),.037,'Stainless',16)
    box('Forced air warmer housing',(0,.014,.759),(.441,.303,.481),'White',.049)
    box('Air warmer control panel',(0,-.142,.900),(.337,.018,.116),'EquipmentBlue',.008)
    text('Air warmer printed label','AIR WARMER',(-.039,-.154,.898),.032,mat='White')
    rod('Air warmer control dial',(.111,-.151,.907),(.111,-.173,.907),.026,'White',16)
    turned('Air warmer filter bezel',(-.052,-.140,.697),(0,-1,0),[(0,.101),(.009,.106),(.014,.096)],'DarkSteel',30)
    for i in range(-4,5):
        z = i*.0185
        half = math.sqrt(.084**2-z**2)
        box('Air warmer filter louvre',(-.052,-.158,.697+z),(half*2,.009,.008),'Guard',.002)
    turned('Air warmer hose coupler',(.200,-.008,.730),(1,0,0),[(0,.050),(.027,.060),(.047,.051)],'DarkSteel',20)
    ribbed_hose('Air warmer corrugated delivery hose',[
        ((.247,-.008,.730),(.346,-.008,.728),(.348,-.040,.512),(.339,-.061,.397)),
        ((.339,-.061,.397),(.326,-.089,.251),(.209,-.129,.267),(.181,-.153,.426)),
        ((.181,-.153,.426),(.167,-.174,.520),(.171,-.178,.593),(.172,-.178,.664))],.037,'Guard',11)
    turned('Air warmer blanket nozzle',(.172,-.178,.654),(0,0,1),[(0,.041),(.030,.043),(.039,.037),(.103,.031)],'White',20)
    box('Air warmer nozzle keeper',(.168,-.129,.685),(.082,.086,.029),'EquipmentBlue',.007)
    curve('Air warmer carrying handle',[(-.115,.081,.985),(-.111,.081,1.060),(-.081,.081,1.076),
          (.081,.081,1.076),(.111,.081,1.060),(.115,.081,.985)],.013,'Guard')


def service_pendant():
    # A folded two-link arm parks the service head behind the anaesthesia workstation.
    ceiling = (.78,1.55)
    elbow = (1.57,2.05)
    head = (1.25,1.49)
    turned('Services pendant ceiling canopy',(*ceiling,3.011),(0,0,-1),
           [(0,.229),(.030,.229),(.075,.186),(.104,.100)],'White',28)
    for point,low,high in ((ceiling,2.748,2.929),(elbow,2.573,2.823),(head,2.355,2.664)):
        rod('Services pendant swivel joint',(*point,low),(*point,high),.094,'White',24)
        rod('Services pendant pivot seal',(*point,low+.030),(*point,low+.044),.095,'DarkSteel',24)
    for (ax,ay),(bx,by),z in ((ceiling,elbow,2.790),(elbow,head,2.619)):
        length = math.hypot(bx-ax,by-ay)
        angle = math.atan2(by-ay,bx-ax)
        for dz,w,h,colour in ((0,.157,.107,'White'),(-.051,.139,.013,'Guard')):
            arm = box('Services pendant articulated arm',((ax+bx)/2,(ay+by)/2,z+dz),(length,w,h),colour,.015 if dz==0 else .003)
            arm.rotation_euler.z = angle
    x,y=head
    box('Pendant suspended utility head',(x,y,1.957),(.407,.213,.805),'White',.030)
    for z in (1.563,2.338):
        box('Pendant end cap',(x,y,z),(.414,.217,.041),'Guard',.010)
    box('Pendant gas outlet fascia',(x,y-.110,2.135),(.352,.015,.193),'Guard',.005)
    text('Pendant medical gas designation','O2     AIR     VAC',(x,y-.120,2.183),.023)
    for dx,colour in ((-.115,'White'),(0,'EquipmentBlue'),(.115,'CylinderGreen')):
        xx=x+dx
        rod('Pendant indexed gas outlet',(xx,y-.120,2.115),(xx,y-.138,2.115),.036,colour,16)
        rod('Pendant gas outlet ferrule',(xx,y-.139,2.115),(xx,y-.150,2.115),.024,'Stainless',16)
        rod('Pendant gas outlet recess',(xx,y-.149,2.115),(xx,y-.153,2.115),.013,'Black',12)
    box('Pendant electrical fascia',(x,y-.110,1.824),(.352,.014,.250),'EquipmentBlue',.005)
    sockets = [(x+dx,z) for dx in (-.096,.096) for z in (1.775,1.875)]
    detail_blocks('Pendant medical power receptacles',[(xx,y-.121,z,.123,.012,.075) for xx,z in sockets],'White')
    detail_blocks('Pendant electrical receptacle slots',[(xx+dx,y-.130,z+.011,.009,.005,.022) for xx,z in sockets for dx in (-.018,.018)]
                  + [(xx,y-.130,z-.015,.012,.005,.010) for xx,z in sockets],'Black')
    for side in (-1,1):
        xx = x+side*.220
        curve('Pendant vertical grab rail',[(x+side*.193,y,2.145),(xx,y-.035,2.127),(xx,y-.041,1.742),(x+side*.193,y,1.727)],.012,'Stainless')


def place_cart_or_pump(build, x, y, degrees):
    first = len(objects)
    build()
    placement = Matrix.Translation((x,y,0)) @ Matrix.Rotation(math.radians(degrees),4,'Z')
    for obj in objects[first:]:
        obj.matrix_basis = placement @ obj.matrix_basis


if __name__ == '__main__':
    place_cart_or_pump(electrosurgery_station,-3.84,.51,22)
    place_cart_or_pump(ultrasound_cart,3.55,3.32,-24)
    place_cart_or_pump(crash_trolley,3.95,-1.55,-29)
    place_cart_or_pump(infusion_pumps,2.49,-.37,0)
    place_cart_or_pump(air_warmer,-4.64,2.24,33)
    service_pendant()
