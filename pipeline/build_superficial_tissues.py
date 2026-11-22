"""Author the eight gross chest-wall tissue maps and the synthetic case insert.

RGB is sRGB colour. Specimen alpha is linear smoothness and fine surface relief
for OrganSurface.shader. No clinical images or captured donor colours are used.
"""
import argparse
from io import BytesIO
from pathlib import Path

import numpy as np
from PIL import Image


LEARNING = Path(__file__).resolve().parents[1] / "unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning"
SIZE = 512
Y, X = np.mgrid[:SIZE, :SIZE] / SIZE
FX, FY = np.meshgrid(np.fft.fftfreq(SIZE), np.fft.fftfreq(SIZE))


def noise(rng, along, across=None, angle=0):
    """Periodic Gaussian field; lengths are pixels, angle is measured in degrees."""
    radians = np.deg2rad(angle)
    a = FX * np.cos(radians) + FY * np.sin(radians)
    b = FY * np.cos(radians) - FX * np.sin(radians)
    window = np.exp(-2 * np.pi**2 * ((a * along)**2 + (b * (across or along))**2))
    field = np.fft.ifft2(np.fft.fft2(rng.normal(size=X.shape)) * window).real
    return np.clip((field - field.mean()) / field.std(), -3.3, 3.3)


def sample(field, u, v):
    """Bilinear, periodic lookup."""
    x, y = u * SIZE, v * SIZE
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    tx, ty = x - ix, y - iy
    lo = field[iy % SIZE, ix % SIZE] * (1-tx) + field[iy % SIZE, (ix+1) % SIZE] * tx
    hi = field[(iy+1) % SIZE, ix % SIZE] * (1-tx) + field[(iy+1) % SIZE, (ix+1) % SIZE] * tx
    return lo * (1-ty) + hi * ty


def lobes(rng, u, v, count):
    """Distances to the two nearest irregular centres and each centre's tone."""
    x, y = u * count, v * count
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    centers = rng.uniform(.09, .91, (count, count, 2))
    tints = rng.uniform(-1, 1, (count, count))
    first = np.full(X.shape, 20.)
    second, shade = first.copy(), np.zeros_like(X)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            cx, cy = ix + dx, iy + dy
            jitter = centers[cy % count, cx % count]
            d = (cx+jitter[..., 0]-x)**2 + (cy+jitter[..., 1]-y)**2
            closer = d < first
            second = np.minimum(second, np.maximum(first, d))
            first = np.minimum(first, d)
            shade = np.where(closer, tints[cy % count, cx % count], shade)
    d = np.sqrt(first)
    return d, np.sqrt(second) - d, shade


def colour(base, *components):
    rgb = np.zeros(X.shape + (3,)) + base
    for field, rgb_change in components:
        rgb += field[..., None] * rgb_change
    return rgb


def skin_surface(rng):
    broad, fine = noise(rng, 24, 19, 27), noise(rng, 1.5)
    u, v = X + .008*noise(rng, 18), Y + .008*noise(rng, 22)
    _, furrow_gap, _ = lobes(rng, u, v, 38)
    furrows = np.exp(-(furrow_gap/.14)**2) * np.clip(.62+.24*noise(rng, 4), .16, 1)
    # Sparse follicular ostia can be resolved up close. Individual epidermal cells cannot.
    pores = np.zeros_like(X)
    yy, xx = np.mgrid[-7:8, -7:8]
    for _ in range(420):
        x, y = rng.integers(SIZE, size=2)
        sx, sy = rng.uniform(1.0, 1.7), rng.uniform(1.1, 2.1)
        spot = np.exp(-.5*((xx/sx)**2+(yy/sy)**2)) * rng.uniform(.40, 1)
        np.add.at(pores, ((y+yy) % SIZE, (x+xx) % SIZE), spot)
    pores = np.minimum(pores, 1)
    middle = noise(rng, 5.5, 3.7, -20)
    rgb = colour((154, 106, 85), (broad, (1.65, 1.15, .85)), (middle, (1.05, .8, .65)),
                 (fine, (1.35, 1.15, .95)), (furrows, (-4.1, -3.4, -3.0)), (pores, (-19, -14, -11)))
    relief = .55 + .004*broad + .024*fine - .10*furrows - .25*pores
    return rgb, relief


def skin_dermis(rng):
    broad, short = noise(rng, 19, 13, 20), noise(rng, 6, 2, -49)
    u, v = X+.011*noise(rng, 18), Y+.013*noise(rng, 24)
    woven = sample(noise(rng, 10, 1.7, 30), u, v)
    cover = np.clip(.52+.46*noise(rng, 17), .06, .94)
    bundle = np.clip(cover*woven+(1-cover)*short+.21*noise(rng, 12, 3, 67), -.75, 1.5)
    rgb = colour((183, 126, 107), (broad, (6.2, 4.0, 3.0)), (bundle, (17, 17, 15)),
                 (noise(rng, 3), (2.8, 1.8, 1.4)))
    return rgb, .58+.12*bundle+.039*broad


def adipose(rng, interior):
    u = X+.030*noise(rng, 30)+.006*noise(rng, 8)
    v = Y+.026*noise(rng, 26)+.005*noise(rng, 8)
    d, gap, shade = lobes(rng, u, v, 8 if interior else 7)
    _, smaller, _ = lobes(rng, u+.007*noise(rng, 11), v, 16)
    septa = np.exp(-(gap/(.084 if interior else .069))**2)
    septa *= np.clip(.72+.24*noise(rng, 17, 9, 39), .16, 1)
    secondary = np.exp(-(smaller/.07)**2)*np.clip(.31+.24*noise(rng, 19), 0, .68)
    crown = np.exp(-(d/.51)**2)-.47
    broad, grain = noise(rng, 22), noise(rng, 3.2, 2.2, 53)
    rgb = colour((215, 171, 91) if interior else (208, 164, 89),
                 (crown, (27, 25, 17)), (shade, (7, 6, 4)), (broad, (5, 5, 3)),
                 (grain, (3.4, 2.7, 2)), (septa, (13, 22, 35)), (secondary, (8, 13, 24)))
    return rgb, .53+.27*crown+.055*grain-.23*septa-.095*secondary


def fascia(rng, interior):
    u, v = X+.006*noise(rng, 31), Y+.008*noise(rng, 33)
    strands = sample(noise(rng, 17, 1.7, -14), u, v)
    coarse = sample(noise(rng, 25, 5.1, -12), u, v)
    short = sample(noise(rng, 6.8, 2.1, 5), u, v)
    cover = np.clip(.72+.13*noise(rng, 21), .40, .95)
    collagen = strands*cover + short*(1-cover) + .18*coarse
    rgb = colour((192, 178, 157) if interior else (207, 199, 182),
                 (collagen, (5.1, 5.3, 5.7)), (noise(rng, 30, 19, 21), (1.9, 2.2, 2.7)),
                 (noise(rng, 2, 1.5), (1.5, 1.45, 1.4)))
    return rgb, .60+.044*collagen+.009*coarse


def muscle(rng, interior):
    # Gross fascicles and their connective partitions. No visible sarcomere bands.
    u, v = X+.005*noise(rng, 27), Y+.008*noise(rng, 29)
    if interior:
        # Short oblique bundle profiles on exposed muscle, with gently bending partitions.
        _, gap, shade = lobes(rng, u*2, v, 13)
        septa = np.exp(-(gap/.075)**2)*np.clip(.62+.27*noise(rng, 14), .10, 1)
        grain = sample(noise(rng, 6.5, 1.35, 56), u, v)
        bundle = .50*shade + .60*sample(noise(rng, 10, 3, 46), u, v)
    else:
        long = sample(noise(rng, 42, 4.4, -60), u, v)
        septa = np.exp(-((long-.48)/.18)**2)*np.clip(.52+.32*noise(rng, 16), .04, 1)
        bundle = sample(noise(rng, 40, 5, -60), u, v)
        grain = sample(noise(rng, 16, 1.45, -60), u, v)
    rgb = colour((128, 66, 57) if interior else (126, 59, 51),
                 (bundle, (7, 5.6, 4.7)), (grain, (5.4, 4.2, 3.7)),
                 (septa, (28, 28, 23)), (noise(rng, 29), (5, 2.9, 2.2)))
    return rgb, .57+.075*grain+.066*bundle-.13*septa


def pixels(values):
    return np.uint8(np.rint(np.clip(values, 0, 255)))


def specimen_maps():
    result = {}
    for i, name in enumerate(("skin", "fat", "fascia", "muscle")):
        for j, prefix in enumerate(("Tissue", "Interior")):
            rng = np.random.default_rng(91341 + i*100 + j)
            if name == "skin":
                rgb, relief = (skin_dermis if j else skin_surface)(rng)
            else:
                rgb, relief = {"fat": adipose, "fascia": fascia, "muscle": muscle}[name](rng, bool(j))
            # Preserve dielectric response even in the deepest microfurrows.
            result[f"Specimen/{prefix}_{name}_Albedo.png"] = pixels(np.dstack((rgb, np.clip(.31+.60*relief, .31, .94)*255)))
    return result


def case_maps():
    rng = np.random.default_rng(54387)
    outer, impression = skin_surface(rng)
    outer = (150, 102, 82) + .80*(outer - (154, 106, 85))
    inner, collagen = skin_dermis(rng)
    # 3.5 mm depth occupies only this much of the 62.5 mm lateral period. Sampling
    # a narrow strip keeps collagen at the same physical scale in both directions.
    depth_v = .36 + Y * (.0035 * 16)
    inner = np.dstack([sample(inner[..., i], X, depth_v) for i in range(3)])
    collagen = sample(collagen, X, depth_v)
    # PNG rows run down; Unity V=0 marks the opening at the exterior.
    depth = 1 - (Y + .5/SIZE)
    waviness = .007*noise(rng, 15, 90)
    t = np.clip((depth-.041-waviness)/.030, 0, 1)
    t = t*t*(3-2*t)
    superficial = colour((151, 96, 77), (noise(rng, 10), (3, 2, 1)))
    inner = superficial*(1-t[..., None]) + inner*t[..., None]
    output = {}
    for name, rgb, height in (("Surface", outer, impression), ("CutFace", inner, collagen)):
        dx = (np.roll(height, 1, 1)-np.roll(height, -1, 1))*2.0
        # In PNG coordinates increasing Y is decreasing V in the mesh UV chart.
        dy = np.gradient(height, axis=0)*4 if name == "CutFace" else (np.roll(height, -1, 0)-np.roll(height, 1, 0))*2
        normals = np.dstack((dx, dy, np.ones_like(dx)))
        normals /= np.linalg.norm(normals, axis=2)[..., None]
        output[f"CaseSkin/{name}_Albedo.png"] = pixels(rgb)
        output[f"CaseSkin/{name}_Normal.png"] = pixels(127.5*(normals+1))
    return output


def write_maps(maps, dest, check=False):
    for name, data in maps.items():
        path = dest / name
        channels = 4 if name.startswith("Specimen/") else 3
        if data.shape != (SIZE, SIZE, channels) or np.ptp(data, axis=(0, 1)).max() < 12:
            raise ValueError(f"Missing texture variation in {name}")
        if check:
            if not path.exists() or not np.array_equal(data, np.asarray(Image.open(path))):
                raise ValueError(f"Regenerate {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            out = BytesIO()
            Image.fromarray(data).save(out, format="PNG", compress_level=7)
            if not path.exists() or path.read_bytes() != out.getvalue():
                path.write_bytes(out.getvalue())
    print(f"{'Verified' if check else 'Authored'} {len(maps)} 512 x 512 texture maps.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=LEARNING)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    write_maps({**specimen_maps(), **case_maps()}, args.out, args.check)
