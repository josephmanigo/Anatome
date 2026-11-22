"""Build the museum's seamless finish maps. RGB is sRGB albedo; alpha is smoothness."""
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "unity/ThoraxLab/Assets/ThoraxLab/Museum"
SIZE = 512
rng = np.random.default_rng(1417)
y, x = np.mgrid[:SIZE, :SIZE]
u, v = x / SIZE, y / SIZE


def noise(cells_x, cells_y):
    knots = rng.uniform(-1, 1, (cells_y, cells_x))
    sx, sy = u * cells_x, v * cells_y
    ix, iy = sx.astype(int), sy.astype(int)
    fx, fy = sx - ix, sy - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def sample(dx, dy):
        return knots[(iy + dy) % cells_y, (ix + dx) % cells_x]
    return (1 - fy) * ((1 - fx) * sample(0, 0) + fx * sample(1, 0)) + fy * ((1 - fx) * sample(0, 1) + fx * sample(1, 1))


def write(name, rgb, smoothness):
    rgba = np.dstack((rgb, smoothness))
    Image.fromarray(np.uint8(np.clip(rgba, 0, 255))).save(OUT / (name + ".png"))


def terrazzo():
    mineral = 6 * noise(5, 5) + 4 * noise(41, 41) + rng.normal(0, 2.3, x.shape)
    rgb = np.array([170, 173, 165]) + mineral[..., None]
    gloss = 102 + mineral * 0.7
    chips = np.array([[221, 211, 188], [128, 132, 120], [89, 99, 92], [198, 188, 167], [155, 134, 110], [213, 206, 184]])
    # Each irregular inclusion wraps at the image border, including large marble chips.
    for _ in range(2100):
        cx, cy = rng.integers(SIZE, size=2)
        radius = rng.uniform(.9, 4.3)
        reach = int(np.ceil(radius)) + 1
        yy, xx = np.mgrid[-reach:reach+1, -reach:reach+1]
        angle = rng.uniform(0, 2*np.pi)
        a = (xx * np.cos(angle) + yy * np.sin(angle)) / radius
        b = (-xx * np.sin(angle) + yy * np.cos(angle)) / (radius * rng.uniform(.4, .85))
        edge = np.maximum.reduce([abs(a), abs(b), abs(a + b * .64) * .79])
        alpha = np.clip((1 - edge) * radius, 0, 1)
        ij = (yy + cy) % SIZE, (xx + cx) % SIZE
        tint = chips[rng.integers(len(chips))] + rng.uniform(-5, 5)
        rgb[ij] += (tint - rgb[ij]) * alpha[..., None]
        gloss[ij] += alpha * 28
    edge = np.minimum.reduce((x, SIZE - 1 - x, y, SIZE - 1 - y))
    seam = np.clip((1.6 - edge) / 1.6, 0, 1)
    rgb = rgb * (1 - seam[..., None] * .28)
    write("Honed terrazzo", rgb, gloss * (1 - .48 * seam))


def timber():
    wander = noise(6, 3) * .032 + noise(17, 3) * .007
    phase = 2 * np.pi * (u * 32 + wander * 9 + .12 * np.sin(2*np.pi*v))
    rings = np.sin(phase) + .4 * np.sin(phase * 3)
    pores = noise(160, 9)
    grain = 4.6 * rings + 8 * noise(48, 3) + 6 * pores + 3 * noise(155, 30)
    rgb = np.array([143, 107, 78]) + grain[..., None] * [1.0, .78, .51]
    write("Quarter sawn oak", rgb, 77 + 16 * pores + grain*.26)


def plaster():
    grain = 4.7 * noise(5, 5) + 3.4 * noise(27, 27) + 2.4 * noise(95, 95) + rng.normal(0, 1.8, x.shape)
    write("Mineral plaster", np.array([218, 216, 205]) + grain[..., None], 40 + grain * 1.2)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    terrazzo()
    timber()
    plaster()
