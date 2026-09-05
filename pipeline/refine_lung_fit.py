"""Apply the measured common lung adaptation to production only, then create final review views."""
import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
production=bpy.data.collections['THORAX_PRODUCTION'];catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records'];records={r['id']:r for r in catalog}
proposal=json.loads((ROOT/'qa/reports/lung-fit-proposal.json').read_text());center=Vector(proposal['center_world_m']);translation=Vector(proposal['proposed_translation_world_m']);scale=proposal['proposed_uniform_scale'];changed=[]
for o in production.objects:
 r=records[o['id']]
 if not (r['id'].startswith('MAL_') or 'pleura' in r['name'].lower()):continue
 if o.get('lung_cage_fit'):continue
 inverse=o.matrix_world.inverted()
 for v in o.data.vertices:v.co=inverse@((o.matrix_world@v.co-center)*scale+center+translation)
 o['lung_cage_fit']=True;changed.append(r['id'])
 r['notes']+=' Production teaching adaptation: the complete five-lobe set shares a further uniform scale of '+str(round(scale,6))+' and translation against the BP3D cage envelope. Original MAL specimen remains separate and unchanged. See docs/lung-production-adaptation.json. This geometric fit does not validate hilar alignment.'
 o['provenance_notes']=r['notes']
if changed:
 proposal['applied_to']=changed;proposal['method']=proposal['method'].replace('Read-only conservative cage-envelope fit proposal, no geometry edited.','Applied common production lung adaptation; source meshes remain immutable.')
 (ROOT/'docs/lung-production-adaptation.json').write_text(json.dumps(proposal,indent=2));(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'blender/Thorax_MASTER.blend'))
scene=bpy.context.scene;scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.cycles.samples=24;scene.view_settings.exposure=-2.0
target=Vector((0,-1.35,1.035))
views=[('anterior',(0,-1.35,2.0),'open'),('organs',(0,-1.35,2.0),'organs'),('posterior',(0,-1.35,.10),'open'),('left_lateral',(.85,-1.35,1.05),'open'),('superior',(0,-.5,1.07),'open'),('intact',(0,-1.35,2.0),'intact')]
for name,loc,mode in views:
 for o in production.objects:
  r=records[o['id']];o.hide_render=r.get('reference_region',False) or (mode!='intact' and (r['layer']<3 or r['tissue']=='membrane')) or (mode=='organs' and r['system'] in ('Skeletal','Muscular'))
 scene.camera.location=loc;scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=.57
 scene.render.filepath=str(ROOT/'qa/renders'/('thorax-'+name+'.png'));bpy.ops.render.render(write_still=True)
print('LUNG_FIT_AND_VIEWS_COMPLETE',changed,flush=True)
