"""Build the editable station chair and its probe-lit Unity material batches."""
import math
import shutil
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[1]
MASTER = ROOT / "blender/Laboratory_MASTER.blend"
COLLECTION = "Operating room - adjustable chair"
PIVOT = (4.50, .80, 0)


def rounded_outline(width, height, radius):
    """Counterclockwise rounded rectangle, measured from its centre."""
    result = []
    for quadrant, (sx, sy) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        for step in range(5):
            angle = (quadrant + step / 4) * math.pi / 2
            result.append((sx * (width - radius) + radius * math.cos(angle),
                           sy * (height - radius) + radius * math.sin(angle)))
    return result


def add_chair(collection, pivot):
    parts = []

    def rings(name, sections, finish, wrap=False):
        count = len(sections[0])
        vertices = [Vector(p) for section in sections for p in section]
        faces = []
        for level in range(len(sections) if wrap else len(sections) - 1):
            lower = level * count
            upper = ((level + 1) % len(sections)) * count
            for i in range(count):
                j = (i + 1) % count
                faces.append((lower + i, lower + j, upper + j, upper + i))
        if not wrap:
            faces.extend((tuple(reversed(range(count))), tuple(range(len(vertices) - count, len(vertices)))))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vertices, [], faces)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        uv = mesh.uv_layers.new(name="Surface metres")
        for face in mesh.polygons:
            face.use_smooth = len(face.vertices) == 4
            axis = max(range(3), key=lambda k: abs(face.normal[k]))
            a, b = ((1, 2), (0, 2), (0, 1))[axis]
            for loop in face.loop_indices:
                point = mesh.vertices[mesh.loops[loop].vertex_index].co
                uv.data[loop].uv = (point[a], point[b])
        centre = sum(vertices, Vector()) / len(vertices)
        mesh.transform(Matrix.Translation(-centre))
        obj = bpy.data.objects.new("Lab chair - " + name, mesh)
        collection.objects.link(obj)
        parts.append(obj)
        obj.parent = pivot
        obj.location = centre
        mesh.materials.append(bpy.data.materials[finish])
        return obj

    def cylinder(name, a, b, radius, finish, sides=12, bevel=0):
        start, end = Vector(a), Vector(b)
        axis = (end - start).normalized()
        tangent = axis.cross(Vector((1, 0, 0)) if abs(axis.x) < .8 else Vector((0, 1, 0))).normalized()
        bitangent = axis.cross(tangent)
        circle = [math.cos(i * math.tau / sides) * tangent + math.sin(i * math.tau / sides) * bitangent
                  for i in range(sides)]
        profile = ((start, radius), (end, radius))
        if bevel:
            profile = ((start, radius - bevel), (start + axis * bevel, radius),
                       (end - axis * bevel, radius), (end, radius - bevel))
        rings(name, [[centre + r * direction for direction in circle] for centre, r in profile], finish)

    def beam(name, direction, stations):
        # The changing section follows the bend in each formed metal part.
        side = Vector((-direction.y, direction.x, 0))
        sections = []
        for i, (radius, z, width, depth) in enumerate(stations):
            prev, following = stations[max(0, i - 1)], stations[min(len(stations) - 1, i + 1)]
            dr, dz = following[0] - prev[0], following[1] - prev[1]
            normal = (-direction * dz + Vector((0, 0, dr))).normalized()
            centre = direction * radius + Vector((0, 0, z))
            sections.append([centre + side * width * u + normal * depth * v for u, v in
                             ((.72, 1), (-.72, 1), (-1, .54), (-1, -.54),
                              (-.72, -1), (.72, -1), (1, -.54), (1, .54))])
        rings(name, sections, "Stainless")

    outline = rounded_outline(.232, .209, .104)
    seat = []
    for scale, height in ((.69, .488), (.94, .493), (1, .508), (1, .523),
                          (.97, .537), (.76, .544), (.36, .539)):
        section = []
        for x, y in outline:
            x = x * scale * (1 - .11 * max(0, y / .209))
            y *= scale
            z = height + .016 * (x / .232) ** 2 - .014 * max(0, -y / .209) ** 3
            section.append((x, y, z))
        seat.append(section)
    rings("moulded waterfall seat", seat, "Guard")
    pan = rounded_outline(.197, .165, .076)
    rings("underside pan", [[(x * scale, y * scale, z) for x, y in pan] for scale, z in
                            ((.64, .459), (.93, .473), (1, .491), (.98, .504))], "Black")

    # A closed shell with a real through-grip and a forward-curved lumbar surface.
    back = []
    for width, height, radius, centre_z, offset in (
        (.205, .158, .065, .867, -.003), (.201, .154, .061, .867, -.013),
        (.179, .135, .050, .875, -.021), (.129, .074, .032, .913, -.024),
        (.084, .023, .018, .953, -.022), (.078, .017, .013, .953, -.016),
        (.078, .017, .013, .953, .014), (.084, .022, .017, .953, .020),
        (.144, .090, .040, .903, .024), (.198, .153, .060, .867, .019),
        (.205, .158, .065, .867, .008),
    ):
        section = []
        for x, h in rounded_outline(width, height, radius):
            z = centre_z + h
            x *= 1 - .12 * max(0, (.85 - z) / .15)
            y = .186 + .15 * (z - .71) - .042 * (x / .205) ** 2 - .014 * math.exp(-((z - .79) / .065) ** 2)
            section.append((x, y + offset, z))
        back.append(section)
    rings("lumbar back and open hand grip", back, "Guard", wrap=True)
    beam("formed back support", Vector((0, 1, 0)),
         ((.045, .457, .026, .009), (.153, .449, .025, .010), (.207, .474, .023, .010),
          (.239, .548, .022, .009), (.234, .653, .021, .009), (.208, .735, .023, .008),
          (.208, .798, .033, .009)))
    for x in (-.018, .018):
        cylinder("back fixing", (x, .206, .772), (x, .221, .772), .005, "Black", 8)

    cylinder("height adjustment paddle stem", (.035, -.01, .472), (.173, -.095, .431),
             .006, "Stainless", 8)
    paddle = rounded_outline(.039, .018, .013)
    rings("height adjustment paddle", [[(.184 + x * scale, -.102 + y * scale - .28 * x, z)
                                       for x, y in paddle] for scale, z in
                                      ((.86, .421), (1, .426), (1, .435), (.86, .439))], "Black")
    cylinder("gas lift barrel", (0, 0, .126), (0, 0, .340), .038, "Black", 16)
    cylinder("exposed lift piston", (0, 0, .329), (0, 0, .475), .024, "Stainless", 16)
    cylinder("five-star hub", (0, 0, .110), (0, 0, .176), .055, "Stainless", 16, .004)
    cylinder("foot ring clamp", (0, 0, .246), (0, 0, .276), .044, "Black", 14)

    foot_ring = []
    for i in range(32):
        angle = math.tau * i / 32
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        foot_ring.append([radial * (.191 + .009 * math.cos(j * math.tau / 6)) +
                          Vector((0, 0, .259 + .009 * math.sin(j * math.tau / 6))) for j in range(6)])
    rings("continuous foot ring", foot_ring, "Stainless", wrap=True)
    for i in range(3):
        angle = math.tau * i / 3
        direction = Vector((math.cos(angle), math.sin(angle), 0))
        cylinder("foot ring stay", direction * .034 + Vector((0, 0, .256)),
                 direction * .190 + Vector((0, 0, .259)), .007, "Stainless", 8)

    for i in range(5):
        angle = math.radians(13) + math.tau * i / 5
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        across = Vector((-radial.y, radial.x, 0))
        beam("tapered base arm", radial, ((.021, .149, .031, .023), (.083, .143, .030, .022),
                                          (.203, .111, .022, .015), (.275, .091, .019, .013)))
        pin = radial * .272
        axle = radial * .288 + Vector((0, 0, .032))
        cylinder("swivel caster fork", pin + Vector((0, 0, .092)), axle, .014, "Black", 10)
        for sign in (-1, 1):
            tyre = axle + across * sign * .016
            cylinder("twin caster tyre", tyre - across * .008, tyre + across * .008,
                     .032, "Black", 12, .003)
        cylinder("caster axle", axle - across * .025, axle + across * .025,
                 .009, "Stainless", 8)

    return parts


def build():
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    bpy.context.preferences.filepaths.save_version = 0
    old = bpy.data.collections.get(COLLECTION) or bpy.data.collections.get("Covered station C - adjustable lab chair")
    if old:
        for obj in list(old.objects):
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh and not mesh.users:
                bpy.data.meshes.remove(mesh)
        bpy.data.collections.remove(old)
    collection = bpy.data.collections.new(COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    pivot = bpy.data.objects.new("Lab chair - station pivot", None)
    collection.objects.link(pivot)
    pivot.empty_display_size = .10
    pivot.location = PIVOT
    pivot.rotation_euler.z = math.radians(-78)
    add_chair(collection, pivot)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER))
    batches = []
    for finish in ("Guard", "Stainless", "Black"):
        group = [obj for obj in collection.objects if obj.type == "MESH" and obj.data.materials[0].name == finish]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        batch = group[0]
        bpy.context.view_layer.objects.active = batch
        bpy.ops.object.join()
        placement = Matrix.Translation(PIVOT)
        batch.data.transform(placement.inverted() @ batch.matrix_world)
        batch.parent = None
        batch.matrix_world = placement
        batch.name = batch.data.name = "Lab_Chair_" + finish
        batches.append(batch)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in batches:
        obj.select_set(True)
    output = ROOT / "exports/LaboratoryChair.fbx"
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={"MESH"}, axis_forward="-Z",
                             axis_up="Y", apply_unit_scale=True, bake_anim=False, path_mode="STRIP")
    shutil.copyfile(output, ROOT / "unity/ThoraxLab/Assets/ThoraxLab/Models" / output.name)


if __name__ == "__main__":
    build()
