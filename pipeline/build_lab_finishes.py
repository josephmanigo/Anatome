"""Seamless laboratory finishes. RGB is sRGB albedo and alpha is linear smoothness."""
from functools import lru_cache
from pathlib import Path
from tempfile import TemporaryDirectory

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "unity/ThoraxLab/Assets/ThoraxLab/Laboratory"
RESOLUTION = 4096
TAU = np.float32(2 * np.pi)


@lru_cache(maxsize=None)
def lattice(seed, nx, ny):
    values = np.random.default_rng(seed).random((ny, nx), dtype=np.float32)
    values *= 2
    values -= 1
    return np.pad(values, ((0, 1), (0, 1)), mode="wrap")


def grain(seed, nx, ny, u, v):
    """Sample one periodic smooth lattice, including oblique or warped coordinates."""
    def axis(coord, cells):
        pos = coord * cells
        lower = np.floor(pos)
        t = pos - lower
        return lower.astype(np.int32) % cells, t * t * (3 - 2 * t)

    ix, tx = axis(u, nx)
    iy, ty = axis(v, ny)
    knots = lattice(seed, nx, ny)
    top = knots[iy, ix] * (1 - tx) + knots[iy, ix + 1] * tx
    bottom = knots[iy + 1, ix] * (1 - tx) + knots[iy + 1, ix + 1] * tx
    return top + (bottom - top) * ty


def smoothstep(low, high, value):
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def resin(u, v):
    bend = grain(11, 4, 5, u, v)
    flow = grain(12, 9, 27, u + .033 * bend, v + .074 * bend)
    veil = grain(13, 21, 92, u, v + .026 * bend)
    fines = grain(14, 925, 1093, u, v)
    sand = grain(15, 1831, 1613, u, v)
    chips = 7 * smoothstep(.34, .77, fines) - 9 * smoothstep(.38, .83, -fines)
    tone = 5.0 * bend + 3.7 * flow + 1.8 * veil + chips + 1.8 * sand
    gloss = 214 + 4 * flow + 2 * veil + 2.5 * fines + 1.5 * sand
    # The clear resin levels most of the aggregate below its top surface.
    height = 1.1 * veil + .64 * fines + .15 * sand + .13 * chips
    return tone, gloss, height


def ceramic(u, v, scrub=False):
    cols, rows = (2, 4) if scrub else (4, 4)
    sx = u * cols + (.5 * (np.floor(v * rows) % 2) if scrub else 0)
    sy = v * rows
    tx, ty = sx % 1, sy % 1
    edge = np.minimum(np.minimum(tx, 1 - tx) * (RESOLUTION / cols),
                      np.minimum(ty, 1 - ty) * (RESOLUTION / rows))
    seed = 30 if scrub else 20
    pigment = grain(seed, 15, 12, u, v)
    orange_peel = grain(seed + 1, 317, 293, u, v)
    grit = grain(seed + 2, 1253, 1327, u, v)
    face = smoothstep(5.4, 16.3, edge) if scrub else smoothstep(2.7, 9.2, edge)
    shoulder = smoothstep(4, 30 if scrub else 22, edge)
    tile_tone = lattice(seed + 3, cols, rows)[np.floor(sy).astype(np.int32) % rows,
                                                np.floor(sx).astype(np.int32) % cols]
    enamel = 3.8 * pigment + 4.2 * tile_tone + 1.45 * orange_peel + .55 * grit
    mortar = (-35 if scrub else 23) + 5.5 * grit + 2.4 * pigment
    tone = mortar + (enamel - mortar) * face
    gloss = 63 + 6 * grit + face * ((142 if scrub else 115) + 5 * pigment + 3 * orange_peel - 6 * grit)
    height = (9.5 if scrub else 6.3) * face + 2.0 * shoulder + .55 * orange_peel + (1.0 - .77 * face) * grit
    return tone, gloss, height


def nonwoven(u, v):
    formation = grain(41, 17, 21, u, v)
    meander = grain(42, 29, 26, u, v)
    # Short, crossing spunbond filaments retain irregular directions between thermal bond points.
    strands_a = grain(43, 1683, 183, u + v + .0036 * meander, v)
    strands_b = grain(44, 167, 1811, u, v - u + .0034 * formation)
    strands_c = grain(45, 1331, 131, u + .004 * formation, v)
    fibres = np.maximum(0, strands_a - .14) + .84 * np.maximum(0, strands_b - .13) + .53 * strands_c
    row = np.floor(v * 206)
    bx = ((u * 196 + .5 * (row % 2)) % 1 - .5) / .126
    by = ((v * 206) % 1 - .5) / .187
    bond = 1 - smoothstep(.43, 1.04, bx * bx + by * by)
    fibres *= 1 - .67 * bond

    def crease(phase, width):
        d = ((phase + .5) % 1 - .5) / width
        return np.exp(-d * d) - .36 * np.exp(-((d - 1.75) / 1.7) ** 2)

    lengthwise = crease(u - .32 + .029 * np.sin(TAU * v + .72), .0066)
    crosswise = crease(v - .27 + .021 * np.sin(TAU * u + 1.35), .0091)
    diagonal = crease(v - u - .48 + .032 * np.sin(TAU * u + .65), .0068)
    folds = lengthwise + .83 * crosswise + .48 * (.62 + .38 * np.cos(TAU * v)) * diagonal
    tone = 6.1 * formation + 2.4 * meander + 7.2 * fibres - 5 * bond - 4.6 * folds
    gloss = 50 + 3.7 * formation + 6.2 * fibres + 11 * bond + 7 * folds
    height = 24 * folds + 1.5 * fibres - 1.25 * bond + 1.2 * formation
    return tone, gloss, height


def stainless(u, v):
    pass_tone = grain(51, 7, 96, u, v)
    drift = .00085 * grain(52, 5, 11, u, v)
    brush = grain(53, 19, 1831, u, v + drift)
    hairline = grain(54, 84, 1573, u, v + .76 * drift)
    satin = grain(55, 129, 613, u, v)
    furrow = smoothstep(.35, .82, -hairline)
    tone = 3.4 * pass_tone + 6.8 * brush + 2 * satin - 5.8 * furrow
    gloss = 166 + 8 * pass_tone + 18 * brush + 4 * satin - 10 * furrow
    height = .9 * brush + .23 * satin - .54 * furrow
    return tone, gloss, height


def linen(u, v):
    sx = u * 448 + .18 * grain(61, 26, 37, u, v)
    sy = v * 496 + .17 * grain(62, 31, 27, u, v)
    warp = np.sin(np.pi * (sx % 1)) * (.78 + .30 * np.cos(np.pi * (np.floor(sx) + sy - .5)))
    weft = np.sin(np.pi * (sy % 1)) * (.78 - .30 * np.cos(np.pi * (np.floor(sy) + sx - .5)))
    yarns = np.maximum(warp, weft)
    slub = grain(63, 789, 136, u, v) + .8 * grain(64, 141, 883, u, v)
    dye = grain(65, 34, 29, u, v)
    tone = 12 * (yarns - .7) + 3 * slub + 2.6 * dye
    gloss = 31 + 13 * yarns + 4.2 * slub
    height = 3.0 * yarns + .34 * slub
    return tone, gloss, height


def ceiling(u, v):
    u, v = u % 1, v % 1
    edge = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v)) * RESOLUTION
    face = smoothstep(5, 17, edge)
    cloud = grain(71, 21, 19, u, v)
    stipple = grain(72, 637, 691, u, v)
    grain_tip = grain(73, 1417, 1531, u, v)
    pores = smoothstep(.40, .84, grain_tip)
    tone = 1.8 * cloud + 2.8 * stipple + .95 * grain_tip - 5.3 * pores - 34 * (1 - face)
    gloss = 57 + 4 * stipple - 5 * pores + 25 * (1 - face)
    height = 5.7 * face + .68 * stipple + .18 * grain_tip - .53 * pores
    return tone, gloss, height


def diffuser(u, v):
    # A half-metre sheet repeats every 100 holes and 116 staggered rows (approximately 5 mm pitch).
    row = np.floor(v * 116)
    dx = ((u * 100 + .5 * (row % 2)) % 1 - .5) * (RESOLUTION / 100)
    dy = ((v * 116) % 1 - .5) * (RESOLUTION / 116)
    radius = np.sqrt(dx * dx + dy * dy)
    opening = 1 - smoothstep(8.0, 9.4, radius)
    lip = np.exp(-((radius - 10.3) / 1.45) ** 2)
    steel = 1.5 * grain(81, 27, 621, u, v) + 1.1 * grain(82, 533, 539, u, v)
    return steel - 75 * opening + 7 * lip, 143 + 2 * steel - 72 * opening + 17 * lip, None


def equipment_coating(u, v):
    flow = grain(91, 53, 57, u, v)
    peel = grain(92, 531, 607, u + .0012 * flow, v - .0009 * flow)
    powder = grain(93, 1423, 1541, u, v)
    crown = smoothstep(.10, .69, peel + .17 * powder)
    tone = .64 * flow + 1.43 * peel + .74 * powder + .48 * (crown - .27)
    gloss = np.clip(152 + 1.6 * flow + 5.5 * peel + 3.2 * powder, 142, 162)
    height = .78 * peel + .30 * powder + .39 * crown + .60 * flow
    return tone, gloss, height


def check_repeat(surface):
    edge = (np.arange(-24, 24, dtype=np.float32)[None, :] + .5) / RESOLUTION
    along = (np.arange(19, dtype=np.float32)[:, None] * 193 + 15.5) / RESOLUTION
    for u, v in ((edge, along), (along.T, edge.T)):
        for raw, wrapped in zip(surface(u, v), surface(u % 1, v % 1)):
            if raw is not None:
                assert np.max(np.abs(raw - wrapped)) < .065, "Discontinuous texture repeat"


def write_finish(directory, name, normal_name, color, gains, surface):
    """Use a one-row halo for central differences, including the first and last scanlines."""
    check_repeat(surface)
    n = RESOLUTION
    u = (np.arange(n, dtype=np.float32)[None, :] + .5) / n
    albedo = Image.new("RGBA", (n, n))
    normals = Image.new("RGB", (n, n)) if normal_name else None
    for start in range(0, n, 96):
        stop = min(start + 96, n)
        v = ((np.arange(start - 1, stop + 1, dtype=np.float32)[:, None] + .5) % n) / n
        tone, gloss, height = surface(u, v)
        assert np.isfinite(tone).all() and np.isfinite(gloss).all()
        packed = np.empty((stop - start, n, 4), dtype=np.uint8)
        for channel, (base, gain) in enumerate(zip(color, gains)):
            packed[..., channel] = np.rint(np.clip(base + gain * tone[1:-1], 0, 255))
        packed[..., 3] = np.rint(np.clip(gloss[1:-1], 0, 255))
        albedo.paste(Image.fromarray(packed), (0, start))
        if normals is not None:
            assert height.shape == (stop - start + 2, n) and np.isfinite(height).all()
            band = height[1:-1]
            dx = .5 * (np.roll(band, 1, axis=1) - np.roll(band, -1, axis=1))
            dy = .5 * (height[2:] - height[:-2])  # PNG rows descend, tangent V ascends.
            inv = 1 / np.sqrt(1 + dx * dx + dy * dy)
            normal = np.stack((dx * inv, dy * inv, inv), axis=2)
            normals.paste(Image.fromarray(np.rint((normal + 1) * 127.5).astype(np.uint8)), (0, start))
    lattice.cache_clear()
    for label, bitmap in ((name, albedo), (normal_name, normals)):
        if bitmap is None:
            continue
        path = directory / (label + ".png")
        bitmap.save(path, compress_level=5)
        bitmap.close()
        path.replace(OUT / path.name)
    print(name, flush=True)


EQUIPMENT_COATING = ("Medical equipment coating", "Medical equipment coating normal",
                     (238, 239, 239), (1, 1, 1), equipment_coating)


def theatre_finishes():
    materials = (
        ("Theatre resin", "Theatre resin normal", (63, 97, 132), (.93, 1, 1.02), resin),
        ("Theatre ceramic panels", "Theatre ceramic normal", (82, 104, 127), (1.02, 1, .96), ceramic),
        ("Surgical nonwoven", "Surgical textile normal", (23, 149, 178), (1.03, .95, .89), nonwoven),
        ("Brushed stainless", "Brushed alloy normal", (194, 201, 205), (1, .98, .96), stainless),
        ("Glazed scrub tile", "Glazed scrub tile normal", (185, 203, 199), (1, .98, .95), lambda u, v: ceramic(u, v, scrub=True)),
        ("Woven linen", "Woven linen normal", (220, 224, 222), (1.02, 1, .95), linen),
        ("Sealed ceiling tile", "Sealed ceiling tile normal", (220, 224, 224), (1.02, 1, .96), ceiling),
        ("Perforated diffuser", None, (178, 189, 194), (1.04, 1, .97), diffuser),
        EQUIPMENT_COATING,
    )
    # Unity ignores dot directories. Importers see each completed PNG only after its rename.
    with TemporaryDirectory(dir=OUT, prefix=".finishes-") as staging:
        for material in materials:
            write_finish(Path(staging), *material)


def monitor_atlas():
    from PIL import ImageDraw, ImageFont
    import sys

    # Display meshes and equipment colour pickups use this 1024 x 512 coordinate system.
    pixels = RESOLUTION // 1024
    background, bone, amber = (8, 20, 30), (180, 201, 213), (219, 184, 100)
    atlas = Image.new("RGB", (RESOLUTION, RESOLUTION // 2), background)
    draw = ImageDraw.Draw(atlas)

    @lru_cache(maxsize=None)
    def font(size):
        try:
            return ImageFont.truetype("DejaVuSans.ttf", size * pixels)
        except OSError:
            return ImageFont.load_default(size=size * pixels)

    def rectangle(x0, y0, x1, y1, fill=None, outline=None, width=1):
        bounds = (x0 * pixels, y0 * pixels, (x1 + 1) * pixels - 1, (y1 + 1) * pixels - 1)
        draw.rectangle(bounds, fill=fill, outline=outline, width=width * pixels)

    def label(x, y, value, size=13, color=(172, 209, 221)):
        draw.text((x * pixels, y * pixels), value, font=font(size), fill=color)

    def header(x, y, title):
        rectangle(x + 4, y + 4, x + 507, y + 251, outline=(59, 88, 104), width=2)
        rectangle(x + 6, y + 6, x + 505, y + 32, fill=(24, 46, 60))
        label(x + 15, y + 9, title, 14, (210, 232, 240))
        label(x + 435, y + 10, "DEMO", 12, (91, 201, 219))

    def wave(x, y, w, h, kind, color):
        for i in range(6):
            xx = (x + i * w / 5) * pixels
            draw.line((xx, (y - h) * pixels, xx, (y + h) * pixels), fill=(26, 44, 54), width=pixels)
        draw.line((x * pixels, y * pixels, (x + w) * pixels, y * pixels), fill=(35, 52, 63), width=pixels)
        sample = np.arange(w * pixels)
        phase = sample / (pixels * (72 if kind == 0 else 101)) % 1
        if kind == 0:
            components = ((.13, .16, .045), (-.18, .32, .023), (1, .37, .020), (-.33, .42, .026), (.22, .67, .085))
            value = sum(a * np.exp(-((phase - b) / c) ** 2) for a, b, c in components)
        else:
            value = .56 * np.sin(phase * np.pi) ** 3 + .18 * np.sin(phase * 2 * np.pi) ** 3
        draw.line(list(zip(x * pixels + sample, (y - h * value) * pixels)), fill=color, width=2 * pixels)

    header(0, 0, "PATIENT MONITOR   /   SIMULATION")
    for row, (name, value, color) in enumerate((("ECG", "72", (110, 218, 134)),
                                                 ("SpO2", "98", (80, 192, 225)), ("RESP", "16", amber))):
        yy = 62 + row * 63
        label(14, yy - 20, name, 12, color)
        wave(60, yy + 7, 316, 21, 0 if row == 0 else 1, color)
        label(416, yy - 22, value, 39, color)
    label(15, 231, "SIMULATED SIGNALS", 11, (112, 151, 170))
    header(512, 0, "THORAX ATLAS   /   AXIAL SECTIONS")
    reference = Path(__file__).resolve().parents[1] / "source/references/open-anatomy"
    sys.path.insert(0, str(reference))
    from vtk_reader import read_vtk_polydata

    models = []
    for name, color in (("superior lobe of right lung", (62, 90, 111)), ("middle lobe of right lung", (68, 94, 113)),
                        ("inferior lobe of right lung", (65, 89, 106)), ("superior lobe of left lung", (61, 88, 110)),
                        ("inferior lobe of left lung", (67, 93, 111)), ("heart", (117, 142, 160)),
                        ("aorta", (151, 176, 189)), ("vertebra", bone), ("ribs", (177, 200, 213)),
                        ("body of sternum", (171, 195, 206))):
        vertices, faces = read_vtk_polydata(reference / "models" / (name + ".vtk"))
        models.append((vertices[faces], color))
    lungs = np.concatenate([item[0].reshape(-1, 3) for item in models[:5]])
    xy = lungs[:, :2]
    centre = (xy.min(0) + xy.max(0)) * .5
    scale = 185 / max(np.ptp(xy, axis=0))
    for index, fraction in enumerate((.43, .65)):
        z = lungs[:, 2].min() + np.ptp(lungs[:, 2]) * fraction
        cx = 648 + index * 232
        for triangles, color in models:
            cuts = triangles[(triangles[:, :, 2].min(1) < z) & (triangles[:, :, 2].max(1) > z)]
            segments = []
            for tri in cuts:
                crossing = []
                for a, b in ((tri[0], tri[1]), (tri[1], tri[2]), (tri[2], tri[0])):
                    if (a[2] < z) != (b[2] < z):
                        p = a + (b - a) * ((z - a[2]) / (b[2] - a[2]))
                        crossing.append(((cx + (p[0] - centre[0]) * scale) * pixels,
                                         (142 - (p[1] - centre[1]) * scale) * pixels))
                if len(crossing) == 2:
                    segments.append(crossing)
            if not segments:
                continue
            a, b = np.asarray(segments, dtype=np.float64).transpose(1, 0, 2)
            for line in range(48 * pixels, 225 * pixels):
                yy = line + .5
                cut = (a[:, 1] <= yy) != (b[:, 1] <= yy)
                start, end = a[cut], b[cut]
                hits = np.sort(start[:, 0] + (end[:, 0] - start[:, 0]) * (yy - start[:, 1]) / (end[:, 1] - start[:, 1]))
                for left, right in zip(hits[::2], hits[1::2]):
                    x0, x1 = int(np.ceil(left - .5)), int(np.floor(right - .5))
                    if x0 <= x1:
                        draw.line((x0, line, x1, line), fill=color)
        label(cx - 36, 224, "SECTION " + str(index + 1), 12)

    header(0, 256, "ANESTHESIA WORKSTATION")
    for idx, (name, val) in enumerate((("FLOW", "4.0"), ("RATE", "12"), ("PEEP", "5"))):
        x0 = 17 + idx * 162
        rectangle(x0, 309, x0 + 151, 389, fill=(18, 40, 54), outline=(67, 102, 119))
        label(x0 + 8, 315, name, 14)
        label(x0 + 9, 339, val, 33, (148, 216, 231))
    for row in range(2):
        wave(24, 427 + row * 46, 457, 20, 1, (94, 178 + row * 21, 205))
    header(512, 256, "EQUIPMENT STATUS")
    for i, key in enumerate(("SYSTEM CHECK", "GAS SUPPLY", "CIRCUIT", "MONITOR")):
        label(535, 310 + i * 41, key, 17)
        label(891, 310 + i * 41, "READY", 15, (110, 208, 167))

    # The diathermy pedal samples the amber digit; the ultrasound samples a 1-texel span at the vertebral edge.
    rectangle(430, 185, 430, 185, fill=amber)
    ramp_left = int(639.5 * pixels)
    ramp_right = int(640.5 * pixels) - 1
    endpoints = np.array([background, bone]) / 255
    linear = np.where(endpoints <= .04045, endpoints / 12.92, ((endpoints + .055) / 1.055) ** 2.4)
    for column in range(ramp_left - 1, ramp_right + 2):
        weight = np.clip((column - ramp_left) / (ramp_right - ramp_left), 0, 1)
        light = linear[0] + weight * (linear[1] - linear[0])
        srgb = np.where(light <= .0031308, 12.92 * light, 1.055 * light ** (1 / 2.4) - .055)
        rgb = tuple(np.rint(srgb * 255).astype(int))
        draw.line((column, 185 * pixels, column, 186 * pixels - 1), fill=rgb)
    for column, expected in ((ramp_left - 1, background), (ramp_left, background),
                             (ramp_right, bone), (ramp_right + 1, bone)):
        assert atlas.getpixel((column, int(185.5 * pixels))) == expected
    with TemporaryDirectory(dir=OUT, prefix=".finishes-") as staging:
        path = Path(staging) / "Theatre monitors.png"
        atlas.save(path)
        path.replace(OUT / path.name)


def legacy_finishes():
    from build_museum_finishes import SIZE, noise, rng, x

    def save(name, color, variation, gloss):
        rgba = np.dstack((np.array(color) + variation[..., None], gloss))
        assert rgba.shape == (SIZE, SIZE, 4) and np.isfinite(rgba).all()
        Image.fromarray(np.uint8(np.clip(rgba, 0, 255))).save(OUT / (name + ".png"))

    # Sheet vinyl has tiny embedded chips and a narrow heat-welded joint on one axis.
    grains = noise(220, 230)
    chips = 26 * np.maximum(0, (grains - .28) / .72) - 43 * np.maximum(0, (-grains - .22) / .78)
    weld = np.clip(1 - np.minimum(x, SIZE - x) / 1.25, 0, 1)
    vinyl = chips + 5 * noise(23, 23) + 3 * noise(5, 5) - 18 * weld
    save("Welded sheet flooring", [156, 170, 163], vinyl, 98 + chips * .4 + weld * 18)

    # Preserve the original wall-guard noise sequence across the retired 512 px steel, linen and glaze passes.
    rng.bit_generator.advance(12 * 210 + 5 * 35 + 18 * 22 + 12 * 12 + 64 * 64)
    stipple = 2.3 * noise(130, 140) + 2 * noise(23, 23) + 1.7 * noise(7, 7)
    save("Washable coating", [237, 237, 233], stipple, 57 + stipple * 2)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    legacy_finishes()
    theatre_finishes()
    monitor_atlas()


if __name__ == "__main__":
    main()
