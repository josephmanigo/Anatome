"""Correct hidden-source transform evaluation for cardiac empty-space annotations."""
import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];objects={o['id']:o for o in bpy.data.collections['THORAX_PRODUCTION'].objects};offset=Vector((0,-1.35,.935));changes=[]
for r in catalog:
 if not r.get('reference_region') or r['id']=='REGION_InterventricularSeptum':continue
 o=objects[r['id']]
 if o.get('source_offset_corrected'):continue
 # Hidden source collection was not dependency-graph evaluated. Its vertices
 # were still in regional local coordinates when these annotations were built.
 o.location+=offset;o['source_offset_corrected']=True;r['notes']+=' Source regional placement explicitly applied; see qa/reports/cardiac-annotation-placement.json.';changes.append(r['id'])
 bpy.context.view_layer.update()
report={'reason':'Hidden source geometry matrix_world was unevaluated during annotation authoring. Explicit source placement offset restored. Tissue geometry was unaffected.','source_offset_m':list(offset),'corrected':changes,'bounds':[]}
for r in catalog:
 if r.get('reference_region'):
  o=objects[r['id']];v=[o.matrix_world@p.co for p in o.data.vertices];report['bounds'].append({'id':r['id'],'min':[min(p[i] for p in v) for i in range(3)],'max':[max(p[i] for p in v) for i in range(3)]})
(ROOT/'qa/reports/cardiac-annotation-placement.json').write_text(json.dumps(report,indent=2));(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'));print('ANNOTATIONS_CORRECTED',changes)
