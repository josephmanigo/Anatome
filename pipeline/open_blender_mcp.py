"""Open the handoff master with the already-installed Blender MCP add-on."""
import bpy,addon_utils
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
addon_utils.enable('blender_mcp',default_set=False,persistent=False)
if not getattr(bpy.types,'blendermcp_server',None) or not bpy.types.blendermcp_server.running:bpy.ops.blendermcp.start_server()
for o in bpy.data.collections['THORAX_PRODUCTION'].objects:
 o.hide_set(bool(o.get('reference_region',False)) or o.get('layer',0)<3 or o.get('tissue')=='membrane')
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.shading.type='MATERIAL';region=area.spaces.active.region_3d;region.view_location=Vector((0,-1.35,1.04));region.view_distance=.65;region.view_rotation=(Vector((0,-1.35,1.04))-Vector((.45,-1.85,1.65))).to_track_quat('-Z','Y');region.view_perspective='PERSP'
print('THORAX_MCP_READY',bpy.data.filepath,flush=True)
