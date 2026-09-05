"""Explicit authoring of hollow airway/esophageal teaching walls from solid casts."""
import bpy,bmesh,json,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
production=bpy.data.collections['THORAX_PRODUCTION'];catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];byid={r['id']:r for r in catalog};report=[]
for o in production.objects:
 r=byid[o['id']]
 if r['tissue']!='airway' and 'esophagus' not in r['name'].lower():continue
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 thickness=.00065 if r['tissue']=='airway' else .0012
 # Offset closed source boundary inward. Inner wall is a separate, reversed shell;
 # the source envelope remains the outside surface. Source endpoints are retained.
 mod=o.modifiers.new('Authored hollow teaching wall','SOLIDIFY');mod.thickness=-thickness;mod.offset=-1;mod.use_even_offset=True;mod.thickness_clamp=.3;bpy.ops.object.modifier_apply(modifier=mod.name)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o.data.update()
 r['source_kind']='source_mesh_with_authored_lumen';r['notes']+=' Source was a solid cast of uncertain luminal/external boundary. Authored inward wall offset '+str(thickness*1000)+' mm; source endpoint terminations retained. Inner shell is an explicit teaching approximation, not segmented tissue thickness.'
 o['source_kind']=r['source_kind'];o['provenance_notes']=r['notes'];report.append({'id':r['id'],'wall_offset_mm':thickness*1000,'method':'inward offset with local clamp, reversed inner shell','source_audit':'source/airway-lumen-audit.json','limitations':['Source surface may trace the lumen or outer boundary; uncertainty retained.','Regional and elemental endpoint closures retained; branch-junction wall continuity needs visual review.']})
for o in production.objects:
 r=byid[o['id']];r['rest_transform_blender']={'position':list(o.location),'rotation_euler':list(o.rotation_euler),'scale':list(o.scale)};r['selection_anchor_local']=[0,0,0];r['interaction']={'grabbable':True,'guided_layer':r['layer'],'snap_distance_m':.075,'cut_tool':'saw' if r['tissue']=='bone' else 'scalpel'}
 # Reduce thin cable display detail. Its radius remains unchanged.
 if r['id'].startswith('REC_') and r['tissue'] in ('nerve','artery','vein') and len(o.data.polygons)>400:
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;d=o.modifiers.new('Cable display budget','DECIMATE');d.ratio=400/len(o.data.polygons);bpy.ops.object.modifier_apply(modifier=d.name)
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
(ROOT/'docs/authored-lumens.json').write_text(json.dumps(report,indent=2));(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
def export(name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in production.objects:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports'/name),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_custom_props=True)
export('Thorax.fbx')
for level,ratio in [(1,.48),(2,.45)]:
 for o in production.objects:
  if len(o.data.polygons)>200:
   bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;d=o.modifiers.new('LOD'+str(level),'DECIMATE');d.ratio=ratio;bpy.ops.object.modifier_apply(modifier=d.name)
 export('Thorax_LOD'+str(level)+'.fbx')
print('AUTHORED_LUMENS',len(report),flush=True)
