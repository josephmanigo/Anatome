"""Bake near-floor ambient occlusion from the actual exported operating-room geometry."""
import math
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def bake(objects, destination):
    vertices, polygons = [], []
    for obj in objects:
        if obj.name in {'Lab_Floor','Lab_Wall','Lab_Ceiling','Lab_Glass','Lab_Light','Lab_Sign',
                        'Lab_VentFace','Lab_InstrumentSteel','Lab_MonitorAtlas'}:
            continue
        start = len(vertices)
        vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
        polygons.extend(tuple(start+i for i in p.vertices) for p in obj.data.polygons)
    scene = BVHTree.FromPolygons(vertices, polygons)
    width,height = 1024,512
    mask = np.ones((height,width),dtype=np.float32)
    directions = []
    samples = 24
    for i in range(samples):
        radius=math.sqrt((i+.5)/samples)
        angle=i*math.pi*(3-math.sqrt(5))
        directions.append(Vector((radius*math.cos(angle),radius*math.sin(angle),math.sqrt(1-radius*radius))))
    radius=1.24
    for row in range(height):
        y=-4.6+9.2*(row+.5)/height
        for column in range(width):
            x=-11.2+17.3*(column+.5)/width
            pos=Vector((x,y,.008))
            if scene.find_nearest(pos,radius)[0] is None:
                continue
            occlusion=0.0
            for direction in directions:
                distance=scene.ray_cast(pos,direction,radius)[3]
                if distance is not None:
                    occlusion+=(1-distance/radius)**1.1
            mask[row,column]=max(.17,1-1.16*occlusion/samples)
    for axis in (0,1):
        mask=(np.roll(mask,-1,axis)+2*mask+np.roll(mask,1,axis))*.25
    assert mask.min()<.4 and mask.max()>.97, 'Floor contact capture lacks occluded or open samples'
    # URP's detail multiplier is twice its linear input. Neutral open floor therefore encodes 0.5.
    pix=np.ones((height,width,4),dtype=np.float32)
    pix[:,:,:3]=mask[:,:,None]*.5
    image=bpy.data.images.new('Operating room floor contact',width=width,height=height,alpha=False)
    image.colorspace_settings.name='Non-Color'
    image.pixels.foreach_set(pix.ravel())
    image.filepath_raw=str(destination)
    image.file_format='PNG'
    image.save()
    bpy.data.images.remove(image)
