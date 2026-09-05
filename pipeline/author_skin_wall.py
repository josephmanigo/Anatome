"""Give the source skin envelope an explicit authored dermal wall and empty interior."""
import bpy,bmesh,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
o=next(o for o in bpy.data.collections['THORAX_PRODUCTION'].objects if o['id']=='FMA7163')
if not o.get('authored_skin_wall'):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 mod=o.modifiers.new('Authored dermal wall 1.8 mm','SOLIDIFY');mod.thickness=.0018;mod.offset=-1;mod.use_even_offset=True;mod.thickness_clamp=.3;bpy.ops.object.modifier_apply(modifier=mod.name)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();o['authored_skin_wall']=True
 catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];r=next(r for r in catalog if r['id']=='FMA7163');r['source_kind']='source_mesh_with_authored_wall';r['source_record']='docs/skin-wall.json';r['notes']+=' Source skin was an enclosing boundary. A 1.8 mm inward dermal wall with local offset clamp now leaves the interior empty. Thickness and regional closures are authored teaching approximations, not segmented histology.';o['source_kind']=r['source_kind'];o['provenance_notes']=r['notes']
 (ROOT/'docs/skin-wall.json').write_text(json.dumps({'id':r['id'],'source_meshes':r['mesh_ids'],'method':'inward Solidify shell, 1.8 mm with local clamp, on the positive-volume normalized source envelope','regional_boundaries':'Existing thorax inlet/inferior closure faces retained as explicit regional treatment; these are not natural skin surfaces.','limitation':'Author-selected dermal thickness; no histological thickness segmentation.','validation':'Native topology and real shallow/curved/grazing/miss strokes rerun on this actual hollow skin volume.'},indent=2));(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
print('SKIN_WALL_READY',flush=True)
