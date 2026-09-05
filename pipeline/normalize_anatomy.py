import bpy,json,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'pipeline'))
from native_mesh import normalize,clear_point,resolve
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
objects={o['id']:o for o in bpy.data.collections['THORAX_PRODUCTION'].objects};report=[]
def replace(o,data):
 v,f=data;m=bpy.data.meshes.new(o.name+'_normalized');m.from_pydata(v,[],f);m.update()
 for mat in o.data.materials:m.materials.append(mat)
 o.data=m
for o in objects.values():
 if o.get('tissue')=='airway':
  replace(o,resolve(o));report.append({'id':o['id'],'operation':'union source envelope components minus union authored voids; resolves intersecting branch wall shells while preserving voids'})
for sample in json.loads((ROOT/'qa/reports/lumen-verification.json').read_text())['samples']:
 if sample['empty'] or not sample['inside_region']:continue
 o=objects[sample['id']];x,y,z=sample['source_sample_mm'];point=o.matrix_world.inverted()@Vector((x*.001,(z-1212.5)*.001-1.35,-y*.001+.935))
 try:replace(o,clear_point(o,point));report.append({'id':o['id'],'operation':'local authored lumen clearance at source sample','source_point_mm':[x,y,z],'clearance_box_mm':[.7,.7,.8]})
 except Exception as ex:report.append({'id':o['id'],'failure':str(ex)})
for o in objects.values():
 before=len(o.data.polygons);replace(o,normalize(o));report.append({'id':o['id'],'operation':'Manifold normalization without hole filling','before_triangles':before,'after_triangles':len(o.data.polygons)})
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
 for p in o.data.polygons:p.use_smooth=True
(ROOT/'qa/reports/geometry-normalization.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'));print('NORMALIZED',len(objects),flush=True)
