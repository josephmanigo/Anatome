"""Create reference-only views of acquired MAL lobes; never save source edits."""
import sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
import bpy
from mathutils import Vector
from vtk_reader import read_vtk_polydata

bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
colors=[('superior lobe of right lung',(0.68,.30,.30,1)),('middle lobe of right lung',(.93,.59,.27,1)),
 ('inferior lobe of right lung',(.40,.30,.62,1)),('superior lobe of left lung',(.37,.64,.55,1)),
 ('inferior lobe of left lung',(.26,.46,.72,1))]
for name,color in colors:
    vertices,faces=read_vtk_polydata(ROOT/'models'/(name+'.vtk'))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata((vertices*.001).tolist(),[],faces.tolist());mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.color=color
    for face in mesh.polygons:face.use_smooth=True
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO';scene.display.shading.color_type='OBJECT'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
scene.display.shading.cavity_type='BOTH';scene.display.shading.background_type='WORLD';scene.world.color=(.87,.87,.87)
scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100
camdata=bpy.data.cameras.new('Source inspection');cam=bpy.data.objects.new('Source inspection',camdata);scene.collection.objects.link(cam)
scene.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=.34
for name,position in [('anterior',(0,.6,.035)),('posterior',(0,-.6,.035)),('left_lateral',(-.6,0,.035))]:
    cam.location=position;cam.rotation_euler=(Vector((0,0,.035))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(ROOT/('lobes-'+name+'.png'));bpy.ops.render.render(write_still=True)
