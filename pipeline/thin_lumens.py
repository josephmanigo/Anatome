import bpy,bmesh,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
checks=json.loads((ROOT/'qa/reports/lumen-verification.json').read_text())['samples'];failed={p['id'] for p in checks if not p['empty'] and p['inside_region']}
catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];records={r['id']:r for r in catalog};changes=[]
for o in bpy.data.collections['THORAX_PRODUCTION'].objects:
 if o['id'] not in failed:continue
 # Previous operation was a closed-surface solidify: original vertex block first,
 # reversed inner-wall block second. Recover only the original boundary block.
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();count=len(bm.verts)
 if count%2:raise RuntimeError('Unexpected Solidify vertex layout')
 bmesh.ops.delete(bm,geom=list(bm.verts)[count//2:],context='VERTS');bm.to_mesh(o.data);bm.free()
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 thickness=.0001 if records[o['id']]['tissue']=='airway' else .0003
 mod=o.modifiers.new('Lumen-preserving teaching wall','SOLIDIFY');mod.thickness=-thickness;mod.offset=-1;mod.use_even_offset=False;mod.thickness_clamp=.1;bpy.ops.object.modifier_apply(modifier=mod.name)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 records[o['id']]['notes']+=' Initial offset failed lumen samples; replaced with '+str(thickness*1000)+' mm authored teaching wall. See qa/reports/lumen-wall-adjustments.json.'
 changes.append({'id':o['id'],'final_wall_offset_mm':thickness*1000,'reason':'preserve source section centroid voids; dimension is authored, not measured'})
(ROOT/'qa/reports/lumen-wall-adjustments.json').write_text(json.dumps(changes,indent=2));(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
