import bpy,json,bmesh
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'));objects=list(bpy.data.collections['THORAX_PRODUCTION'].objects);reports=[]
for level,ratio in [(1,.48),(2,.45)]:
 for o in objects:
  if len(o.data.polygons)>200:
   bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;d=o.modifiers.new('LOD'+str(level),'DECIMATE');d.ratio=ratio;bpy.ops.object.modifier_apply(modifier=d.name)
  bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(ROOT/'exports'/('Thorax_LOD'+str(level)+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_custom_props=True)
 reports.append({'level':level,'objects':len(objects),'triangles':sum(len(o.data.polygons) for o in objects),'role':'intact display only; cutting always uses LOD0'})
(ROOT/'qa/reports/lods.json').write_text(json.dumps(reports,indent=2));print(reports)
