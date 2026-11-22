import bpy,hashlib,json,math,numpy as np
from itertools import product
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(ROOT/'exports/Laboratory.fbx'))
objects=[o for o in bpy.data.objects if o.type=='MESH'];triangles=0;all_vertices=[]
for o in objects:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles);all_vertices.extend([(o.matrix_world@p.co)[:] for p in o.data.vertices])
points=np.asarray(all_vertices);size=points.max(0)-points.min(0);expected=json.loads((ROOT/'qa/reports/environment-build.json').read_text())
checks={'batch_count':len(objects)==expected['renderers'],'triangle_count':triangles==expected['triangles'],'finite_geometry':bool(np.isfinite(points).all()),'adjustable_table_batches':any(o.name.startswith('Lab_MainTable_') for o in objects),'material_assignments':all(len(o.data.materials)>0 for o in objects)}
floor=bpy.data.objects['Lab_Floor']
# Check the learner's interior floor and ceiling independently of the recessed pass-through.
fp = np.array([(floor.matrix_world @ p.co)[:] for p in floor.data.vertices])
checks['metre_scale_room'] = bool(np.allclose(fp.max(0)[:2],[6,4.50],atol=.06) and -11.11 < fp.min(0)[0] < -10.98 and -4.68 < fp.min(0)[1] < -4.48 and 2.99 < size[2] < 3.35)
coordinates=np.array([item.uv[:] for item in floor.data.uv_layers.active.data])
windings=np.abs(np.ptp(coordinates,axis=0))
checks['floor_uv_spans_metres']=bool(windings[0]>=11.99 and windings[1]>=8.99)
steel=bpy.data.objects['Lab_Stainless']
def surface_height(obj, x, y, start=1.4, direction=-1):
    inverse = obj.matrix_world.inverted()
    hit, point, _, _ = obj.ray_cast(inverse @ Vector((x,y,start)), inverse.to_3x3() @ Vector((0,0,direction)))
    return (obj.matrix_world @ point).z if hit else None

def scrub_xy(local_x,local_y):
    return -7.66-local_x, -4.47-(local_y-4.43)
for cx in (-.59,.59):
    deck=surface_height(steel,*scrub_xy(cx+.43,4.05))
    bottoms=[surface_height(steel,*scrub_xy(cx+dx,y)) for dx in (-.10,.10) for y in (3.97,4.05)]
    checks['external_scrub_basin_'+str(cx)] = deck is not None and all(h is not None and .14<deck-h<.20 for h in bottoms)
checks['threshold_level_with_theatre_and_lobby'] = all(abs(surface_height(floor,x,-2.52) or 0)<.004
    and surface_height(floor,x,-2.52) is not None for x in (-5.47,-5.97,-6.04,-6.65,-7.28))
checks['staff_corridor_to_scrub_without_crossing_theatre'] = all(surface_height(floor,x,-2.59) is not None for x in (-10.25,-9.30,-9.12,-8.6,-7.6))
checks['ceiling_downflow_above_patient'] = all(2.93 < (surface_height(bpy.data.objects['Lab_VentFace'],x,y,1.9,1) or 0) < 2.99
    for x in (-.52,.52) for y in (-2.35,-1.35,-.35))
cushion = bpy.data.objects['Lab_MainTable_Cushion']
checks['four_padded_sections_at_training_height'] = all(
    abs((surface_height(cushion,0,-1.35+dy) or 0)-.89) < .002 for dy in (-.944,-.526,.09,.764))
checks['separate_table_cushions'] = all(surface_height(cushion,0,-1.35+dy) is None for dy in (-.803,-.249,.4295))
checks['two_overhead_surgical_optical_fields'] = all(
    2.15 < (surface_height(bpy.data.objects['Lab_Display'],x,y,1.7,1) or 0) < 2.5 for x,y in ((-.52,-.95),(.52,-.57)))
checks['headwall_gas_trolleys'] = all(
    1.17 < (surface_height(bpy.data.objects[name],x,y) or 0) < 1.22 for name,x,y in
    (('Lab_EquipmentBlue',1.76,2.13),('Lab_CylinderGreen',2.06,2.23)))
checks['head_of_table_monitor'] = 1.92 < (surface_height(bpy.data.objects['Lab_White'],.745,.853,2.1) or 0) < 1.98
checks['mayo_tray_at_working_height'] = .93 < (surface_height(steel,-2.82,-.06) or 0) < .955
checks['both_patient_approaches_clear'] = all(
    (surface_height(obj,centre+offset,y,1.75) or 0) < .035 for obj in objects
    for centre in (-.85,.71) for offset in (-.18,0,.18) for y in (-2.70,-2.10,-1.35,-.65,-.20))
wall = bpy.data.objects['Lab_Wall']
inverse = wall.matrix_world.inverted()
def tile_face_x(height):
    hit, point, _, _ = wall.ray_cast(inverse @ Vector((0,2,height)), inverse.to_3x3() @ Vector((1,0,0)))
    return (wall.matrix_world @ point).x if hit else 0
checks['full_height_tiled_walls'] = all(5.94 < tile_face_x(z) < 5.99 for z in (.16,1.36,2.82))
def left_surface(obj,y,z):
    inv=obj.matrix_world.inverted()
    hit,p,_,_=obj.ray_cast(inv @ Vector((-4.8,y,z)),inv.to_3x3() @ Vector((-1,0,0)))
    return (obj.matrix_world @ p).x if hit else None
checks['window_sightline_into_observation_room'] = (left_surface(wall,1.1,1.82) < -9.14 and
    -6.00 < left_surface(bpy.data.objects['Lab_Glass'],1.1,1.82) < -5.9 and -6.00 < left_surface(wall,2.5,1.82) < -5.93)
def ray_world(obj, origin, direction):
    inverse=obj.matrix_world.inverted()
    hit,point,normal,_=obj.ray_cast(inverse @ origin, inverse.to_3x3() @ direction)
    if hit:
        return obj.matrix_world @ point, (inverse.to_3x3().transposed() @ normal).normalized()
    return None

glass=bpy.data.objects['Lab_Glass']
bevelled_seats=bpy.data.objects['Lab_EquipmentBlue']
keyboards=bpy.data.objects['Lab_DarkSteel']
displays=bpy.data.objects['Lab_MonitorAtlas']

def clear_observation_view(eye, target, row):
    span=target-eye
    direction=span.normalized()
    pane=ray_world(glass,eye,direction)
    if not pane or not (-6.0<pane[0].x<-5.94 and abs(pane[0].y-row)<.672):
        return False
    opaque=(ray_world(obj,eye,direction) for obj in objects if obj!=glass)
    return all(hit is None or (hit[0]-eye).length_squared>=span.length_squared for hit in opaque)

for station,py in enumerate((-.138,1.258),1):
    label=f'observation_station_{station}_'
    # Locate the exported monitor before sampling its seat, keyboard and view.
    face_hits=[ray_world(displays,Vector((-8.65,py+dy,.965)),Vector((1,0,0))) for dy in (-.223,0,.223)]
    centre=face_hits[1]
    checks[label+'screen_faces_chair']=bool(centre and -7.1<centre[0].x<-6.15 and
        all(hit and abs(hit[0].x-centre[0].x)<.002 and hit[1].x<-.99 for hit in face_hits))
    if not centre:
        continue
    seat_x=centre[0].x-.891
    checks[label+'seat_keyboard_and_worktop_centered']=all(
        .527 < (surface_height(bevelled_seats,seat_x,py+dy) or 0) < .558 and
        .777 < (surface_height(keyboards,seat_x+.59,py+dy,.81) or 0) < .784 and
        .757 < (surface_height(steel,seat_x+.47,py+dy) or 0) < .763 for dy in (-.153,0,.153))
    eye=Vector((seat_x,py,1.19))
    checks[label+'seated_view_through_pane_towards_table']=clear_observation_view(eye,Vector((0,-1.35,1.03)),py)
    # Sample the thorax extent from nine seated head positions, including a diagonal lean.
    checks[label+'lean_keeps_specimen_in_view']=all(
        clear_observation_view(eye+Vector((dx,dy,0)),Vector(target),py)
        for dx,dy in product((-.10,0,.10),repeat=2)
        for target in product((-.25,.25),(-1.51,-1.19),(.98,1.10)))
checks['observation_rear_aisle_clear_for_both_seats']=all(
    (surface_height(obj,x,y,1.95) or 0)<.025 for obj in objects
    for x in (-9.12,-8.64,-8.12) for y in (-.66,-.28,.42,1.26,1.74))
checks['opening_to_scrub_lobby'] = all(left_surface(wall,-2.52+dy,1.13)<-9.14 for dy in (-.79,0,.79))
checks['independent_sliding_scrub_leaves'] = all('Lab_Scrub'+side+'_'+finish in bpy.data.objects for side in ('South','North')
    for finish in ('White','Stainless','DarkSteel','Glass'))
checks['fixed_table_pedestal'] = .18 < (surface_height(steel,.15,-1.35) or 0) < .49
field = bpy.data.objects['Lab_MainTable_Blue']
hem = min((field.matrix_world @ v.co).z for v in field.data.vertices)
checks['draped_table_with_open_specimen_field'] = (.28 < hem < .46 and surface_height(field,0,-1.35) is None
    and .892 < (surface_height(field,0,-2.22) or 0) < .92)
mobile_field = bpy.data.objects['Lab_Blue']
checks['sterile_instrument_table_drape'] = all(.955 < (surface_height(mobile_field,x,y) or 0) < .99
    for x,y in ((2.33,-2.21),(2.04,-2.57),(2.67,-1.38)))
instruments = bpy.data.objects['Lab_InstrumentSteel']
bottom = surface_height(instruments,2.605,-1.65)
rim = surface_height(instruments,2.605+.137,-1.65)
checks['recessed_stainless_instrument_bowl'] = bottom is not None and rim is not None and .045 < rim-bottom < .091
screens = bpy.data.objects['Lab_MonitorAtlas'].data
pane_ids = set()
for face in screens.polygons:
    uv = np.mean([screens.uv_layers.active.data[i].uv[:] for i in face.loop_indices],axis=0)
    pane_ids.add((int(uv[0]*2),int(uv[1]*2)))
checks['four_distinct_monitor_atlas_panes'] = len(pane_ids)==4
def max_near(obj,x,y,start=1.83):
    return max((surface_height(obj,x+dx,y+dy,start) or 0) for dx in (-.13,0,.13) for dy in (-.12,0,.12))
white=bpy.data.objects['Lab_White']
checks['electrosurgery_and_smoke_workstation'] = .98 < max_near(white,-3.84,.51) < 1.14
checks['mobile_ultrasound_console'] = 1.36 < max_near(white,3.55,3.32) < 1.68
checks['resuscitation_trolley_and_defibrillator'] = 1.16 < max_near(white,3.95,-1.55) < 1.51
checks['forced_air_warmer_housing'] = .90 < max_near(white,-4.64,2.24) < 1.04
checks['pole_mounted_infusion_pump'] = 1.37 < max_near(white,2.49,-.48,1.53) < 1.52
checks['ceiling_services_pendant_at_head_end'] = 1.53 < (surface_height(white,1.25,1.49,1.45,1) or 0) < 1.62
checks['former_in_room_basin_removed'] = all(surface_height(steel,x,4.05) is None for x in (3.55,3.98))
checks['digital_timer_housing_above_service_rail'] = 2.51 < (surface_height(bpy.data.objects['Lab_Black'],-.82,4.357,2.7) or 0) < 2.58
checks['room_renderer_budget'] = len(objects) <= 40 and triangles < 185000

chair_file = ROOT / 'exports/LaboratoryChair.fbx'
bpy.ops.import_scene.fbx(filepath=str(chair_file))
parts = {o.name: o for o in bpy.context.selected_objects if o.type == 'MESH'}
cost = {name: sum(len(face.vertices) - 2 for face in obj.data.polygons) for name, obj in parts.items()}
checks['chair_material_batches_and_budget'] = (
 set(parts) == {'Lab_Chair_Guard', 'Lab_Chair_Stainless', 'Lab_Chair_Black'} and sum(cost.values()) <= 4000
 and all(len(o.data.materials) == 1 and o.data.materials[0].name.split('.')[0] == n.removeprefix('Lab_Chair_')
         for n, o in parts.items()))
runtime_models = ROOT / 'unity/ThoraxLab/Assets/ThoraxLab/Models'
for model in ('Laboratory.fbx', 'LaboratoryChair.fbx'):
 checks['unity_copy_' + model] = (ROOT / 'exports' / model).read_bytes() == (runtime_models / model).read_bytes()
chair_points = {name: np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for name, o in parts.items()}
all_chair = np.concatenate(list(chair_points.values()))
lower, upper = all_chair.min(0), all_chair.max(0)
checks['chair_scale_and_finite_geometry'] = bool(
 np.isfinite(all_chair).all() and np.all((upper - lower > [.57, .57, .99]) & (upper - lower < [.68, .68, 1.07])))

pivot = Vector((4.50, .80, 0))
heading = Matrix.Rotation(math.radians(-78), 3, 'Z')
pad = parts['Lab_Chair_Guard']
metal = parts['Lab_Chair_Stainless']
def chair_hit(obj, local_origin, local_direction):
 inv = obj.matrix_world.inverted()
 hit, point, _, _ = obj.ray_cast(inv @ (pivot + heading @ Vector(local_origin)),
                                 inv.to_3x3() @ heading @ Vector(local_direction))
 return obj.matrix_world @ point if hit else None

seat = chair_hit(pad, (0, 0, 1.15), (0, 0, -1))
front = chair_hit(pad, (0, -.198, 1.15), (0, 0, -1))
side = chair_hit(pad, (.175, 0, 1.15), (0, 0, -1))
checks['chair_contoured_seat_and_waterfall_edge'] = (
 seat is not None and front is not None and side is not None and .52 < seat.z < .56 and front.z < seat.z < side.z)
checks['chair_open_grip_and_back'] = (
 chair_hit(pad, (0, -.15, .953), (0, 1, 0)) is None and
 all(chair_hit(pad, (0, -.15, z), (0, 1, 0)) is not None for z in (.91, .992)))
checks['chair_support_piston_and_foot_ring'] = (
 chair_hit(metal, (0, -.35, .62), (0, 1, 0)) is not None and
 chair_hit(metal, (0, -.35, .38), (0, 1, 0)) is not None and
 chair_hit(metal, (0, -.191, .40), (0, 0, -1)) is not None)
tyres = chair_points['Lab_Chair_Black']
contacts = tyres[np.abs(tyres[:, 2]) < .001]
caster_heights = []
for i in range(5):
 angle = math.radians(-65) + math.tau * i / 5
 xy = np.array((pivot.x + .288 * math.cos(angle), pivot.y + .288 * math.sin(angle)))
 sample = contacts[np.linalg.norm(contacts[:, :2] - xy, axis=1) < .045]
 floor_z = surface_height(floor, *xy)
 caster_heights.append(float(sample[:, 2].min() - floor_z) if len(sample) and floor_z is not None else None)
checks['five_casters_touch_room_floor'] = all(z is not None and abs(z) < .001 for z in caster_heights)

left_hit, trolley_point, _, _ = steel.ray_cast(steel.matrix_world.inverted() @ Vector((4.30,1.60,.858)),
    steel.matrix_world.inverted().to_3x3() @ Vector((-1,0,0)))
trolley_edge = (steel.matrix_world @ trolley_point).x if left_hit else float('inf')
rail = bpy.data.objects['Lab_Guard']
inv = rail.matrix_world.inverted()
hit, point, _, _ = rail.ray_cast(inv @ Vector((upper[0], pivot.y, .91)), inv.to_3x3() @ Vector((1, 0, 0)))
rail_clearance = (rail.matrix_world @ point).x - upper[0] if hit else 0
# The tracked learner starts at (+.85, +.65) in the Unity XZ plane.
spawn = np.array((-.85, -.65))
spawn_clearance = np.linalg.norm(spawn - np.clip(spawn, lower[:2], upper[:2]))
checks['chair_clear_of_station_wall_aisle_and_spawn'] = bool(
 lower[0] - trolley_edge > .15 and rail_clearance > 1 and spawn_clearance > 5)

chair = {'sha256': hashlib.sha256(chair_file.read_bytes()).hexdigest(),
         'batches': cost, 'triangles': sum(cost.values()), 'bounds_min_m': lower.tolist(),
         'bounds_max_m': upper.tolist(), 'size_m': (upper-lower).tolist(), 'seat_height_m': seat.z if seat else None,
         'caster_floor_gaps_m': caster_heights, 'station_clearance_m': float(lower[0]-trolley_edge),
         'wall_aisle_width_m': float(rail_clearance), 'spawn_clearance_m': float(spawn_clearance)}
report={'checks':checks,'passed':all(checks.values()),'objects':len(objects),'triangles':triangles,
        'bounds_size_m':size.tolist(),'names':[o.name for o in objects],'chair':chair,
        'total_room_and_chair_batches':len(objects)+len(parts),'total_room_and_chair_triangles':triangles+sum(cost.values())}
(ROOT/'qa/reports/environment-roundtrip.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2));assert report['passed']
