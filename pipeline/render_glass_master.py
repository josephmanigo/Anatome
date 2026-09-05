"""Match editable cabinet glazing to the transparent runtime material."""
import bpy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Laboratory_MASTER.blend'))
m=bpy.data.materials['Glass'];bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Alpha'].default_value=.20;bs.inputs['Transmission Weight'].default_value=.55;bs.inputs['Roughness'].default_value=.16
m.diffuse_color=(*m.diffuse_color[:3],.20)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Laboratory_MASTER.blend'))
bpy.context.scene.render.filepath=str(ROOT/'qa/renders/laboratory-overview.png');bpy.ops.render.render(write_still=True)
print('GLASS_MASTER_REVIEW_COMPLETE',flush=True)
